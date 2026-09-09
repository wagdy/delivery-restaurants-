using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Customers;

public class UpdateCustomerRequest
{
    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
}
