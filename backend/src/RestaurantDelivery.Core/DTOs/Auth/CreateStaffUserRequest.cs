using System.ComponentModel.DataAnnotations;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Auth;

public class CreateStaffUserRequest
{
    [Required, MaxLength(200)]
    [RegularExpression(@"^[A-Za-z ]+$", ErrorMessage = "Name can only contain letters and spaces.")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "A valid email address is required.")]
    public string Email { get; set; } = string.Empty;

    // Optional, but [RegularExpression] only runs against a non-null value — RegularExpressionAttribute.IsValid
    // returns true for null, so a blank phone number still passes while a provided one must be digits-only.
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "Phone number must contain only numbers.")]
    public string? PhoneNumber { get; set; }

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; }
}
