using System.Threading.Channels;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

// Registered as a singleton (both under this concrete type and IWhatsAppBroadcastQueue -
// see Program.cs) so every request-scoped caller (PromoCodeService, CampaignService)
// writes into the same in-memory queue that the one long-running
// WhatsAppBroadcastBackgroundService reads from. Unbounded: a job is just a phone-number
// list plus a string, and this app has no realistic volume where memory would become a
// concern before the queue drains.
public class WhatsAppBroadcastQueue : IWhatsAppBroadcastQueue
{
    private readonly Channel<WhatsAppBroadcastJob> _channel = Channel.CreateUnbounded<WhatsAppBroadcastJob>();

    public ChannelReader<WhatsAppBroadcastJob> Reader => _channel.Reader;

    public void Enqueue(WhatsAppBroadcastJob job)
    {
        // Always succeeds immediately on an unbounded channel and is never awaited -
        // that's what keeps Enqueue() itself synchronous and non-blocking for callers.
        _channel.Writer.TryWrite(job);
    }
}
