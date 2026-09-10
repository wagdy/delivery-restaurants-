namespace RestaurantDelivery.Infrastructure.ExternalServices.OpenWa;

// Mirrors OpenWA's SendTextMessageDto for POST /api/sessions/{sessionId}/messages/send-text -
// only the two fields this app ever sends. Every other optional field on that DTO (mentions,
// linkPreview, customLinkPreview, quotedMessageId) is left at its server-side default.
internal record OpenWaSendTextRequest(string ChatId, string Text);
