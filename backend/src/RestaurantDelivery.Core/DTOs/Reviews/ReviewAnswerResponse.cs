using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Reviews;

// Carries the question's own text/type alongside the answer - the "View Details" modal
// renders straight from this list with no separate question lookup needed, and it stays
// correct even if the question's wording is edited or the question is later deactivated.
public class ReviewAnswerResponse
{
    public int SurveyQuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public SurveyQuestionType QuestionType { get; set; }
    public string AnswerValue { get; set; } = string.Empty;
}
