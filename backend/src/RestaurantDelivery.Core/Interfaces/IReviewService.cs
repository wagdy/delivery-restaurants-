using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Common;
using RestaurantDelivery.Core.DTOs.Reviews;

namespace RestaurantDelivery.Core.Interfaces;

public interface IReviewService
{
    // activeOnly=true is what the future customer-facing survey page passes; the admin
    // builder omits it so it can also see and re-activate retired questions.
    Task<List<SurveyQuestionResponse>> GetQuestionsAsync(bool activeOnly);

    Task<ServiceResult<SurveyQuestionResponse>> CreateQuestionAsync(SurveyQuestionRequest request);
    Task<ServiceResult<SurveyQuestionResponse>> UpdateQuestionAsync(int id, SurveyQuestionRequest request);
    Task<ServiceResult<bool>> DeleteQuestionAsync(int id);

    Task<PagedResult<OrderReviewResponse>> GetReviewsAsync(int page, int pageSize);
    Task<ServiceResult<OrderReviewDetailResponse>> GetReviewByIdAsync(int id);

    // customerId is the caller's own id when authenticated, null for an anonymous/guest
    // submission - never trusted from the request body itself.
    Task<ServiceResult<OrderReviewDetailResponse>> SubmitReviewAsync(SubmitReviewRequest request, string? customerId);
}
