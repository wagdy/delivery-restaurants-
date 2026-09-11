using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Reviews;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

// Manages the matrix-section lookup list the Survey Configuration tab's "Manage Sections"
// dialog reads/writes - same Module.Reviews policy as ReviewsController, since only staff
// who can already edit survey questions should be able to add/remove the sections those
// questions link to.
[ApiController]
[Route("api/survey-sections")]
[Authorize(Policy = "Module.Reviews")]
public class SurveySectionsController : ControllerBase
{
    private readonly ISurveySectionService _service;

    public SurveySectionsController(ISurveySectionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<SurveyMatrixSectionResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpPost]
    public async Task<ActionResult<SurveyMatrixSectionResponse>> Create(SurveyMatrixSectionRequest request)
    {
        var result = await _service.CreateAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result.Succeeded)
        {
            var message = result.Errors.FirstOrDefault() ?? string.Empty;
            return message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { errors = result.Errors })
                : Conflict(new { errors = result.Errors });
        }

        return NoContent();
    }
}
