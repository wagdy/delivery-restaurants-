using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.MenuItems;

public class MenuItemRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0.01, 100000)]
    public decimal Price { get; set; }

    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;

    public List<int> AddOnIds { get; set; } = new();
}
