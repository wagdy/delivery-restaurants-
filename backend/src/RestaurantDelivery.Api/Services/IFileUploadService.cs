using Microsoft.AspNetCore.Http;
using RestaurantDelivery.Core.Common;

namespace RestaurantDelivery.Api.Services;

public interface IFileUploadService
{
    Task<FileUploadResult> SaveImageAsync(IFormFile file, string subfolder);
}
