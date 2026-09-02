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

    // Staff (Admin/CaptainOrder) log in with phone number + password, a separate path
    // from the email-based LoginAsync used by customers and pre-existing staff accounts.
    Task<ServiceResult<AuthResponse>> LoginStaffAsync(StaffLoginRequest request);
}
