using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Categories;

public class CategoryRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }
}
