using Microsoft.AspNetCore.Http;
using RestaurantDelivery.Core.Common;

namespace RestaurantDelivery.Api.Services;

public interface IFileUploadService
{
    // maxDimension/convertToWebp default to off so every existing caller (Settings'
    // logo/background/center-logo, MenuItem's photo) keeps saving the original file
    // untouched - only a caller that explicitly opts in (Category's grid thumbnails)
    // gets resized, re-encoded output. Forcing this on every image would wrongly crop
    // a restaurant's full-width background image or logo down to a square thumbnail.
    Task<FileUploadResult> SaveImageAsync(IFormFile file, string subfolder, int? maxDimension = null, bool convertToWebp = false);
}
