using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Auth;

namespace RestaurantDelivery.Core.Interfaces;

public interface IAuthService
{
    Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ServiceResult<UserProfileResponse>> GetProfileAsync(string userId);

    // Admin-only: provisions an Admin or CaptainOrder account. Distinct from RegisterAsync,
    // which always self-registers a Customer — a staff role can never be self-assigned.
    Task<ServiceResult<UserProfileResponse>> CreateStaffUserAsync(CreateStaffUserRequest request);

    // Log in by phone number + password - used by customers (registered via RegisterAsync)
    // and staff (provisioned via CreateStaffUserAsync). Matches any role by phone; the
    // legacy email-based LoginAsync above remains for the two pre-existing seeded staff
    // accounts (and any legacy email-registered customer) that have no phone number.
    Task<ServiceResult<AuthResponse>> LoginByPhoneAsync(PhoneLoginRequest request);
}
