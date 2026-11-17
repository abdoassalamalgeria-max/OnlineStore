using BookStore.Domain.Catalog;
using BookStore.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Infrastructure.Persistence.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.Property(r => r.AuthorName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(Review.MaxCommentLength);

        // One review per customer per book; also serves "my review" lookups.
        builder.HasIndex(r => new { r.BookId, r.UserId }).IsUnique();
        // Newest-first listing per book.
        builder.HasIndex(r => new { r.BookId, r.CreatedAt });

        builder.HasOne<Book>().WithMany().HasForeignKey(r => r.BookId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint("ck_reviews_rating", $"rating BETWEEN {Review.MinRating} AND {Review.MaxRating}"));
    }
}
