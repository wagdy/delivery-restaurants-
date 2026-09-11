using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Reviews;

namespace RestaurantDelivery.Core.Interfaces;

// Manages SurveyMatrixSection - the named grouping lookup a StarRating SurveyQuestion
// optionally belongs to (SurveyQuestion.MatrixSectionId). Kept as its own small service
// rather than folded into IReviewService, mirroring how Category has its own
// ICategoryService distinct from IMenuItemService.
public interface ISurveySectionService
{
    Task<List<SurveyMatrixSectionResponse>> GetAllAsync();
    Task<ServiceResult<SurveyMatrixSectionResponse>> CreateAsync(SurveyMatrixSectionRequest request);

    // Blocked (not silently cascaded) if any SurveyQuestion still references this section -
    // see the implementation's own doc comment.
    Task<ServiceResult<bool>> DeleteAsync(int id);
}
