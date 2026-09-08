using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Infrastructure.Data.Configurations;

public class OrderReviewConfiguration : IEntityTypeConfiguration<OrderReview>
{
    public void Configure(EntityTypeBuilder<OrderReview> builder)
    {
        // At most one review per order - ReviewService.SubmitReviewAsync checks this
        // up front for a friendly error, this is the hard backstop against a race.
        // OrderId is nullable for a general store-wide review (no order context); Postgres
        // (like every mainstream SQL engine) treats each NULL as distinct for uniqueness
        // purposes, so any number of store-wide reviews can coexist under this same index.
        builder.HasIndex(r => r.OrderId).IsUnique();

        // Deleting an order takes its review with it, same as it already does its
        // OrderItems (see OrderConfiguration) - a review pointing at a deleted order
        // wouldn't mean anything. Optional: a store-wide review has no order at all.
        builder.HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        // Guest reviews keep CustomerId null; deleting a customer account shouldn't erase
        // their review history, same SetNull convention as Order.UserId.
        builder.HasOne(r => r.Customer)
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(r => r.Answers)
            .WithOne(a => a.OrderReview)
            .HasForeignKey(a => a.OrderReviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
