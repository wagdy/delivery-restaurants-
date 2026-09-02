using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Auth;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.Succeeded)
        {
            return Unauthorized(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    // Requires the Staff module specifically (not just any Admin) - provisioning staff
    // accounts and roles is a Staff-tab action, so a restricted admin without that module
    // can't grant themselves or anyone else more access.
    [Authorize(Policy = "Module.Staff")]
    [HttpPost("staff")]
    public async Task<ActionResult<UserProfileResponse>> CreateStaff(CreateStaffUserRequest request)
    {
        var result = await _authService.CreateStaffUserAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    // Staff (Admin/CaptainOrder) login with phone number + password - separate from the
    // email-based Login above, which customers and pre-existing staff accounts still use.
    [HttpPost("staff-login")]
    public async Task<ActionResult<AuthResponse>> StaffLogin(StaffLoginRequest request)
    {
        var result = await _authService.LoginStaffAsync(request);
        if (!result.Succeeded)
        {
            return Unauthorized(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _authService.GetProfileAsync(userId);
        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }
}
