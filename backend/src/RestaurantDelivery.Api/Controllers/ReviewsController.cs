using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Reviews;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

// Every action here is staff-only (Module.Reviews) - the two anonymous/public actions
// (the active question list and review submission, used by the customer-facing survey
// page at /rate/store and /customer-review/:orderId) live separately in
// PublicReviewsController, so a glance at this controller's attributes alone is enough to
// confirm nothing here is reachable without the Reviews permission.
[ApiController]
[Route("api/reviews")]
[Authorize(Policy = "Module.Reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _service;

    public ReviewsController(IReviewService service)
    {
        _service = service;
    }

    // Admin builder only - activeOnly=false (the default) also returns retired questions
    // so they can be re-activated. The public survey page uses
    // PublicReviewsController.GetQuestions instead, which always forces activeOnly=true.
    [HttpGet("questions")]
    public async Task<ActionResult<List<SurveyQuestionResponse>>> GetQuestions([FromQuery] bool activeOnly = false)
    {
        return Ok(await _service.GetQuestionsAsync(activeOnly));
    }

    [HttpPost("questions")]
    public async Task<ActionResult<SurveyQuestionResponse>> CreateQuestion(SurveyQuestionRequest request)
    {
        var result = await _service.CreateQuestionAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [HttpPut("questions/{id:int}")]
    public async Task<ActionResult<SurveyQuestionResponse>> UpdateQuestion(int id, SurveyQuestionRequest request)
    {
        var result = await _service.UpdateQuestionAsync(id, request);
        if (!result.Succeeded)
        {
            var message = result.Errors.FirstOrDefault() ?? string.Empty;
            return message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [HttpDelete("questions/{id:int}")]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var result = await _service.DeleteQuestionAsync(id);
        if (!result.Succeeded)
        {
            var message = result.Errors.FirstOrDefault() ?? string.Empty;
            return message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(await _service.GetReviewsAsync(page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderReviewDetailResponse>> GetById(int id)
    {
        var result = await _service.GetReviewByIdAsync(id);
        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }
}
