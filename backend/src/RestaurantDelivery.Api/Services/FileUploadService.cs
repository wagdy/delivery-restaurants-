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

            // EXIF/XMP carry camera and location metadata that has no business being
            // served to customers, and cost ~1.5 KB on every image for nothing.
            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;

            var fileName = $"{Guid.NewGuid()}.webp";
            var filePath = Path.Combine(uploadsFolder, fileName);

            // FileFormat must be set explicitly. Left unset, ImageSharp encoded these as
            // LOSSLESS WebP (a VP8L chunk), and Quality only applies to lossy encoding -
            // so the 75 below was silently doing nothing at all. The result looked
            // compressed because the file was .webp, while a photo barely shrank: measured
            // on a real 1080x1350 upload from this app, lossless produced 612 KB where
            // lossy produces a small fraction of that. Category images escaped notice only
            // because they are downscaled to 200px first, where even lossless is small.
            await image.SaveAsync(filePath, new WebpEncoder
            {
                FileFormat = WebpFileFormatType.Lossy,
                Quality = WebpQuality
            });

            return FileUploadResult.Success($"/uploads/{subfolder}/{fileName}");
        }
    }
}
