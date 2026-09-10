using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class RestaurantSettingsConfiguration : IEntityTypeConfiguration<RestaurantSettings>
{
    public void Configure(EntityTypeBuilder<RestaurantSettings> builder)
    {
        builder.Property(s => s.RestaurantName).HasMaxLength(200);
        builder.Property(s => s.LogoUrl).HasMaxLength(2048);
        builder.Property(s => s.PrimaryColor).HasMaxLength(7).IsRequired();
        builder.Property(s => s.AccentColor).HasMaxLength(7).IsRequired();
        // Explicit HasDefaultValue so the SQL column default matches the C# property
        // default - without it, EF's ADD COLUMN migration would backfill the existing
        // singleton settings row with "" instead of a usable color.
        builder.Property(s => s.HeaderColor).HasMaxLength(7).IsRequired().HasDefaultValue("#3f51b5");
        builder.Property(s => s.BodyColor).HasMaxLength(7).IsRequired().HasDefaultValue("#fafafa");
        builder.Property(s => s.IconColor).HasMaxLength(7).IsRequired().HasDefaultValue("#ffffff");
        builder.Property(s => s.BackgroundImageUrl).HasMaxLength(2048);
        builder.Property(s => s.CenterLogoUrl).HasMaxLength(2048);
        builder.Property(s => s.Address).HasMaxLength(500);
        builder.Property(s => s.Phone).HasMaxLength(30);
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.Property(s => s.FooterAbout).HasMaxLength(1000);
        builder.Property(s => s.FaviconUrl).HasMaxLength(2048);
        builder.Property(s => s.TabTitle).HasMaxLength(100);

        builder.Property(s => s.TaxPercentage).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(s => s.IsCashEnabled).HasDefaultValue(true);
        builder.Property(s => s.IsVisaEnabled).HasDefaultValue(false);
        builder.Property(s => s.VisaFawryUrl).HasMaxLength(2048);
        builder.Property(s => s.IsInstapayEnabled).HasDefaultValue(false);
        builder.Property(s => s.InstapayAccount).HasMaxLength(200);

        // Defaulted to the frontend's own previous hardcoded constant (CartService's old
        // DELIVERY_FEE = 4.99) so the migration that adds this column doesn't silently
        // make every delivery free until an admin notices and sets a real value.
        builder.Property(s => s.BaseDeliveryFee).HasPrecision(10, 2).HasDefaultValue(4.99m);
        builder.Property(s => s.ManagerWhatsApp1).HasMaxLength(30);
        builder.Property(s => s.ManagerWhatsApp2).HasMaxLength(30);
        builder.Property(s => s.ManagerWhatsApp3).HasMaxLength(30);
    }
}
