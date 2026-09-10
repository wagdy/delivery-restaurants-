using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Auth;

public class RegisterRequest
{
    // Customers log in with phone number, not email - see AuthService.LoginAsync's
    // Identifier detection. Required and must be unique across all accounts (checked in
    // AuthService.RegisterAsync).
    [Required]
    [RegularExpression(@"^01[0125][0-9]{8}$", ErrorMessage = "Please enter a valid 11-digit Egyptian mobile number starting with 01.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    [RegularExpression(@"^[a-zA-Z\u0600-\u06FF\s]+$", ErrorMessage = "Please enter a valid name without numbers or symbols.")]
    public string FullName { get; set; } = string.Empty;

    public string? Address { get; set; }
}
