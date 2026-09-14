namespace RestaurantDelivery.Core.DTOs.Maintenance;

// What happened to one stored image. Every record is reported, including the ones left
// alone, so a dry run is a complete account of what an apply would do rather than a list
// of things that changed.
public class ImageBackfillEntry
{
    // "Category", "MenuItem", or the settings field name - enough to find the record.
    public string Owner { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string OriginalUrl { get; set; } = string.Empty;

    public string? NewUrl { get; set; }

    public long OriginalBytes { get; set; }

    public long NewBytes { get; set; }

    // Null when the image was re-encoded; otherwise why it wasn't.
    public string? SkippedReason { get; set; }
}

public class ImageBackfillResult
{
    // False means nothing was written and no record was changed - the entries below are a
    // preview.
    public bool Applied { get; set; }

    public int Examined { get; set; }

    public int Converted { get; set; }

    public int Skipped { get; set; }

    public long BytesBefore { get; set; }

    public long BytesAfter { get; set; }

    public long BytesSaved => BytesBefore - BytesAfter;

    public List<ImageBackfillEntry> Entries { get; set; } = new();
}
