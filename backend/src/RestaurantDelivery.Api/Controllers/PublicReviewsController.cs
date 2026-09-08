using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Reviews;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

// Fully anonymous - backs the customer-facing survey page at /rate/store and
// /customer-review/:orderId. No [Authorize] anywhere in this controller: a guest order can be
// reviewed too (matches this app's "guest checkout is a first-class flow" convention),
// and a general store-wide review has no order or account to gate on at all. Kept
// separate from ReviewsController (staff-only, Module.Reviews) so it's obvious at a
// glance that nothing here requires a permission.
[ApiController]
[Route("api/public/reviews")]
public class PublicReviewsController : ControllerBase
{
    private readonly IReviewService _service;

    public PublicReviewsController(IReviewService service)
    {
        _service = service;
    }

    // Always active-only - unlike ReviewsController.GetQuestions (the admin builder's
    // version, which also needs to see retired questions), the public survey page must
    // never be able to render or submit answers against a deactivated question.
    [HttpGet("questions")]
    public async Task<ActionResult<List<SurveyQuestionResponse>>> GetQuestions()
    {
        return Ok(await _service.GetQuestionsAsync(activeOnly: true));
    }

    // If the caller happens to be signed in (the interceptor on an already-logged-in
    // customer's browser attaches their token automatically), their own id is captured
    // as the reviewer; an anonymous/guest submission leaves it null rather than being
    // rejected. Deliberately not accepted as a request field - see the comment on
    // SubmitReviewRequest for why a client-supplied customer id would be unsafe to trust.
    [HttpPost("submit")]
    public async Task<ActionResult<OrderReviewDetailResponse>> Submit(SubmitReviewRequest request)
    {
        var customerId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        var result = await _service.SubmitReviewAsync(request, customerId);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }
}
