using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Orders;

public class OrderItemRequest
{
    [Required]
    public int MenuItemId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }

    // Required when the menu item has variants, refused when it does not. Validated
    // against that item's own variants server-side, so a client cannot price a line by
    // naming a cheap variant that belongs to something else.
    public int? VariantId { get; set; }

    public List<int> AddOnIds { get; set; } = new();
}
