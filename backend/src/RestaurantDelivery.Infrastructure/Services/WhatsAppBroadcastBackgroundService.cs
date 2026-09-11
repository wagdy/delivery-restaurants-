using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

// Drains WhatsAppBroadcastQueue for the lifetime of the app - the ONE place broadcast
// messages actually get sent, one at a time, with a randomized human-like delay between
// each (3-7s, per explicit anti-ban requirement) so a self-hosted WhatsApp gateway (OpenWA)
// never looks like it's bulk-spamming and risks the linked number getting banned. Runs
// entirely outside any HTTP request's lifetime, so a broadcast to hundreds of customers -
// which at this pace can genuinely take tens of minutes - never blocks whichever admin
// request (Create Promo Code / Create Campaign) enqueued it.
//
// Known limitation, called out deliberately rather than silently: this queue is in-memory
// only. A deploy or restart while a broadcast is mid-flight loses whatever's left in it,
// with no record of which customers were or weren't reached. Acceptable for how this
// feature is used today (an admin announcing a promo/campaign, not a compliance-critical
// send), but worth knowing if broadcasts grow large enough for that gap to matter - a
// durable queue (an outbox table, drained the same way) would be the next step if so.
public class WhatsAppBroadcastBackgroundService : BackgroundService
{
    private const int MinDelayMs = 3000;
    private const int MaxDelayMs = 7000;

    private readonly WhatsAppBroadcastQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhatsAppBroadcastBackgroundService> _logger;

    public WhatsAppBroadcastBackgroundService(
        WhatsAppBroadcastQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<WhatsAppBroadcastBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation("Starting WhatsApp broadcast to {Count} customer(s).", job.PhoneNumbers.Count);

            foreach (var phoneNumber in job.PhoneNumbers)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                // IWhatsAppNotificationService is registered via AddHttpClient<,> (scoped),
                // so a fresh DI scope is created per send rather than resolving it once
                // outside the loop - the request that originally enqueued this job (and its
                // own scope) is long gone by the time this loop actually runs.
                using var scope = _scopeFactory.CreateScope();
                var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppNotificationService>();

                try
                {
                    await whatsAppService.SendBroadcastMessageAsync(phoneNumber, job.Message);
                }
                catch (Exception ex)
                {
                    // Defensive only - SendBroadcastMessageAsync's own contract is to never
                    // throw (see IWhatsAppNotificationService) - but one bad send must never
                    // be able to take down the rest of the broadcast either way.
                    _logger.LogError(ex, "Broadcast send failed for {Phone}.", phoneNumber);
                }

                try
                {
                    // The anti-ban delay, per explicit requirement - randomized so
                    // consecutive sends don't look bot-regular to WhatsApp's own abuse
                    // detection the way a fixed interval would.
                    await Task.Delay(Random.Shared.Next(MinDelayMs, MaxDelayMs), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // App is shutting down - stop this job's remaining sends rather than
                    // pushing through them.
                    return;
                }
            }

            _logger.LogInformation("Finished WhatsApp broadcast to {Count} customer(s).", job.PhoneNumbers.Count);
        }
    }
}
