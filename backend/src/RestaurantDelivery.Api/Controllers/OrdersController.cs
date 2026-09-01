using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Orders;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _service;

    public OrdersController(IOrderService service)
    {
        _service = service;
    }

    // Public: supports both guest checkout and authenticated checkout.
    // If the caller has a valid JWT, the order is linked to their account; otherwise it's a guest order.
    // Admins and captains are exempt from auto-linking: a manually entered phone-in order belongs
    // to the customer on the form, and a captain has no business placing personal orders here.
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request)
    {
        var userId = User.Identity?.IsAuthenticated == true
            && !User.IsInRole("Admin")
            && !User.IsInRole("CaptainOrder")
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        var result = await _service.CreateAsync(request, userId);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    // Admins get full order management; captains (delivery drivers) get read + status-update only.
    [Authorize(Policy = "OrdersAccess")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetAllAsync(status, page, pageSize);
        return Ok(result.Data);
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _service.GetMyOrdersAsync(userId);
        return Ok(result.Data);
    }

    // Admins and captains can view any order; authenticated customers can only view their own.
    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        var canViewAnyOrder = User.IsInRole("Admin") || User.IsInRole("CaptainOrder");
        if (!canViewAnyOrder && result.Data!.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            return Forbid();
        }

        return Ok(result.Data);
    }

    // Editing line items/customer info stays an admin-only capability.
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<OrderResponse>> Update(int id, UpdateOrderRequest request)
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

    // Captains use this to accept an order (-> OutForDelivery) and mark it delivered.
    [Authorize(Policy = "OrdersAccess")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(int id, UpdateOrderStatusRequest request)
    {
        var result = await _service.UpdateStatusAsync(id, request.Status);
        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [Authorize(Roles = "Admin")]
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
