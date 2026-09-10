namespace RestaurantDelivery.Core.Entities;

// A short-lived, WhatsApp-delivered OTP for AuthService.ResetPasswordAsync - deliberately
// not ASP.NET Identity's own built-in password-reset token (a long opaque string meant for
// a link, not something a customer can read off WhatsApp and type on a phone keypad).
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string AppUserId { get; set; } = string.Empty;
    public AppUser AppUser { get; set; } = null!;

    // Hashed via IPasswordHasher<AppUser> (the same hasher AuthService already uses for
    // account passwords), never stored in plaintext - whoever reads this row directly
    // from the database should not be able to reset the account's password with it.
    public string OtpHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public bool IsUsed { get; set; }

    // Brute-force guard: a 6-digit OTP only has 1,000,000 possibilities, which is
    // realistically guessable within a 10-minute window with no cap on wrong attempts -
    // see AuthService.ResetPasswordAsync's MaxOtpAttempts check.
    public int FailedAttempts { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
