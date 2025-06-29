namespace JwtApi.Models.DTOs;

/// <summary>
/// Response model for successful authentication
/// </summary>
public class AuthResponse
{
    /// <summary>
    /// JWT access token
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// Refresh token for obtaining new access tokens
    /// </summary>
    public required string RefreshToken { get; set; }

    /// <summary>
    /// Token type (always "Bearer")
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Token expiration time in seconds
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// User information
    /// </summary>
    public required UserResponse User { get; set; }

    /// <summary>
    /// Pod information for load balancing testing
    /// </summary>
    public PodInfo? PodInfo { get; set; }
}

/// <summary>
/// User information in authentication response
/// </summary>
public class UserResponse
{
    /// <summary>
    /// User's unique identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// User's username
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// User's email address
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// User's first name
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// User's last name
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// User's display name
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// User's initials for avatar
    /// </summary>
    public required string Initials { get; set; }

    /// <summary>
    /// User's role
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// Whether the account is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When the account was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the user last logged in
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// Pod information for session affinity testing
/// </summary>
public class PodInfo
{
    /// <summary>
    /// Pod name/identifier
    /// </summary>
    public required string PodName { get; set; }

    /// <summary>
    /// Pod IP address
    /// </summary>
    public required string PodIP { get; set; }

    /// <summary>
    /// Machine name
    /// </summary>
    public required string MachineName { get; set; }

    /// <summary>
    /// When the response was generated
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}