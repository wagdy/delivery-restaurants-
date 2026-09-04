namespace RestaurantDelivery.Core.DTOs.SubCategories;

public class SubCategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public int CategoryId { get; set; }
}
