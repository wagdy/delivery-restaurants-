using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Reviews;

public class SurveyQuestionResponse
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;

    // Serializes as a string (e.g. "StarRating") via the API's global
    // JsonStringEnumConverter, same convention as Order.Status/PaymentMethod elsewhere.
    public SurveyQuestionType Type { get; set; }

    // Split from the stored comma-separated string into a plain list - only populated
    // when Type is SingleChoice.
    public List<string>? Options { get; set; }

    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}
