using BookStore.Domain.Catalog;
using FluentValidation;

namespace BookStore.Application.Features.Catalog;

/// <summary>Sort orders for the catalog. "Bestselling" arrives with orders in phase 5.</summary>
public enum BookSort
{
    Newest,
    PriceAsc,
    PriceDesc,
    Title,
}

/// <summary>Query-string filters for GET /api/v1/books. Every filter is optional and they combine with AND.</summary>
public sealed record BookQuery
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 48;

    /// <summary>Free text matched against the title and author names (case-insensitive, partial).</summary>
    public string? Search { get; init; }

    /// <summary>Category slug.</summary>
    public string? Category { get; init; }

    /// <summary>Author slug.</summary>
    public string? Author { get; init; }

    /// <summary>Book language code: ar, fr, en, ber.</summary>
    public string? Language { get; init; }

    /// <summary>Minimum effective price in DZD (inclusive).</summary>
    public int? MinPrice { get; init; }

    /// <summary>Maximum effective price in DZD (inclusive).</summary>
    public int? MaxPrice { get; init; }

    /// <summary>Only featured books when true.</summary>
    public bool? Featured { get; init; }

    /// <summary>Only books with stock when true.</summary>
    public bool? InStock { get; init; }

    public BookSort Sort { get; init; } = BookSort.Newest;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
}

public sealed class BookQueryValidator : AbstractValidator<BookQuery>
{
    public BookQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Category).MaximumLength(100);
        RuleFor(q => q.Author).MaximumLength(100);
        RuleFor(q => q.Language)
            .Must(BookLanguages.IsValid)
            .When(q => q.Language is not null)
            .WithMessage($"Language must be one of: {string.Join(", ", BookLanguages.All)}.");
        RuleFor(q => q.MinPrice).GreaterThanOrEqualTo(0);
        RuleFor(q => q.MaxPrice).GreaterThanOrEqualTo(0);
        RuleFor(q => q.MaxPrice)
            .GreaterThanOrEqualTo(q => q.MinPrice)
            .When(q => q.MinPrice is not null && q.MaxPrice is not null)
            .WithMessage("MaxPrice must be greater than or equal to MinPrice.");
        RuleFor(q => q.Sort).IsInEnum();
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, BookQuery.MaxPageSize);
    }
}
