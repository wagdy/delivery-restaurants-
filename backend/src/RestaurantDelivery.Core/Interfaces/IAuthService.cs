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

    // Used by the admin "Create Order" POS's New Customer mode (see OrderService.CreateAsync):
    // returns the existing customer's id if this phone number is already registered
    // (self-healing a cashier picking "New" for a repeat customer), otherwise provisions a
    // brand-new Customer-role account on the spot. Unlike RegisterAsync, the customer never
    // chooses their own password - see the implementation's own doc comment for that trade-off.
    Task<ServiceResult<string>> FindOrCreateCustomerByPhoneAsync(string fullName, string phoneNumber, string? address);
}
