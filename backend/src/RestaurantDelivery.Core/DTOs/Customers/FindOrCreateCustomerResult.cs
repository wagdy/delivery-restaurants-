namespace RestaurantDelivery.Core.DTOs.Customers;

public class FindOrCreateCustomerResult
{
    public string CustomerId { get; set; } = string.Empty;

    // False when this phone number was already registered and the existing account was
    // returned instead of creating a new one - see AuthService.FindOrCreateCustomerByPhoneAsync.
    // Only a true IsNewCustomer got the welcome bonus/WhatsApp notification just now.
    public bool IsNewCustomer { get; set; }
}
