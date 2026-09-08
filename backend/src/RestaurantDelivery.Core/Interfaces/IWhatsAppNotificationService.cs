using RestaurantDelivery.Core.DTOs.Loyalty;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

// Every method here must never throw - the implementation is responsible for catching and
// logging its own failures internally, since callers (AuthService.RegisterAsync,
// OrderService.UpdateStatusAsync) invoke these with a bare `await` and a WhatsApp/Green API
// outage must never fail a registration or a captain's "mark delivered" tap.
public interface IWhatsAppNotificationService
{
    Task SendWelcomeMessageAsync(string phoneNumber, string customerName);

    Task SendOrderConfirmationAsync(string phoneNumber, string customerName, Order order, int pointsEarned, int newTotalPoints, bool tierUpgraded, List<PunchUpdateSummary> punchUpdates);

    // Fired on order CREATION, distinct from SendOrderConfirmationAsync above (which fires
    // on DELIVERY, with the full points/rewards breakdown) - this is the immediate "we got
    // your order" touchpoint, plus a same-message-flow alert to whoever Site Settings has
    // configured as the shift manager. managerPhone is nullable because that setting is
    // optional: no manager alert is sent when it's unset, but the customer's own message
    // still goes out regardless.
    Task SendOrderNotificationsAsync(Order order, string? managerPhone);
}
