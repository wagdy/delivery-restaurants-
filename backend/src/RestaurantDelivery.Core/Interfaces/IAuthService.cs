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

    // The Staff tab's management table (list/edit/delete), backing StaffController - kept
    // separate from CreateStaffUserAsync above (which stays wired to the pre-existing
    // POST api/auth/staff) since listing/editing/deleting existing accounts is a distinct
    // concern from provisioning a brand-new one.
    Task<ServiceResult<List<StaffAccountResponse>>> GetStaffAccountsAsync();

    Task<ServiceResult<StaffAccountResponse>> UpdateStaffUserAsync(string id, UpdateStaffUserRequest request);

    // Hard delete, per explicit product decision - see the implementation's own doc
    // comment for why this is the one place in the app that removes an AppUser row
    // outright rather than soft-deleting it, and what that trades away. requestingUserId
    // is the caller's own id (from their JWT), checked so a staff member can never delete
    // their own account through this endpoint and lock themselves out.
    Task<ServiceResult<bool>> DeleteStaffUserAsync(string id, string? requestingUserId);

    // Shared by the admin "Create Order" POS's New Customer mode (see OrderService.CreateAsync)
    // and the Scanner page's own "New Customer" tab (see CustomersController.Register):
    // returns the existing customer's id if this phone number is already registered
    // (self-healing a staff member re-entering a repeat customer's details, IsNewCustomer:
    // false - no welcome bonus/WhatsApp is re-sent), otherwise provisions a brand-new
    // Customer-role account, awards the 100-point welcome bonus, and sends the welcome
    // WhatsApp notification - the exact same side effects RegisterAsync's self-service path
    // gets, so a customer's welcome treatment never depends on which page created them.
    // Unlike RegisterAsync, the customer never chooses their own password - see the
    // implementation's own doc comment for that trade-off. isPastCustomer swaps the
    // welcome WhatsApp for SendPastCustomerWelcomeAsync's no-review-link variant (Customer
    // Insights' "Register Past Customer" dialog, see CrmController.RegisterPastCustomer)
    // - everything else (bonus, reactivation, auto-generated password) is identical.
    Task<ServiceResult<FindOrCreateCustomerResult>> FindOrCreateCustomerByPhoneAsync(string fullName, string phoneNumber, string? address, bool isPastCustomer = false);

    // Customer accounts only (see ResetPasswordAsync's own doc comment for why staff
    // accounts are deliberately excluded from this whole flow). Always succeeds from the
    // caller's perspective regardless of whether the phone number is actually registered
    // - see the implementation for the anti-enumeration reasoning.
    Task<ServiceResult<bool>> ForgotPasswordAsync(ForgotPasswordRequest request);

    // Deliberately scoped to Role == Customer, unlike LoginAsync/FindOrCreateCustomerByPhoneAsync's
    // "phone number is universal" treatment - this is a public, unauthenticated endpoint
    // that ends in a full password takeover, and Admin/CaptainOrder accounts are a much
    // higher-value target than a customer account. Staff password recovery still has no
    // self-service path (matches CreateStaffUserAsync's own admin-gated-only provisioning).
    Task<ServiceResult<bool>> ResetPasswordAsync(ResetPasswordRequest request);
}
