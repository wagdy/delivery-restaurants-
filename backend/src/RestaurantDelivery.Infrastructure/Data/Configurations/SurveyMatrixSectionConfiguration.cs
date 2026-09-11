using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class SurveyMatrixSectionConfiguration : IEntityTypeConfiguration<SurveyMatrixSection>
{
    public void Configure(EntityTypeBuilder<SurveyMatrixSection> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.Name).IsUnique();
    }
}
