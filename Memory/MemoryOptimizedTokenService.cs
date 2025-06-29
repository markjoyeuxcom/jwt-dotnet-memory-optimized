using System.Buffers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.ObjectPool;
using Microsoft.IdentityModel.Tokens;
using JwtApi.Models;

namespace JwtApi.Memory;

/// <summary>
/// Memory-optimized JWT token service using object pooling and efficient memory management
/// </summary>
public interface IMemoryOptimizedTokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
    bool IsTokenBlacklisted(string token);
    void BlacklistToken(string token, TimeSpan expiry);
}

/// <summary>
/// High-performance JWT token service with memory optimizations
/// </summary>
public class MemoryOptimizedTokenService : IMemoryOptimizedTokenService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ObjectPool<StringBuilder> _stringBuilderPool;
    private readonly ArrayPool<byte> _byteArrayPool;
    private readonly ArrayPool<char> _charArrayPool;
    private readonly ILogger<MemoryOptimizedTokenService> _logger;
    
    // Cached token validation parameters to avoid recreation
    private readonly TokenValidationParameters _tokenValidationParameters;
    private readonly SymmetricSecurityKey _securityKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public MemoryOptimizedTokenService(
        IConfiguration configuration,
        IMemoryCache cache,
        ObjectPool<StringBuilder> stringBuilderPool,
        ArrayPool<byte> byteArrayPool,
        ArrayPool<char> charArrayPool,
        ILogger<MemoryOptimizedTokenService> logger)
    {
        _configuration = configuration;
        _cache = cache;
        _stringBuilderPool = stringBuilderPool;
        _byteArrayPool = byteArrayPool;
        _charArrayPool = charArrayPool;
        _logger = logger;

        // Pre-compute security objects to avoid recreation on each token operation
        var jwtKey = _configuration["Jwt:Key"] ?? "your-super-secret-jwt-key-change-this-in-production-must-be-at-least-32-characters-long";
        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        
        _securityKey = new SymmetricSecurityKey(keyBytes);
        _signingCredentials = new SigningCredentials(_securityKey, SecurityAlgorithms.HmacSha256);
        _tokenHandler = new JwtSecurityTokenHandler();

        _tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _securityKey,
            ValidateIssuer = true,
            ValidIssuer = _configuration["Jwt:Issuer"] ?? "JwtApi",
            ValidateAudience = true,
            ValidAudience = _configuration["Jwt:Audience"] ?? "JwtApiUsers",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = true
        };
    }

    /// <summary>
    /// Generate JWT access token with memory-efficient claim building
    /// </summary>
    public string GenerateAccessToken(User user)
    {
        try
        {
            var now = DateTime.UtcNow;
            var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "15");
            var expiry = now.AddMinutes(expiryMinutes);

            // Use memory-efficient claim creation
            var claims = new List<Claim>(capacity: 8) // Pre-allocate with expected capacity
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role),
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Add optional claims efficiently
            if (!string.IsNullOrEmpty(user.FirstName))
                claims.Add(new(ClaimTypes.GivenName, user.FirstName));
            
            if (!string.IsNullOrEmpty(user.LastName))
                claims.Add(new(ClaimTypes.Surname, user.LastName));

            // Create token descriptor with pre-computed credentials
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiry,
                NotBefore = now,
                IssuedAt = now,
                Issuer = _configuration["Jwt:Issuer"] ?? "JwtApi",
                Audience = _configuration["Jwt:Audience"] ?? "JwtApiUsers",
                SigningCredentials = _signingCredentials
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = _tokenHandler.WriteToken(token);

            // Cache token metadata for fast validation
            var cacheKey = $"token_meta:{tokenString.GetHashCode()}";
            _cache.Set(cacheKey, new TokenMetadata 
            { 
                UserId = user.Id, 
                Username = user.Username,
                ExpiresAt = expiry 
            }, expiry);

            _logger.LogDebug("Generated access token for user {UserId} (cached as {CacheKey})", user.Id, cacheKey);
            
            return tokenString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating access token for user {UserId}", user.Id);
            throw;
        }
    }

    /// <summary>
    /// Generate cryptographically secure refresh token using pooled byte arrays
    /// </summary>
    public string GenerateRefreshToken()
    {
        const int tokenSize = 64;
        var buffer = _byteArrayPool.Rent(tokenSize);
        
        try
        {
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(buffer, 0, tokenSize);
            
            // Use efficient base64 encoding with pooled char array
            var base64Length = ((tokenSize + 2) / 3) * 4;
            var charBuffer = _charArrayPool.Rent(base64Length);
            
            try
            {
                if (Convert.TryToBase64Chars(buffer.AsSpan(0, tokenSize), charBuffer, out var charsWritten))
                {
                    return new string(charBuffer, 0, charsWritten);
                }
                
                // Fallback to standard conversion
                return Convert.ToBase64String(buffer, 0, tokenSize);
            }
            finally
            {
                _charArrayPool.Return(charBuffer);
            }
        }
        finally
        {
            _byteArrayPool.Return(buffer);
        }
    }

    /// <summary>
    /// Validate JWT token with caching for performance
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            // Check cache first for recently validated tokens
            var cacheKey = $"validated_token:{token.GetHashCode()}";
            if (_cache.TryGetValue(cacheKey, out ClaimsPrincipal? cachedPrincipal))
            {
                _logger.LogDebug("Token validation cache hit for {CacheKey}", cacheKey);
                return cachedPrincipal;
            }

            // Check if token is blacklisted
            if (IsTokenBlacklisted(token))
            {
                _logger.LogWarning("Attempted to validate blacklisted token");
                return null;
            }

            // Validate token
            var principal = _tokenHandler.ValidateToken(token, _tokenValidationParameters, out var validatedToken);
            
            if (validatedToken is JwtSecurityToken jwtToken)
            {
                // Cache the validated principal for a short time to improve performance
                var cacheExpiry = jwtToken.ValidTo > DateTime.UtcNow.AddMinutes(5) 
                    ? DateTime.UtcNow.AddMinutes(5) 
                    : jwtToken.ValidTo;
                
                _cache.Set(cacheKey, principal, cacheExpiry);
                _logger.LogDebug("Token validated and cached for {CacheKey} until {Expiry}", cacheKey, cacheExpiry);
            }

            return principal;
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogDebug("Token validation failed: Token expired");
            return null;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _logger.LogWarning("Token validation failed: Invalid signature");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation");
            return null;
        }
    }

    /// <summary>
    /// Check if token is blacklisted
    /// </summary>
    public bool IsTokenBlacklisted(string token)
    {
        var blacklistKey = $"blacklist:{token.GetHashCode()}";
        return _cache.TryGetValue(blacklistKey, out _);
    }

    /// <summary>
    /// Add token to blacklist
    /// </summary>
    public void BlacklistToken(string token, TimeSpan expiry)
    {
        var blacklistKey = $"blacklist:{token.GetHashCode()}";
        _cache.Set(blacklistKey, true, expiry);
        
        // Also remove from validation cache
        var validationKey = $"validated_token:{token.GetHashCode()}";
        _cache.Remove(validationKey);
        
        _logger.LogInformation("Token blacklisted with key {BlacklistKey} for {Expiry}", blacklistKey, expiry);
    }

    /// <summary>
    /// Metadata for cached tokens
    /// </summary>
    private class TokenMetadata
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}

/// <summary>
/// Object pool policy for StringBuilder with memory-efficient settings
/// </summary>
public class StringBuilderPooledObjectPolicy : IPooledObjectPolicy<StringBuilder>
{
    public StringBuilder Create() => new(256); // Start with reasonable capacity

    public bool Return(StringBuilder obj)
    {
        if (obj.Capacity > 4096) // Don't pool very large builders
            return false;

        obj.Clear();
        return true;
    }
}