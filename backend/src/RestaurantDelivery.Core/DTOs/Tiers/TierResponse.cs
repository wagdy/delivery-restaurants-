namespace RestaurantDelivery.Core.DTOs.Tiers;

public class TierResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinPoints { get; set; }
    public int? MaxPoints { get; set; }
}
