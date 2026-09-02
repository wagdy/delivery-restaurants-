using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Customers;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Policy = "Module.Customers")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;

    public CustomersController(ICustomerService service)
    {
        _service = service;
    }

    [HttpGet("insights")]
    public async Task<ActionResult<List<CustomerInsightResponse>>> GetInsights()
    {
        return Ok(await _service.GetCustomerInsightsAsync());
    }
}
