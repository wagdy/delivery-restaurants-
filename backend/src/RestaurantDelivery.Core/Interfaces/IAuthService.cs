using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Auth;

namespace RestaurantDelivery.Core.Interfaces;

public interface IAuthService
{
    Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest request);

    // Single sign-in entry point for every account (customers and staff alike) - see
    // LoginRequest.Identifier and AuthService.LoginAsync for the email-vs-phone detection.
    Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ServiceResult<UserProfileResponse>> GetProfileAsync(string userId);

    // Admin-only: provisions an Admin or CaptainOrder account. Distinct from RegisterAsync,
    // which always self-registers a Customer — a staff role can never be self-assigned.
    Task<ServiceResult<UserProfileResponse>> CreateStaffUserAsync(CreateStaffUserRequest request);
}
