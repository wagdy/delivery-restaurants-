namespace RestaurantDelivery.Core.Entities;

// A managed, named grouping for StarRating survey questions (e.g. "Service", "Food"),
// replacing SurveyQuestion's old free-text Category string with a real FK
// (SurveyQuestion.MatrixSectionId) - see that property's own doc comment for why: a
// dropdown sourced from this table can't drift into "Service"/"service "/"Servcie" the
// way a free-text field could, which is exactly what made the public survey's ratings
// matrix (SurveyFormComponent.starRatingCategories) unreliable to group by.
public class SurveyMatrixSection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
