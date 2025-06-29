using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JwtApi.Models;

/// <summary>
/// User entity with memory-optimized design
/// </summary>
[Table("Users")]
public class User
{
    /// <summary>
    /// Unique identifier for the user
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Unique username (case-insensitive)
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [Column(TypeName = "nvarchar(50)")]
    public required string Username { get; set; }

    /// <summary>
    /// User's email address (case-insensitive)
    /// </summary>
    [Required]
    [EmailAddress]
    [StringLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public required string Email { get; set; }

    /// <summary>
    /// Hashed password (never exposed in API responses)
    /// </summary>
    [Required]
    [JsonIgnore]
    [Column(TypeName = "nvarchar(255)")]
    public required string PasswordHash { get; set; }

    /// <summary>
    /// User's first name
    /// </summary>
    [StringLength(50)]
    [Column(TypeName = "nvarchar(50)")]
    public string? FirstName { get; set; }

    /// <summary>
    /// User's last name
    /// </summary>
    [StringLength(50)]
    [Column(TypeName = "nvarchar(50)")]
    public string? LastName { get; set; }

    /// <summary>
    /// User's role in the system
    /// </summary>
    [Required]
    [StringLength(20)]
    [Column(TypeName = "nvarchar(20)")]
    public string Role { get; set; } = "User";

    /// <summary>
    /// Whether the user account is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the user account was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the user account was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the user last logged in
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Navigation property for refresh tokens
    /// </summary>
    [JsonIgnore]
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    /// <summary>
    /// Get user's full name for display
    /// </summary>
    [NotMapped]
    public string DisplayName => 
        !string.IsNullOrEmpty(FirstName) && !string.IsNullOrEmpty(LastName) 
            ? $"{FirstName} {LastName}" 
            : Username;

    /// <summary>
    /// Get user's initials for avatar display
    /// </summary>
    [NotMapped]
    public string Initials
    {
        get
        {
            if (!string.IsNullOrEmpty(FirstName) && !string.IsNullOrEmpty(LastName))
            {
                return $"{FirstName[0]}{LastName[0]}".ToUpperInvariant();
            }
            return Username.Length > 0 ? Username[0].ToString().ToUpperInvariant() : "U";
        }
    }

    /// <summary>
    /// Check if user has specific role
    /// </summary>
    /// <param name="role">Role to check</param>
    /// <returns>True if user has the role</returns>
    public bool HasRole(string role) => 
        string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Update last login timestamp
    /// </summary>
    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Mark user as updated
    /// </summary>
    public void MarkAsUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}