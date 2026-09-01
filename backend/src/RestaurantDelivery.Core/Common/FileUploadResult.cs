namespace RestaurantDelivery.Core.Common;

public class FileUploadResult
{
    public bool Succeeded { get; private init; }
    public string? RelativePath { get; private init; }
    public string? Error { get; private init; }

    public static FileUploadResult Success(string relativePath) => new() { Succeeded = true, RelativePath = relativePath };

    public static FileUploadResult Failure(string error) => new() { Succeeded = false, Error = error };
}
