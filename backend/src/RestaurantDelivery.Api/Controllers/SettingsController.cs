using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Api.Services;
using RestaurantDelivery.Core.DTOs.Common;
using RestaurantDelivery.Core.DTOs.Settings;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    // Longest edge for the full-viewport page background. The logo uploads deliberately
    // keep their original file: they are already small (the current centre logo is 23 KB),
    // and re-encoding a brand mark risks visible artefacting for no meaningful saving. The
    // favicon is left alone for a harder reason - FileUploadService cannot decode .ico at
    // all, and the frontend maps the stored extension to a <link type>, which a silent
    // rewrite to .webp would break.
    private const int BackgroundImageMaxDimension = 1920;

    private readonly ISettingsService _service;
    private readonly IFileUploadService _fileUploadService;

    public SettingsController(ISettingsService service, IFileUploadService fileUploadService)
    {
        _service = service;
        _fileUploadService = fileUploadService;
    }

    [HttpGet]
    public async Task<ActionResult<RestaurantSettingsResponse>> Get()
    {
        return Ok(await _service.GetAsync());
    }

    [Authorize(Policy = "Module.Settings")]
    [HttpPut]
    public async Task<ActionResult<RestaurantSettingsResponse>> Update(UpdateRestaurantSettingsRequest request)
    {
        var result = await _service.UpdateAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [Authorize(Policy = "Module.Settings")]
    [HttpPost("upload-logo")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<ImageUploadResponse>> UploadLogo(IFormFile file)
    {
        var result = await _fileUploadService.SaveImageAsync(file, "branding");
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = new[] { result.Error } });
        }

        var url = $"{Request.Scheme}://{Request.Host}{result.RelativePath}";
        return Ok(new ImageUploadResponse { Url = url });
    }

    [Authorize(Policy = "Module.Settings")]
    [HttpPost("upload-background-image")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<ImageUploadResponse>> UploadBackgroundImage(IFormFile file)
    {
        // The page background is stretched over the whole viewport with background-size:
        // cover (see styles.scss), so it is downloaded by every visitor on every page.
        // Stored unprocessed it was the single largest asset the site served - the one in
        // production right now is a 2.1 MB PNG. 1920 on the longest edge covers any
        // realistic screen; cover-scaling hides the difference either way.
        var result = await _fileUploadService.SaveImageAsync(
            file, "branding", maxDimension: BackgroundImageMaxDimension, convertToWebp: true);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = new[] { result.Error } });
        }

        var url = $"{Request.Scheme}://{Request.Host}{result.RelativePath}";
        return Ok(new ImageUploadResponse { Url = url });
    }

    [Authorize(Policy = "Module.Settings")]
    [HttpPost("upload-center-logo")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<ImageUploadResponse>> UploadCenterLogo(IFormFile file)
    {
        var result = await _fileUploadService.SaveImageAsync(file, "branding");
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = new[] { result.Error } });
        }

        var url = $"{Request.Scheme}://{Request.Host}{result.RelativePath}";
        return Ok(new ImageUploadResponse { Url = url });
    }

    // Saved the same way as the logo (no resize/webp conversion - see
    // FileUploadService.SaveImageAsync's isSvg-style special case, which now also covers
    // .ico for exactly this endpoint). The frontend applies the returned URL to the
    // <link rel="icon"> tag itself; this endpoint only stores the file.
    [Authorize(Policy = "Module.Settings")]
    [HttpPost("upload-favicon")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<ImageUploadResponse>> UploadFavicon(IFormFile file)
    {
        var result = await _fileUploadService.SaveImageAsync(file, "branding");
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = new[] { result.Error } });
        }

        var url = $"{Request.Scheme}://{Request.Host}{result.RelativePath}";
        return Ok(new ImageUploadResponse { Url = url });
    }
}
