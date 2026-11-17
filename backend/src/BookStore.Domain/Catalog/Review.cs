using BookStore.Domain.Common;

namespace BookStore.Domain.Catalog;

/// <summary>
/// A customer's rating (1–5) and optional comment on a book. One per customer per book (edited in place).
/// Only customers who bought the book may write one; that rule is enforced by the review service.
/// </summary>
public sealed class Review : AuditableEntity
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxCommentLength = 2000;

    private Review()
    {
        AuthorName = string.Empty;
    }

    public Review(Guid bookId, Guid userId, string authorName, int rating, string? comment)
    {
        BookId = bookId;
        UserId = userId;
        AuthorName = authorName;
        Update(rating, comment);
    }

    public Guid BookId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Display name at the time of writing (keeps reviews readable without joining users).</summary>
    public string AuthorName { get; private set; }

    public int Rating { get; private set; }
    public string? Comment { get; private set; }

    public void Update(int rating, string? comment)
    {
        if (rating is < MinRating or > MaxRating)
        {
            throw new DomainException($"Rating must be between {MinRating} and {MaxRating}.");
        }

        var trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (trimmed?.Length > MaxCommentLength)
        {
            throw new DomainException($"Comment must be at most {MaxCommentLength} characters.");
        }

        Rating = rating;
        Comment = trimmed;
    }
}
