using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class LoyaltyWalletPassRegistrationConfiguration : IEntityTypeConfiguration<LoyaltyWalletPassRegistration>
{
    public void Configure(EntityTypeBuilder<LoyaltyWalletPassRegistration> builder)
    {
        builder.Property(r => r.DeviceLibraryIdentifier).HasMaxLength(200).IsRequired();
        builder.Property(r => r.PushToken).HasMaxLength(500).IsRequired();
        builder.Property(r => r.PassTypeIdentifier).HasMaxLength(200).IsRequired();

        builder.HasIndex(r => new { r.DeviceLibraryIdentifier, r.PassTypeIdentifier, r.AppUserId }).IsUnique();

        builder.HasOne(r => r.AppUser)
            .WithMany()
            .HasForeignKey(r => r.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
