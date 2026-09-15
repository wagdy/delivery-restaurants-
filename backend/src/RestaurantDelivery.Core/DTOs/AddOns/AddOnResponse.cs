namespace RestaurantDelivery.Core.DTOs.AddOns;

public class AddOnResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Null when no Arabic name has been entered - the client falls back to Name.
    public string? NameAr { get; set; }
    public decimal Price { get; set; }
}
