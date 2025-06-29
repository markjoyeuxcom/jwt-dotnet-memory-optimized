using JwtApi.Data;
using JwtApi.Models;
using JwtApi.Models.DTOs;
using JwtApi.Memory;
using Microsoft.EntityFrameworkCore;

namespace JwtApi.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, ClientInfo clientInfo);
    Task<AuthResult> LoginAsync(LoginRequest request, ClientInfo clientInfo);
    Task<AuthResult> RefreshTokenAsync(string refreshToken, ClientInfo clientInfo);
    Task<AuthResult> LogoutAsync(string refreshToken);
}

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserService _userService;
    private readonly IMemoryOptimizedTokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ApplicationDbContext context,
        IUserService userService,
        IMemoryOptimizedTokenService tokenService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _userService = userService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, ClientInfo clientInfo)
    {
        try
        {
            // Check if user already exists
            if (await _userService.ExistsAsync(request.Username, request.Email))
            {
                return AuthResult.Failure("User with this username or email already exists");
            }

            // Create new user
            var user = new User
            {
                Username = request.Username,
                Email = request.Email.ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName
            };

            await _userService.CreateAsync(user);
            return AuthResult.Success(user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration");
            return AuthResult.Failure("Registration failed");
        }
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, ClientInfo clientInfo)
    {
        try
        {
            var user = await _userService.GetByEmailAsync(request.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return AuthResult.Failure("Invalid email or password");
            }

            if (!user.IsActive)
            {
                return AuthResult.Failure("Account is inactive");
            }

            // Generate tokens
            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = RefreshToken.Create(user.Id, clientInfo.IpAddress, clientInfo.UserAgent);

            // Save refresh token
            _context.RefreshTokens.Add(refreshToken);
            
            // Update last login
            user.UpdateLastLogin();
            await _context.SaveChangesAsync();

            return AuthResult.Success(user, accessToken, refreshToken.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login");
            return AuthResult.Failure("Login failed");
        }
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, ClientInfo clientInfo)
    {
        try
        {
            var token = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (token == null || !token.IsValid)
            {
                return AuthResult.Failure("Invalid or expired refresh token");
            }

            var user = token.User!;
            if (!user.IsActive)
            {
                return AuthResult.Failure("Account is inactive");
            }

            // Generate new tokens
            var newAccessToken = _tokenService.GenerateAccessToken(user);
            var newRefreshToken = RefreshToken.Create(user.Id, clientInfo.IpAddress, clientInfo.UserAgent);

            // Revoke old token and add new one
            token.Revoke();
            _context.RefreshTokens.Add(newRefreshToken);
            await _context.SaveChangesAsync();

            return AuthResult.Success(user, newAccessToken, newRefreshToken.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return AuthResult.Failure("Token refresh failed");
        }
    }

    public async Task<AuthResult> LogoutAsync(string refreshToken)
    {
        try
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (token != null)
            {
                token.Revoke();
                await _context.SaveChangesAsync();
            }

            return AuthResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return AuthResult.Failure("Logout failed");
        }
    }
}

public class AuthResult
{
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }
    public User? User { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public int? UserId { get; private set; }

    private AuthResult(bool success, string? errorMessage = null, User? user = null, 
        string? accessToken = null, string? refreshToken = null, int? userId = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
        User = user;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        UserId = userId;
    }

    public static AuthResult Success(User user, string accessToken, string refreshToken)
        => new(true, user: user, accessToken: accessToken, refreshToken: refreshToken);

    public static AuthResult Success(int userId)
        => new(true, userId: userId);

    public static AuthResult Success()
        => new(true);

    public static AuthResult Failure(string errorMessage)
        => new(false, errorMessage);
}