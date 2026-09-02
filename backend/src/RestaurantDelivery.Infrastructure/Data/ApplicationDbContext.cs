using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<AppUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<RestaurantSettings> RestaurantSettings => Set<RestaurantSettings>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<AddOn> AddOns => Set<AddOn>();
    public DbSet<WebPushSubscription> WebPushSubscriptions => Set<WebPushSubscription>();

    // Named CustomRoles, not Roles - IdentityDbContext<AppUser> already inherits
    // DbSet<IdentityRole> Roles (mapped to the unused AspNetRoles table); reusing that
    // name here would silently hide the inherited member.
    public DbSet<Role> CustomRoles => Set<Role>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
