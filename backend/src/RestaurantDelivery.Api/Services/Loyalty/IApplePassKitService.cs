namespace RestaurantDelivery.Api.Services.Loyalty;

public record UpdatablePassesResponse(List<string> SerialNumbers, string LastUpdated);

// Implements Apple's PassKit Web Service protocol (register/unregister/updatable-passes).
public interface IApplePassKitService
{
    // Returns true = newly registered (201), false = already registered/token refreshed
    // (200), null = the referenced AppUser doesn't exist (404).
    Task<bool?> RegisterDeviceAsync(string appUserId, string deviceLibraryIdentifier, string passTypeIdentifier, string pushToken, CancellationToken ct);

    Task<bool> UnregisterDeviceAsync(string appUserId, string deviceLibraryIdentifier, string passTypeIdentifier, CancellationToken ct);

    Task<UpdatablePassesResponse?> GetUpdatablePassesAsync(string deviceLibraryIdentifier, string passTypeIdentifier, DateTimeOffset? updatedSince, CancellationToken ct);
}
