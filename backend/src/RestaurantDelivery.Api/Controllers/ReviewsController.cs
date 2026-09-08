using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Reviews;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _service;

    public ReviewsController(IReviewService service)
    {
        _service = service;
    }

    // Public: the future customer-facing survey page needs the active question list
    // without being logged in (a guest order can be reviewed too). The admin builder
    // calls this same endpoint with activeOnly=false to also see retired questions.
    [HttpGet("questions")]
    public async Task<ActionResult<List<SurveyQuestionResponse>>> GetQuestions([FromQuery] bool activeOnly = false)
    {
        return Ok(await _service.GetQuestionsAsync(activeOnly));
    }

    [Authorize(Policy = "Module.Reviews")]
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

    [Authorize(Policy = "Module.Reviews")]
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

    [Authorize(Policy = "Module.Reviews")]
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

    [Authorize(Policy = "Module.Reviews")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(await _service.GetReviewsAsync(page, pageSize));
    }

    [Authorize(Policy = "Module.Reviews")]
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

    // Public, same reasoning as GetQuestions above - if the caller happens to be signed
    // in, their own id is captured as the reviewer; an anonymous/guest submission leaves
    // it null rather than being rejected.
    [HttpPost]
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
