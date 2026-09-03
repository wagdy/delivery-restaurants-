namespace RestaurantDelivery.Core.DTOs.Loyalty;

// Body of Apple's PassKit Web Service device-registration call.
public class RegisterDeviceRequest
{
    public string PushToken { get; set; } = string.Empty;
}
