using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Auth;
using RestaurantDelivery.Core.DTOs.Customers;

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

    // Shared by the admin "Create Order" POS's New Customer mode (see OrderService.CreateAsync)
    // and the Scanner page's own "New Customer" tab (see CustomersController.Register):
    // returns the existing customer's id if this phone number is already registered
    // (self-healing a staff member re-entering a repeat customer's details, IsNewCustomer:
    // false - no welcome bonus/WhatsApp is re-sent), otherwise provisions a brand-new
    // Customer-role account, awards the 100-point welcome bonus, and sends the welcome
    // WhatsApp notification - the exact same side effects RegisterAsync's self-service path
    // gets, so a customer's welcome treatment never depends on which page created them.
    // Unlike RegisterAsync, the customer never chooses their own password - see the
    // implementation's own doc comment for that trade-off.
    Task<ServiceResult<FindOrCreateCustomerResult>> FindOrCreateCustomerByPhoneAsync(string fullName, string phoneNumber, string? address);
}
