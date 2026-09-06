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

    public Task<List<CustomerCrmResponse>> GetCrmCustomersAsync()
    {
        // Left join against LoyaltyProfiles (AppUser has no nav property to it, by design -
        // see LoyaltyProfileConfiguration) so a customer who's never opened the Rewards tab
        // still appears, with zeroed-out loyalty fields.
        var query =
            from u in _context.Users
            where u.Role == UserRole.Customer
            join p in _context.LoyaltyProfiles on u.Id equals p.AppUserId into profileJoin
            from profile in profileJoin.DefaultIfEmpty()
            orderby u.Orders.Count() descending
            select new CustomerCrmResponse
            {
                Id = u.Id,
                FullName = u.FullName,
                PhoneNumber = u.PhoneNumber,
                TotalOrders = u.Orders.Count(),
                AverageOrderValue = u.Orders.Any() ? u.Orders.Average(o => o.TotalAmount) : 0m,
                CurrentPoints = profile != null ? profile.CurrentPoints : 0,
                TotalLifetimePoints = profile != null ? profile.TotalLifetimePoints : 0,
                MembershipTier = profile != null ? (profile.MembershipTier ?? "Unranked") : "Unranked"
            };

        return query.ToListAsync();
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
