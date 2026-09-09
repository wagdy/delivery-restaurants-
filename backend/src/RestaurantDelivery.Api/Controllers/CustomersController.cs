using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Customers;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

// Currently just one action: the Scanner page's "New Customer" tab. Gated by
// Module.Scanner (not Module.CustomerInsights/Crm, which CrmController already owns) since
// registering a walk-in customer on the spot is squarely a Scanner-staff action, not a CRM
// reporting one.
[ApiController]
[Route("api/customers")]
[Authorize(Policy = "Module.Scanner")]
public class CustomersController : ControllerBase
{
    private readonly IAuthService _authService;

    public CustomersController(IAuthService authService)
    {
        _authService = authService;
    }

    // Shares AuthService.FindOrCreateCustomerByPhoneAsync with the Create Order page's own
    // New Customer section, so a customer registered from either page gets the identical
    // 100-point welcome bonus and welcome WhatsApp notification. Re-submitting an
    // already-registered phone number resolves to that existing account (IsNewCustomer:
    // false in the response, no bonus/notification re-sent) rather than erroring - the same
    // "self-healing, not a hard failure" behavior that method already has.
    [HttpPost("register")]
    public async Task<ActionResult<FindOrCreateCustomerResult>> Register(RegisterCustomerRequest request)
    {
        var result = await _authService.FindOrCreateCustomerByPhoneAsync(request.CustomerName, request.Phone, address: null);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }
}
