namespace RestaurantDelivery.Core.Entities;

// Singleton row (always Id = 1) holding site-wide branding and footer content,
// editable from the admin dashboard instead of being hardcoded in the frontend.
public class RestaurantSettings
{
    public int Id { get; set; }

    public string RestaurantName { get; set; } = "Restaurant Delivery";
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#3f51b5";
    public string AccentColor { get; set; } = "#ff4081";

    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FooterAbout { get; set; }
}
