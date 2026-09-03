using System.Text.Json.Serialization;

namespace RestaurantDelivery.Infrastructure.ExternalServices.GreenApi;

internal record GreenApiSendMessageRequest(
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("message")] string Message);
