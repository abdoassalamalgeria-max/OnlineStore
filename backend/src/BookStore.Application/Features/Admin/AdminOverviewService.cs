using BookStore.Application.Abstractions;
using BookStore.Domain.Ordering;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Application.Features.Admin;

public sealed record LowStockBookDto(Guid Id, string Slug, string Title, int Stock);

/// <summary>Headline numbers for the admin dashboard.</summary>
public sealed record AdminOverviewDto(
    int Books,
    int Authors,
    int Categories,
    int LowStockBooks,
    int OutOfStockBooks,
    /// <summary>Orders per status, e.g. { "Paid": 3, "Shipped": 1 }.</summary>
    IReadOnlyDictionary<string, int> OrdersByStatus,
    /// <summary>Paid orders waiting to be shipped.</summary>
    int ToShip,
    int RequiringAttention,
    /// <summary>Sum of totals of paid, shipped and delivered orders (DZD).</summary>
    long Revenue,
    long RevenueLast30Days,
    IReadOnlyList<LowStockBookDto> LowStock);

public interface IAdminOverviewService
{
    Task<AdminOverviewDto> GetAsync(CancellationToken cancellationToken);
}

public sealed class AdminOverviewService(IAppDbContext db, TimeProvider clock) : IAdminOverviewService
{
    /// <summary>A book is "low stock" at or below this many copies (but not zero).</summary>
    public const int LowStockThreshold = 5;

    private static readonly OrderStatus[] Sold = [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered];

    public async Task<AdminOverviewDto> GetAsync(CancellationToken cancellationToken)
    {
        var since = clock.GetUtcNow().AddDays(-30);
        var byStatus = await db.Orders.GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var sold = db.Orders.Where(o => Sold.Contains(o.Status));

        return new AdminOverviewDto(
            await db.Books.CountAsync(cancellationToken),
            await db.Authors.CountAsync(cancellationToken),
            await db.Categories.CountAsync(cancellationToken),
            await db.Books.CountAsync(b => b.Stock > 0 && b.Stock <= LowStockThreshold, cancellationToken),
            await db.Books.CountAsync(b => b.Stock == 0, cancellationToken),
            byStatus.ToDictionary(s => s.Status.ToString(), s => s.Count),
            await db.Orders.CountAsync(o => o.Status == OrderStatus.Paid && !o.RequiresAttention, cancellationToken),
            await db.Orders.CountAsync(o => o.RequiresAttention, cancellationToken),
            await sold.SumAsync(o => (long)o.Total, cancellationToken),
            await sold.Where(o => o.PaidAt >= since).SumAsync(o => (long)o.Total, cancellationToken),
            await db.Books.AsNoTracking()
                .Where(b => b.Stock <= LowStockThreshold)
                .OrderBy(b => b.Stock).ThenBy(b => b.Title)
                .Take(10)
                .Select(b => new LowStockBookDto(b.Id, b.Slug, b.Title, b.Stock))
                .ToListAsync(cancellationToken));
    }
}
