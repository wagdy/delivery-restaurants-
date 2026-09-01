namespace RestaurantDelivery.Core.DTOs.Dgtera;

// Our own normalized shape for one order pulled from Dgtera - IDgteraClient is
// responsible for mapping whatever Odoo's API actually returns onto this, so the rest
// of the app (DgteraSyncService) never depends on Odoo's field names directly.
public class DgteraOrderDto
{
    // Odoo's own record id (e.g. the "id" field on pos.order/sale.order) - this is the
    // de-duplication key, so it must be stable across syncs for the same POS order.
    public string ExternalId { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public DateTime OrderDateUtc { get; set; }

    // Raw status string as Odoo reports it (e.g. "draft", "paid", "done", "cancel") -
    // DgteraSyncService maps this to our OrderStatus enum.
    public string RawStatus { get; set; } = string.Empty;

    public List<DgteraOrderLineDto> Lines { get; set; } = new();
}
