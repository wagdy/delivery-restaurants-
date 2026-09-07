using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.CustomerPhone)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.DeliveryAddress)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(o => o.TotalAmount)
            .HasPrecision(10, 2);

        builder.Property(o => o.Notes).HasMaxLength(1000);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.PromoCodeText).HasMaxLength(50);
        builder.Property(o => o.DiscountAmount).HasPrecision(10, 2);
        builder.Property(o => o.TaxAmount).HasPrecision(10, 2);
        builder.Property(o => o.DeliveryFee).HasPrecision(10, 2);

        builder.Property(o => o.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(Core.Enums.PaymentMethod.Cash);

        builder.Property(o => o.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(Core.Enums.PaymentStatus.Confirmed);

        builder.Property(o => o.ExternalSource).HasMaxLength(50);
        builder.Property(o => o.ExternalOrderId).HasMaxLength(100);

        builder.Property(o => o.IsAcknowledged).HasDefaultValue(false);

        // Postgres treats every NULL as distinct in a unique index, so orders placed
        // normally (both columns null) never collide with each other here - only an
        // actual repeat (same source + same external id) is rejected.
        builder.HasIndex(o => new { o.ExternalSource, o.ExternalOrderId }).IsUnique();

        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CreatedAt);

        // Guest orders keep UserId null; deleting a user shouldn't delete their order history.
        builder.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(o => o.OrderItems)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
