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
