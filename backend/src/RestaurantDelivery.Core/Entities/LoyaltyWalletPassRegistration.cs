namespace RestaurantDelivery.Core.Entities;

// A device's registration for push updates on a customer's Apple Wallet pass, per
// Apple's PassKit Web Service protocol (register/unregister/updatable-passes/log).
public class LoyaltyWalletPassRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DeviceLibraryIdentifier { get; set; } = string.Empty;

    public string PushToken { get; set; } = string.Empty;

    public string PassTypeIdentifier { get; set; } = string.Empty;

    // The pass's serial number, which is the customer's AppUserId.
    public string AppUserId { get; set; } = string.Empty;

    public AppUser AppUser { get; set; } = null!;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastPassUpdateAt { get; set; }
}
