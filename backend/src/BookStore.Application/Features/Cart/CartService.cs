using BookStore.Application.Abstractions;
using BookStore.Application.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using CartEntity = BookStore.Domain.Shopping.Cart;

namespace BookStore.Application.Features.Cart;

public sealed record CartLineRequest(Guid BookId, int Quantity);

public sealed record UpdateCartLineRequest(int Quantity);

/// <summary>Guest cart sent from browser storage, for pricing (preview) or merging at sign-in.</summary>
public sealed record GuestCartRequest(IReadOnlyList<CartLineRequest> Items);

public sealed record CartLineDto(
    Guid BookId,
    string Slug,
    string Title,
    string Language,
    string? CoverImageUrl,
    int UnitPrice,
    int Price,
    int Quantity,
    int Stock,
    int LineTotal,
    bool Available);

/// <summary>Prices are current catalog prices; the order snapshots them at checkout.</summary>
public sealed record CartDto(IReadOnlyList<CartLineDto> Items, int ItemCount, int Subtotal, bool HasUnavailableItems);

public sealed class CartLineRequestValidator : AbstractValidator<CartLineRequest>
{
    public CartLineRequestValidator()
    {
        RuleFor(r => r.BookId).NotEmpty();
        RuleFor(r => r.Quantity).InclusiveBetween(1, CartEntity.MaxQuantityPerItem);
    }
}

public sealed class UpdateCartLineRequestValidator : AbstractValidator<UpdateCartLineRequest>
{
    // 0 removes the line.
    public UpdateCartLineRequestValidator() =>
        RuleFor(r => r.Quantity).InclusiveBetween(0, CartEntity.MaxQuantityPerItem);
}

public sealed class GuestCartRequestValidator : AbstractValidator<GuestCartRequest>
{
    public GuestCartRequestValidator()
    {
        RuleFor(r => r.Items).NotNull().Must(items => items.Count <= 50).WithMessage("At most 50 lines.");
        RuleForEach(r => r.Items).SetValidator(new CartLineRequestValidator());
    }
}

public interface ICartService
{
    Task<CartDto> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<CartDto> AddAsync(Guid userId, CartLineRequest request, CancellationToken cancellationToken);
    Task<CartDto> UpdateAsync(Guid userId, Guid bookId, UpdateCartLineRequest request, CancellationToken cancellationToken);
    Task<CartDto> RemoveAsync(Guid userId, Guid bookId, CancellationToken cancellationToken);

    /// <summary>Adds a guest cart into the user's cart (quantities add up, capped per line). Unknown books are skipped.</summary>
    Task<CartDto> MergeAsync(Guid userId, GuestCartRequest request, CancellationToken cancellationToken);

    /// <summary>Prices a guest cart without saving anything.</summary>
    Task<CartDto> PreviewAsync(GuestCartRequest request, CancellationToken cancellationToken);
}

public sealed class CartService(
    IAppDbContext db,
    TimeProvider clock,
    IValidator<CartLineRequest> lineValidator,
    IValidator<UpdateCartLineRequest> updateValidator,
    IValidator<GuestCartRequest> guestValidator) : ICartService
{
    public async Task<CartDto> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var lines = await db.Carts.AsNoTracking()
            .Where(c => c.UserId == userId)
            .SelectMany(c => c.Items)
            .Select(i => new CartLineRequest(i.BookId, i.Quantity))
            .ToListAsync(cancellationToken);
        return await PriceAsync(lines, cancellationToken);
    }

    public async Task<CartDto> AddAsync(Guid userId, CartLineRequest request, CancellationToken cancellationToken)
    {
        await lineValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (!await db.Books.AnyAsync(b => b.Id == request.BookId, cancellationToken))
        {
            throw new NotFoundException("Book", request.BookId);
        }

        var cart = await LoadOrCreateAsync(userId, cancellationToken);
        cart.Add(request.BookId, request.Quantity);
        await SaveAsync(cart, cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }

    public async Task<CartDto> UpdateAsync(Guid userId, Guid bookId, UpdateCartLineRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var cart = await LoadOrCreateAsync(userId, cancellationToken);
        if (!cart.SetQuantity(bookId, request.Quantity))
        {
            throw new NotFoundException("Cart item", bookId);
        }

        await SaveAsync(cart, cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }

    public async Task<CartDto> RemoveAsync(Guid userId, Guid bookId, CancellationToken cancellationToken)
    {
        var cart = await LoadOrCreateAsync(userId, cancellationToken);
        if (cart.Remove(bookId))
        {
            await SaveAsync(cart, cancellationToken);
        }

        return await GetAsync(userId, cancellationToken);
    }

    public async Task<CartDto> MergeAsync(Guid userId, GuestCartRequest request, CancellationToken cancellationToken)
    {
        await guestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var ids = request.Items.Select(i => i.BookId).Distinct().ToList();
        var existing = await db.Books.Where(b => ids.Contains(b.Id)).Select(b => b.Id).ToListAsync(cancellationToken);

        var cart = await LoadOrCreateAsync(userId, cancellationToken);
        foreach (var line in request.Items.Where(i => existing.Contains(i.BookId)))
        {
            cart.Add(line.BookId, line.Quantity);
        }

        await SaveAsync(cart, cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }

    public async Task<CartDto> PreviewAsync(GuestCartRequest request, CancellationToken cancellationToken)
    {
        await guestValidator.ValidateAndThrowAsync(request, cancellationToken);
        // Collapse duplicate lines the same way a saved cart would.
        var lines = request.Items
            .GroupBy(i => i.BookId)
            .Select(g => new CartLineRequest(g.Key, Math.Min(g.Sum(i => i.Quantity), CartEntity.MaxQuantityPerItem)))
            .ToList();
        return await PriceAsync(lines, cancellationToken);
    }

    private async Task<CartEntity> LoadOrCreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (cart is null)
        {
            cart = new CartEntity(userId);
            db.Carts.Add(cart);
        }

        return cart;
    }

    private async Task SaveAsync(CartEntity cart, CancellationToken cancellationToken)
    {
        cart.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Joins cart lines with current book data. Lines whose book was deleted are dropped.</summary>
    private async Task<CartDto> PriceAsync(IReadOnlyList<CartLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.BookId).ToList();
        var books = await db.Books.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .Select(b => new { b.Id, b.Slug, b.Title, b.Language, b.CoverImageUrl, b.EffectivePrice, b.Price, b.Stock })
            .ToDictionaryAsync(b => b.Id, cancellationToken);

        var items = lines
            .Where(l => books.ContainsKey(l.BookId))
            .Select(l =>
            {
                var b = books[l.BookId];
                return new CartLineDto(
                    b.Id, b.Slug, b.Title, b.Language, b.CoverImageUrl,
                    b.EffectivePrice, b.Price, l.Quantity, b.Stock,
                    b.EffectivePrice * l.Quantity, Available: b.Stock >= l.Quantity);
            })
            .OrderBy(i => i.Title, StringComparer.Ordinal)
            .ToList();

        return new CartDto(items, items.Sum(i => i.Quantity), items.Sum(i => i.LineTotal), items.Any(i => !i.Available));
    }
}
