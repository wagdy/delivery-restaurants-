using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class LoyaltySettingsConfiguration : IEntityTypeConfiguration<LoyaltySettings>
{
    public void Configure(EntityTypeBuilder<LoyaltySettings> builder)
    {
        // Explicit HasDefaultValue so the SQL column default matches the C# property
        // default - without it, EF's ADD COLUMN migration would backfill the singleton
        // settings row with 0 instead of the ratios this app already behaves with today.
        builder.Property(s => s.PointsPerCurrencyUnit).HasPrecision(10, 4).HasDefaultValue(0.1m);
        builder.Property(s => s.RedemptionValuePer100Points).HasPrecision(10, 2).HasDefaultValue(10m);
    }
}
