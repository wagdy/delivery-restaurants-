namespace RestaurantDelivery.Core.Enums;

public enum SurveyQuestionType
{
    StarRating = 0,
    ShortText = 1,
    LongText = 2,

    // SurveyQuestion.Options holds the comma-separated choice labels for this type only -
    // meaningless (and left null) for every other question type.
    SingleChoice = 3
}
