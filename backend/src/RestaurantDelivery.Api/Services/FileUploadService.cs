using Microsoft.AspNetCore.Http;
using RestaurantDelivery.Core.Common;

namespace RestaurantDelivery.Api.Services;

public class FileUploadService : IFileUploadService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg"
    };

    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IWebHostEnvironment _webHostEnvironment;

    public FileUploadService(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<FileUploadResult> SaveImageAsync(IFormFile file, string subfolder)
    {
        if (file.Length == 0)
        {
            return FileUploadResult.Failure("No file was uploaded.");
        }

        if (file.Length > MaxImageSizeBytes)
        {
            return FileUploadResult.Failure("Image must be 5 MB or smaller.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return FileUploadResult.Failure("Only JPG, PNG, WEBP, GIF, and SVG images are allowed.");
        }

        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", subfolder);
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return FileUploadResult.Success($"/uploads/{subfolder}/{fileName}");
    }
}
