namespace RestaurantDelivery.Core.DTOs.MenuItems;

public class BulkMenuItemImportResult
{
    public int RowsProcessed { get; set; }
    public int ItemsCreated { get; set; }
    public int ItemsUpdated { get; set; }
    public int RowsSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
}
