namespace RestaurantDelivery.Core.DTOs.Loyalty;

// Returned to the Scanner UI after decoding a customer's digital-card QR (which encodes
// their raw AppUserId).
public class ScannerCustomerResponse
{
    public string AppUserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int CurrentPoints { get; set; }
    public int TotalLifetimePoints { get; set; }
    public string MembershipTier { get; set; } = string.Empty;
    public List<CustomerCampaignProgressResponse> Campaigns { get; set; } = [];
}
