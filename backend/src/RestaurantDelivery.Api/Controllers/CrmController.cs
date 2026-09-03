using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Customers;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

// A separate controller (not an extra action on CustomersController) specifically so this
// route's [Authorize] policy isn't ANDed with CustomersController's own class-level
// Module.Customers policy - ASP.NET Core combines a class-level and action-level
// [Authorize] as "both must pass", not "either" - Crm is meant to be an independent
// permission from the pre-existing Customers module.
[ApiController]
[Route("api/crm")]
[Authorize(Policy = "Module.Crm")]
public class CrmController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CrmController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet("customers")]
    public async Task<ActionResult<List<CustomerCrmResponse>>> GetCustomers()
    {
        return Ok(await _customerService.GetCrmCustomersAsync());
    }
}
