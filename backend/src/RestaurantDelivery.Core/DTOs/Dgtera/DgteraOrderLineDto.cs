namespace RestaurantDelivery.Core.DTOs.Dgtera;

public class DgteraOrderLineDto
{
    public string ProductName { get; set; } = string.Empty;

    // The product's category name in Odoo (e.g. pos.category / product.category's
    // "name"). DgteraSyncService resolves this to a local Category, creating one if no
    // category with that name exists yet.
    public string? CategoryName { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
