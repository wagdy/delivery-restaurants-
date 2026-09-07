namespace RestaurantDelivery.Core.Entities;

// Singleton row (always Id = 1) holding site-wide branding and footer content,
// editable from the admin dashboard instead of being hardcoded in the frontend.
public class RestaurantSettings
{
    public int Id { get; set; }

    // Nullable/optional - an admin can clear the name entirely (e.g. to let the header's
    // logo speak for itself with no text beside it); see app.component.html's brand-name
    // @if guards, which already treat an empty/null name as "don't render the text".
    public string? RestaurantName { get; set; } = "Restaurant Delivery";
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

    // Browser tab icon - distinct from LogoUrl/CenterLogoUrl (both shown in the storefront
    // header itself). Null falls back to the build-time default in index.html
    // (favicon.ico), never removed/overwritten on disk - see SettingsService.applyTheme's
    // frontend counterpart, which only touches the <link rel="icon"> tag at runtime.
    public string? FaviconUrl { get; set; }

    // Browser tab text - distinct from RestaurantName (shown in-app, e.g. the storefront
    // header and "Welcome to X" heading). Null falls back to the same default the tab
    // title already had before this feature existed - see SettingsService.applyTheme.
    public string? TabTitle { get; set; }

    // Applied to every order's subtotal (after any promo discount) at checkout - see
    // OrderService.CreateAsync and CheckoutService.ValidatePromoAsync, which both read
    // this same value so the preview and the actual order total never disagree.
    public decimal TaxPercentage { get; set; }

    // At least one payment method should normally be enabled, but this isn't enforced
    // server-side - an admin mid-reconfiguration (e.g. swapping payment providers) may
    // briefly have all three off, which just means checkout shows no payment options yet
    // rather than the settings save failing outright.
    public bool IsCashEnabled { get; set; } = true;

    public bool IsVisaEnabled { get; set; }

    // The external payment link (e.g. a Fawry pay-by-link URL) customers are redirected
    // to via window.location.href when they choose Visa at checkout - see
    // checkout.component.ts's placeOrder(). Only meaningful when IsVisaEnabled is true,
    // but kept even when disabled so re-enabling Visa doesn't lose a previously entered link.
    public string? VisaFawryUrl { get; set; }

    public bool IsInstapayEnabled { get; set; }

    // Free text by design (phone, email, or @username) - shown verbatim to the customer
    // in the Instapay reveal block at checkout so they know where to send payment.
    public string? InstapayAccount { get; set; }

    // The flat delivery fee applied to every order today - named "Base" rather than just
    // "DeliveryFee" because the natural next step, if per-area pricing is ever needed, is a
    // separate DeliveryZones table (AreaName, Fee) that checkout looks up by the customer's
    // chosen area first and falls back to this value when no zone matches. Nothing about
    // that extension requires changing this column - it just stops being the only fee.
    public decimal BaseDeliveryFee { get; set; }
}
