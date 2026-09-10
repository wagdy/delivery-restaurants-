using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Customers;

public class RegisterCustomerRequest
{
    [Required, MaxLength(200)]
    [RegularExpression(@"^[a-zA-Z\u0600-\u06FF\s]+$", ErrorMessage = "Please enter a valid name without numbers or symbols.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    [RegularExpression(@"^01[0125][0-9]{8}$", ErrorMessage = "Please enter a valid 11-digit Egyptian mobile number starting with 01.")]
    public string Phone { get; set; } = string.Empty;
}
