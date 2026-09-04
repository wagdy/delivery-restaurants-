using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Api.Services;
using RestaurantDelivery.Core.DTOs.Categories;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly ICategoryService _service;
    private readonly IFileUploadService _fileUploadService;

    public CategoriesController(ICategoryService service, IFileUploadService fileUploadService)
    {
        _service = service;
        _fileUploadService = fileUploadService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [Authorize(Policy = "Module.MenuItems")]
    [HttpPost]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<CategoryResponse>> Create([FromForm] CategoryFormRequest form)
    {
        string? imageUrl = null;
        if (form.Image is not null)
        {
            var uploadResult = await _fileUploadService.SaveImageAsync(form.Image, "categories");
            if (!uploadResult.Succeeded)
            {
                return BadRequest(new { errors = new[] { uploadResult.Error } });
            }

            imageUrl = $"{Request.Scheme}://{Request.Host}{uploadResult.RelativePath}";
        }

        var result = await _service.CreateAsync(new CategoryRequest { Name = form.Name, ImageUrl = imageUrl });
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [Authorize(Policy = "Module.MenuItems")]
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(ReorderCategoriesRequest request)
    {
        var result = await _service.ReorderAsync(request.OrderedIds);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return NoContent();
    }

    [Authorize(Policy = "Module.MenuItems")]
    [HttpPut("{id:int}")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<CategoryResponse>> Update(int id, [FromForm] CategoryFormRequest form)
    {
        string? imageUrl = null;
        var updateImage = form.Image is not null;
        if (updateImage)
        {
            var uploadResult = await _fileUploadService.SaveImageAsync(form.Image!, "categories");
            if (!uploadResult.Succeeded)
            {
                return BadRequest(new { errors = new[] { uploadResult.Error } });
            }

            imageUrl = $"{Request.Scheme}://{Request.Host}{uploadResult.RelativePath}";
        }

        var result = await _service.UpdateAsync(id, new CategoryRequest { Name = form.Name, ImageUrl = imageUrl }, updateImage);
        if (!result.Succeeded)
        {
            var message = result.Errors.FirstOrDefault() ?? string.Empty;
            return message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [Authorize(Policy = "Module.MenuItems")]
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
}
