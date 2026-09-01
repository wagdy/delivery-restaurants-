using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Push;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

[ApiController]
[Route("api/push")]
public class PushController : ControllerBase
{
    private readonly IPushNotificationService _service;

    public PushController(IPushNotificationService service)
    {
        _service = service;
    }

    // Public: the frontend needs this before a user has even logged in to call
    // PushManager.subscribe(), since permission can be requested pre-auth.
    [HttpGet("vapid-public-key")]
    public ActionResult<VapidPublicKeyResponse> GetVapidPublicKey()
    {
        return Ok(new VapidPublicKeyResponse { PublicKey = _service.GetVapidPublicKey() });
    }

    [Authorize]
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe(PushSubscriptionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _service.SubscribeAsync(userId, request);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return NoContent();
    }

    [Authorize]
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe(UnsubscribeRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _service.UnsubscribeAsync(userId, request.Endpoint);
        return NoContent();
    }
}
