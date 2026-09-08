using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Entities;

// One configurable question on the post-delivery customer survey (see
// WhatsAppNotificationService.SendPostDeliveryPointsNotificationAsync's rating link - this
// is the question set that link's future landing page renders and submits against).
public class SurveyQuestion
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public SurveyQuestionType Type { get; set; }

    // Comma-separated choice labels (e.g. "Great,Good,Average,Poor") - only meaningful,
    // and only ever set, when Type is SingleChoice.
    public string? Options { get; set; }

    // An inactive question is kept (and its historical ReviewAnswers stay intact) but no
    // longer offered on the survey form - the same "soft off-switch" convention as
    // PromoCode.IsActive/LoyaltyCampaign.IsActive elsewhere in this app, so retiring a
    // question never orphans or rewrites past answers.
    public bool IsActive { get; set; } = true;

    // Determines render order on the survey form and in the admin builder - admins can
    // reorder questions without their Id (and therefore every historical ReviewAnswer
    // referencing them) changing.
    public int DisplayOrder { get; set; }

    public ICollection<ReviewAnswer> Answers { get; set; } = new List<ReviewAnswer>();
}
