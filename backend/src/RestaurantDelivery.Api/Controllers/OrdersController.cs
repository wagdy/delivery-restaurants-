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
    private const long MaxBulkUploadSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IOrderService _service;
    private readonly IBulkOrderImportService _bulkImportService;

    public OrdersController(IOrderService service, IBulkOrderImportService bulkImportService)
    {
        _service = service;
        _bulkImportService = bulkImportService;
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

    [Authorize(Roles = "Admin")]
    [HttpGet("excel-template")]
    public IActionResult GetExcelTemplate()
    {
        var stream = _bulkImportService.GenerateTemplate();
        return File(
            stream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "bulk-order-template.xlsx");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("bulk-upload")]
    [RequestSizeLimit(MaxBulkUploadSizeBytes)]
    public async Task<ActionResult<BulkOrderImportResult>> BulkUpload(IFormFile file)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "No file was uploaded." } });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { errors = new[] { "Only .xlsx or .xls files are accepted." } });
        }

        await using var stream = file.OpenReadStream();

        BulkOrderImportResult result;
        try
        {
            result = await _bulkImportService.ImportOrdersAsync(stream);
        }
        catch (Exception ex)
        {
            // A workbook ClosedXML can't even open (corrupt file, not really an Excel
            // file despite the extension) fails here, before any per-row handling -
            // everything else is caught inside ImportOrdersAsync per-order instead.
            return BadRequest(new { errors = new[] { $"Could not read the uploaded file: {ex.Message}" } });
        }

        return Ok(result);
    }
}
