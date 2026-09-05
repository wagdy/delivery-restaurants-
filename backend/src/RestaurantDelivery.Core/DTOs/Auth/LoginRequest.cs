using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Auth;

public class LoginRequest
{
    // Either an email address or a phone number - AuthService.LoginAsync detects which
    // by checking for '@' and looks the account up accordingly. Covers both customers
    // (phone) and staff (phone, or email for the two pre-existing legacy accounts) with
    // a single field, so the client never needs to know or ask which kind of account it is.
    [Required]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
