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
        var result = await _fileUploadService.SaveImageAsync(file, "branding");
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
}
