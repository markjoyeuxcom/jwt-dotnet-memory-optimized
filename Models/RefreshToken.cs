using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JwtApi.Models;

/// <summary>
/// Refresh token entity for JWT token rotation
/// </summary>
[Table("RefreshTokens")]
public class RefreshToken
{
    /// <summary>
    /// Unique identifier for the refresh token
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// The actual token value (cryptographically secure)
    /// </summary>
    [Required]
    [StringLength(255)]
    [Column(TypeName = "nvarchar(255)")]
    public required string Token { get; set; }

    /// <summary>
    /// User ID this token belongs to
    /// </summary>
    [Required]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    /// <summary>
    /// When this token expires
    /// </summary>
    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When this token was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this token has been revoked
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// When this token was revoked (if applicable)
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// IP address where token was created/used
    /// </summary>
    [StringLength(45)] // IPv6 max length
    [Column(TypeName = "nvarchar(45)")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent that created/used this token
    /// </summary>
    [StringLength(500)]
    [Column(TypeName = "nvarchar(500)")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Navigation property to the user
    /// </summary>
    [JsonIgnore]
    public virtual User? User { get; set; }

    /// <summary>
    /// Check if this token is currently valid
    /// </summary>
    [NotMapped]
    public bool IsValid => !IsRevoked && DateTime.UtcNow < ExpiresAt;

    /// <summary>
    /// Check if this token has expired
    /// </summary>
    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Revoke this token
    /// </summary>
    public void Revoke()
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Create a new refresh token for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client user agent</param>
    /// <param name="expiryDays">Token expiry in days (default 7)</param>
    /// <returns>New refresh token</returns>
    public static RefreshToken Create(int userId, string? ipAddress = null, string? userAgent = null, int expiryDays = 7)
    {
        return new RefreshToken
        {
            Token = GenerateSecureToken(),
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
    }

    /// <summary>
    /// Generate a cryptographically secure token
    /// </summary>
    /// <returns>Secure token string</returns>
    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}