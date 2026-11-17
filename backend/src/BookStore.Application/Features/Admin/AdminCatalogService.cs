using BookStore.Application.Abstractions;
using BookStore.Application.Common;
using BookStore.Application.Features.Catalog;
using BookStore.Domain.Catalog;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Application.Features.Admin;

public interface IAdminCatalogService
{
    Task<PagedResult<AdminBookListItemDto>> ListBooksAsync(AdminBookQuery query, CancellationToken cancellationToken);
    Task<AdminBookDto> GetBookAsync(Guid id, CancellationToken cancellationToken);
    Task<AdminBookDto> CreateBookAsync(BookUpsertRequest request, CancellationToken cancellationToken);
    Task<AdminBookDto> UpdateBookAsync(Guid id, BookUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteBookAsync(Guid id, CancellationToken cancellationToken);
    Task<AdminBookDto> SetCoverAsync(Guid id, Stream image, CancellationToken cancellationToken);
    Task<AdminBookDto> RemoveCoverAsync(Guid id, CancellationToken cancellationToken);

    Task<AuthorDto> CreateAuthorAsync(AuthorUpsertRequest request, CancellationToken cancellationToken);
    Task<AuthorDto> UpdateAuthorAsync(Guid id, AuthorUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteAuthorAsync(Guid id, CancellationToken cancellationToken);

    Task<CategoryDto> CreateCategoryAsync(CategoryUpsertRequest request, CancellationToken cancellationToken);
    Task<CategoryDto> UpdateCategoryAsync(Guid id, CategoryUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken);

    Task<WilayaDto> UpdateWilayaAsync(int code, WilayaUpdateRequest request, CancellationToken cancellationToken);

    Task<PagedResult<AdminReviewDto>> ListReviewsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task DeleteReviewAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Catalog administration (Admin role only, enforced by the Api).</summary>
public sealed class AdminCatalogService(
    IAppDbContext db,
    IFileStorage storage,
    IImageProcessor images,
    IBookTextSearch textSearch,
    IValidator<BookUpsertRequest> bookValidator,
    IValidator<AuthorUpsertRequest> authorValidator,
    IValidator<CategoryUpsertRequest> categoryValidator,
    IValidator<WilayaUpdateRequest> wilayaValidator) : IAdminCatalogService
{
    // ---------------- Books ----------------

    public async Task<PagedResult<AdminBookListItemDto>> ListBooksAsync(AdminBookQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var books = db.Books.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var isbn = CatalogRules.NormalizeIsbn(term);
            // An ISBN (with or without hyphens) is looked up exactly; anything else is a title/author search.
            books = isbn.Length is 10 or 13 && isbn.All(c => char.IsAsciiDigit(c) || c == 'X')
                ? books.Where(b => b.Isbn == isbn)
                : textSearch.Matching(books, term);
        }

        if (query.LowStock is true)
        {
            books = books.Where(b => b.Stock <= AdminOverviewService.LowStockThreshold);
        }

        var total = await books.CountAsync(cancellationToken);
        var items = await books
            .OrderByDescending(b => b.UpdatedAt).ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new AdminBookListItemDto(
                b.Id, b.Slug, b.Title, b.Language, b.Price, b.SalePrice, b.Stock, b.IsFeatured, b.CoverImageUrl,
                b.Authors.OrderBy(a => a.Name).Select(a => a.Name).ToList(), b.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminBookListItemDto>(items, page, pageSize, total);
    }

    public async Task<AdminBookDto> GetBookAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await db.Books.AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new AdminBookDto(
                b.Id, b.Slug, b.Title, b.Description, b.Isbn, b.Price, b.SalePrice, b.Stock, b.CoverImageUrl,
                b.PageCount, b.Language, b.PublishedOn, b.Publisher, b.IsFeatured,
                b.Authors.Select(a => a.Id).ToList(),
                b.Categories.Select(c => c.Id).ToList(),
                db.Orders.Any(o => o.Items.Any(i => i.BookId == b.Id))))
            .FirstOrDefaultAsync(cancellationToken);
        return book ?? throw new NotFoundException("Book", id);
    }

    public async Task<AdminBookDto> CreateBookAsync(BookUpsertRequest request, CancellationToken cancellationToken)
    {
        await bookValidator.ValidateAndThrowAsync(request, cancellationToken);
        var isbn = CatalogRules.NormalizeIsbn(request.Isbn);
        await EnsureBookUniqueAsync(request.Slug, isbn, exceptId: null, cancellationToken);
        var (authors, categories) = await LoadRelationsAsync(request, cancellationToken);

        var book = new Book(request.Title.Trim(), request.Slug, request.Description.Trim(), isbn,
            request.Price, request.Stock, request.PageCount, request.Language, request.PublishedOn, request.Publisher.Trim());
        book.SetPrice(request.Price, request.SalePrice);
        book.SetFeatured(request.IsFeatured);
        book.SetAuthors(authors);
        book.SetCategories(categories);
        db.Books.Add(book);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBookAsync(book.Id, cancellationToken);
    }

    public async Task<AdminBookDto> UpdateBookAsync(Guid id, BookUpsertRequest request, CancellationToken cancellationToken)
    {
        await bookValidator.ValidateAndThrowAsync(request, cancellationToken);
        var isbn = CatalogRules.NormalizeIsbn(request.Isbn);
        await EnsureBookUniqueAsync(request.Slug, isbn, exceptId: id, cancellationToken);
        var book = await TrackedBookAsync(id, cancellationToken);
        var (authors, categories) = await LoadRelationsAsync(request, cancellationToken);

        book.UpdateDetails(request.Title.Trim(), request.Slug, request.Description.Trim(), isbn,
            request.PageCount, request.Language, request.PublishedOn, request.Publisher.Trim());
        book.SetPrice(request.Price, request.SalePrice);
        // Stock is set absolutely; reservations of pending orders were already taken out of it.
        book.SetStock(request.Stock);
        book.SetFeatured(request.IsFeatured);
        book.SetAuthors(authors);
        book.SetCategories(categories);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBookAsync(id, cancellationToken);
    }

    public async Task DeleteBookAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await TrackedBookAsync(id, cancellationToken);
        if (await db.Orders.AnyAsync(o => o.Items.Any(i => i.BookId == id), cancellationToken))
        {
            throw new ConflictException("This book appears in orders and cannot be deleted. Set its stock to 0 to stop selling it.");
        }

        var cover = book.CoverImageUrl;
        db.Books.Remove(book); // cascades: join rows, cart lines, reviews
        await db.SaveChangesAsync(cancellationToken);
        if (cover is not null)
        {
            await storage.DeleteAsync(cover, cancellationToken);
        }
    }

    public async Task<AdminBookDto> SetCoverAsync(Guid id, Stream image, CancellationToken cancellationToken)
    {
        var book = await TrackedBookAsync(id, cancellationToken);
        var cover = images.ToCover(image);

        // A new file name per upload so CDNs and browsers never serve a stale cover.
        var path = $"covers/{book.Slug}-{Guid.NewGuid().ToString("N")[..8]}.{cover.Extension}";
        using var content = new MemoryStream(cover.Data, writable: false);
        var url = await storage.SaveAsync(path, content, cover.ContentType, cancellationToken);

        var previous = book.CoverImageUrl;
        book.SetCoverImage(url);
        await db.SaveChangesAsync(cancellationToken);
        if (previous is not null)
        {
            await storage.DeleteAsync(previous, cancellationToken);
        }

        return await GetBookAsync(id, cancellationToken);
    }

    public async Task<AdminBookDto> RemoveCoverAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await TrackedBookAsync(id, cancellationToken);
        var previous = book.CoverImageUrl;
        book.SetCoverImage(null);
        await db.SaveChangesAsync(cancellationToken);
        if (previous is not null)
        {
            await storage.DeleteAsync(previous, cancellationToken);
        }

        return await GetBookAsync(id, cancellationToken);
    }

    // ---------------- Authors ----------------

    public async Task<AuthorDto> CreateAuthorAsync(AuthorUpsertRequest request, CancellationToken cancellationToken)
    {
        await authorValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (await db.Authors.AnyAsync(a => a.Slug == request.Slug, cancellationToken))
        {
            throw Invalid(nameof(request.Slug), "This slug is already used.");
        }

        var author = new Author(request.Name.Trim(), request.Slug, Clean(request.Bio));
        db.Authors.Add(author);
        await db.SaveChangesAsync(cancellationToken);
        return new AuthorDto(author.Id, author.Name, author.Slug, author.Bio, 0);
    }

    public async Task<AuthorDto> UpdateAuthorAsync(Guid id, AuthorUpsertRequest request, CancellationToken cancellationToken)
    {
        await authorValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (await db.Authors.AnyAsync(a => a.Slug == request.Slug && a.Id != id, cancellationToken))
        {
            throw Invalid(nameof(request.Slug), "This slug is already used.");
        }

        var author = await db.Authors.FirstOrDefaultAsync(a => a.Id == id, cancellationToken) ?? throw new NotFoundException("Author", id);
        author.Update(request.Name.Trim(), request.Slug, Clean(request.Bio));
        await db.SaveChangesAsync(cancellationToken);
        var count = await db.Books.CountAsync(b => b.Authors.Any(a => a.Id == id), cancellationToken);
        return new AuthorDto(author.Id, author.Name, author.Slug, author.Bio, count);
    }

    public async Task DeleteAuthorAsync(Guid id, CancellationToken cancellationToken)
    {
        var author = await db.Authors.FirstOrDefaultAsync(a => a.Id == id, cancellationToken) ?? throw new NotFoundException("Author", id);
        if (await db.Books.AnyAsync(b => b.Authors.Any(a => a.Id == id), cancellationToken))
        {
            throw new ConflictException("This author still has books. Reassign or delete them first.");
        }

        db.Authors.Remove(author);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------------- Categories ----------------

    public async Task<CategoryDto> CreateCategoryAsync(CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        await categoryValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (await db.Categories.AnyAsync(c => c.Slug == request.Slug, cancellationToken))
        {
            throw Invalid(nameof(request.Slug), "This slug is already used.");
        }

        var category = new Category(request.NameAr.Trim(), request.NameFr.Trim(), request.NameEn.Trim(), request.Slug);
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return new CategoryDto(category.Id, category.Slug, category.NameAr, category.NameFr, category.NameEn, 0);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid id, CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        await categoryValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (await db.Categories.AnyAsync(c => c.Slug == request.Slug && c.Id != id, cancellationToken))
        {
            throw Invalid(nameof(request.Slug), "This slug is already used.");
        }

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken) ?? throw new NotFoundException("Category", id);
        category.Update(request.NameAr.Trim(), request.NameFr.Trim(), request.NameEn.Trim(), request.Slug);
        await db.SaveChangesAsync(cancellationToken);
        var count = await db.Books.CountAsync(b => b.Categories.Any(c => c.Id == id), cancellationToken);
        return new CategoryDto(category.Id, category.Slug, category.NameAr, category.NameFr, category.NameEn, count);
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken) ?? throw new NotFoundException("Category", id);
        if (await db.Books.AnyAsync(b => b.Categories.Any(c => c.Id == id), cancellationToken))
        {
            throw new ConflictException("This category still has books. Move them to another category first.");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------------- Wilayas ----------------

    public async Task<WilayaDto> UpdateWilayaAsync(int code, WilayaUpdateRequest request, CancellationToken cancellationToken)
    {
        await wilayaValidator.ValidateAndThrowAsync(request, cancellationToken);
        var wilaya = await db.Wilayas.FirstOrDefaultAsync(w => w.Code == code, cancellationToken) ?? throw new NotFoundException("Wilaya", code);
        wilaya.SetShippingFee(request.ShippingFee);
        wilaya.SetDeliveryAvailable(request.IsDeliveryAvailable);
        await db.SaveChangesAsync(cancellationToken);
        return new WilayaDto(wilaya.Code, wilaya.NameAr, wilaya.NameFr, wilaya.ShippingFee, wilaya.IsDeliveryAvailable);
    }

    // ---------------- Reviews ----------------

    public async Task<PagedResult<AdminReviewDto>> ListReviewsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await db.Reviews.CountAsync(cancellationToken);
        var items = await db.Reviews.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(db.Books, r => r.BookId, b => b.Id, (r, b) => new AdminReviewDto(
                r.Id, b.Id, b.Slug, b.Title, r.AuthorName, r.Rating, r.Comment, r.CreatedAt))
            .ToListAsync(cancellationToken);
        return new PagedResult<AdminReviewDto>(items, page, pageSize, total);
    }

    public async Task DeleteReviewAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await db.Reviews.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken) == 0)
        {
            throw new NotFoundException("Review", id);
        }
    }

    // ---------------- helpers ----------------

    private async Task<Book> TrackedBookAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Books.Include(b => b.Authors).Include(b => b.Categories).FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
        ?? throw new NotFoundException("Book", id);

    private async Task EnsureBookUniqueAsync(string slug, string isbn, Guid? exceptId, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        if (await db.Books.AnyAsync(b => b.Slug == slug && b.Id != exceptId, cancellationToken))
        {
            failures.Add(new ValidationFailure(nameof(BookUpsertRequest.Slug), "This slug is already used by another book."));
        }

        if (await db.Books.AnyAsync(b => b.Isbn == isbn && b.Id != exceptId, cancellationToken))
        {
            failures.Add(new ValidationFailure(nameof(BookUpsertRequest.Isbn), "This ISBN is already used by another book."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }

    private async Task<(List<Author> Authors, List<Category> Categories)> LoadRelationsAsync(BookUpsertRequest request, CancellationToken cancellationToken)
    {
        var authorIds = request.AuthorIds.Distinct().ToList();
        var categoryIds = request.CategoryIds.Distinct().ToList();
        var authors = await db.Authors.Where(a => authorIds.Contains(a.Id)).ToListAsync(cancellationToken);
        var categories = await db.Categories.Where(c => categoryIds.Contains(c.Id)).ToListAsync(cancellationToken);

        var failures = new List<ValidationFailure>();
        if (authors.Count != authorIds.Count)
        {
            failures.Add(new ValidationFailure(nameof(BookUpsertRequest.AuthorIds), "Unknown author."));
        }

        if (categories.Count != categoryIds.Count)
        {
            failures.Add(new ValidationFailure(nameof(BookUpsertRequest.CategoryIds), "Unknown category."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Keep the admin's author order (first = main author).
        return (authorIds.Select(id => authors.First(a => a.Id == id)).ToList(), categories);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static ValidationException Invalid(string property, string message) => new([new ValidationFailure(property, message)]);
}
