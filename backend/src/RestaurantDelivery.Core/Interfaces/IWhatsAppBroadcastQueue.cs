using RestaurantDelivery.Core.Common;

namespace RestaurantDelivery.Core.Interfaces;

// A fire-and-forget queue for WhatsApp broadcast jobs (new Promo Code / Campaign
// announcements to every registered customer) - Enqueue returns immediately, so the HTTP
// request that created the promo code or campaign is never blocked on however long
// actually sending to hundreds of customers (with an anti-ban delay between each) takes.
// WhatsAppBroadcastBackgroundService is the one place that ever drains it.
public interface IWhatsAppBroadcastQueue
{
    void Enqueue(WhatsAppBroadcastJob job);
}
