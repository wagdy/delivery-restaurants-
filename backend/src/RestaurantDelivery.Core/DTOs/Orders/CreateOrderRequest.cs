using System.ComponentModel.DataAnnotations;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Orders;

public class CreateOrderRequest
{
    [Required, MaxLength(200)]
    // Arabic letters as well as Latin - this restaurant's customers and staff write
    // their names in both. Matches the pattern RegisterRequest/RegisterCustomerRequest
    // already used, which is how the mismatch showed up: someone could register as
    // "محمد" and then be unable to place an order under their own name.
    [RegularExpression(@"^[a-zA-Z\u0600-\u06FF\s]+$", ErrorMessage = "Please enter a valid name without numbers or symbols.")]
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

    // The customer's "Special request" box at checkout - "no onions", "ring the top
    // bell", that sort of thing. Stored in the Order.Notes column that already existed
    // (MaxLength 1000) rather than a new SpecialRequest one: that column is already
    // surfaced on OrderResponse and already rendered to staff in the admin order-details
    // dialog, and until now nothing but the bulk Excel import could write it. A second
    // free-text field would have needed a migration and its own display, and left two
    // near-identical notes on the same order.
    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Store pickup instead of delivery. Re-validated server-side against
    // RestaurantSettings.IsPickupEnabled (see OrderService.CreateAsync) rather than
    // trusted: this endpoint takes guest orders with no token at all, so an unchecked
    // flag would be a way to skip the delivery charge on a real delivery.
    public bool IsPickup { get; set; }

    // Honored ONLY for staff-created orders (see OrderService.CreateAsync) - the admin
    // "Create Order" screen sets a per-order fee for pickups and out-of-range addresses.
    // A customer's own checkout has this ignored entirely in favour of
    // RestaurantSettings.BaseDeliveryFee; this endpoint takes guest orders with no token
    // at all, so a client-supplied fee was simply a way to skip the delivery charge.
    [Range(0, double.MaxValue)]
    public decimal DeliveryFee { get; set; }

    // Set only by the admin "Create Order" screen when the admin picked an existing
    // registered customer via phone search - re-validated server-side against a real
    // Customer-role AppUser (see OrderService.CreateAsync), never trusted at face value.
    // A normal customer's own checkout never sends this (OrdersController.Create derives
    // their id from their own JWT instead, ignoring this field entirely).
    public string? CustomerId { get; set; }

    // True only from the admin "Create Order" screen's New Customer mode - tells
    // OrderService.CreateAsync to find-or-create a real Customer-role AppUser from
    // CustomerName/CustomerPhone/DeliveryAddress instead of leaving the order a guest
    // order. Deliberately only honored for staff-created orders (see CreateAsync) - a
    // public/guest checkout request could otherwise attach itself to any stranger's
    // account just by guessing their phone number.
    public bool IsNewCustomer { get; set; }
}
