using Microsoft.AspNetCore.Http;
using RestaurantDelivery.Core.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace RestaurantDelivery.Api.Services;

public class FileUploadService : IFileUploadService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg", ".ico"
    };

    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const int WebpQuality = 75;

    private readonly IWebHostEnvironment _webHostEnvironment;

    public FileUploadService(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<FileUploadResult> SaveImageAsync(IFormFile file, string subfolder, int? maxDimension = null, bool convertToWebp = false)
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
            return FileUploadResult.Failure("Only JPG, PNG, WEBP, GIF, SVG, and ICO images are allowed.");
        }

        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", subfolder);
        Directory.CreateDirectory(uploadsFolder);

        // SVG (vector/XML) and ICO (a Windows-icon container format) are both formats
        // ImageSharp can't decode as a raster image - saved as-is regardless of
        // convertToWebp, same as before this method could resize. ICO support exists
        // specifically for SettingsController's favicon upload.
        var isUnsupportedByImageSharp =
            string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".ico", StringComparison.OrdinalIgnoreCase);

        if (convertToWebp && !isUnsupportedByImageSharp)
        {
            return await SaveResizedWebpAsync(file, uploadsFolder, subfolder, maxDimension);
        }

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return FileUploadResult.Success($"/uploads/{subfolder}/{fileName}");
    }

    private static async Task<FileUploadResult> SaveResizedWebpAsync(IFormFile file, string uploadsFolder, string subfolder, int? maxDimension)
    {
        Image image;
        try
        {
            await using var inputStream = file.OpenReadStream();
            image = await Image.LoadAsync(inputStream);
        }
        catch (UnknownImageFormatException)
        {
            return FileUploadResult.Failure("The uploaded file is not a valid image.");
        }

        using (image)
        {
            if (maxDimension is int max)
            {
                // ResizeMode.Max scales the image down to fit within max x max while
                // preserving its aspect ratio (never upscales, never crops) - the grid's
                // own CSS (object-fit: cover) handles cropping to a perfect square for
                // display, so the stored file only needs to be small, not pre-cropped.
                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(max, max)
                }));
            }

            var fileName = $"{Guid.NewGuid()}.webp";
            var filePath = Path.Combine(uploadsFolder, fileName);
            await image.SaveAsync(filePath, new WebpEncoder { Quality = WebpQuality });

            return FileUploadResult.Success($"/uploads/{subfolder}/{fileName}");
        }
    }
}
