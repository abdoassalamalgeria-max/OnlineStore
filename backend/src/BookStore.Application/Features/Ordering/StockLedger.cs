using BookStore.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Application.Features.Ordering;

public sealed record StockLine(Guid BookId, int Quantity);

/// <summary>
/// Atomic stock changes. Each reservation is a single conditional UPDATE
/// (<c>SET stock = stock - q WHERE id = @id AND stock &gt;= q</c>), so two customers racing for the
/// last copy cannot both succeed. Call inside <see cref="IAppDbContext.InTransactionAsync{T}"/> so a
/// partial reservation is rolled back when any line fails.
/// </summary>
public sealed class StockLedger(IAppDbContext db)
{
    /// <summary>Reserves every line; returns the ids that could not be reserved (empty = success).</summary>
    public async Task<IReadOnlyList<Guid>> TryReserveAsync(IEnumerable<StockLine> lines, CancellationToken cancellationToken)
    {
        var failed = new List<Guid>();

        // Same lock order for every transaction (by id) so concurrent checkouts cannot deadlock.
        foreach (var line in lines.OrderBy(l => l.BookId))
        {
            var quantity = line.Quantity;
            var updated = await db.Books
                .Where(b => b.Id == line.BookId && b.Stock >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Stock, b => b.Stock - quantity), cancellationToken);
            if (updated == 0)
            {
                failed.Add(line.BookId);
            }
        }

        return failed;
    }

    public async Task ReleaseAsync(IEnumerable<StockLine> lines, CancellationToken cancellationToken)
    {
        foreach (var line in lines.OrderBy(l => l.BookId))
        {
            var quantity = line.Quantity;
            await db.Books
                .Where(b => b.Id == line.BookId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Stock, b => b.Stock + quantity), cancellationToken);
        }
    }
}

/// <summary>Some books in the cart do not have enough stock. Mapped to 409 with the affected book ids.</summary>
public sealed class InsufficientStockException(IReadOnlyList<Guid> bookIds)
    : Exception("Some items are no longer available in the requested quantity.")
{
    public IReadOnlyList<Guid> BookIds { get; } = bookIds;
}
