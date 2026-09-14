using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Api.Services;
using RestaurantDelivery.Core.DTOs.Maintenance;

namespace RestaurantDelivery.Api.Controllers;

// One-off repair operations an admin runs by hand. Restricted to the Admin role itself
// rather than a Module.* policy: these rewrite stored content site-wide, which is not
// something a scoped custom role should inherit by holding one module.
[ApiController]
[Route("api/maintenance")]
[Authorize(Roles = "Admin")]
public class MaintenanceController : ControllerBase
{
    private readonly IImageBackfillService _imageBackfillService;

    public MaintenanceController(IImageBackfillService imageBackfillService)
    {
        _imageBackfillService = imageBackfillService;
    }

    // Re-encodes images uploaded before the pipeline resized and converted them.
    //
    // Defaults to a dry run. Nothing is written and no record changes until apply=true is
    // passed explicitly, so the report can be read first - it lists every image, what it
    // would become, and why anything was skipped. Running it twice is harmless: already
    // converted images are skipped.
    //
    // POST /api/maintenance/reencode-images            preview
    // POST /api/maintenance/reencode-images?apply=true actually rewrite
    [HttpPost("reencode-images")]
    public async Task<ActionResult<ImageBackfillResult>> ReencodeImages([FromQuery] bool apply = false, CancellationToken ct = default)
    {
        var result = await _imageBackfillService.RunAsync(apply, ct);
        return Ok(result);
    }
}
