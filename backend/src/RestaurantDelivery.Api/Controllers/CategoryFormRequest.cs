using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Api.Controllers;

// Multipart/form-data counterpart to RestaurantDelivery.Core.DTOs.Categories.CategoryRequest.
// Lives in the Api project rather than Core because IFormFile needs the ASP.NET Core
// framework reference Core's plain Microsoft.NET.Sdk project doesn't have - the same
// reasoning behind ILoyaltyRealtimeNotifier's Core/Api split for IHubContext.
public class CategoryFormRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // A newly-selected file to upload. Null means "leave the existing image as-is" on
    // update, or "no image" on create - there's no separate action to clear an image
    // once set.
    public IFormFile? Image { get; set; }
}
