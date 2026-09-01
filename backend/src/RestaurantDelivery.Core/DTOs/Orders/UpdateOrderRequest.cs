using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Orders;

public class UpdateOrderRequest
{
    [Required, MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string DeliveryAddress { get; set; } = string.Empty;

    [MinLength(1)]
    public List<OrderItemRequest> Items { get; set; } = new();
}
