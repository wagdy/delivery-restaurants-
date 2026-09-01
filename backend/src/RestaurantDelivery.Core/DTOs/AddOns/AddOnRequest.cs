using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.AddOns;

public class AddOnRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100000)]
    public decimal Price { get; set; }
}
