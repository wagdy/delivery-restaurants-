using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.SubCategories;

public class SubCategoryRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Optional Arabic display name. The admin forms send null when the field is left
    // blank; CategoryService/MenuItemService normalise blank-but-present values to null
    // too, so "no Arabic name" is one value in the database rather than three.
    [MaxLength(100)]
    public string? NameAr { get; set; }

    [Required]
    public int CategoryId { get; set; }
}
