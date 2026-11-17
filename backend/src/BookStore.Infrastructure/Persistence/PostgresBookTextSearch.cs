using BookStore.Application.Abstractions;
using BookStore.Application.Common;
using BookStore.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Infrastructure.Persistence;

/// <summary>ILIKE '%term%' on title and author name; both columns have pg_trgm GIN indexes.</summary>
internal sealed class PostgresBookTextSearch : IBookTextSearch
{
    public IQueryable<Book> Matching(IQueryable<Book> books, string term)
    {
        var pattern = LikePattern.Contains(term);
        return books.Where(b =>
            EF.Functions.ILike(b.Title, pattern) ||
            b.Authors.Any(a => EF.Functions.ILike(a.Name, pattern)));
    }
}
