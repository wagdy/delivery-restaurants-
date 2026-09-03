namespace RestaurantDelivery.Infrastructure.ExternalServices.GreenApi;

// Bound from the "GreenApi" config section. Real values belong in environment variables
// (GreenApi__ApiUrl, GreenApi__IdInstance, GreenApi__ApiTokenInstance, Frontend__BaseUrl) -
// never commit them, same as Jwt:Key elsewhere in this app.
public class GreenApiOptions
{
    // Instance-specific API root, e.g. "https://7107.api.greenapi.com" - no trailing slash.
    public string ApiUrl { get; set; } = string.Empty;

    public string IdInstance { get; set; } = string.Empty;

    public string ApiTokenInstance { get; set; } = string.Empty;

    // This app's own public frontend origin (e.g. the web Railway domain) - used to build
    // the "view your digital loyalty card" link in the welcome message. Not a Green API
    // setting itself, but there's no other natural home for it and only this service needs it.
    public string FrontendBaseUrl { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiUrl) && !string.IsNullOrWhiteSpace(IdInstance) && !string.IsNullOrWhiteSpace(ApiTokenInstance);
}
