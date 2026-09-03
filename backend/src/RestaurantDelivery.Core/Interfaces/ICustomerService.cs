using RestaurantDelivery.Core.DTOs.Customers;

namespace RestaurantDelivery.Core.Interfaces;

public interface ICustomerService
{
    Task<List<CustomerInsightResponse>> GetCustomerInsightsAsync();

    // For the CRM admin screen - includes each customer's loyalty standing, unlike
    // GetCustomerInsightsAsync which is order-stats only.
    Task<List<CustomerCrmResponse>> GetCrmCustomersAsync();
}
