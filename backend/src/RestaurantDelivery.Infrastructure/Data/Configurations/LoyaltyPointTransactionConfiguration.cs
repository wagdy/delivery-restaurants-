using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class LoyaltyPointTransactionConfiguration : IEntityTypeConfiguration<LoyaltyPointTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyPointTransaction> builder)
    {
        builder.Property(t => t.CheckAmount).HasPrecision(10, 2);

        builder.Property(t => t.TransactionType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.CheckReference).HasMaxLength(200);

        builder.HasIndex(t => t.CustomerId);
        builder.HasIndex(t => t.CreatedAt);

        // Two independent FKs to AppUser (customer being credited/debited, and the staff
        // member who acted) - both WithMany() with no lambda, so neither adds a reverse
        // collection navigation onto AppUser.cs.
        builder.HasOne(t => t.Customer)
            .WithMany()
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Admin)
            .WithMany()
            .HasForeignKey(t => t.AdminId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
