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
    private readonly IAuthService _authService;

    public CrmController(ICustomerService customerService, IAuthService authService)
    {
        _customerService = customerService;
        _authService = authService;
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

    // The "Register Past Customer" dialog on this same Customer Insights page - for a
    // walk-in/branch customer who never placed a delivery order, so there's no Scanner/
    // Create Order context to register them from. Shares
    // AuthService.FindOrCreateCustomerByPhoneAsync with those two flows (100-point bonus,
    // auto-generated password, reactivation of a soft-deleted match), but with
    // isPastCustomer: true so the welcome WhatsApp is SendPastCustomerWelcomeAsync's
    // no-review-link variant instead. A separate action from CustomersController.Register
    // rather than a body flag on that one, since this page is gated by
    // Module.CustomerInsights, not Module.Scanner, and ASP.NET Core combines (ANDs) a
    // class-level [Authorize] with an action-level one rather than overriding it - putting
    // this here, in the controller already gated the way this page needs, is what actually
    // lets a Crm/Customers-only admin (with no Scanner grant) use it.
    [HttpPost("customers/register-past")]
    public async Task<ActionResult<FindOrCreateCustomerResult>> RegisterPastCustomer(RegisterCustomerRequest request)
    {
        var result = await _authService.FindOrCreateCustomerByPhoneAsync(request.CustomerName, request.Phone, address: null, isPastCustomer: true);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }
}
