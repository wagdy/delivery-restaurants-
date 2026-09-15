namespace RestaurantDelivery.Core.DTOs.SubCategories;

public class SubCategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Null when no Arabic name has been entered - the client falls back to Name.
    public string? NameAr { get; set; }
    public int DisplayOrder { get; set; }
    public int CategoryId { get; set; }
}
