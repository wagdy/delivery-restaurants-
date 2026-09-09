namespace RestaurantDelivery.Core.DTOs.Customers;

public class FindOrCreateCustomerResult
{
    public string CustomerId { get; set; } = string.Empty;

    // True for a brand-new account AND for a reactivated (previously soft-deleted) one -
    // both got the welcome bonus/WhatsApp notification just now. False only when this
    // phone number matched an existing, still-active account, which was returned as-is
    // with no bonus/notification re-sent. See AuthService.FindOrCreateCustomerByPhoneAsync.
    public bool IsNewCustomer { get; set; }
}
