using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Tiers;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

// Named plural (TiersController), matching every other admin-CRUD controller in this app
// (CategoriesController, SubCategoriesController, CampaignsController) rather than the
// literal singular "TierController" - the route is api/tiers either way.
//
// GetAll is public (no [Authorize]) like Categories/SubCategories - the storefront's own
// Rewards tab needs the full tier list (name + point range) to compute "points to next
// tier" for any customer, not just admins; only the mutating endpoints below are gated
// behind the same Module.Campaigns policy the rest of the Campaign Manager already uses,
// since this panel lives inside that same admin screen.
[ApiController]
[Route("api/tiers")]
public class TiersController : ControllerBase
{
    private readonly ITierService _service;

    public TiersController(ITierService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<TierResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [Authorize(Policy = "Module.Campaigns")]
    [HttpPost]
    public async Task<ActionResult<TierResponse>> Create(TierRequest request)
    {
        var result = await _service.CreateAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [Authorize(Policy = "Module.Campaigns")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<TierResponse>> Update(int id, TierRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        if (!result.Succeeded)
        {
            var message = result.Errors.FirstOrDefault() ?? string.Empty;
            return message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [Authorize(Policy = "Module.Campaigns")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return NoContent();
    }
}
