namespace BookStore.Application.Features.Catalog;

public sealed record AuthorRefDto(Guid Id, string Name, string Slug);

/// <summary>Category with its name in every UI language; the frontend picks the active locale.</summary>
public sealed record CategoryDto(Guid Id, string Slug, string NameAr, string NameFr, string NameEn, int BookCount);

public sealed record CategoryRefDto(Guid Id, string Slug, string NameAr, string NameFr, string NameEn);

public sealed record AuthorDto(Guid Id, string Name, string Slug, string? Bio, int BookCount);

/// <summary>Card-sized view of a book for lists and grids.</summary>
public sealed record BookSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    int Price,
    int? SalePrice,
    int EffectivePrice,
    bool InStock,
    string? CoverImageUrl,
    string Language,
    bool IsFeatured,
    double? AverageRating,
    int ReviewCount,
    IReadOnlyList<AuthorRefDto> Authors);

public sealed record BookDetailsDto(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    string Isbn,
    int Price,
    int? SalePrice,
    int EffectivePrice,
    int Stock,
    bool InStock,
    string? CoverImageUrl,
    int PageCount,
    string Language,
    DateOnly? PublishedOn,
    string Publisher,
    bool IsFeatured,
    double? AverageRating,
    int ReviewCount,
    IReadOnlyList<AuthorRefDto> Authors,
    IReadOnlyList<CategoryRefDto> Categories);

public sealed record WilayaDto(int Code, string NameAr, string NameFr, int ShippingFee, bool IsDeliveryAvailable);
