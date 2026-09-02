using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Auth;

public class PhoneLoginRequest
{
    [Required]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "Phone number must contain only numbers.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
