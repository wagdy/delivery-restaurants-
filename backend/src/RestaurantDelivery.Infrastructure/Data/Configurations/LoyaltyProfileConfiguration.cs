using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class LoyaltyProfileConfiguration : IEntityTypeConfiguration<LoyaltyProfile>
{
    public void Configure(EntityTypeBuilder<LoyaltyProfile> builder)
    {
        builder.HasKey(p => p.AppUserId);

        // Plain string now (was an enum with HasConversion<string>()) - see
        // LoyaltyProfile.MembershipTier's own comment for why this is a snapshot, not a
        // foreign key. 100 matches LoyaltyTier.Name's own max length.
        builder.Property(p => p.MembershipTier).HasMaxLength(100);

        builder.Property(p => p.ReferralCode).HasMaxLength(8).IsRequired();
        builder.HasIndex(p => p.ReferralCode).IsUnique();

        // Unidirectional: AppUser gets no reverse LoyaltyProfile navigation, matching how
        // AppUser.CustomRole is wired - keeps AppUser.cs untouched by this feature.
        builder.HasOne(p => p.AppUser)
            .WithMany()
            .HasForeignKey(p => p.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
