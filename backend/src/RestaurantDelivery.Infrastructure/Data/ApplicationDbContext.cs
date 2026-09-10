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
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<AddOn> AddOns => Set<AddOn>();
    public DbSet<WebPushSubscription> WebPushSubscriptions => Set<WebPushSubscription>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();

    public DbSet<LoyaltyProfile> LoyaltyProfiles => Set<LoyaltyProfile>();
    public DbSet<LoyaltyPointTransaction> LoyaltyPointTransactions => Set<LoyaltyPointTransaction>();
    public DbSet<LoyaltyWalletPassRegistration> LoyaltyWalletPassRegistrations => Set<LoyaltyWalletPassRegistration>();
    public DbSet<LoyaltyCampaign> LoyaltyCampaigns => Set<LoyaltyCampaign>();
    public DbSet<LoyaltyCampaignProgress> LoyaltyCampaignProgress => Set<LoyaltyCampaignProgress>();
    public DbSet<LoyaltyPunchTransaction> LoyaltyPunchTransactions => Set<LoyaltyPunchTransaction>();
    public DbSet<LoyaltySettings> LoyaltySettings => Set<LoyaltySettings>();
    public DbSet<LoyaltyTier> LoyaltyTiers => Set<LoyaltyTier>();

    // Named CustomRoles, not Roles - IdentityDbContext<AppUser> already inherits
    // DbSet<IdentityRole> Roles (mapped to the unused AspNetRoles table); reusing that
    // name here would silently hide the inherited member.
    public DbSet<Role> CustomRoles => Set<Role>();

    public DbSet<SurveyQuestion> SurveyQuestions => Set<SurveyQuestion>();
    public DbSet<OrderReview> OrderReviews => Set<OrderReview>();
    public DbSet<ReviewAnswer> ReviewAnswers => Set<ReviewAnswer>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
