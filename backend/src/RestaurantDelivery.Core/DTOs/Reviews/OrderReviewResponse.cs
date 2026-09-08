namespace RestaurantDelivery.Core.DTOs.Reviews;

// The "Submitted Reviews" table row shape - deliberately light (no answers) since the
// admin table only needs enough to decide whether to open "View Details".
public class OrderReviewResponse
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    // Read from Order.CustomerName (not Customer.FullName) so a guest's review - who has
    // no AppUser at all - still shows a real name, exactly how the Orders table already
    // treats CustomerName as the canonical display name regardless of registration.
    public string CustomerName { get; set; } = string.Empty;

    public int OverallRating { get; set; }
    public DateTime CreatedAt { get; set; }
}
