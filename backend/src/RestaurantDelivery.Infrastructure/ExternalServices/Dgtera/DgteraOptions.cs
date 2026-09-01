namespace RestaurantDelivery.Infrastructure.ExternalServices.Dgtera;

// Bound from the "Dgtera" config section. Real values belong in environment variables /
// user-secrets (Dgtera__Database, Dgtera__Username, Dgtera__ApiKey) - never commit them,
// same as Jwt:Key and Vapid:PrivateKey elsewhere in this app.
public class DgteraOptions
{
    // Odoo instance base URL, e.g. "https://ontaktik.dgtera.com" - no trailing slash.
    public string BaseUrl { get; set; } = string.Empty;

    // The Odoo database name (visible in the Odoo login screen's database selector, or
    // ask whoever manages the Dgtera instance).
    public string Database { get; set; } = string.Empty;

    // The Odoo user to authenticate as for this integration - ideally a dedicated
    // "API/integration" user with read-only access to POS orders, not a personal login.
    public string Username { get; set; } = string.Empty;

    // Odoo API key (Settings > My Profile > Account Security > API Keys) or that user's
    // password. An API key is strongly preferred - it can be scoped and revoked
    // independently of the user's actual login password.
    public string ApiKey { get; set; } = string.Empty;
}
