using System.ComponentModel.DataAnnotations;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Orders;

public class CreateOrderRequest
{
    [Required, MaxLength(200)]
    [RegularExpression(@"^[A-Za-z ]+$", ErrorMessage = "Name can only contain letters and spaces.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "Phone number must contain only numbers.")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string DeliveryAddress { get; set; } = string.Empty;

    [MinLength(1)]
    public List<OrderItemRequest> Items { get; set; } = new();

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    // Re-validated server-side against the live PromoCodes table (see
    // OrderService.CreateAsync) - never trusted at face value for the discount amount.
    [MaxLength(50)]
    public string? PromoCodeText { get; set; }

    // See Order.DeliveryFee's own doc comment - the frontend's known delivery fee,
    // passed through so the persisted total's breakdown is internally consistent.
    [Range(0, double.MaxValue)]
    public decimal DeliveryFee { get; set; }

    // Set only by the admin "Create Order" screen when the admin picked an existing
    // registered customer via phone search - re-validated server-side against a real
    // Customer-role AppUser (see OrderService.CreateAsync), never trusted at face value.
    // A normal customer's own checkout never sends this (OrdersController.Create derives
    // their id from their own JWT instead, ignoring this field entirely).
    public string? CustomerId { get; set; }
}
