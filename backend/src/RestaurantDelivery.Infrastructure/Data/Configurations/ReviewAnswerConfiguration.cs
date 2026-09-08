using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class ReviewAnswerConfiguration : IEntityTypeConfiguration<ReviewAnswer>
{
    public void Configure(EntityTypeBuilder<ReviewAnswer> builder)
    {
        builder.Property(a => a.AnswerValue).HasMaxLength(2000).IsRequired();

        // A customer can only answer a given question once per review.
        builder.HasIndex(a => new { a.OrderReviewId, a.SurveyQuestionId }).IsUnique();

        // A question that already has real customer answers can't be deleted (see
        // ReviewService.DeleteQuestionAsync's own friendly-error check) - Restrict here is
        // the hard backstop, same convention as LoyaltyCampaignProgress -> Customer.
        builder.HasOne(a => a.SurveyQuestion)
            .WithMany(q => q.Answers)
            .HasForeignKey(a => a.SurveyQuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
