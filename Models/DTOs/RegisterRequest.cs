using System.ComponentModel.DataAnnotations;

namespace JwtApi.Models.DTOs;

/// <summary>
/// Request model for user registration
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// Desired username (3-50 characters)
    /// </summary>
    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
    public required string Username { get; set; }

    /// <summary>
    /// User's email address
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(100, ErrorMessage = "Email must not exceed 100 characters")]
    public required string Email { get; set; }

    /// <summary>
    /// User's password (minimum 6 characters)
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public required string Password { get; set; }

    /// <summary>
    /// Password confirmation (must match password)
    /// </summary>
    [Required(ErrorMessage = "Password confirmation is required")]
    [Compare(nameof(Password), ErrorMessage = "Password and confirmation do not match")]
    public required string ConfirmPassword { get; set; }

    /// <summary>
    /// User's first name (optional)
    /// </summary>
    [StringLength(50, ErrorMessage = "First name must not exceed 50 characters")]
    public string? FirstName { get; set; }

    /// <summary>
    /// User's last name (optional)
    /// </summary>
    [StringLength(50, ErrorMessage = "Last name must not exceed 50 characters")]
    public string? LastName { get; set; }
}