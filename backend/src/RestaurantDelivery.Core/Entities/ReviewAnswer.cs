namespace RestaurantDelivery.Core.Entities;

// One customer's response to one SurveyQuestion, within one OrderReview. AnswerValue is
// plain text for every question type - a star rating is stored as its digit string, a
// single-choice answer as the chosen label - since every type is rendered back the same
// simple way in the admin "View Details" modal with no type-specific parsing needed there.
public class ReviewAnswer
{
    public int Id { get; set; }

    public int OrderReviewId { get; set; }
    public OrderReview OrderReview { get; set; } = null!;

    public int SurveyQuestionId { get; set; }
    public SurveyQuestion SurveyQuestion { get; set; } = null!;

    public string AnswerValue { get; set; } = string.Empty;
}
