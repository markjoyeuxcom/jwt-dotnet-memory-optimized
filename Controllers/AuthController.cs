using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using JwtApi.Models;
using JwtApi.Models.DTOs;
using JwtApi.Services;
using JwtApi.Memory;

namespace JwtApi.Controllers;

/// <summary>
/// Authentication controller with memory-optimized operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("AuthPolicy")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly IMemoryOptimizedTokenService _tokenService;
    private readonly IHighPerformanceCache _cache;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IUserService userService,
        IMemoryOptimizedTokenService tokenService,
        IHighPerformanceCache cache,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _userService = userService;
        _tokenService = tokenService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>Success message or validation errors</returns>
    /// <response code="201">User registered successfully</response>
    /// <response code="400">Invalid registration data</response>
    /// <response code="409">User already exists</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            _logger.LogInformation("Registration attempt for username: {Username}, email: {Email}", 
                request.Username, request.Email);

            var result = await _authService.RegisterAsync(request, GetClientInfo());
            
            if (!result.Success)
            {
                _logger.LogWarning("Registration failed for {Username}: {Error}", request.Username, result.ErrorMessage);
                return result.ErrorMessage?.Contains("already exists") == true 
                    ? Conflict(new { error = result.ErrorMessage })
                    : BadRequest(new { error = result.ErrorMessage });
            }

            _logger.LogInformation("User registered successfully: {Username} (ID: {UserId})", 
                request.Username, result.UserId);

            return Created(string.Empty, new
            {
                message = "User registered successfully",
                userId = result.UserId,
                podInfo = GetPodInfo()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration for {Username}", request.Username);
            return StatusCode(500, new { error = "Internal server error during registration" });
        }
    }

    /// <summary>
    /// Authenticate user and return JWT tokens
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>JWT tokens and user information</returns>
    /// <response code="200">Login successful</response>
    /// <response code="400">Invalid login data</response>
    /// <response code="401">Invalid credentials</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Login attempt for email: {Email}", request.Email);

            var result = await _authService.LoginAsync(request, GetClientInfo());
            
            if (!result.Success)
            {
                _logger.LogWarning("Login failed for {Email}: {Error}", request.Email, result.ErrorMessage);
                return Unauthorized(new { error = result.ErrorMessage });
            }

            _logger.LogInformation("User logged in successfully: {Email} (ID: {UserId})", 
                request.Email, result.User?.Id);

            var response = new AuthResponse
            {
                AccessToken = result.AccessToken!,
                RefreshToken = result.RefreshToken!,
                ExpiresIn = 900, // 15 minutes
                User = new UserResponse
                {
                    Id = result.User!.Id,
                    Username = result.User.Username,
                    Email = result.User.Email,
                    FirstName = result.User.FirstName,
                    LastName = result.User.LastName,
                    DisplayName = result.User.DisplayName,
                    Initials = result.User.Initials,
                    Role = result.User.Role,
                    IsActive = result.User.IsActive,
                    CreatedAt = result.User.CreatedAt,
                    LastLoginAt = result.User.LastLoginAt
                },
                PodInfo = GetPodInfo()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login for {Email}", request.Email);
            return StatusCode(500, new { error = "Internal server error during login" });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    /// <param name="refreshToken">Valid refresh token</param>
    /// <returns>New access token</returns>
    /// <response code="200">Token refreshed successfully</response>
    /// <response code="400">Invalid refresh token</response>
    /// <response code="401">Refresh token expired or revoked</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            _logger.LogDebug("Token refresh attempt");

            var result = await _authService.RefreshTokenAsync(request.RefreshToken, GetClientInfo());
            
            if (!result.Success)
            {
                _logger.LogWarning("Token refresh failed: {Error}", result.ErrorMessage);
                return Unauthorized(new { error = result.ErrorMessage });
            }

            _logger.LogInformation("Token refreshed successfully for user ID: {UserId}", result.User?.Id);

            return Ok(new
            {
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                expiresIn = 900,
                podInfo = GetPodInfo()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { error = "Internal server error during token refresh" });
        }
    }

    /// <summary>
    /// Logout user and revoke refresh token
    /// </summary>
    /// <param name="refreshToken">Refresh token to revoke</param>
    /// <returns>Logout confirmation</returns>
    /// <response code="200">Logout successful</response>
    /// <response code="400">Invalid refresh token</response>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Logout attempt for user ID: {UserId}", userId);

            var result = await _authService.LogoutAsync(request.RefreshToken);
            
            if (!result.Success)
            {
                _logger.LogWarning("Logout failed: {Error}", result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }

            // Blacklist current access token if present
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (authHeader?.StartsWith("Bearer ") == true)
            {
                var token = authHeader.Substring("Bearer ".Length);
                _tokenService.BlacklistToken(token, TimeSpan.FromHours(1));
            }

            _logger.LogInformation("User logged out successfully: {UserId}", userId);

            return Ok(new
            {
                message = "Logout successful",
                podInfo = GetPodInfo()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { error = "Internal server error during logout" });
        }
    }

    /// <summary>
    /// Verify JWT token validity
    /// </summary>
    /// <returns>Token validation result</returns>
    /// <response code="200">Token is valid</response>
    /// <response code="401">Token is invalid or expired</response>
    [HttpGet("verify")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyToken()
    {
        try
        {
            var userId = GetCurrentUserId();
            var user = await _userService.GetByIdAsync(userId);
            
            if (user == null)
            {
                return Unauthorized(new { valid = false, error = "User not found" });
            }

            return Ok(new
            {
                valid = true,
                user = new UserResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    DisplayName = user.DisplayName,
                    Initials = user.Initials,
                    Role = user.Role,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt
                },
                podInfo = GetPodInfo()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token verification");
            return StatusCode(500, new { error = "Internal server error during verification" });
        }
    }

    /// <summary>
    /// Test endpoint for session affinity
    /// </summary>
    /// <returns>Pod information for load balancing testing</returns>
    /// <response code="200">Pod information returned</response>
    [HttpGet("test/session-affinity")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult TestSessionAffinity([FromQuery] string? requestId = null)
    {
        var podInfo = GetPodInfo();
        var testId = requestId ?? Guid.NewGuid().ToString("N")[..8];
        
        _logger.LogInformation("Session affinity test request {TestId} handled by pod {PodName}", 
            testId, podInfo.PodName);

        return Ok(new
        {
            testId,
            podInfo,
            timestamp = DateTime.UtcNow,
            message = $"Request served by {podInfo.PodName}",
            headers = new
            {
                userAgent = Request.Headers.UserAgent.ToString(),
                forwardedFor = Request.Headers["X-Forwarded-For"].ToString(),
                realIP = Request.Headers["X-Real-IP"].ToString(),
                host = Request.Headers.Host.ToString()
            }
        });
    }

    /// <summary>
    /// Get client information from request
    /// </summary>
    private ClientInfo GetClientInfo()
    {
        return new ClientInfo
        {
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };
    }

    /// <summary>
    /// Get current user ID from claims
    /// </summary>
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    /// <summary>
    /// Get pod information for session affinity testing
    /// </summary>
    private PodInfo GetPodInfo()
    {
        return new PodInfo
        {
            PodName = Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName,
            PodIP = Environment.GetEnvironmentVariable("POD_IP") ?? "localhost",
            MachineName = Environment.MachineName
        };
    }
}

/// <summary>
/// Refresh token request model
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// Refresh token to use for getting new access token
    /// </summary>
    public required string RefreshToken { get; set; }
}

/// <summary>
/// Client information for audit logging
/// </summary>
public class ClientInfo
{
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}