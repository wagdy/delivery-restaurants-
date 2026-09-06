using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Checkout;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

[ApiController]
[Route("api/checkout")]
public class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _service;

    public CheckoutController(ICheckoutService service)
    {
        _service = service;
    }

    // Public: guests must be able to apply a promo code at checkout too, exactly like
    // POST /api/orders itself.
    [HttpPost("validate-promo")]
    public async Task<ActionResult<ValidatePromoResponse>> ValidatePromo(ValidatePromoRequest request)
    {
        var result = await _service.ValidatePromoAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }
}
