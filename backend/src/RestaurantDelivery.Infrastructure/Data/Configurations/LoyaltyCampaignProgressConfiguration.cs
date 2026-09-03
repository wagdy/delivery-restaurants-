using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class LoyaltyCampaignProgressConfiguration : IEntityTypeConfiguration<LoyaltyCampaignProgress>
{
    public void Configure(EntityTypeBuilder<LoyaltyCampaignProgress> builder)
    {
        builder.HasIndex(p => new { p.CustomerId, p.CampaignId }).IsUnique();

        builder.HasOne(p => p.Customer)
            .WithMany()
            .HasForeignKey(p => p.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Campaign)
            .WithMany()
            .HasForeignKey(p => p.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
