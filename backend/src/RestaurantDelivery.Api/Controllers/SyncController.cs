using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Dgtera;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

[ApiController]
[Route("api/sync")]
[Authorize(Roles = "Admin")]
public class SyncController : ControllerBase
{
    private readonly IDgteraSyncService _dgteraSyncService;

    public SyncController(IDgteraSyncService dgteraSyncService)
    {
        _dgteraSyncService = dgteraSyncService;
    }

    // Manually triggered from the admin dashboard's "Sync Dgtera Orders" button. Always
    // returns 200 with a result summary (fetched/created/updated/skipped + per-order
    // errors) rather than failing the whole request over one bad record - errors from
    // Dgtera being unreachable or misconfigured show up as populated Errors instead.
    [HttpPost("orders")]
    public async Task<ActionResult<SyncOrdersResult>> SyncOrders(CancellationToken cancellationToken)
    {
        var result = await _dgteraSyncService.SyncOrdersAsync(cancellationToken);
        return Ok(result);
    }
}
