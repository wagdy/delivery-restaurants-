using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.SubCategories;

public class ReorderSubCategoriesRequest
{
    [Required]
    public int CategoryId { get; set; }

    // Sub-category IDs, all belonging to CategoryId above, in their new display order -
    // each ID's position in this list is its new DisplayOrder.
    [Required, MinLength(1)]
    public List<int> OrderedIds { get; set; } = new();
}
