namespace RestaurantDelivery.Infrastructure.ExternalServices.OpenWa;

// Bound from the "OpenWa" config section. Real values belong in environment variables
// (OpenWa__ApiBaseUrl, OpenWa__ApiKey, OpenWa__SessionId) - never commit them, same as
// GreenApiOptions.
public class OpenWaSettings
{
    // The OpenWA service's own base URL, no trailing slash. Prefer Railway's private
    // network hostname (e.g. "http://openwa.railway.internal:2785") when the C# backend
    // and the OpenWA service share a Railway project/environment - it's free, faster, and
    // never leaves Railway's network. Falls back to the OpenWA service's public domain if
    // they're hosted separately.
    public string ApiBaseUrl { get; set; } = string.Empty;

    // An OPERATOR-role API key created in the OpenWA dashboard (Settings > API Keys) -
    // POST .../messages/send-text requires OPERATOR, a VIEWER key gets a 403. Keep this
    // distinct from API_MASTER_KEY (the dashboard's own bootstrap/admin credential, which
    // never belongs in this app).
    public string ApiKey { get; set; } = string.Empty;

    // The WhatsApp session id created once via the dashboard (or POST /api/sessions) after
    // scanning the QR code with the dedicated WhatsApp number - see the migration guide.
    // One session is enough for this app's single restaurant WhatsApp number.
    public string SessionId { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiBaseUrl) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(SessionId);
}
