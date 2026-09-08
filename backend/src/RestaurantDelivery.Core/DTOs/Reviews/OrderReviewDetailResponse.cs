namespace RestaurantDelivery.Core.DTOs.Reviews;

// The "View Details" modal's full payload - everything OrderReviewResponse has, plus
// every answer the customer gave.
public class OrderReviewDetailResponse
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int OverallRating { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ReviewAnswerResponse> Answers { get; set; } = new();
}
