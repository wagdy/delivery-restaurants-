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

    // Phone + password login, used by customers and staff alike - separate from the
    // email-based Login above, which only the two pre-existing legacy staff accounts
    // (and any legacy email-registered customer) still need.
    [HttpPost("login-by-phone")]
    public async Task<ActionResult<AuthResponse>> LoginByPhone(PhoneLoginRequest request)
    {
        var result = await _authService.LoginByPhoneAsync(request);
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
