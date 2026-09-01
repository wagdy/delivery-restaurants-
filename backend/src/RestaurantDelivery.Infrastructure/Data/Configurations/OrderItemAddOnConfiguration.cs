using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class OrderItemAddOnConfiguration : IEntityTypeConfiguration<OrderItemAddOn>
{
    public void Configure(EntityTypeBuilder<OrderItemAddOn> builder)
    {
        builder.Property(oa => oa.Name).HasMaxLength(100).IsRequired();
        builder.Property(oa => oa.Price).HasPrecision(10, 2);

        builder.HasOne(oa => oa.OrderItem)
            .WithMany(oi => oi.AddOns)
            .HasForeignKey(oa => oa.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Real order history — block deleting an add-on that's actually been ordered,
        // matching how MenuItem/Category deletion is blocked when referenced by orders.
        builder.HasOne(oa => oa.AddOn)
            .WithMany()
            .HasForeignKey(oa => oa.AddOnId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
