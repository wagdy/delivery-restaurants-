using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Core.DTOs.Loyalty;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.ExternalServices.GreenApi;

// Sends plain WhatsApp text messages via Green API (green-api.com) - no pre-approved
// templates required. Every public method here swallows and logs its own failures: callers
// (AuthService.RegisterAsync, OrderService.UpdateStatusAsync) invoke these with a bare
// `await`, and a Green API outage must never fail a registration or a captain's "mark
// delivered" tap.
public class WhatsAppNotificationService : IWhatsAppNotificationService
{
    private const string EgyptCountryCode = "20";

    // Hardcoded per explicit instruction (rather than derived from
    // GreenApiOptions.FrontendBaseUrl, as these two links briefly were) - both link
    // templates below now point at this literal production URL regardless of
    // environment. Trade-off: a future domain change (e.g. a custom domain going live)
    // means editing this constant and redeploying, rather than just updating the
    // GreenApi__FrontendBaseUrl environment variable. Update this in one place if that
    // ever changes.
    private const string RatingBaseUrl = "https://web-production-1bacf.up.railway.app";

    private readonly HttpClient _httpClient;
    private readonly GreenApiOptions _options;
    private readonly ILogger<WhatsAppNotificationService> _logger;

    public WhatsAppNotificationService(HttpClient httpClient, IOptions<GreenApiOptions> options, ILogger<WhatsAppNotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task SendWelcomeMessageAsync(string phoneNumber, string customerName)
    {
        var cardLink = string.IsNullOrWhiteSpace(_options.FrontendBaseUrl)
            ? null
            : $"{_options.FrontendBaseUrl.TrimEnd('/')}/?tab=rewards";

        var message = $"مرحباً {customerName}، أهلاً بك في نظام ولاء Otantik! تم تسجيل حسابك بنجاح.";
        if (cardLink is not null)
        {
            message += $"\nشاهد بطاقة الولاء الرقمية الخاصة بك هنا: {cardLink}";
        }

        return SendMessageAsync(phoneNumber, message);
    }

    public Task SendOrderConfirmationAsync(string phoneNumber, string customerName, Order order, int pointsEarned, int newTotalPoints, bool tierUpgraded, List<PunchUpdateSummary> punchUpdates)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"مرحباً {customerName}، تم تسليم طلبك رقم #{order.Id} بنجاح!");
        builder.AppendLine();
        builder.AppendLine("ملخص الطلب:");
        foreach (var item in order.OrderItems)
        {
            builder.AppendLine($"- {item.Quantity}x {item.MenuItem.Name}");
        }

        builder.AppendLine($"الإجمالي: {order.TotalAmount:0.##} جنيه");

        if (pointsEarned > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"🎉 لقد حصلت على {pointsEarned} نقطة! رصيدك الآن {newTotalPoints} نقطة.");
            if (tierUpgraded)
            {
                builder.AppendLine("لقد ترقّت إلى مستوى عضوية أعلى، مبروك!");
            }
        }

        foreach (var punch in punchUpdates)
        {
            builder.AppendLine();
            builder.AppendLine(punch.RewardsEarnedThisOrder > 0
                ? $"🎉 مبروك! لقد حصلت على مكافأة مجانية في بطاقة {punch.CampaignTitle}!"
                : $"تم إضافة ختم في بطاقة {punch.CampaignTitle}! رصيدك الآن {punch.CurrentPunches} من {punch.TargetPunches}.");
        }

        return SendMessageAsync(phoneNumber, builder.ToString().TrimEnd());
    }

    public Task SendPostDeliveryPointsNotificationAsync(string phoneNumber, string customerName, int earnedPoints, int newTotalPoints)
    {
        // A 0-point delivery is possible (e.g. an order small enough that the
        // currency-per-point formula rounds down to nothing) - "✅ تم إضافة 0 نقاط" would
        // read as broken rather than encouraging, so this touchpoint simply doesn't fire
        // for it. SendOrderConfirmationAsync already covers that order regardless.
        if (earnedPoints <= 0)
        {
            return Task.CompletedTask;
        }

        // A specified, exact template - deliberately its own literal text rather than
        // delegating to SendLoyaltyWalletUpdateAsync below, since it differs from that
        // method's template in three small ways: no "،" after the customer's name, no
        // blank line between the greeting and the wallet-update line, and "نقاط ."
        // (plural, with a space before the period) instead of "نقطة." on the
        // earned-points line. Written with \n concatenation (matching every other
        // template in this file) rather than a verbatim string literal, so the actual
        // line endings sent to WhatsApp can't vary with how this source file happens to
        // be checked out (CRLF vs LF) - the rendered message is identical either way.
        var message =
            $"مرحباً {customerName}\n" +
            "💳 تحديث جديد لمحفظة نقاط أوتانتيك الخاصة بك:\n" +
            $"✅ تم إضافة {earnedPoints} نقاط .\n" +
            $"رصيدك الحالي هو: {newTotalPoints} نقطة.\n\n" +
            "يسعدنا دائماً خدمتك! شاركنا تقييمك لتجربتك اليوم عبر الرابط التالي:\n" +
            $"{RatingBaseUrl}/rate/store";

        return SendMessageAsync(phoneNumber, message);
    }

    public Task SendLoyaltyWalletUpdateAsync(string phoneNumber, string customerName, bool isRedemption, int transactionPoints, int totalBalance)
    {
        var actionText = isRedemption ? "🔻 تم استبدال" : "✅ تم إضافة";

        var message =
            $"مرحباً {customerName}،\n\n" +
            "💳 تحديث جديد لمحفظة نقاط أوتانتيك الخاصة بك:\n" +
            $"{actionText} {transactionPoints} نقطة.\n" +
            $"رصيدك الحالي هو: {totalBalance} نقطة.\n\n" +
            "يسعدنا دائماً خدمتك! شاركنا تقييمك لتجربتك اليوم عبر الرابط التالي:\n" +
            $"{RatingBaseUrl}/rate/store";

        return SendMessageAsync(phoneNumber, message);
    }

    public async Task SendOrderNotificationsAsync(Order order, IEnumerable<string?> managerPhones)
    {
        var customerMessage =
            $"مرحباً {order.CustomerName}، تم استلام طلبك بنجاح. شكراً لطلبك من مطعم أوتانتيك! رقم الطلب: #{order.Id}";
        await SendMessageAsync(order.CustomerPhone, customerMessage);

        var validManagerPhones = managerPhones.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (validManagerPhones.Count == 0)
        {
            return;
        }

        // Same message to every configured manager, sent concurrently - formatting each
        // number into Green API's chatId shape happens inside SendMessageAsync itself
        // (see ToChatId), same as every other message this service sends.
        var managerMessage = BuildManagerMessage(order);
        await Task.WhenAll(validManagerPhones.Select(phone => SendMessageAsync(phone!, managerMessage)));
    }

    // Every line total (and the subtotal below) uses Quantity × (UnitPrice + add-ons'
    // prices) - OrderItem.UnitPrice alone excludes add-ons (mirrors
    // OrderService.CalculateTotal's own formula), so a plain Quantity × UnitPrice would
    // silently under-report both figures for any item with add-ons selected. There's no
    // stored Order.Subtotal column - it's always derived from the order's own items so it
    // can never drift from what TotalAmount was actually built from.
    private static string BuildManagerMessage(Order order)
    {
        static decimal LineTotal(OrderItem item) => item.Quantity * (item.UnitPrice + item.AddOns.Sum(a => a.Price));

        var itemsList = new StringBuilder();
        foreach (var item in order.OrderItems)
        {
            itemsList.AppendLine($"- {item.Quantity}x {item.MenuItem.Name} ({LineTotal(item):0.##} جنيه)");
        }

        var subtotal = order.OrderItems.Sum(LineTotal);

        var message = new StringBuilder();
        message.AppendLine("🚨 طلب جديد بانتظار التأكيد!");
        message.AppendLine($"رقم الطلب: #{order.Id}");
        message.AppendLine($"العميل: {order.CustomerName}");
        message.AppendLine($"الهاتف: {order.CustomerPhone}");
        message.AppendLine($"العنوان: {order.DeliveryAddress}");
        message.AppendLine();
        message.AppendLine("📦 محتويات الطلب:");
        message.Append(itemsList);
        message.AppendLine();
        message.AppendLine("💰 تفاصيل الحساب:");
        message.AppendLine($"المجموع: {subtotal:0.##} جنيه");
        message.AppendLine($"التوصيل: {order.DeliveryFee:0.##} جنيه");
        message.AppendLine($"الضريبة: {order.TaxAmount:0.##} جنيه");
        if (order.DiscountAmount > 0)
        {
            message.AppendLine($"الخصم: {order.DiscountAmount:0.##} جنيه");
        }
        message.AppendLine($"الإجمالي المطلوب: {order.TotalAmount:0.##} جنيه");

        return message.ToString().TrimEnd();
    }

    private async Task SendMessageAsync(string phoneNumber, string message)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Green API is not configured - skipping WhatsApp message to {Phone}.", phoneNumber);
            return;
        }

        try
        {
            var url = $"{_options.ApiUrl}/waInstance{_options.IdInstance}/sendMessage/{_options.ApiTokenInstance}";
            var payload = new GreenApiSendMessageRequest(ToChatId(phoneNumber), message);

            using var response = await _httpClient.PostAsJsonAsync(url, payload);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Green API send failed ({Status}) to {Phone}: {Body}", response.StatusCode, phoneNumber, body);
            }
        }
        catch (Exception ex)
        {
            // Never let a WhatsApp/network failure bubble up to the caller (registration,
            // order-delivered) - this is purely a notification side effect.
            _logger.LogError(ex, "Unexpected error sending WhatsApp message to {Phone}.", phoneNumber);
        }
    }

    // Formats a local Egyptian number (e.g. "01123555570") into Green API's chatId format
    // ("201123555570@c.us") - also accepts numbers already in +/00-prefixed international
    // form. Kept as the single phone formatter for every message this service sends,
    // rather than a second copy under a different name.
    private static string ToChatId(string phoneNumber)
    {
        var normalized = phoneNumber.Trim();
        if (normalized.StartsWith('+'))
        {
            normalized = normalized[1..];
        }
        else if (normalized.StartsWith("00"))
        {
            normalized = normalized[2..];
        }
        else if (normalized.StartsWith('0'))
        {
            // Assumes a local Egyptian number (e.g. "01012345678") - fine for this
            // Egypt-based restaurant, but would mis-tag a non-Egyptian local-format number.
            normalized = EgyptCountryCode + normalized[1..];
        }

        return $"{normalized}@c.us";
    }
}
