using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Auth;

public class ForgotPasswordRequest
{
    [Required]
    public string PhoneNumber { get; set; } = string.Empty;
}
