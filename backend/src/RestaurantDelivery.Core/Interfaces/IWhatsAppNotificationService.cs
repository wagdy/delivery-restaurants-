using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

// Every method here must never throw - the implementation is responsible for catching and
// logging its own failures internally, since callers (AuthService.RegisterAsync,
// OrderService.UpdateStatusAsync) invoke these with a bare `await` and a WhatsApp/Green API
// outage must never fail a registration or a captain's "mark delivered" tap.
public interface IWhatsAppNotificationService
{
    // The single, unified welcome touchpoint every brand-new customer gets, regardless of
    // which page created their account (self-service Register, the Scanner's New Customer
    // tab, or the Create Order page's New Customer section) - see
    // AuthService.AwardWelcomeBonusAndNotifyAsync, the one place this is called from.
    Task SendWelcomeMessageAsync(string phoneNumber, string customerName);

    // The immediate "we got your order" touchpoint on order CREATION, plus the same
    // detailed alert sent to every configured manager number (up to the 3 Site Settings
    // slots - RestaurantSettings.ManagerWhatsApp1/2/3). Entries are individually
    // optional: null/empty/whitespace ones are filtered out, and an empty list after
    // filtering just skips the manager alert entirely - the customer's own confirmation
    // still sends regardless.
    Task SendOrderNotificationsAsync(Order order, IEnumerable<string?> managerPhones);

    // The WhatsApp touchpoint fired when a *registered* customer's order reaches
    // Delivered - its own exact, specified wallet-style template (close to but not
    // literally shared with SendLoyaltyWalletUpdateAsync below - see that method's
    // implementation comment for the specific differences). There used to also be a
    // detailed order-summary message here (an "OrderConfirmation" send with
    // items/totals/points/tier breakdown) - removed entirely, not replaced, per explicit
    // instruction. newTotalPoints is the customer's balance *after* this order's points
    // were added (OrderLoyaltyResult.NewTotalPoints), not their balance before. Only
    // meaningful for a registered customer (a guest order has no loyalty profile to
    // credit points to) - see SendGuestDeliveryThankYouAsync below for the guest
    // counterpart. The caller is responsible for choosing between the two.
    Task SendPostDeliveryPointsNotificationAsync(string phoneNumber, string customerName, int earnedPoints, int newTotalPoints);

    // The Delivered-touchpoint counterpart for a *guest* order (order.UserId is null) -
    // no points/balance line, since a guest has no LoyaltyProfile for either figure to
    // come from. Links to the same order-specific rating flow
    // ("/rate/store?orderId=<id>") the registered-customer path's link would otherwise
    // cover, so a guest can still leave feedback on this specific order despite having
    // no account - PublicReviewsController.Submit already accepts an anonymous
    // (CustomerId-less) review tied to a real OrderId.
    Task SendGuestDeliveryThankYouAsync(string phoneNumber, string customerName, int orderId);

    // Fired from LoyaltyService.EarnPointsAsync/RedeemPointsAsync after a staff-scanned
    // manual wallet transaction (Scanner UI - QR scan or phone lookup) succeeds.
    // transactionPoints is always the positive magnitude of the transaction (the caller
    // passes request.PointsToRedeem as-is for a redemption, not its negated ledger value),
    // since "🔻 تم استبدال -50 نقطة" would read as broken. totalBalance is the customer's
    // resulting CurrentPoints.
    Task SendLoyaltyWalletUpdateAsync(string phoneNumber, string customerName, bool isRedemption, int transactionPoints, int totalBalance);
}
