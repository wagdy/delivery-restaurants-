using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class MenuItemAddOnConfiguration : IEntityTypeConfiguration<MenuItemAddOn>
{
    public void Configure(EntityTypeBuilder<MenuItemAddOn> builder)
    {
        builder.HasKey(ma => new { ma.MenuItemId, ma.AddOnId });

        // Assignment rows are lightweight admin configuration, not transactional
        // history, so it's safe to cascade them away if either side is deleted.
        builder.HasOne(ma => ma.MenuItem)
            .WithMany(m => m.MenuItemAddOns)
            .HasForeignKey(ma => ma.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ma => ma.AddOn)
            .WithMany(a => a.MenuItemAddOns)
            .HasForeignKey(ma => ma.AddOnId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
