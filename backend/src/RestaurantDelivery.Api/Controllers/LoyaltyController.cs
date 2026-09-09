using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Api.Configuration;
using RestaurantDelivery.Api.Services.Loyalty;
using RestaurantDelivery.Core.DTOs.Loyalty;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Api.Controllers;

// No class-level [Authorize] - actions have mixed auth needs: customer-JWT-authenticated
// (me/wallet links), staff-only (earn/redeem/scanner lookup via Module.Scanner), and the
// Apple PassKit protocol group (its own "ApplePass <token>" scheme, no JWT at all).
[ApiController]
[Route("api/loyalty")]
public class LoyaltyController : ControllerBase
{
    private readonly ILoyaltyService _loyaltyService;
    private readonly ICampaignService _campaignService;
    private readonly ApplicationDbContext _context;
    private readonly IApplePassBuilder _passBuilder;
    private readonly IApplePassKitService _passKitService;
    private readonly IWalletAuthTokenService _authTokenService;
    private readonly IGoogleWalletService _googleWalletService;
    private readonly AppleWalletSettings _appleSettings;
    private readonly GoogleWalletSettings _googleSettings;
    private readonly ILogger<LoyaltyController> _logger;

    public LoyaltyController(
        ILoyaltyService loyaltyService,
        ICampaignService campaignService,
        ApplicationDbContext context,
        IApplePassBuilder passBuilder,
        IApplePassKitService passKitService,
        IWalletAuthTokenService authTokenService,
        IGoogleWalletService googleWalletService,
        IOptions<AppleWalletSettings> appleOptions,
        IOptions<GoogleWalletSettings> googleOptions,
        ILogger<LoyaltyController> logger)
    {
        _loyaltyService = loyaltyService;
        _campaignService = campaignService;
        _context = context;
        _passBuilder = passBuilder;
        _passKitService = passKitService;
        _authTokenService = authTokenService;
        _googleWalletService = googleWalletService;
        _appleSettings = appleOptions.Value;
        _googleSettings = googleOptions.Value;
        _logger = logger;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<LoyaltyMeResponse>> Me(CancellationToken ct)
    {
        var response = await _loyaltyService.GetOrCreateProfileAsync(GetAppUserId(), ct);
        response.AppleWalletAvailable = _appleSettings.IsConfigured;
        response.GoogleWalletAvailable = _googleSettings.IsConfigured;
        return Ok(response);
    }

    [Authorize(Policy = "Module.Scanner")]
    [HttpPost("earn")]
    public async Task<ActionResult<LoyaltyTransactionResponse>> Earn(EarnPointsRequest request, CancellationToken ct)
    {
        var result = await _loyaltyService.EarnPointsAsync(GetAppUserId(), request, ct);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    [Authorize(Policy = "Module.Scanner")]
    [HttpPost("redeem")]
    public async Task<ActionResult<LoyaltyTransactionResponse>> Redeem(RedeemPointsRequest request, CancellationToken ct)
    {
        var result = await _loyaltyService.RedeemPointsAsync(GetAppUserId(), request, ct);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    // Global earn/redeem ratios - edited from the Campaign Manager screen, so gated the
    // same way as the rest of that screen (Module.Campaigns) rather than Module.Scanner.
    [Authorize(Policy = "Module.Campaigns")]
    [HttpGet("settings")]
    public async Task<ActionResult<LoyaltySettingsResponse>> GetSettings(CancellationToken ct)
    {
        return Ok(await _loyaltyService.GetSettingsAsync(ct));
    }

    [Authorize(Policy = "Module.Campaigns")]
    [HttpPut("settings")]
    public async Task<ActionResult<LoyaltySettingsResponse>> UpdateSettings(UpdateLoyaltySettingsRequest request, CancellationToken ct)
    {
        var result = await _loyaltyService.UpdateSettingsAsync(request, ct);
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    // Combined lookup for the Scanner UI after decoding a customer's digital-card QR
    // (which encodes their raw AppUserId) - points/tier plus every active campaign's
    // progress in one call, so the Scanner screen only needs one request per scan.
    [Authorize(Policy = "Module.Scanner")]
    [HttpGet("scanner/{customerId}")]
    public async Task<ActionResult<ScannerCustomerResponse>> GetScannerCustomer(string customerId, CancellationToken ct)
    {
        var appUser = await _context.Users.FindAsync([customerId], ct);
        if (appUser is null || appUser.IsDeleted)
        {
            return NotFound(new { error = "Customer not found." });
        }

        return Ok(await BuildScannerCustomerResponseAsync(appUser, ct));
    }

    // Manual fallback for the Scanner UI when a customer's phone is scratched/unreadable
    // or their phone battery is dead - staff type the number instead of scanning the QR.
    // Exact match only (delivery has no phone-normalization utility, unlike the punch-card
    // source app) - staff should type the number as the customer originally registered it.
    [Authorize(Policy = "Module.Scanner")]
    [HttpGet("scanner/by-phone")]
    public async Task<ActionResult<ScannerCustomerResponse>> GetScannerCustomerByPhone([FromQuery] string phone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new { error = "Phone number is required." });
        }

        var appUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone.Trim(), ct);
        if (appUser is null || appUser.IsDeleted)
        {
            return NotFound(new { error = "Customer not found." });
        }

        return Ok(await BuildScannerCustomerResponseAsync(appUser, ct));
    }

    private async Task<ScannerCustomerResponse> BuildScannerCustomerResponseAsync(AppUser appUser, CancellationToken ct)
    {
        var profile = await _loyaltyService.GetOrCreateProfileAsync(appUser.Id, ct);
        var campaigns = await _campaignService.GetMyProgressAsync(appUser.Id, ct);

        return new ScannerCustomerResponse
        {
            AppUserId = appUser.Id,
            FullName = appUser.FullName,
            PhoneNumber = appUser.PhoneNumber,
            CurrentPoints = profile.CurrentPoints,
            TotalLifetimePoints = profile.TotalLifetimePoints,
            MembershipTier = profile.MembershipTier,
            Campaigns = campaigns
        };
    }

    // Deliberately no path-param customerId (the source app took one, unauthenticated -
    // an IDOR hole) - the customer always comes from their own token.
    [Authorize]
    [HttpGet("wallet/apple/pass")]
    public async Task<IActionResult> GetApplePass(CancellationToken ct)
    {
        if (!_appleSettings.IsConfigured)
        {
            return Problem(detail: "Apple Wallet is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable, title: "Apple Wallet not configured");
        }

        var appUserId = GetAppUserId();
        var appUser = await _context.Users.FindAsync([appUserId], ct);
        if (appUser is null)
        {
            return NotFound();
        }

        await _loyaltyService.GetOrCreateProfileAsync(appUserId, ct);
        var profile = await _context.LoyaltyProfiles.FirstAsync(p => p.AppUserId == appUserId, ct);

        var pkpass = _passBuilder.Build(appUser, profile, appUser.Id);
        return File(pkpass, "application/vnd.apple.pkpass", "loyalty.pkpass");
    }

    [Authorize]
    [HttpGet("wallet/google/save-link")]
    public async Task<IActionResult> GetGoogleWalletSaveLink(CancellationToken ct)
    {
        if (!_googleSettings.IsConfigured)
        {
            return Problem(detail: "Google Wallet is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable, title: "Google Wallet not configured");
        }

        var appUserId = GetAppUserId();
        var appUser = await _context.Users.FindAsync([appUserId], ct);
        if (appUser is null)
        {
            return NotFound();
        }

        await _loyaltyService.GetOrCreateProfileAsync(appUserId, ct);
        var profile = await _context.LoyaltyProfiles.FirstAsync(p => p.AppUserId == appUserId, ct);

        try
        {
            var saveUrl = await _googleWalletService.GenerateSaveLinkAsync(appUser, profile, ct);
            return Ok(new { saveUrl });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable, title: "Google Wallet not configured");
        }
    }

    // --- Apple PassKit Web Service protocol - route shapes are fixed by Apple's own Wallet
    // daemon, not ours to rename. Authenticated via the "ApplePass <token>" header, not JWT. ---

    [HttpPost("wallet/apple/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}")]
    public async Task<IActionResult> RegisterDevice(string deviceLibraryIdentifier, string passTypeIdentifier, string serialNumber, RegisterDeviceRequest request, CancellationToken ct)
    {
        if (!TryGetApplePassToken(out var token) || !_authTokenService.Validate(serialNumber, token))
        {
            return Unauthorized();
        }

        var result = await _passKitService.RegisterDeviceAsync(serialNumber, deviceLibraryIdentifier, passTypeIdentifier, request.PushToken, ct);
        return result switch
        {
            null => NotFound(),
            true => StatusCode(StatusCodes.Status201Created),
            false => Ok()
        };
    }

    [HttpDelete("wallet/apple/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}")]
    public async Task<IActionResult> UnregisterDevice(string deviceLibraryIdentifier, string passTypeIdentifier, string serialNumber, CancellationToken ct)
    {
        if (!TryGetApplePassToken(out var token) || !_authTokenService.Validate(serialNumber, token))
        {
            return Unauthorized();
        }

        var removed = await _passKitService.UnregisterDeviceAsync(serialNumber, deviceLibraryIdentifier, passTypeIdentifier, ct);
        return removed ? Ok() : NotFound();
    }

    [HttpGet("wallet/apple/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}")]
    public async Task<IActionResult> GetUpdatablePasses(string deviceLibraryIdentifier, string passTypeIdentifier, [FromQuery] string? passesUpdatedSince, CancellationToken ct)
    {
        DateTimeOffset? since = DateTimeOffset.TryParse(passesUpdatedSince, out var parsed) ? parsed : null;
        var result = await _passKitService.GetUpdatablePassesAsync(deviceLibraryIdentifier, passTypeIdentifier, since, ct);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("wallet/apple/v1/passes/{passTypeIdentifier}/{serialNumber}")]
    public async Task<IActionResult> GetLatestPass(string passTypeIdentifier, string serialNumber, CancellationToken ct)
    {
        if (!TryGetApplePassToken(out var token) || !_authTokenService.Validate(serialNumber, token))
        {
            return Unauthorized();
        }

        if (!_appleSettings.IsConfigured || !string.Equals(passTypeIdentifier, _appleSettings.PassTypeIdentifier, StringComparison.Ordinal))
        {
            return NotFound();
        }

        var appUser = await _context.Users.FindAsync([serialNumber], ct);
        if (appUser is null)
        {
            return NotFound();
        }

        var profile = await _context.LoyaltyProfiles.FirstOrDefaultAsync(p => p.AppUserId == serialNumber, ct);
        if (profile is null)
        {
            return NotFound();
        }

        var lastModified = new DateTimeOffset(DateTime.SpecifyKind(profile.LastActivityDate, DateTimeKind.Utc));

        if (Request.Headers.IfModifiedSince.Count > 0 &&
            DateTimeOffset.TryParse(Request.Headers.IfModifiedSince, out var ifModifiedSince) &&
            lastModified <= ifModifiedSince)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        var pkpass = _passBuilder.Build(appUser, profile, appUser.Id);
        return new FileContentResult(pkpass, "application/vnd.apple.pkpass") { LastModified = lastModified };
    }

    [HttpPost("wallet/apple/v1/log")]
    public async Task<IActionResult> LogPassKitError()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        _logger.LogWarning("Wallet device log: {Log}", body);
        return Ok();
    }

    private string GetAppUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated request is missing a NameIdentifier claim.");

    private bool TryGetApplePassToken(out string? token)
    {
        token = null;
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("ApplePass ", StringComparison.Ordinal))
        {
            return false;
        }

        token = header["ApplePass ".Length..];
        return true;
    }
}
