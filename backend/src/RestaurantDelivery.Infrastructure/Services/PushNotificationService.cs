using System.Net;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Push;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class PushNotificationService : IPushNotificationService
{
    private readonly PushServiceClient _pushServiceClient;
    private readonly IWebPushSubscriptionRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        PushServiceClient pushServiceClient,
        IWebPushSubscriptionRepository repository,
        IConfiguration configuration,
        ILogger<PushNotificationService> logger)
    {
        _pushServiceClient = pushServiceClient;
        _repository = repository;
        _configuration = configuration;
        _logger = logger;
    }

    public string GetVapidPublicKey() =>
        _configuration["Vapid:PublicKey"] ?? throw new InvalidOperationException("Vapid:PublicKey is not configured.");

    public async Task<ServiceResult<bool>> SubscribeAsync(string userId, PushSubscriptionRequest request)
    {
        var existing = await _repository.GetByEndpointAsync(request.Endpoint);
        if (existing is not null)
        {
            // Re-subscribing (e.g. permission re-granted, or the browser rotated the
            // endpoint's keys) updates the existing row instead of creating a duplicate.
            existing.UserId = userId;
            existing.P256dh = request.Keys.P256dh;
            existing.Auth = request.Keys.Auth;
            await _repository.SaveChangesAsync();
            return ServiceResult<bool>.Success(true);
        }

        var subscription = new WebPushSubscription
        {
            UserId = userId,
            Endpoint = request.Endpoint,
            P256dh = request.Keys.P256dh,
            Auth = request.Keys.Auth
        };

        await _repository.AddAsync(subscription);
        await _repository.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    public async Task UnsubscribeAsync(string userId, string endpoint)
    {
        await _repository.RemoveByEndpointAsync(endpoint, userId);
        await _repository.SaveChangesAsync();
    }

    public async Task NotifyCaptainsOfNewOrderAsync(Order order)
    {
        List<WebPushSubscription> subscriptions;
        try
        {
            subscriptions = await _repository.GetForCaptainsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load captain push subscriptions for order {OrderId}.", order.Id);
            return;
        }

        if (subscriptions.Count == 0)
        {
            return;
        }

        var payload = BuildNewOrderPayload(order);
        var expiredEndpoints = new List<string>();

        foreach (var subscription in subscriptions)
        {
            var pushSubscription = new PushSubscription { Endpoint = subscription.Endpoint };
            pushSubscription.SetKey(PushEncryptionKeyName.P256DH, subscription.P256dh);
            pushSubscription.SetKey(PushEncryptionKeyName.Auth, subscription.Auth);

            var message = new PushMessage(payload) { TimeToLive = 60 };

            try
            {
                await _pushServiceClient.RequestPushMessageDeliveryAsync(pushSubscription, message);
            }
            catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                // The push service confirms this subscription no longer exists
                // (uninstalled, permission revoked, etc.) — stop trying it.
                _logger.LogInformation(
                    "Removing expired push subscription {Endpoint} ({StatusCode}).",
                    subscription.Endpoint,
                    ex.StatusCode);
                expiredEndpoints.Add(subscription.Endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to deliver push notification for order {OrderId} to subscription {Endpoint}.",
                    order.Id,
                    subscription.Endpoint);
            }
        }

        if (expiredEndpoints.Count > 0)
        {
            foreach (var endpoint in expiredEndpoints)
            {
                var stale = await _repository.GetByEndpointAsync(endpoint);
                if (stale is not null)
                {
                    _repository.Remove(stale);
                }
            }
            await _repository.SaveChangesAsync();
        }
    }

    private static string BuildNewOrderPayload(Order order)
    {
        // Shape expected by Angular's built-in service worker push handler:
        // https://angular.dev/ecosystem/service-workers/push-notifications
        var payload = new
        {
            notification = new
            {
                title = "New Order Available!",
                body = $"Order #{order.Id} · ${order.TotalAmount:F2} · {order.DeliveryAddress}",
                icon = "assets/icons/icon-128x128.png",
                vibrate = new[] { 200, 100, 200 },
                tag = $"order-{order.Id}",
                data = new
                {
                    orderId = order.Id,
                    onActionClick = new
                    {
                        @default = new
                        {
                            operation = "navigateLastFocusedOrOpen",
                            url = $"/captain?orderId={order.Id}"
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }
}
