using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.Property(m => m.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(m => m.Description)
            .HasMaxLength(1000);

        builder.Property(m => m.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.ImageUrl)
            .HasMaxLength(2048);

        builder.Property(m => m.Price)
            .HasPrecision(10, 2);

        builder.HasIndex(m => m.Category);

        // SubCategoryService.DeleteAsync already blocks deleting a sub-category that
        // still has menu items, so SetNull here is a defensive fallback (e.g. the
        // cascading category delete above), not the normal path.
        builder.HasOne(m => m.SubCategory)
            .WithMany(sc => sc.MenuItems)
            .HasForeignKey(m => m.SubCategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
