using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class RestaurantSettingsConfiguration : IEntityTypeConfiguration<RestaurantSettings>
{
    public void Configure(EntityTypeBuilder<RestaurantSettings> builder)
    {
        builder.Property(s => s.RestaurantName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.LogoUrl).HasMaxLength(2048);
        builder.Property(s => s.PrimaryColor).HasMaxLength(7).IsRequired();
        builder.Property(s => s.AccentColor).HasMaxLength(7).IsRequired();
        // Explicit HasDefaultValue so the SQL column default matches the C# property
        // default - without it, EF's ADD COLUMN migration would backfill the existing
        // singleton settings row with "" instead of a usable color.
        builder.Property(s => s.HeaderColor).HasMaxLength(7).IsRequired().HasDefaultValue("#3f51b5");
        builder.Property(s => s.BodyColor).HasMaxLength(7).IsRequired().HasDefaultValue("#fafafa");
        builder.Property(s => s.BackgroundImageUrl).HasMaxLength(2048);
        builder.Property(s => s.CenterLogoUrl).HasMaxLength(2048);
        builder.Property(s => s.Address).HasMaxLength(500);
        builder.Property(s => s.Phone).HasMaxLength(30);
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.Property(s => s.FooterAbout).HasMaxLength(1000);
    }
}
