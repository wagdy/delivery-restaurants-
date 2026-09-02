using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Infrastructure.Data.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        // "Admin@12345/Captain@12345" are dev-only fallbacks. This seeder always runs
        // (including in Production, since a fresh DB has no other way to get an initial
        // Admin), so a real deployment must override these via Seed__AdminPassword /
        // Seed__CaptainPassword *before* the first boot - once the account exists,
        // changing the variable has no effect on it.
        var adminPassword = configuration["Seed:AdminPassword"] ?? "Admin@12345";
        var captainPassword = configuration["Seed:CaptainPassword"] ?? "Captain@12345";

        await SeedStaffUserAsync(userManager, "admin@restaurant.com", adminPassword, "Restaurant Administrator", UserRole.Admin);
        await SeedStaffUserAsync(userManager, "captain@restaurant.com", captainPassword, "Demo Delivery Captain", UserRole.CaptainOrder);
        await SeedMenuItemsAsync(context);
        await SeedCategoriesAsync(context);
        await SeedAddOnsAsync(context);
        await SeedRestaurantSettingsAsync(context);
        await SeedRolesAsync(context);
    }

    private static async Task SeedStaffUserAsync(
        UserManager<AppUser> userManager,
        string email,
        string password,
        string fullName,
        UserRole role)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            Role = role
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed {role} user ({email}): {errors}");
        }
    }

    private static async Task SeedMenuItemsAsync(ApplicationDbContext context)
    {
        if (context.MenuItems.Any())
        {
            return;
        }

        context.MenuItems.AddRange(
            new MenuItem { Name = "Garlic Bread", Description = "Toasted baguette with garlic butter and herbs.", Price = 5.99m, Category = "Starters", IsAvailable = true },
            new MenuItem { Name = "Mozzarella Sticks", Description = "Breaded mozzarella served with marinara sauce.", Price = 7.49m, Category = "Starters", IsAvailable = true },
            new MenuItem { Name = "Caesar Salad", Description = "Romaine lettuce, parmesan, croutons, Caesar dressing.", Price = 8.99m, Category = "Starters", IsAvailable = true },

            new MenuItem { Name = "Margherita Pizza", Description = "Tomato sauce, fresh mozzarella, basil.", Price = 12.99m, Category = "Mains", IsAvailable = true },
            new MenuItem { Name = "Pepperoni Pizza", Description = "Tomato sauce, mozzarella, pepperoni.", Price = 14.49m, Category = "Mains", IsAvailable = true },
            new MenuItem { Name = "Classic Cheeseburger", Description = "Beef patty, cheddar, lettuce, tomato, brioche bun.", Price = 11.99m, Category = "Mains", IsAvailable = true },
            new MenuItem { Name = "Grilled Chicken Alfredo", Description = "Fettuccine pasta in creamy Alfredo sauce.", Price = 13.99m, Category = "Mains", IsAvailable = true },

            new MenuItem { Name = "Soft Drink", Description = "Cola, lemon-lime, or orange soda.", Price = 2.49m, Category = "Drinks", IsAvailable = true },
            new MenuItem { Name = "Fresh Orange Juice", Description = "Freshly squeezed orange juice.", Price = 3.99m, Category = "Drinks", IsAvailable = true },
            new MenuItem { Name = "Iced Tea", Description = "Sweetened black iced tea.", Price = 2.99m, Category = "Drinks", IsAvailable = true }
        );

        await context.SaveChangesAsync();
    }

    private static async Task SeedCategoriesAsync(ApplicationDbContext context)
    {
        if (await context.Categories.AnyAsync())
        {
            return;
        }

        // Derive from whatever categories already exist on menu items, so seeding
        // never orphans real data whether this runs on a fresh DB or an existing one.
        var existingCategoryNames = await context.MenuItems
            .Select(m => m.Category)
            .Distinct()
            .ToListAsync();

        if (existingCategoryNames.Count == 0)
        {
            existingCategoryNames = new List<string> { "Starters", "Mains", "Drinks" };
        }

        context.Categories.AddRange(existingCategoryNames.Select(
            (name, index) => new Category { Name = name, DisplayOrder = index }));
        await context.SaveChangesAsync();
    }

    private static async Task SeedAddOnsAsync(ApplicationDbContext context)
    {
        if (await context.AddOns.AnyAsync())
        {
            return;
        }

        var extraSauce = new AddOn { Name = "Extra Sauce", Price = 0.75m };
        var extraCheese = new AddOn { Name = "Extra Cheese", Price = 1.00m };
        var sideRice = new AddOn { Name = "Side of Rice", Price = 1.50m };
        var sidePasta = new AddOn { Name = "Side of Pasta", Price = 2.00m };

        context.AddOns.AddRange(extraSauce, extraCheese, sideRice, sidePasta);

        var menuItems = await context.MenuItems.ToListAsync();
        MenuItemAddOn Assign(string itemName, AddOn addOn) => new()
        {
            MenuItem = menuItems.First(m => m.Name == itemName),
            AddOn = addOn
        };

        var assignments = new List<MenuItemAddOn>();
        if (menuItems.Any(m => m.Name == "Margherita Pizza"))
        {
            assignments.Add(Assign("Margherita Pizza", extraCheese));
            assignments.Add(Assign("Margherita Pizza", extraSauce));
        }
        if (menuItems.Any(m => m.Name == "Pepperoni Pizza"))
        {
            assignments.Add(Assign("Pepperoni Pizza", extraCheese));
            assignments.Add(Assign("Pepperoni Pizza", extraSauce));
        }
        if (menuItems.Any(m => m.Name == "Classic Cheeseburger"))
        {
            assignments.Add(Assign("Classic Cheeseburger", extraCheese));
            assignments.Add(Assign("Classic Cheeseburger", extraSauce));
        }
        if (menuItems.Any(m => m.Name == "Grilled Chicken Alfredo"))
        {
            assignments.Add(Assign("Grilled Chicken Alfredo", sideRice));
            assignments.Add(Assign("Grilled Chicken Alfredo", sidePasta));
            assignments.Add(Assign("Grilled Chicken Alfredo", extraSauce));
        }
        if (menuItems.Any(m => m.Name == "Caesar Salad"))
        {
            assignments.Add(Assign("Caesar Salad", extraSauce));
        }

        context.Set<MenuItemAddOn>().AddRange(assignments);

        await context.SaveChangesAsync();
    }

    private static async Task SeedRestaurantSettingsAsync(ApplicationDbContext context)
    {
        if (context.RestaurantSettings.Any())
        {
            return;
        }

        context.RestaurantSettings.Add(new RestaurantSettings
        {
            RestaurantName = "Restaurant Delivery",
            PrimaryColor = "#3f51b5",
            AccentColor = "#ff4081",
            FooterAbout = "Fresh, delicious food delivered fast to your door.",
            Address = "123 Main Street, Your City",
            Phone = "555-0100",
            Email = "hello@restaurantdelivery.com"
        });

        await context.SaveChangesAsync();
    }

    // Gives the staff-creation Role picker at least one usable option on a fresh DB.
    // Existing seeded admin/captain accounts are untouched - they keep full access via
    // the CustomRoleId == null default (see AuthService.ResolveAdminModuleNamesAsync).
    private static async Task SeedRolesAsync(ApplicationDbContext context)
    {
        if (await context.CustomRoles.AnyAsync())
        {
            return;
        }

        context.CustomRoles.Add(new Role
        {
            Name = "Full Access",
            Modules = AdminModules.Orders | AdminModules.MenuItems | AdminModules.Settings | AdminModules.Staff | AdminModules.Customers
        });

        await context.SaveChangesAsync();
    }
}
