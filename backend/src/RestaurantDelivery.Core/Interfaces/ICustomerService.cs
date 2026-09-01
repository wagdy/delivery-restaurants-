using RestaurantDelivery.Core.DTOs.Customers;

namespace RestaurantDelivery.Core.Interfaces;

public interface ICustomerService
{
    Task<List<CustomerInsightResponse>> GetCustomerInsightsAsync();
}
