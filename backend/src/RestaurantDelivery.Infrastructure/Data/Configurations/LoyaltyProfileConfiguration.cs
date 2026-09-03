using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class LoyaltyProfileConfiguration : IEntityTypeConfiguration<LoyaltyProfile>
{
    public void Configure(EntityTypeBuilder<LoyaltyProfile> builder)
    {
        builder.HasKey(p => p.AppUserId);

        builder.Property(p => p.MembershipTier)
            .HasConversion<string>()
            .HasMaxLength(20);

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
