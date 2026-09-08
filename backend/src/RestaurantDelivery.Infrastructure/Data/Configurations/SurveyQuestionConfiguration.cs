using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class SurveyQuestionConfiguration : IEntityTypeConfiguration<SurveyQuestion>
{
    public void Configure(EntityTypeBuilder<SurveyQuestion> builder)
    {
        builder.Property(q => q.Text).HasMaxLength(500).IsRequired();
        builder.Property(q => q.Options).HasMaxLength(1000);
        builder.Property(q => q.IsActive).HasDefaultValue(true);
    }
}
