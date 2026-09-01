using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantDelivery.Core.DTOs.Categories;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _service;

    public CategoriesController(ICategoryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryRequest request)
    {
        var result = await _service.CreateAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> Update(int id, CategoryRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        if (!result.Succeeded)
        {
            var message = result.Errors.FirstOrDefault() ?? string.Empty;
            return message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
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
}
