namespace RestaurantDelivery.Infrastructure.ExternalServices.GreenApi;

// Bound from the "GreenApi" config section. Real values belong in environment variables
// (GreenApi__ApiUrl, GreenApi__IdInstance, GreenApi__ApiTokenInstance) - never commit them,
// same as Jwt:Key elsewhere in this app.
public class GreenApiOptions
{
    // Instance-specific API root, e.g. "https://7107.api.greenapi.com" - no trailing slash.
    public string ApiUrl { get; set; } = string.Empty;

    public string IdInstance { get; set; } = string.Empty;

    public string ApiTokenInstance { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiUrl) && !string.IsNullOrWhiteSpace(IdInstance) && !string.IsNullOrWhiteSpace(ApiTokenInstance);
}
