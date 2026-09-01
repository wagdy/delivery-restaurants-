using Microsoft.AspNetCore.Identity;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Entities;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
