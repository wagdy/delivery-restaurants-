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

    // Managed section this question belongs to (e.g. "Service", "Food", "Atmosphere") -
    // only meaningful for Type == StarRating, which the public survey renders as a single
    // grouped ratings matrix (see SurveyFormComponent). Null falls back to a generic
    // "General Rating" bucket rather than leaving a blank section header. Unused by every
    // other question type, which still render as their own individual field. A real FK
    // into SurveyMatrixSection (replacing an earlier free-text Category string) so a
    // section name can't drift into near-duplicates ("Service"/"service ") across
    // questions - see SurveyMatrixSection's own doc comment.
    public int? MatrixSectionId { get; set; }
    public SurveyMatrixSection? MatrixSection { get; set; }

    // An inactive question is kept (and its historical ReviewAnswers stay intact) but no
    // longer offered on the survey form - the same "soft off-switch" convention as
    // PromoCode.IsActive/LoyaltyCampaign.IsActive elsewhere in this app, so retiring a
    // question never orphans or rewrites past answers.
    public bool IsActive { get; set; } = true;

    // A soft delete, not IsActive again: deleting a question the admin no longer wants
    // to see anywhere (including their own builder) can't be a hard Remove() once
    // ReviewAnswers reference it - Postgres would reject it (ReviewAnswerConfiguration's
    // SurveyQuestion FK is DeleteBehavior.Restrict) rather than silently orphan or
    // cascade-delete real customer answers. IsDeleted hides the row everywhere
    // (ReviewService.GetQuestionsAsync excludes it unconditionally, for both the admin
    // builder and the public survey) while the row - and every answer pointing at it -
    // stays intact for historical "Submitted Reviews" detail views.
    public bool IsDeleted { get; set; }

    // Determines render order on the survey form and in the admin builder - admins can
    // reorder questions without their Id (and therefore every historical ReviewAnswer
    // referencing them) changing.
    public int DisplayOrder { get; set; }

    public ICollection<ReviewAnswer> Answers { get; set; } = new List<ReviewAnswer>();
}
