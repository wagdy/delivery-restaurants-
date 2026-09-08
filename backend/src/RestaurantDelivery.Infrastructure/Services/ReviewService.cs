using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Common;
using RestaurantDelivery.Core.DTOs.Reviews;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Services;

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _context;

    public ReviewService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SurveyQuestionResponse>> GetQuestionsAsync(bool activeOnly)
    {
        var query = _context.SurveyQuestions.AsQueryable();
        if (activeOnly)
        {
            query = query.Where(q => q.IsActive);
        }

        var questions = await query.OrderBy(q => q.DisplayOrder).ThenBy(q => q.Id).ToListAsync();
        return questions.Select(MapQuestionResponse).ToList();
    }

    public async Task<ServiceResult<SurveyQuestionResponse>> CreateQuestionAsync(SurveyQuestionRequest request)
    {
        var question = new SurveyQuestion
        {
            Text = request.Text.Trim(),
            Type = request.Type,
            Options = SerializeOptions(request),
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder
        };

        _context.SurveyQuestions.Add(question);
        await _context.SaveChangesAsync();

        return ServiceResult<SurveyQuestionResponse>.Success(MapQuestionResponse(question));
    }

    public async Task<ServiceResult<SurveyQuestionResponse>> UpdateQuestionAsync(int id, SurveyQuestionRequest request)
    {
        var question = await _context.SurveyQuestions.FindAsync(id);
        if (question is null)
        {
            return ServiceResult<SurveyQuestionResponse>.Failure("Question not found.");
        }

        question.Text = request.Text.Trim();
        question.Type = request.Type;
        question.Options = SerializeOptions(request);
        question.IsActive = request.IsActive;
        question.DisplayOrder = request.DisplayOrder;

        await _context.SaveChangesAsync();

        return ServiceResult<SurveyQuestionResponse>.Success(MapQuestionResponse(question));
    }

    public async Task<ServiceResult<bool>> DeleteQuestionAsync(int id)
    {
        var question = await _context.SurveyQuestions.FindAsync(id);
        if (question is null)
        {
            return ServiceResult<bool>.Failure("Question not found.");
        }

        var answerCount = await _context.ReviewAnswers.CountAsync(a => a.SurveyQuestionId == id);
        if (answerCount > 0)
        {
            return ServiceResult<bool>.Failure(
                $"Cannot delete this question because {answerCount} customer answer(s) already reference it. Deactivate it instead.");
        }

        _context.SurveyQuestions.Remove(question);
        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    public async Task<PagedResult<OrderReviewResponse>> GetReviewsAsync(int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _context.OrderReviews
            .Include(r => r.Order)
            .Include(r => r.Customer)
            .OrderByDescending(r => r.CreatedAt)
            .AsQueryable();

        var totalCount = await query.CountAsync();
        var reviews = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<OrderReviewResponse>
        {
            Items = reviews.Select(MapReviewResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ServiceResult<OrderReviewDetailResponse>> GetReviewByIdAsync(int id)
    {
        var review = await _context.OrderReviews
            .Include(r => r.Order)
            .Include(r => r.Customer)
            .Include(r => r.Answers)
                .ThenInclude(a => a.SurveyQuestion)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (review is null)
        {
            return ServiceResult<OrderReviewDetailResponse>.Failure("Review not found.");
        }

        return ServiceResult<OrderReviewDetailResponse>.Success(MapDetailResponse(review));
    }

    public async Task<ServiceResult<OrderReviewDetailResponse>> SubmitReviewAsync(SubmitReviewRequest request, string? customerId)
    {
        // Order-specific link (SendPostDeliveryPointsNotificationAsync's "/customer-review/{id}")
        // vs. general store-wide link (SendLoyaltyWalletUpdateAsync's "/rate/store", which
        // has no order context at all) - only the former has an order to validate/dedupe.
        if (request.OrderId is { } orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order is null)
            {
                return ServiceResult<OrderReviewDetailResponse>.Failure("Order not found.");
            }

            if (order.Status != OrderStatus.Delivered)
            {
                return ServiceResult<OrderReviewDetailResponse>.Failure("Only a delivered order can be reviewed.");
            }

            if (await _context.OrderReviews.AnyAsync(r => r.OrderId == orderId))
            {
                return ServiceResult<OrderReviewDetailResponse>.Failure("This order has already been reviewed.");
            }
        }

        // Silently drops an answer for a question that's since been deactivated or
        // deleted, rather than failing the whole submission over stale form state.
        var questionIds = request.Answers.Select(a => a.SurveyQuestionId).Distinct().ToList();
        var validQuestionIds = await _context.SurveyQuestions
            .Where(q => questionIds.Contains(q.Id) && q.IsActive)
            .Select(q => q.Id)
            .ToListAsync();

        var review = new OrderReview
        {
            OrderId = request.OrderId,
            CustomerId = customerId,
            OverallRating = request.OverallRating,
            Answers = request.Answers
                .Where(a => validQuestionIds.Contains(a.SurveyQuestionId))
                .Select(a => new ReviewAnswer
                {
                    SurveyQuestionId = a.SurveyQuestionId,
                    AnswerValue = a.AnswerValue.Trim()
                })
                .ToList()
        };

        _context.OrderReviews.Add(review);
        await _context.SaveChangesAsync();

        // Re-fetched (rather than mapped from the in-memory graph) so the response's
        // per-answer QuestionText/QuestionType come from the same Include chain
        // GetReviewByIdAsync already uses - the just-saved review's Answers don't have
        // their SurveyQuestion navigation populated yet.
        return await GetReviewByIdAsync(review.Id);
    }

    private static string? SerializeOptions(SurveyQuestionRequest request) =>
        request.Type == SurveyQuestionType.SingleChoice && request.Options is { Count: > 0 }
            ? string.Join(",", request.Options.Select(o => o.Trim()).Where(o => o.Length > 0))
            : null;

    private static SurveyQuestionResponse MapQuestionResponse(SurveyQuestion question) => new()
    {
        Id = question.Id,
        Text = question.Text,
        Type = question.Type,
        Options = question.Options?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        IsActive = question.IsActive,
        DisplayOrder = question.DisplayOrder
    };

    // Order.CustomerName first (a guest's snapshot name at order time), then
    // Customer.FullName (a signed-in customer's order-less store review), then a static
    // placeholder (a fully anonymous store review - no order, no account).
    private static string ResolveCustomerName(OrderReview review) =>
        review.Order?.CustomerName ?? review.Customer?.FullName ?? "Anonymous Customer";

    private static OrderReviewResponse MapReviewResponse(OrderReview review) => new()
    {
        Id = review.Id,
        OrderId = review.OrderId,
        CustomerName = ResolveCustomerName(review),
        OverallRating = review.OverallRating,
        CreatedAt = review.CreatedAt
    };

    private static OrderReviewDetailResponse MapDetailResponse(OrderReview review) => new()
    {
        Id = review.Id,
        OrderId = review.OrderId,
        CustomerName = ResolveCustomerName(review),
        OverallRating = review.OverallRating,
        CreatedAt = review.CreatedAt,
        Answers = review.Answers.Select(a => new ReviewAnswerResponse
        {
            SurveyQuestionId = a.SurveyQuestionId,
            QuestionText = a.SurveyQuestion.Text,
            QuestionType = a.SurveyQuestion.Type,
            AnswerValue = a.AnswerValue
        }).ToList()
    };
}
