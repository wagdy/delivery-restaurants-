using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.DTOs.Maintenance;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace RestaurantDelivery.Api.Services;

public interface IImageBackfillService
{
    Task<ImageBackfillResult> RunAsync(bool apply, CancellationToken ct = default);
}

// A one-off repair for images uploaded before the pipeline resized and re-encoded them.
// Those files are already on the volume at full resolution - fixing the upload path does
// nothing for them, and re-uploading five images by hand through the admin UI is the only
// alternative.
//
// Deliberately conservative, because this rewrites live content:
//   - dry run unless the caller explicitly asks to apply, so the report can be read first
//   - never deletes the original file, so a bad result is reversible by pointing the
//     record back at the old URL
//   - skips anything already .webp, so running it twice changes nothing
//   - skips whatever it cannot decode rather than failing the batch
public class ImageBackfillService : IImageBackfillService
{
    private const int WebpQuality = 75;

    // Matched to the upload paths these images arrive through, so a backfilled file is
    // identical to one uploaded today. Logos and the favicon are absent on purpose: the
    // upload path leaves them alone too (see SettingsController).
    private const int CategoryMaxDimension = 200;
    private const int MenuItemMaxDimension = 800;
    private const int BackgroundMaxDimension = 1920;

    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IReadThroughCache _cache;
    private readonly ILogger<ImageBackfillService> _logger;

    public ImageBackfillService(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        IReadThroughCache cache,
        ILogger<ImageBackfillService> logger)
    {
        _context = context;
        _environment = environment;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ImageBackfillResult> RunAsync(bool apply, CancellationToken ct = default)
    {
        var result = new ImageBackfillResult { Applied = apply };

        var categories = await _context.Categories.Where(c => c.ImageUrl != null).ToListAsync(ct);
        foreach (var category in categories)
        {
            var entry = await ProcessAsync("Category", category.Name, category.ImageUrl!, "categories", CategoryMaxDimension, apply, ct);
            Record(result, entry);
            if (apply && entry.NewUrl is not null)
            {
                category.ImageUrl = entry.NewUrl;
            }
        }

        var menuItems = await _context.MenuItems.Where(m => m.ImageUrl != null).ToListAsync(ct);
        foreach (var item in menuItems)
        {
            var entry = await ProcessAsync("MenuItem", item.Name, item.ImageUrl!, "menu-items", MenuItemMaxDimension, apply, ct);
            Record(result, entry);
            if (apply && entry.NewUrl is not null)
            {
                item.ImageUrl = entry.NewUrl;
            }
        }

        var settings = await _context.RestaurantSettings.FirstOrDefaultAsync(ct);
        if (settings?.BackgroundImageUrl is not null)
        {
            var entry = await ProcessAsync(
                "Settings", nameof(settings.BackgroundImageUrl), settings.BackgroundImageUrl, "branding", BackgroundMaxDimension, apply, ct);
            Record(result, entry);
            if (apply && entry.NewUrl is not null)
            {
                settings.BackgroundImageUrl = entry.NewUrl;
            }
        }

        if (apply && result.Converted > 0)
        {
            await _context.SaveChangesAsync(ct);

            // The menu, category and settings responses all embed these URLs, so a cached
            // copy would keep pointing at the originals until its TTL expired.
            _cache.Invalidate(CacheGroup.Menu);
            _cache.Invalidate(CacheGroup.Categories);
            _cache.Invalidate(CacheGroup.Settings);

            _logger.LogInformation(
                "Image backfill re-encoded {Converted} of {Examined} images, {BytesSaved} bytes saved",
                result.Converted, result.Examined, result.BytesSaved);
        }

        return result;
    }

    private async Task<ImageBackfillEntry> ProcessAsync(
        string owner, string label, string url, string subfolder, int maxDimension, bool apply, CancellationToken ct)
    {
        var entry = new ImageBackfillEntry { Owner = owner, Label = label, OriginalUrl = url };

        // URLs are stored absolute (built from Request.Scheme/Host at upload time), so the
        // path has to be pulled back out to find the file on disk. Anything that is not one
        // of this app's own /uploads/ URLs is left alone - it could be pointing anywhere.
        var relativePath = ExtractUploadsPath(url);
        if (relativePath is null)
        {
            entry.SkippedReason = "not a local /uploads/ URL";
            return entry;
        }

        var extension = Path.GetExtension(relativePath);
        if (string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase))
        {
            entry.SkippedReason = "already webp";
            return entry;
        }

        if (string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".ico", StringComparison.OrdinalIgnoreCase))
        {
            entry.SkippedReason = "vector or icon format ImageSharp cannot decode";
            return entry;
        }

        var sourcePath = Path.Combine(_environment.WebRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(sourcePath))
        {
            entry.SkippedReason = "file missing from disk";
            return entry;
        }

        entry.OriginalBytes = new FileInfo(sourcePath).Length;

        Image image;
        try
        {
            image = await Image.LoadAsync(sourcePath, ct);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            entry.SkippedReason = "not a decodable image";
            return entry;
        }

        using (image)
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxDimension, maxDimension)
            }));

            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;

            var newFileName = $"{Guid.NewGuid()}.webp";
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", subfolder);
            var newPath = Path.Combine(uploadsFolder, newFileName);

            if (!apply)
            {
                // Encoded to memory rather than disk so the dry run can report the real
                // resulting size instead of an estimate - the whole point of previewing.
                using var buffer = new MemoryStream();
                await image.SaveAsync(buffer, new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = WebpQuality }, ct);
                entry.NewBytes = buffer.Length;
                entry.NewUrl = null;
                return entry;
            }

            Directory.CreateDirectory(uploadsFolder);
            await image.SaveAsync(newPath, new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = WebpQuality }, ct);
            entry.NewBytes = new FileInfo(newPath).Length;

            // The original is left in place on purpose. It costs a few MB of volume and
            // makes this reversible: pointing the record back at OriginalUrl restores
            // exactly what was there before.
            entry.NewUrl = ReplaceFileName(url, subfolder, newFileName);
            return entry;
        }
    }

    private static void Record(ImageBackfillResult result, ImageBackfillEntry entry)
    {
        result.Entries.Add(entry);
        result.Examined++;

        if (entry.SkippedReason is not null)
        {
            result.Skipped++;
            return;
        }

        result.Converted++;
        result.BytesBefore += entry.OriginalBytes;
        result.BytesAfter += entry.NewBytes;
    }

    // "https://host/uploads/categories/x.png" -> "uploads/categories/x.png"
    private static string? ExtractUploadsPath(string url)
    {
        var index = url.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var path = url[(index + 1)..];

        // Strip any query string or fragment before it reaches Path.Combine.
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        // Refuse anything with traversal segments rather than resolving it - this value
        // comes from the database, but the file it names is about to be read from disk.
        return path.Contains("..", StringComparison.Ordinal) ? null : path;
    }

    // Rebuilds the stored URL with the new filename, keeping whatever scheme and host the
    // original was saved with so the record stays consistent with its neighbours.
    private static string ReplaceFileName(string originalUrl, string subfolder, string newFileName)
    {
        var index = originalUrl.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
        var prefix = originalUrl[..index];
        return $"{prefix}/uploads/{subfolder}/{newFileName}";
    }
}
