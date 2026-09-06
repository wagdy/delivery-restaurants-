using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.Property(p => p.CodeText).HasMaxLength(50).IsRequired();
        builder.Property(p => p.DiscountValue).HasPrecision(5, 2);
        builder.Property(p => p.TargetIds).HasMaxLength(2000);

        builder.HasIndex(p => p.CodeText).IsUnique();
    }
}
