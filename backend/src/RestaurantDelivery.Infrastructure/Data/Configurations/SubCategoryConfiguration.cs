using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class SubCategoryConfiguration : IEntityTypeConfiguration<SubCategory>
{
    public void Configure(EntityTypeBuilder<SubCategory> builder)
    {
        builder.Property(sc => sc.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(sc => new { sc.CategoryId, sc.Name }).IsUnique();

        // A category delete is already blocked (see CategoryService.DeleteAsync) while
        // any menu item still references it, so by the time a category can be deleted
        // its sub-categories are necessarily empty too - cascading here just removes
        // those now-empty rows along with it instead of leaving them orphaned.
        builder.HasOne(sc => sc.Category)
            .WithMany()
            .HasForeignKey(sc => sc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
