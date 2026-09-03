using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class LoyaltyPunchTransactionConfiguration : IEntityTypeConfiguration<LoyaltyPunchTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyPunchTransaction> builder)
    {
        builder.Property(t => t.TransactionType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.CheckReference).HasMaxLength(200);

        builder.HasIndex(t => t.CreatedAt);

        // Filtered unique index: only guards against the same order punching the same
        // campaign's progress twice (manual Scanner punches, where OrderId is null, are
        // unrestricted - a cashier can punch the same customer/campaign repeatedly).
        builder.HasIndex(t => new { t.OrderId, t.ProgressId })
            .IsUnique()
            .HasFilter("\"OrderId\" IS NOT NULL");

        builder.HasOne(t => t.Progress)
            .WithMany()
            .HasForeignKey(t => t.ProgressId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Admin)
            .WithMany()
            .HasForeignKey(t => t.AdminId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(t => t.Order)
            .WithMany()
            .HasForeignKey(t => t.OrderId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
