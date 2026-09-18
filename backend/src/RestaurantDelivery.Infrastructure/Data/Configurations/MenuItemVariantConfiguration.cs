using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class MenuItemVariantConfiguration : IEntityTypeConfiguration<MenuItemVariant>
{
    public void Configure(EntityTypeBuilder<MenuItemVariant> builder)
    {
        builder.Property(v => v.Name).IsRequired().HasMaxLength(100);
        builder.Property(v => v.NameAr).HasMaxLength(100);
        builder.Property(v => v.Price).HasColumnType("decimal(10,2)");

        // A variant has no meaning apart from its item, and the order history does not
        // reference it (OrderItem snapshots the name and price instead - see
        // OrderItem.VariantName), so cascading it away with the item is safe. That
        // snapshot is exactly what makes this cascade safe rather than destructive.
        builder.HasOne(v => v.MenuItem)
            .WithMany(m => m.Variants)
            .HasForeignKey(v => v.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.MenuItemId);
    }
}
