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

    // Header/body colors are independent of Primary/Accent above - those still drive
    // Material buttons/chips/FABs, these two specifically paint the storefront's top
    // navbar and page background (see styles.scss's --app-header-color/--app-body-color).
    public string HeaderColor { get; set; } = "#3f51b5";
    public string BodyColor { get; set; } = "#fafafa";

    // When set, overrides BodyColor as the page background (see styles.scss).
    public string? BackgroundImageUrl { get; set; }

    // A prominent logo shown centered in the storefront header, distinct from LogoUrl
    // (the small brand-mark shown next to the restaurant name). Null falls back to a
    // plain solid block of HeaderColor - see app.component.html.
    public string? CenterLogoUrl { get; set; }

    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FooterAbout { get; set; }
}
