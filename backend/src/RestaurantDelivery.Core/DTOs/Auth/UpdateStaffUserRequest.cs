using System.ComponentModel.DataAnnotations;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Auth;

// Mirrors CreateStaffUserRequest minus Password - editing a staff account never changes
// their password (that's ResetPasswordAsync's job, and staff aren't eligible for that
// self-service flow either - see ResetPasswordAsync's own doc comment).
public class UpdateStaffUserRequest
{
    [Required, MaxLength(200)]
    // Arabic letters as well as Latin - this restaurant's customers and staff write
    // their names in both. Matches the pattern RegisterRequest/RegisterCustomerRequest
    // already used, which is how the mismatch showed up: someone could register as
    // "محمد" and then be unable to place an order under their own name.
    [RegularExpression(@"^[a-zA-Z\u0600-\u06FF\s]+$", ErrorMessage = "Please enter a valid name without numbers or symbols.")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "Phone number must contain only numbers.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; }

    // Required when Role is Admin (must reference an existing Role); must be null when
    // Role is CaptainOrder - same rule as CreateStaffUserRequest.RoleId.
    public int? RoleId { get; set; }
}
