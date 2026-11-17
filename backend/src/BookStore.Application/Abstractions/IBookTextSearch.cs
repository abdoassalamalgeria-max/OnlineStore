using BookStore.Domain.Catalog;

namespace BookStore.Application.Abstractions;

/// <summary>
/// Provider-specific free-text filter for the catalog (PostgreSQL ILIKE + pg_trgm in Infrastructure).
/// Kept behind an interface so Application never references the database provider.
/// </summary>
public interface IBookTextSearch
{
    /// <summary>Books whose title or any author name contains <paramref name="term"/>, case-insensitively.</summary>
    IQueryable<Book> Matching(IQueryable<Book> books, string term);
}
