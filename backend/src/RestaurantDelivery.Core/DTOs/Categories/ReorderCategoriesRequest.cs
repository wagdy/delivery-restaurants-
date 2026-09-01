using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Categories;

public class ReorderCategoriesRequest
{
    // Category IDs in their new display order - each ID's position in this list is its
    // new DisplayOrder, so the caller doesn't need to compute indexes itself.
    [Required, MinLength(1)]
    public List<int> OrderedIds { get; set; } = new();
}
