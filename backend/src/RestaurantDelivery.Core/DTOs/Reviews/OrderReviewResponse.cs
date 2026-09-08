namespace RestaurantDelivery.Core.DTOs.Reviews;

// The "Submitted Reviews" table row shape - deliberately light (no answers) since the
// admin table only needs enough to decide whether to open "View Details".
public class OrderReviewResponse
{
    public int Id { get; set; }

    // Null for a general store-wide review (no specific order) - see OrderReview.OrderId.
    public int? OrderId { get; set; }

    // Read from Order.CustomerName (not Customer.FullName) so a guest's review - who has
    // no AppUser at all - still shows a real name, exactly how the Orders table already
    // treats CustomerName as the canonical display name regardless of registration. Falls
    // back to Customer.FullName for an order-less store review from a signed-in customer,
    // and finally to a static placeholder for an anonymous store review (no order, no
    // account) - see ReviewService.MapReviewResponse.
    public string CustomerName { get; set; } = string.Empty;

    public int OverallRating { get; set; }
    public DateTime CreatedAt { get; set; }
}
