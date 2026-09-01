namespace RestaurantDelivery.Core.DTOs.Orders;

public class BulkOrderImportResult
{
    public int RowsProcessed { get; set; }
    public int OrdersCreated { get; set; }
    public int RowsSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
}
