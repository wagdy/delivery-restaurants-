using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

// Every method here must never throw - the implementation is responsible for catching and
// logging its own failures internally, since callers (AuthService.RegisterAsync,
// OrderService.UpdateStatusAsync) invoke these with a bare `await` and a WhatsApp/Green API
// outage must never fail a registration or a captain's "mark delivered" tap.
public interface IWhatsAppNotificationService
{
    Task SendWelcomeMessageAsync(string phoneNumber, string customerName);

    // The immediate "we got your order" touchpoint on order CREATION, plus the same
    // detailed alert sent to every configured manager number (up to the 3 Site Settings
    // slots - RestaurantSettings.ManagerWhatsApp1/2/3). Entries are individually
    // optional: null/empty/whitespace ones are filtered out, and an empty list after
    // filtering just skips the manager alert entirely - the customer's own confirmation
    // still sends regardless.
    Task SendOrderNotificationsAsync(Order order, IEnumerable<string?> managerPhones);

    // The only WhatsApp touchpoint fired when an order reaches Delivered - its own
    // exact, specified wallet-style template (close to but not literally shared with
    // SendLoyaltyWalletUpdateAsync below - see that method's implementation comment for
    // the specific differences). There used to also be a detailed order-summary message
    // here (an "OrderConfirmation" send with items/totals/points/tier breakdown) -
    // removed entirely, not replaced, per explicit instruction. newTotalPoints is the
    // customer's balance *after* this order's points were added
    // (OrderLoyaltyResult.NewTotalPoints), not their balance before. Only meaningful for
    // a registered customer (a guest order has no loyalty profile to credit points to),
    // which the caller is responsible for checking before calling this.
    Task SendPostDeliveryPointsNotificationAsync(string phoneNumber, string customerName, int earnedPoints, int newTotalPoints);

    // Fired from LoyaltyService.EarnPointsAsync/RedeemPointsAsync after a staff-scanned
    // manual wallet transaction (Scanner UI - QR scan or phone lookup) succeeds.
    // transactionPoints is always the positive magnitude of the transaction (the caller
    // passes request.PointsToRedeem as-is for a redemption, not its negated ledger value),
    // since "🔻 تم استبدال -50 نقطة" would read as broken. totalBalance is the customer's
    // resulting CurrentPoints.
    Task SendLoyaltyWalletUpdateAsync(string phoneNumber, string customerName, bool isRedemption, int transactionPoints, int totalBalance);
}
