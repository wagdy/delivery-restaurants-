using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Reviews;

// Public - a guest's own order can be reviewed too (matches this app's "guest checkout is
// a first-class flow" convention elsewhere), so this isn't gated on being signed in.
// ReviewService.SubmitReviewAsync separately validates the order exists, is actually
// Delivered, and doesn't already have a review, so none of that is trusted here.
public class SubmitReviewRequest
{
    [Required]
    public int OrderId { get; set; }

    [Range(1, 5, ErrorMessage = "Overall rating must be between 1 and 5.")]
    public int OverallRating { get; set; }

    public List<SubmitReviewAnswerRequest> Answers { get; set; } = new();
}

public class SubmitReviewAnswerRequest
{
    [Required]
    public int SurveyQuestionId { get; set; }

    [Required, MaxLength(2000)]
    public string AnswerValue { get; set; } = string.Empty;
}
