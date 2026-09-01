using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Orders;

public class OrderItemRequest
{
    [Required]
    public int MenuItemId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }

    public List<int> AddOnIds { get; set; } = new();
}
