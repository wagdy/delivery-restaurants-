namespace RestaurantDelivery.Api.Configuration;

// Policy names for the per-IP rate limiters configured in Program.cs, referenced from
// [EnableRateLimiting] on the handful of endpoints that are reachable without a token.
//
// Only anonymous endpoints are limited. Everything else already requires a JWT, so abuse
// there is bounded by an account an admin can disable - and a blanket limiter would risk
// throttling legitimate staff work like a bulk menu import.
public static class RateLimitPolicies
{
    // Sign-in. Deliberately the most generous of these: the real brute-force defense is
    // now account lockout (see Identity's Lockout options in Program.cs), and a whole
    // restaurant's staff can share one NAT'd connection at the start of a shift, so a
    // tight per-IP cap here would lock out the wrong people.
    public const string Login = "rl-login";

    // Account creation - throttled to stop automated signup floods filling the customers
    // table (and the loyalty profiles created alongside each one).
    public const string Register = "rl-register";

    // Requesting a password-reset OTP. The strictest policy here, because every single
    // call sends a real WhatsApp message: unthrottled, it's both a way to harass a
    // customer whose number someone knows and a way to spend the restaurant's Green API
    // balance. Limits the sender, not the target - see the note in Program.cs.
    public const string OtpRequest = "rl-otp-request";

    // Submitting an OTP. PasswordResetToken.FailedAttempts already caps guesses at 5 per
    // issued code; this stops an attacker cheaply cycling new codes to buy more guesses.
    public const string OtpVerify = "rl-otp-verify";

    // Promo code validation - the one endpoint that answers "is this string a real
    // discount code?", so without a limit it's a free code-guessing oracle.
    public const string PromoValidate = "rl-promo-validate";

    // Guest checkout. Unauthenticated order creation also triggers a WhatsApp alert to
    // the manager per order, so a flood is simultaneously a kitchen DoS and a bill.
    public const string OrderCreate = "rl-order-create";
}
