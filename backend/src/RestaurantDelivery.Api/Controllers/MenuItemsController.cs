using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Api.Services;
using RestaurantDelivery.Core.DTOs.Common;
using RestaurantDelivery.Core.DTOs.MenuItems;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

[ApiController]
[Route("api/menuitems")]
public class MenuItemsController : ControllerBase
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IMenuItemService _service;
    private readonly IFileUploadService _fileUploadService;

    public MenuItemsController(IMenuItemService service, IFileUploadService fileUploadService)
    {
        _service = service;
        _fileUploadService = fileUploadService;
    }

    [HttpGet]
    public async Task<ActionResult<List<MenuItemResponse>>> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] bool? isAvailable)
    {
        return Ok(await _service.GetAllAsync(category, search, isAvailable));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MenuItemResponse>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<MenuItemResponse>> Create(MenuItemRequest request)
    {
        var result = await _service.CreateAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<MenuItemResponse>> Update(int id, MenuItemRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result.Succeeded)
        {
            var message = result.Errors.FirstOrDefault() ?? string.Empty;
            return message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { errors = result.Errors })
                : Conflict(new { errors = result.Errors });
        }

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("upload-image")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<ImageUploadResponse>> UploadImage(IFormFile file)
    {
        var result = await _fileUploadService.SaveImageAsync(file, "menu-items");
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = new[] { result.Error } });
        }

        var url = $"{Request.Scheme}://{Request.Host}{result.RelativePath}";
        return Ok(new ImageUploadResponse { Url = url });
    }
}
