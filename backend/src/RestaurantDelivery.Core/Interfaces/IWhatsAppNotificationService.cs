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
    // your order" touchpoint, plus the same detailed alert sent to every configured manager
    // number (up to the 3 Site Settings slots - RestaurantSettings.ManagerWhatsApp1/2/3).
    // Entries are individually optional: null/empty/whitespace ones are filtered out, and
    // an empty list after filtering just skips the manager alert entirely - the customer's
    // own confirmation still sends regardless.
    Task SendOrderNotificationsAsync(Order order, IEnumerable<string?> managerPhones);

    // A second, separate touchpoint fired alongside SendOrderConfirmationAsync when an
    // order reaches Delivered - unified with SendLoyaltyWalletUpdateAsync below (the
    // manual Scanner earn/redeem message): same wallet-update template, same rating
    // link, just a different point of origin. newTotalPoints is the customer's balance
    // *after* this order's points were added (OrderLoyaltyResult.NewTotalPoints), not
    // their balance before. Only meaningful for a registered customer (a guest order has
    // no loyalty profile to credit points to), which the caller is responsible for
    // checking before calling this.
    Task SendPostDeliveryPointsNotificationAsync(string phoneNumber, string customerName, int earnedPoints, int newTotalPoints);

    // Fired from LoyaltyService.EarnPointsAsync/RedeemPointsAsync after a staff-scanned
    // manual wallet transaction (Scanner UI - QR scan or phone lookup) succeeds, and also
    // (via SendPostDeliveryPointsNotificationAsync above) from an order reaching
    // Delivered - both are just "the wallet changed" events sharing one template.
    // transactionPoints is always the positive magnitude of the transaction (the caller
    // passes request.PointsToRedeem as-is for a redemption, not its negated ledger value),
    // since "🔻 تم استبدال -50 نقطة" would read as broken. totalBalance is the customer's
    // resulting CurrentPoints.
    Task SendLoyaltyWalletUpdateAsync(string phoneNumber, string customerName, bool isRedemption, int transactionPoints, int totalBalance);
}
