using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Auth;

public class RegisterRequest
{
    // Customers log in with phone number, not email - see AuthService.LoginByPhoneAsync.
    // Required and must be unique across all accounts (checked in AuthService.RegisterAsync).
    [Required]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "Phone number must contain only numbers.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    [RegularExpression(@"^[A-Za-z ]+$", ErrorMessage = "Name can only contain letters and spaces.")]
    public string FullName { get; set; } = string.Empty;

    public string? Address { get; set; }
}
