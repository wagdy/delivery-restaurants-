namespace RestaurantDelivery.Core.Common;

// One broadcast "blast" - every phone number gets the exact same message, sent one at a
// time with a randomized delay between each (see WhatsAppBroadcastBackgroundService) to
// avoid the self-hosted WhatsApp gateway's linked number getting banned for bulk-sending.
public class WhatsAppBroadcastJob
{
    public required List<string> PhoneNumbers { get; init; }
    public required string Message { get; init; }
}
