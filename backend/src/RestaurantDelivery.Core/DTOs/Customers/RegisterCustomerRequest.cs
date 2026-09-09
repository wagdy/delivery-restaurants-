using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Customers;

public class RegisterCustomerRequest
{
    [Required, MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;
}
