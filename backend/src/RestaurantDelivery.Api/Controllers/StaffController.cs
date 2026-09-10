using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Auth;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

// The Staff tab's management table (list/edit/delete existing staff accounts) - creation
// stays at the pre-existing POST api/auth/staff (AuthController.CreateStaff), unchanged,
// so the existing Create Account form needs no changes. Same Module.Staff policy as that
// endpoint - listing/editing/deleting staff is exactly as sensitive as creating them.
[ApiController]
[Route("api/staff")]
[Authorize(Policy = "Module.Staff")]
public class StaffController : ControllerBase
{
    private readonly IAuthService _authService;

    public StaffController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<List<StaffAccountResponse>>> GetStaff()
    {
        var result = await _authService.GetStaffAccountsAsync();
        return Ok(result.Data);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<StaffAccountResponse>> UpdateStaff(string id, UpdateStaffUserRequest request)
    {
        var result = await _authService.UpdateStaffUserAsync(id, request);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteStaff(string id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _authService.DeleteStaffUserAsync(id, currentUserId);
        return result.Succeeded ? Ok() : BadRequest(new { errors = result.Errors });
    }
}
