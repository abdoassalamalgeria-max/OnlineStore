using BookStore.Application.Common;
using BookStore.Domain.Catalog;
using FluentValidation;

namespace BookStore.Application.Features.Admin;

// ---------------- Books ----------------

public sealed record AdminBookListItemDto(
    Guid Id,
    string Slug,
    string Title,
    string Language,
    int Price,
    int? SalePrice,
    int Stock,
    bool IsFeatured,
    string? CoverImageUrl,
    IReadOnlyList<string> Authors,
    DateTimeOffset UpdatedAt);

public sealed record AdminBookDto(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    string Isbn,
    int Price,
    int? SalePrice,
    int Stock,
    string? CoverImageUrl,
    int PageCount,
    string Language,
    DateOnly? PublishedOn,
    string Publisher,
    bool IsFeatured,
    IReadOnlyList<Guid> AuthorIds,
    IReadOnlyList<Guid> CategoryIds,
    /// <summary>True once the book appears in an order: it can then no longer be deleted.</summary>
    bool HasOrders);

public sealed record BookUpsertRequest(
    string Title,
    string Slug,
    string Description,
    string Isbn,
    int Price,
    int? SalePrice,
    int Stock,
    int PageCount,
    string Language,
    DateOnly? PublishedOn,
    string Publisher,
    bool IsFeatured,
    IReadOnlyList<Guid> AuthorIds,
    IReadOnlyList<Guid> CategoryIds);

public sealed record AdminBookQuery(string? Search, int Page = 1, int PageSize = 20, bool? LowStock = null);

public sealed class BookUpsertRequestValidator : AbstractValidator<BookUpsertRequest>
{
    public BookUpsertRequestValidator()
    {
        RuleFor(b => b.Title).NotEmpty().MaximumLength(300);
        RuleFor(b => b.Slug).NotEmpty().MaximumLength(200).Must(CatalogRules.IsValidSlug)
            .WithMessage("Slug: lowercase letters, digits and hyphens only (e.g. ce-que-le-jour).");
        RuleFor(b => b.Description).NotEmpty().MaximumLength(5000);
        RuleFor(b => b.Isbn).Must(CatalogRules.IsValidIsbn).WithMessage("Enter a valid ISBN-10 or ISBN-13.");
        RuleFor(b => b.Price).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(b => b.SalePrice).GreaterThan(0).LessThan(b => b.Price).When(b => b.SalePrice is not null)
            .WithMessage("The sale price must be positive and lower than the price.");
        RuleFor(b => b.Stock).InclusiveBetween(0, 100_000);
        RuleFor(b => b.PageCount).InclusiveBetween(1, 20_000);
        RuleFor(b => b.Language).Must(BookLanguages.IsValid)
            .WithMessage($"Language must be one of: {string.Join(", ", BookLanguages.All)}.");
        RuleFor(b => b.Publisher).NotEmpty().MaximumLength(200);
        RuleFor(b => b.AuthorIds).NotEmpty().WithMessage("Choose at least one author.");
        RuleFor(b => b.CategoryIds).NotEmpty().WithMessage("Choose at least one category.");
    }
}

// ---------------- Authors & categories ----------------

public sealed record AuthorUpsertRequest(string Name, string Slug, string? Bio);

public sealed record CategoryUpsertRequest(string NameAr, string NameFr, string NameEn, string Slug);

public sealed class AuthorUpsertRequestValidator : AbstractValidator<AuthorUpsertRequest>
{
    public AuthorUpsertRequestValidator()
    {
        RuleFor(a => a.Name).NotEmpty().MaximumLength(200);
        RuleFor(a => a.Slug).NotEmpty().MaximumLength(200).Must(CatalogRules.IsValidSlug)
            .WithMessage("Slug: lowercase letters, digits and hyphens only.");
        RuleFor(a => a.Bio).MaximumLength(2000);
    }
}

public sealed class CategoryUpsertRequestValidator : AbstractValidator<CategoryUpsertRequest>
{
    public CategoryUpsertRequestValidator()
    {
        RuleFor(c => c.NameAr).NotEmpty().MaximumLength(100);
        RuleFor(c => c.NameFr).NotEmpty().MaximumLength(100);
        RuleFor(c => c.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Slug).NotEmpty().MaximumLength(100).Must(CatalogRules.IsValidSlug)
            .WithMessage("Slug: lowercase letters, digits and hyphens only.");
    }
}

// ---------------- Wilayas & reviews ----------------

public sealed record WilayaUpdateRequest(int ShippingFee, bool IsDeliveryAvailable);

public sealed class WilayaUpdateRequestValidator : AbstractValidator<WilayaUpdateRequest>
{
    public WilayaUpdateRequestValidator() => RuleFor(w => w.ShippingFee).InclusiveBetween(0, 100_000);
}

public sealed record AdminReviewDto(
    Guid Id,
    Guid BookId,
    string BookSlug,
    string BookTitle,
    string AuthorName,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt);
