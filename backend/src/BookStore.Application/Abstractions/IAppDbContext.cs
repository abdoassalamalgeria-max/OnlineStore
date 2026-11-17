using BookStore.Domain.Catalog;
using BookStore.Domain.Ordering;
using BookStore.Domain.Payments;
using BookStore.Domain.Shipping;
using BookStore.Domain.Shopping;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Application.Abstractions;

/// <summary>
/// The persistence surface Application services use. Implemented by Infrastructure's AppDbContext;
/// services compose LINQ over these sets and project straight to DTOs.
/// </summary>
public interface IAppDbContext
{
    DbSet<Book> Books { get; }
    DbSet<Author> Authors { get; }
    DbSet<Category> Categories { get; }
    DbSet<Wilaya> Wilayas { get; }
    DbSet<Cart> Carts { get; }
    DbSet<Order> Orders { get; }
    DbSet<PaymentEvent> PaymentEvents { get; }
    DbSet<Review> Reviews { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> in one database transaction, committing only if it completes.
    /// Transient failures retry the whole operation, so it must be safe to re-run from scratch
    /// (the change tracker is cleared before every attempt).
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    /// <summary>Forgets tracked entities (used before retrying after a concurrency conflict).</summary>
    void ResetChangeTracker();
}
