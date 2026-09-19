namespace RestaurantDelivery.Infrastructure.ExternalServices;

// The post-delivery points message, shared by both WhatsApp providers.
//
// Written once rather than copied into each: the Green API and OpenWA implementations
// carried byte-identical Arabic for this message, and money-adjacent copy that has to be
// edited in two places is copy that eventually says two different things. The providers
// still own how a message is SENT; this owns what it says.
internal static class DeliveryPointsMessage
{
    // True when there is anything at all to report. A delivery that earned nothing AND
    // redeemed nothing sends no message - the pre-existing rule, widened only so that a
    // redemption still reports even when the order earned 0 points back.
    //
    // That widening matters: an order can easily redeem points and earn none, because
    // points are earned on the amount actually paid and a redemption reduces it. Under
    // the old earned-only guard, the customer who most needed to see "🔻 تم خصم 200
    // نقطة" was precisely the one who got no message.
    public static bool ShouldSend(int earnedPoints, int redeemedPoints) =>
        earnedPoints > 0 || redeemedPoints > 0;

    public static string Build(
        string customerName,
        int earnedPoints,
        int redeemedPoints,
        int totalPointsBalance,
        string ratingBaseUrl)
    {
        var message =
            $"مرحباً {customerName}\n" +
            "💳 تحديث جديد لمحفظة نقاط أوتانتيك الخاصة بك:\n\n" +
            "💡 ملخص نقاطك:\n";

        // Each line is omitted when its figure is 0, following the rule this message
        // already applied to earned points: "تم إضافة 0 نقاط" reads as something broken
        // rather than as nothing having happened. The vast majority of orders redeem
        // nothing, so a permanent "🔻 تم خصم: 0 نقطة" line would be on almost every
        // message, telling the customer about a transaction that never occurred.
        if (redeemedPoints > 0)
        {
            message += $"🔻 تم خصم: {redeemedPoints} نقطة (استبدال)\n";
        }

        if (earnedPoints > 0)
        {
            message += $"🟢 تم إضافة: {earnedPoints} نقطة (من هذا الطلب)\n";
        }

        // The balance always shows - it is the one figure that is meaningful whatever
        // happened, and it is what the customer actually came to the message for.
        message +=
            $"🏆 إجمالي رصيدك الحالي: {totalPointsBalance} نقطة\n\n" +
            "يسعدنا دائماً خدمتك! شاركنا تقييمك لتجربتك اليوم عبر الرابط التالي:\n" +
            $"{ratingBaseUrl}/rate/store";

        return message;
    }
}
