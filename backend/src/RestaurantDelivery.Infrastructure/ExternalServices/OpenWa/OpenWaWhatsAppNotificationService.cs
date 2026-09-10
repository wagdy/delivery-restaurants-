using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.ExternalServices.OpenWa;

// A drop-in replacement for WhatsAppNotificationService (Green API) - implements the exact
// same IWhatsAppNotificationService contract, so cutting over is a single DI-line swap in
// Program.cs with zero changes to any caller (AuthService, LoyaltyService, OrderService).
// Message CONTENT is deliberately identical to the Green API implementation; only the
// transport differs (OpenWA's session-scoped REST API + X-API-Key header, instead of Green
// API's instance/token-in-URL scheme). Kept as an independent copy of the message templates
// rather than sharing them with WhatsAppNotificationService, specifically so adding this
// file carries zero risk to the currently-live Green API path - see the migration guide for
// when/how to consolidate the two once Green API is actually retired.
public class OpenWaWhatsAppNotificationService : IWhatsAppNotificationService
{
    private const string EgyptCountryCode = "20";
    private const string RatingBaseUrl = "https://web-production-1bacf.up.railway.app";

    // Same footer, same reasoning as WhatsAppNotificationService's own PromotionalFooter -
    // appended by SendMessageAsync itself below, regardless of message type.
    private const string PromotionalFooter =
        "تقدر دلوقتي تشوف المنيو وتتابع نقاطك وعروضك من هنا:\n" +
        RatingBaseUrl + "/";

    private readonly HttpClient _httpClient;
    private readonly OpenWaSettings _settings;
    private readonly ILogger<OpenWaWhatsAppNotificationService> _logger;

    public OpenWaWhatsAppNotificationService(HttpClient httpClient, IOptions<OpenWaSettings> options, ILogger<OpenWaWhatsAppNotificationService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public Task SendWelcomeMessageAsync(string phoneNumber, string customerName, string password)
    {
        // Exact same template as WhatsAppNotificationService's own SendWelcomeMessageAsync -
        // see that method's doc comment for why the "100 نقطة" figures are literal.
        var message =
            $"مرحباً {customerName}، 🌟\n" +
            "أهلاً بك في عائلة أوتانتيك! سعداء بانضمامك لبرنامج الولاء الخاص بنا.\n\n" +
            "🎉 بمناسبة تسجيلك، تم إهداؤك 100 نقطة ترحيبية في محفظتك!\n" +
            "رصيدك الحالي هو: 100 نقطة.\n\n" +
            "🔐 بيانات الدخول لحسابك:\n" +
            $"رقم الهاتف: {phoneNumber}\n" +
            $"كلمة المرور: {password}\n" +
            "(يمكنك تغيير كلمة المرور في أي وقت من إعدادات حسابك)\n\n" +
            "نتمنى لك تجربة سعيدة ومميزة معنا دائماً. شاركنا تقييمك لزيارتك اليوم عبر الرابط التالي:\n" +
            $"{RatingBaseUrl}/rate/store";

        return SendMessageAsync(phoneNumber, message);
    }

    public Task SendPastCustomerWelcomeAsync(string phoneNumber, string customerName, string password)
    {
        // Exact same template as WhatsAppNotificationService's own
        // SendPastCustomerWelcomeAsync - a past-visit welcome with no review link, since
        // there's no delivery order to review yet. No longer includes its own menu-link
        // line - SendMessageAsync's PromotionalFooter now appends that exact same line to
        // every outgoing message, so keeping a second copy here would show it twice.
        var message =
            $"مرحباً {customerName}، 🌟\n" +
            "سعداء جداً بزياراتك لفرع أوتانتيك! عشان إنت عميل مميز، ضفناك لبرنامج الولاء الخاص بينا.\n\n" +
            "🎉 تم إهداؤك 100 نقطة ترحيبية في محفظتك!\n\n" +
            "🔐 بيانات الدخول لحسابك:\n" +
            $"رقم الهاتف: {phoneNumber}\n" +
            $"كلمة المرور: {password}\n" +
            "(يمكنك تغيير كلمة المرور في أي وقت من إعدادات حسابك)\n\n" +
            "في انتظارك تنورنا مرة تانية قريباً!";

        return SendMessageAsync(phoneNumber, message);
    }

    public Task SendPasswordResetOtpAsync(string phoneNumber, string otpCode)
    {
        var message =
            "مرحباً، 🔐\n" +
            "طلبنا تغيير كلمة المرور لحسابك في أوتانتيك.\n\n" +
            $"كود التحقق الخاص بك هو: {otpCode}\n" +
            "(هذا الكود صالح لمدة 10 دقائق)\n\n" +
            "إذا لم تطلب هذا التغيير، يرجى تجاهل هذه الرسالة.";

        return SendMessageAsync(phoneNumber, message);
    }

    public Task SendPostDeliveryPointsNotificationAsync(string phoneNumber, string customerName, int earnedPoints, int newTotalPoints)
    {
        if (earnedPoints <= 0)
        {
            return Task.CompletedTask;
        }

        var message =
            $"مرحباً {customerName}\n" +
            "💳 تحديث جديد لمحفظة نقاط أوتانتيك الخاصة بك:\n" +
            $"✅ تم إضافة {earnedPoints} نقاط .\n" +
            $"رصيدك الحالي هو: {newTotalPoints} نقطة.\n\n" +
            "يسعدنا دائماً خدمتك! شاركنا تقييمك لتجربتك اليوم عبر الرابط التالي:\n" +
            $"{RatingBaseUrl}/rate/store";

        return SendMessageAsync(phoneNumber, message);
    }

    public Task SendGuestDeliveryThankYouAsync(string phoneNumber, string customerName, int orderId)
    {
        // No visible order number in the text (per explicit instruction - customers never
        // see an order ID/number) - orderId is still passed through into the URL below so
        // the rating page itself knows which order to attach the review to, but that's an
        // invisible routing parameter, not a number displayed for the customer to read.
        var message =
            $"مرحباً {customerName}،\n" +
            "شكراً لطلبك من مطعم أوتانتيك، نتمنى أن تكون قد استمتعت بوجبتك! 🧡\n\n" +
            "شاركنا تقييمك لمساعدتنا على تقديم الأفضل دائماً عبر الرابط التالي:\n" +
            $"{RatingBaseUrl}/rate/store?orderId={orderId}";

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
        // No order number in the customer's own confirmation (per explicit instruction) -
        // the manager alert below still includes it (BuildManagerMessage), since that's an
        // internal staff notification, not something a customer ever sees.
        var customerMessage =
            $"مرحباً {order.CustomerName}، تم استلام طلبك بنجاح. شكراً لطلبك من مطعم أوتانتيك!";
        await SendMessageAsync(order.CustomerPhone, customerMessage);

        var validManagerPhones = managerPhones.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (validManagerPhones.Count == 0)
        {
            return;
        }

        var managerMessage = BuildManagerMessage(order);
        await Task.WhenAll(validManagerPhones.Select(phone => SendMessageAsync(phone!, managerMessage)));
    }

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

    // The one method whose transport actually differs from Green API's WhatsAppNotificationService:
    // POSTs to OpenWA's session-scoped /api/sessions/{sessionId}/messages/send-text instead of
    // Green API's /waInstanceX/sendMessage/Y, authenticating via the X-API-Key header instead of
    // embedding credentials in the URL. Same "never throw" contract as every other
    // IWhatsAppNotificationService implementation - every caller invokes this with a bare `await`,
    // and a gateway outage must never fail a registration, an OTP request, or a captain's "mark
    // delivered" tap.
    private async Task SendMessageAsync(string phoneNumber, string message)
    {
        if (!_settings.IsConfigured)
        {
            _logger.LogWarning("OpenWA is not configured - skipping WhatsApp message to {Phone}.", phoneNumber);
            return;
        }

        // Every outgoing message gets the promotional footer appended here, right before
        // it goes out - the one place every SendXxxAsync method above funnels through.
        var fullMessage = $"{message}\n\n{PromotionalFooter}";

        try
        {
            var url = $"{_settings.ApiBaseUrl.TrimEnd('/')}/api/sessions/{_settings.SessionId}/messages/send-text";
            var payload = new OpenWaSendTextRequest(ToChatId(phoneNumber), fullMessage);

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Add("X-API-Key", _settings.ApiKey);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                // A 409 specifically means the session exists but isn't connected right now
                // (QR never scanned, or the linked phone went offline/was unlinked) - still
                // logged as an error since it's a real delivery failure either way, just
                // distinguishable from a 4xx/5xx by the status code in this line.
                _logger.LogError("OpenWA send failed ({Status}) to {Phone}: {Body}", response.StatusCode, phoneNumber, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending WhatsApp message via OpenWA to {Phone}.", phoneNumber);
        }
    }

    // Identical to WhatsAppNotificationService.ToChatId - OpenWA and Green API both speak the
    // same WhatsApp Web chatId shape ("201012345678@c.us").
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
