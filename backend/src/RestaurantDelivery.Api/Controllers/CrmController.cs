using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Common;
using RestaurantDelivery.Core.DTOs.Customers;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

// Serves the merged "Customer Insights" dashboard (formerly two separate pages, CRM and
// Customers) - gated by "Module.CustomerInsights", which succeeds for either of the two
// pre-existing module grants (Crm or Customers) rather than requiring both, so no
// already-configured staff role loses access to this page by the merge alone.
[ApiController]
[Route("api/crm")]
[Authorize(Policy = "Module.CustomerInsights")]
public class CrmController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CrmController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet("customers")]
    public async Task<ActionResult<PagedResult<CustomerAnalyticsResponse>>> GetCustomers(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
    {
        return Ok(await _customerService.GetAnalyticsPagedAsync(page, pageSize, search));
    }

    [HttpGet("customers/export")]
    public async Task<IActionResult> ExportCustomers([FromQuery] string? search = null)
    {
        var stream = await _customerService.ExportAnalyticsAsync(search);
        return File(
            stream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "customer-insights.xlsx");
    }

    [HttpPut("customers/{id}")]
    public async Task<ActionResult<CustomerAnalyticsResponse>> UpdateCustomer(string id, UpdateCustomerRequest request)
    {
        var result = await _customerService.UpdateCustomerAsync(id, request);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    // Soft delete - see AppUser.IsDeleted. The customer disappears from this list and every
    // other lookup, but their Orders/OrderReviews/LoyaltyProfile history is untouched.
    [HttpDelete("customers/{id}")]
    public async Task<IActionResult> DeleteCustomer(string id)
    {
        var result = await _customerService.DeleteCustomerAsync(id);
        return result.Succeeded ? Ok() : BadRequest(new { errors = result.Errors });
    }
}
