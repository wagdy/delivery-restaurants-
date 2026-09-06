using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.PromoCodes;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

// Unlike TiersController, GetAll here is admin-gated, not public - a customer only ever
// needs to submit a code string and get back a discount preview (see
// CheckoutController.ValidatePromo); publicly listing every code would let anyone browse
// promo codes that were never advertised to them.
[ApiController]
[Route("api/promo-codes")]
[Authorize(Policy = "Module.PromoCodes")]
public class PromoCodesController : ControllerBase
{
    private readonly IPromoCodeService _service;

    public PromoCodesController(IPromoCodeService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<PromoCodeResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpPost]
    public async Task<ActionResult<PromoCodeResponse>> Create(PromoCodeRequest request)
    {
        var result = await _service.CreateAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PromoCodeResponse>> Update(int id, PromoCodeRequest request)
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
