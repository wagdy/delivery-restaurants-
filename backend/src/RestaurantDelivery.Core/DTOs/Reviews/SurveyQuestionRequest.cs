using System.ComponentModel.DataAnnotations;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Reviews;

public class SurveyQuestionRequest : IValidatableObject
{
    [Required, MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    public SurveyQuestionType Type { get; set; }

    // Required, and only meaningful, when Type is SingleChoice - validated below rather
    // than with a plain attribute, since "required" here depends on Type.
    public List<string>? Options { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type == SurveyQuestionType.SingleChoice && (Options is null || Options.Count(o => !string.IsNullOrWhiteSpace(o)) < 2))
        {
            yield return new ValidationResult(
                "A Single Choice question needs at least 2 options.",
                new[] { nameof(Options) });
        }
    }
}
