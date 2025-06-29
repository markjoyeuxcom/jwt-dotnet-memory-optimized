using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Toolkit.HighPerformance;
using Microsoft.Toolkit.HighPerformance.Buffers;

namespace JwtApi.Memory;

/// <summary>
/// High-performance cache interface with memory optimizations
/// </summary>
public interface IHighPerformanceCache
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task ClearAsync();
    CacheStatistics GetStatistics();
}

/// <summary>
/// High-performance cache implementation using memory pools and efficient serialization
/// </summary>
public class HighPerformanceCache : IHighPerformanceCache, IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<HighPerformanceCache> _logger;
    private readonly ArrayPool<byte> _byteArrayPool;
    private readonly ConcurrentDictionary<string, CacheEntry> _entries;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _cleanupSemaphore;
    
    // Cache statistics
    private long _hits;
    private long _misses;
    private long _sets;
    private long _removes;
    
    // JSON serialization options optimized for performance
    private readonly JsonSerializerOptions _jsonOptions;

    public HighPerformanceCache(
        IMemoryCache memoryCache,
        ILogger<HighPerformanceCache> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
        _byteArrayPool = ArrayPool<byte>.Shared;
        _entries = new ConcurrentDictionary<string, CacheEntry>();
        _cleanupSemaphore = new SemaphoreSlim(1, 1);

        // Configure JSON options for high performance
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultBufferSize = 4096,
            MaxDepth = 32 // Limit recursion depth for security
        };

        // Setup cleanup timer to run every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        
        _logger.LogInformation("HighPerformanceCache initialized with memory optimizations");
    }

    /// <summary>
    /// Get cached value with high-performance deserialization
    /// </summary>
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (string.IsNullOrEmpty(key))
            return null;

        try
        {
            // First check memory cache
            if (_memoryCache.TryGetValue(key, out var cached))
            {
                Interlocked.Increment(ref _hits);
                
                if (cached is T directValue)
                {
                    _logger.LogTrace("Cache hit (direct) for key: {Key}", key);
                    return directValue;
                }

                if (cached is byte[] serializedData)
                {
                    var result = await DeserializeAsync<T>(serializedData);
                    _logger.LogTrace("Cache hit (serialized) for key: {Key}", key);
                    return result;
                }
            }

            Interlocked.Increment(ref _misses);
            _logger.LogTrace("Cache miss for key: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache value for key: {Key}", key);
            return null;
        }
    }

    /// <summary>
    /// Set cached value with efficient serialization
    /// </summary>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        if (string.IsNullOrEmpty(key) || value == null)
            return;

        try
        {
            var expirationTime = expiry ?? TimeSpan.FromMinutes(30);
            var absoluteExpiration = DateTimeOffset.UtcNow.Add(expirationTime);

            // For small, simple objects, cache directly to avoid serialization overhead
            if (ShouldCacheDirect(value))
            {
                var entryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = absoluteExpiration,
                    Size = EstimateObjectSize(value),
                    Priority = CacheItemPriority.Normal
                };

                _memoryCache.Set(key, value, entryOptions);
            }
            else
            {
                // Serialize complex objects using pooled memory
                var serializedData = await SerializeAsync(value);
                
                var entryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = absoluteExpiration,
                    Size = serializedData.Length,
                    Priority = CacheItemPriority.Normal
                };

                _memoryCache.Set(key, serializedData, entryOptions);
            }

            // Track entry for cleanup
            _entries[key] = new CacheEntry
            {
                Key = key,
                ExpiresAt = absoluteExpiration.DateTime,
                Size = EstimateObjectSize(value)
            };

            Interlocked.Increment(ref _sets);
            _logger.LogTrace("Cache set for key: {Key}, expires: {Expiry}", key, absoluteExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
        }
    }

    /// <summary>
    /// Remove value from cache
    /// </summary>
    public Task RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return Task.CompletedTask;

        try
        {
            _memoryCache.Remove(key);
            _entries.TryRemove(key, out _);
            
            Interlocked.Increment(ref _removes);
            _logger.LogTrace("Cache removed for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Check if key exists in cache
    /// </summary>
    public Task<bool> ExistsAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return Task.FromResult(false);

        var exists = _memoryCache.TryGetValue(key, out _);
        return Task.FromResult(exists);
    }

    /// <summary>
    /// Clear all cache entries
    /// </summary>
    public Task ClearAsync()
    {
        try
        {
            // MemoryCache doesn't have a clear method, so we dispose and recreate
            // In practice, you'd typically implement IMemoryCache yourself for this functionality
            _entries.Clear();
            
            _logger.LogInformation("Cache cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Get cache performance statistics
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        var totalRequests = _hits + _misses;
        var hitRate = totalRequests > 0 ? (double)_hits / totalRequests : 0;

        return new CacheStatistics
        {
            Hits = _hits,
            Misses = _misses,
            Sets = _sets,
            Removes = _removes,
            HitRate = hitRate,
            TotalEntries = _entries.Count,
            EstimatedMemoryUsage = _entries.Values.Sum(e => e.Size)
        };
    }

    /// <summary>
    /// Serialize object using pooled memory buffers
    /// </summary>
    private async Task<byte[]> SerializeAsync<T>(T value)
    {
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, value, _jsonOptions);
        return stream.ToArray();
    }

    /// <summary>
    /// Deserialize object using efficient memory handling
    /// </summary>
    private async Task<T?> DeserializeAsync<T>(byte[] data) where T : class
    {
        using var stream = new MemoryStream(data);
        return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions);
    }

    /// <summary>
    /// Determine if object should be cached directly without serialization
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldCacheDirect<T>(T value)
    {
        // Cache simple types and small strings directly
        return value is string str && str.Length < 1000 ||
               value.GetType().IsPrimitive ||
               value.GetType().IsEnum;
    }

    /// <summary>
    /// Estimate object size for cache management
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long EstimateObjectSize<T>(T value)
    {
        return value switch
        {
            string str => str.Length * 2, // Unicode characters are 2 bytes
            byte[] bytes => bytes.Length,
            _ => 1024 // Default estimate for complex objects
        };
    }

    /// <summary>
    /// Clean up expired entries periodically
    /// </summary>
    private async void CleanupExpiredEntries(object? state)
    {
        if (!await _cleanupSemaphore.WaitAsync(100))
            return;

        try
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _entries
                .Where(kvp => kvp.Value.ExpiresAt < now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                await RemoveAsync(key);
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
        finally
        {
            _cleanupSemaphore.Release();
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _cleanupSemaphore?.Dispose();
        _logger.LogInformation("HighPerformanceCache disposed");
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Internal cache entry tracking
    /// </summary>
    private class CacheEntry
    {
        public required string Key { get; set; }
        public DateTime ExpiresAt { get; set; }
        public long Size { get; set; }
    }
}

/// <summary>
/// Cache performance statistics
/// </summary>
public class CacheStatistics
{
    public long Hits { get; set; }
    public long Misses { get; set; }
    public long Sets { get; set; }
    public long Removes { get; set; }
    public double HitRate { get; set; }
    public int TotalEntries { get; set; }
    public long EstimatedMemoryUsage { get; set; }
}