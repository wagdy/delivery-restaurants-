using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.Property(t => t.OtpHash).HasMaxLength(500).IsRequired();

        builder.HasIndex(t => t.AppUserId);

        // No independent value once its user's own row is gone - this app never hard
        // deletes an AppUser (see AppUser.IsDeleted), so Cascade here is mostly moot in
        // practice, just the sensible default for a purely derived, ephemeral row.
        builder.HasOne(t => t.AppUser)
            .WithMany()
            .HasForeignKey(t => t.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
