using BookStore.Application.Abstractions;
using BookStore.Application.Common;
using BookStore.Domain.Catalog;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Application.Features.Catalog;

public interface ICatalogService
{
    Task<PagedResult<BookSummaryDto>> SearchBooksAsync(BookQuery query, CancellationToken cancellationToken);
    Task<BookDetailsDto> GetBookBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AuthorDto>> GetAuthorsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<WilayaDto>> GetWilayasAsync(CancellationToken cancellationToken);
}

/// <summary>Read-only catalog queries. Every query is AsNoTracking and projects directly to DTOs.</summary>
public sealed class CatalogService(
    IAppDbContext db,
    IBookTextSearch textSearch,
    IValidator<BookQuery> validator) : ICatalogService
{
    public async Task<PagedResult<BookSummaryDto>> SearchBooksAsync(BookQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);

        var books = ApplyFilters(db.Books.AsNoTracking(), query);
        var total = await books.CountAsync(cancellationToken);

        var items = await ApplySort(books, query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(b => new BookSummaryDto(
                b.Id,
                b.Slug,
                b.Title,
                b.Price,
                b.SalePrice,
                b.EffectivePrice,
                b.Stock > 0,
                b.CoverImageUrl,
                b.Language,
                b.IsFeatured,
                db.Reviews.Where(r => r.BookId == b.Id).Average(r => (double?)r.Rating),
                db.Reviews.Count(r => r.BookId == b.Id),
                b.Authors.OrderBy(a => a.Name).Select(a => new AuthorRefDto(a.Id, a.Name, a.Slug)).ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<BookSummaryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<BookDetailsDto> GetBookBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var book = await db.Books.AsNoTracking()
            .Where(b => b.Slug == slug)
            .Select(b => new BookDetailsDto(
                b.Id,
                b.Slug,
                b.Title,
                b.Description,
                b.Isbn,
                b.Price,
                b.SalePrice,
                b.EffectivePrice,
                b.Stock,
                b.Stock > 0,
                b.CoverImageUrl,
                b.PageCount,
                b.Language,
                b.PublishedOn,
                b.Publisher,
                b.IsFeatured,
                db.Reviews.Where(r => r.BookId == b.Id).Average(r => (double?)r.Rating),
                db.Reviews.Count(r => r.BookId == b.Id),
                b.Authors.OrderBy(a => a.Name).Select(a => new AuthorRefDto(a.Id, a.Name, a.Slug)).ToList(),
                b.Categories.OrderBy(c => c.Slug)
                    .Select(c => new CategoryRefDto(c.Id, c.Slug, c.NameAr, c.NameFr, c.NameEn))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return book ?? throw new NotFoundException("Book", slug);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking()
            .OrderBy(c => c.Slug)
            .Select(c => new CategoryDto(c.Id, c.Slug, c.NameAr, c.NameFr, c.NameEn, c.Books.Count))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AuthorDto>> GetAuthorsAsync(CancellationToken cancellationToken) =>
        await db.Authors.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AuthorDto(a.Id, a.Name, a.Slug, a.Bio, a.Books.Count))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WilayaDto>> GetWilayasAsync(CancellationToken cancellationToken) =>
        await db.Wilayas.AsNoTracking()
            .OrderBy(w => w.Code)
            .Select(w => new WilayaDto(w.Code, w.NameAr, w.NameFr, w.ShippingFee, w.IsDeliveryAvailable))
            .ToListAsync(cancellationToken);

    private IQueryable<Book> ApplyFilters(IQueryable<Book> books, BookQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            books = textSearch.Matching(books, query.Search);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            books = books.Where(b => b.Categories.Any(c => c.Slug == query.Category));
        }

        if (!string.IsNullOrWhiteSpace(query.Author))
        {
            books = books.Where(b => b.Authors.Any(a => a.Slug == query.Author));
        }

        if (query.Language is not null)
        {
            books = books.Where(b => b.Language == query.Language);
        }

        if (query.MinPrice is not null)
        {
            books = books.Where(b => b.EffectivePrice >= query.MinPrice);
        }

        if (query.MaxPrice is not null)
        {
            books = books.Where(b => b.EffectivePrice <= query.MaxPrice);
        }

        if (query.Featured is true)
        {
            books = books.Where(b => b.IsFeatured);
        }

        if (query.InStock is true)
        {
            books = books.Where(b => b.Stock > 0);
        }

        return books;
    }

    // Id is the final tie-breaker so pages never overlap or skip rows when sort keys are equal.
    private static IQueryable<Book> ApplySort(IQueryable<Book> books, BookSort sort) => sort switch
    {
        BookSort.PriceAsc => books.OrderBy(b => b.EffectivePrice).ThenBy(b => b.Id),
        BookSort.PriceDesc => books.OrderByDescending(b => b.EffectivePrice).ThenBy(b => b.Id),
        BookSort.Title => books.OrderBy(b => b.Title).ThenBy(b => b.Id),
        _ => books.OrderByDescending(b => b.PublishedOn).ThenByDescending(b => b.CreatedAt).ThenBy(b => b.Id),
    };
}
