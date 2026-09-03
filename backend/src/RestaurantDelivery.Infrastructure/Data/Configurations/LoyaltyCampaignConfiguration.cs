using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class LoyaltyCampaignConfiguration : IEntityTypeConfiguration<LoyaltyCampaign>
{
    public void Configure(EntityTypeBuilder<LoyaltyCampaign> builder)
    {
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(1000).IsRequired();
        builder.Property(c => c.CategoryName).HasMaxLength(200);

        builder.HasIndex(c => c.IsActive);
    }
}
