using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.DTOs.Customers;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _context;

    public CustomerService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<CustomerInsightResponse>> GetCustomerInsightsAsync()
    {
        // Count() and Average() here are correlated subqueries over each customer's Orders
        // navigation — EF Core translates the whole projection into a single SQL statement
        // (GROUP BY / correlated aggregates), so the database does the aggregation and only
        // one summary row per customer ever crosses the wire. No orders are loaded into memory.
        // The ternary avoids AVG's behavior on a customer with zero orders producing a null
        // that would otherwise surface as an exception from the non-nullable decimal projection.
        return _context.Users
            .Where(u => u.Role == UserRole.Customer)
            .OrderByDescending(u => u.Orders.Count())
            .Select(u => new CustomerInsightResponse
            {
                CustomerName = u.FullName,
                PhoneNumber = u.PhoneNumber,
                TotalOrders = u.Orders.Count(),
                AverageOrderValue = u.Orders.Any() ? u.Orders.Average(o => o.TotalAmount) : 0m
            })
            .ToListAsync();
    }
}
