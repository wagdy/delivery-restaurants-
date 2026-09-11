using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Reviews;

public class SurveyMatrixSectionRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
