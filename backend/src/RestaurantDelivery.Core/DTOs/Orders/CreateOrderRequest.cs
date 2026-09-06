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
}
