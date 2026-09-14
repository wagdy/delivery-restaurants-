using System.ComponentModel.DataAnnotations;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Auth;

public class CreateStaffUserRequest
{
    [Required, MaxLength(200)]
    // Arabic letters as well as Latin - this restaurant's customers and staff write
    // their names in both. Matches the pattern RegisterRequest/RegisterCustomerRequest
    // already used, which is how the mismatch showed up: someone could register as
    // "محمد" and then be unable to place an order under their own name.
    [RegularExpression(@"^[a-zA-Z\u0600-\u06FF\s]+$", ErrorMessage = "Please enter a valid name without numbers or symbols.")]
    public string FullName { get; set; } = string.Empty;

    // Staff accounts log in with phone number, not email - see AuthService.LoginAsync's
    // Identifier detection. Required and must be globally unique (checked in
    // AuthService.CreateStaffUserAsync).
    [Required]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "Phone number must contain only numbers.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; }

    // Required when Role is Admin (must reference an existing Role); must be null when
    // Role is CaptainOrder. Validated in AuthService.CreateStaffUserAsync.
    public int? RoleId { get; set; }
}
