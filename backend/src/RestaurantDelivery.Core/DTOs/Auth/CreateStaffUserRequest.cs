using System.ComponentModel.DataAnnotations;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Auth;

public class CreateStaffUserRequest
{
    [Required, MaxLength(200)]
    [RegularExpression(@"^[A-Za-z ]+$", ErrorMessage = "Name can only contain letters and spaces.")]
    public string FullName { get; set; } = string.Empty;

    // Staff accounts log in with phone number, not email - see AuthService.LoginStaffAsync.
    // Required and must be unique among staff (checked in AuthService.CreateStaffUserAsync).
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
