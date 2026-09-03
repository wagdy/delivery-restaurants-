using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Loyalty;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

// No class-level [Authorize] - CRUD is Module.Campaigns, my-progress is any authenticated
// customer, punch/redeem-reward are Module.Scanner.
[ApiController]
[Route("api/campaigns")]
public class CampaignsController : ControllerBase
{
    private readonly ICampaignService _campaignService;

    public CampaignsController(ICampaignService campaignService)
    {
        _campaignService = campaignService;
    }

    [Authorize(Policy = "Module.Campaigns")]
    [HttpGet]
    public async Task<ActionResult<List<CampaignResponse>>> GetAll(CancellationToken ct)
    {
        return Ok(await _campaignService.GetAllAsync(ct));
    }

    [Authorize(Policy = "Module.Campaigns")]
    [HttpPost]
    public async Task<ActionResult<CampaignResponse>> Create(CreateCampaignRequest request, CancellationToken ct)
    {
        var result = await _campaignService.CreateAsync(request, ct);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [Authorize(Policy = "Module.Campaigns")]
    [HttpPut("{id:guid}/toggle-status")]
    public async Task<ActionResult<CampaignResponse>> ToggleStatus(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.ToggleStatusAsync(id, ct);
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }

    [Authorize(Policy = "Module.Campaigns")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.DeleteAsync(id, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return NoContent();
    }

    [Authorize]
    [HttpGet("my-progress")]
    public async Task<ActionResult<List<CustomerCampaignProgressResponse>>> GetMyProgress(CancellationToken ct)
    {
        return Ok(await _campaignService.GetMyProgressAsync(GetAppUserId(), ct));
    }

    [Authorize(Policy = "Module.Scanner")]
    [HttpPost("punch")]
    public async Task<ActionResult<PunchResult>> Punch(PunchRequest request, CancellationToken ct)
    {
        var result = await _campaignService.PunchAsync(GetAppUserId(), request, ct);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [Authorize(Policy = "Module.Scanner")]
    [HttpPost("redeem-reward")]
    public async Task<ActionResult<RedeemRewardResult>> RedeemReward(RedeemRewardRequest request, CancellationToken ct)
    {
        var result = await _campaignService.RedeemRewardAsync(GetAppUserId(), request, ct);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    private string GetAppUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated request is missing a NameIdentifier claim.");
}
