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
        builder.Property(q => q.IsDeleted).HasDefaultValue(false);

        // Restrict, not SetNull/Cascade - a section still assigned to a question must be
        // guarded against deletion at the application layer (see
        // SurveySectionService.DeleteAsync, mirroring CategoryService's own in-use guard)
        // rather than silently un-grouping questions or blocking on a raw DB exception.
        builder.HasOne(q => q.MatrixSection)
            .WithMany()
            .HasForeignKey(q => q.MatrixSectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
