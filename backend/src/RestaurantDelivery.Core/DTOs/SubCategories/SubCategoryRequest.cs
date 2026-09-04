using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.SubCategories;

public class SubCategoryRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }
}
