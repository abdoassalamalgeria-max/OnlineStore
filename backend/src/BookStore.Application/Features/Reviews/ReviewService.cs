using BookStore.Application.Abstractions;
using BookStore.Application.Common;
using BookStore.Domain.Catalog;
using BookStore.Domain.Ordering;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Application.Features.Reviews;

public sealed record ReviewDto(Guid Id, string AuthorName, int Rating, string? Comment, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>Average and per-star counts; Distribution[0] is the number of 1-star reviews.</summary>
public sealed record RatingSummaryDto(double? Average, int Count, IReadOnlyList<int> Distribution);

public sealed record BookReviewsDto(RatingSummaryDto Summary, PagedResult<ReviewDto> Reviews);

/// <summary>What the signed-in user may do on a book page.</summary>
public sealed record MyReviewDto(bool CanReview, ReviewDto? Review);

public sealed record UpsertReviewRequest(int Rating, string? Comment);

public sealed class UpsertReviewRequestValidator : AbstractValidator<UpsertReviewRequest>
{
    public UpsertReviewRequestValidator()
    {
        RuleFor(r => r.Rating).InclusiveBetween(Review.MinRating, Review.MaxRating);
        RuleFor(r => r.Comment).MaximumLength(Review.MaxCommentLength);
    }
}

public interface IReviewService
{
    Task<BookReviewsDto> GetForBookAsync(string slug, int page, int pageSize, CancellationToken cancellationToken);
    Task<MyReviewDto> GetMineAsync(Guid userId, string slug, CancellationToken cancellationToken);
    Task<ReviewDto> UpsertMineAsync(Guid userId, string authorName, string slug, UpsertReviewRequest request, CancellationToken cancellationToken);
    Task DeleteMineAsync(Guid userId, string slug, CancellationToken cancellationToken);
}

public sealed class ReviewService(IAppDbContext db, IValidator<UpsertReviewRequest> validator) : IReviewService
{
    public const int MaxPageSize = 50;

    /// <summary>Orders in these states prove a purchase (Pending/Failed/Cancelled do not).</summary>
    private static readonly OrderStatus[] PurchasedStatuses = [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered];

    public async Task<BookReviewsDto> GetForBookAsync(string slug, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var bookId = await BookIdAsync(slug, cancellationToken);
        var reviews = db.Reviews.AsNoTracking().Where(r => r.BookId == bookId);

        var counts = await reviews
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var distribution = Enumerable.Range(Review.MinRating, Review.MaxRating)
            .Select(star => counts.FirstOrDefault(c => c.Rating == star)?.Count ?? 0)
            .ToList();
        var total = distribution.Sum();
        double? average = total == 0
            ? null
            : Math.Round(counts.Sum(c => c.Rating * c.Count) / (double)total, 1);

        var items = await reviews
            .OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewDto(r.Id, r.AuthorName, r.Rating, r.Comment, r.CreatedAt, r.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new BookReviewsDto(new RatingSummaryDto(average, total, distribution), new PagedResult<ReviewDto>(items, page, pageSize, total));
    }

    public async Task<MyReviewDto> GetMineAsync(Guid userId, string slug, CancellationToken cancellationToken)
    {
        var bookId = await BookIdAsync(slug, cancellationToken);
        var review = await db.Reviews.AsNoTracking()
            .Where(r => r.BookId == bookId && r.UserId == userId)
            .Select(r => new ReviewDto(r.Id, r.AuthorName, r.Rating, r.Comment, r.CreatedAt, r.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
        return new MyReviewDto(await HasPurchasedAsync(userId, bookId, cancellationToken), review);
    }

    public async Task<ReviewDto> UpsertMineAsync(Guid userId, string authorName, string slug, UpsertReviewRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var bookId = await BookIdAsync(slug, cancellationToken);
        if (!await HasPurchasedAsync(userId, bookId, cancellationToken))
        {
            throw new ForbiddenException("Only customers who bought this book can review it.");
        }

        var review = await db.Reviews.FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId, cancellationToken);
        if (review is null)
        {
            review = new Review(bookId, userId, authorName, request.Rating, request.Comment);
            db.Reviews.Add(review);
        }
        else
        {
            review.Update(request.Rating, request.Comment);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (db.Reviews.Local.Any(r => r == review))
        {
            // The same user submitted twice at once; the unique (book, user) index kept only one.
            throw new ConflictException("A review was just saved for this book. Reload and edit it.");
        }

        return new ReviewDto(review.Id, review.AuthorName, review.Rating, review.Comment, review.CreatedAt, review.UpdatedAt);
    }

    public async Task DeleteMineAsync(Guid userId, string slug, CancellationToken cancellationToken)
    {
        var bookId = await BookIdAsync(slug, cancellationToken);
        await db.Reviews.Where(r => r.BookId == bookId && r.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<Guid> BookIdAsync(string slug, CancellationToken cancellationToken)
    {
        var id = await db.Books.Where(b => b.Slug == slug).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(cancellationToken);
        return id ?? throw new NotFoundException("Book", slug);
    }

    private Task<bool> HasPurchasedAsync(Guid userId, Guid bookId, CancellationToken cancellationToken) =>
        db.Orders.AnyAsync(o =>
            o.UserId == userId &&
            PurchasedStatuses.Contains(o.Status) &&
            o.Items.Any(i => i.BookId == bookId), cancellationToken);
}
