using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using JwtApi.Memory;

namespace JwtApi.Middleware;

/// <summary>
/// Middleware for monitoring memory usage and performance metrics
/// </summary>
public class MemoryMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MemoryMonitoringMiddleware> _logger;
    private readonly IHighPerformanceCache _cache;
    private readonly ArrayPool<char> _charArrayPool;
    private static readonly ActivitySource ActivitySource = new("JwtApi.MemoryMonitoring");

    public MemoryMonitoringMiddleware(
        RequestDelegate next,
        ILogger<MemoryMonitoringMiddleware> logger,
        IHighPerformanceCache cache,
        ArrayPool<char> charArrayPool)
    {
        _next = next;
        _logger = logger;
        _cache = cache;
        _charArrayPool = charArrayPool;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var activity = ActivitySource.StartActivity("MemoryMonitoring");
        
        // Capture initial memory state
        var initialMemory = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();
        
        // Add custom headers for debugging
        var requestId = context.TraceIdentifier;
        context.Response.Headers.Add("X-Request-ID", requestId);
        context.Response.Headers.Add("X-Memory-Initial", initialMemory.ToString());

        try
        {
            // Monitor memory allocations during request processing
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            // Capture final memory state
            var finalMemory = GC.GetTotalMemory(false);
            var memoryDelta = finalMemory - initialMemory;
            
            // Add performance headers
            context.Response.Headers.Add("X-Memory-Final", finalMemory.ToString());
            context.Response.Headers.Add("X-Memory-Delta", memoryDelta.ToString());
            context.Response.Headers.Add("X-Response-Time", stopwatch.ElapsedMilliseconds.ToString());
            context.Response.Headers.Add("X-GC-Gen0", GC.CollectionCount(0).ToString());
            context.Response.Headers.Add("X-GC-Gen1", GC.CollectionCount(1).ToString());
            context.Response.Headers.Add("X-GC-Gen2", GC.CollectionCount(2).ToString());

            // Log memory usage for monitoring
            if (memoryDelta > 1_000_000) // Log if more than 1MB allocated
            {
                _logger.LogWarning(
                    "High memory allocation detected: {MemoryDelta:N0} bytes for {Method} {Path} in {ElapsedMs}ms",
                    memoryDelta,
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds);
            }
            else if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Request completed: {Method} {Path} - Memory: {MemoryDelta:N0} bytes, Time: {ElapsedMs}ms",
                    context.Request.Method,
                    context.Request.Path,
                    memoryDelta,
                    stopwatch.ElapsedMilliseconds);
            }

            // Cache performance metrics
            await CachePerformanceMetrics(context, memoryDelta, stopwatch.ElapsedMilliseconds);

            // Add activity tags
            activity?.SetTag("memory.delta", memoryDelta);
            activity?.SetTag("response.time", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("http.method", context.Request.Method);
            activity?.SetTag("http.route", context.Request.Path);
        }
    }

    /// <summary>
    /// Cache performance metrics for analysis
    /// </summary>
    private async Task CachePerformanceMetrics(HttpContext context, long memoryDelta, long responseTimeMs)
    {
        try
        {
            var metrics = new RequestMetrics
            {
                RequestId = context.TraceIdentifier,
                Method = context.Request.Method,
                Path = context.Request.Path.Value ?? string.Empty,
                StatusCode = context.Response.StatusCode,
                MemoryDelta = memoryDelta,
                ResponseTimeMs = responseTimeMs,
                Timestamp = DateTime.UtcNow,
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                RemoteIP = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            };

            var cacheKey = $"metrics:{DateTime.UtcNow:yyyyMMddHH}"; // Group by hour
            await _cache.SetAsync(cacheKey, metrics, TimeSpan.FromHours(24));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching performance metrics");
        }
    }
}

/// <summary>
/// Middleware for adding pod identification headers
/// </summary>
public class PodIdentificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PodIdentificationMiddleware> _logger;
    private readonly string _podName;
    private readonly string _podIP;
    private readonly string _machineName;

    public PodIdentificationMiddleware(
        RequestDelegate next,
        ILogger<PodIdentificationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _podName = Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName;
        _podIP = Environment.GetEnvironmentVariable("POD_IP") ?? "localhost";
        _machineName = Environment.MachineName;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add pod identification headers
        context.Response.Headers.Add("X-Pod-Name", _podName);
        context.Response.Headers.Add("X-Pod-IP", _podIP);
        context.Response.Headers.Add("X-Machine-Name", _machineName);
        context.Response.Headers.Add("X-Server-Framework", ".NET 8.0");
        context.Response.Headers.Add("X-Memory-Optimized", "true");

        await _next(context);
    }
}

/// <summary>
/// Request performance metrics
/// </summary>
public class RequestMetrics
{
    public required string RequestId { get; set; }
    public required string Method { get; set; }
    public required string Path { get; set; }
    public int StatusCode { get; set; }
    public long MemoryDelta { get; set; }
    public long ResponseTimeMs { get; set; }
    public DateTime Timestamp { get; set; }
    public string? UserAgent { get; set; }
    public string? RemoteIP { get; set; }
}