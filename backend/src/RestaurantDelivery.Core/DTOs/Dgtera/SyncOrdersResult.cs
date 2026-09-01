namespace RestaurantDelivery.Core.DTOs.Dgtera;

public class SyncOrdersResult
{
    public int OrdersFetched { get; set; }
    public int OrdersCreated { get; set; }
    public int OrdersUpdated { get; set; }
    public int OrdersSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
}
