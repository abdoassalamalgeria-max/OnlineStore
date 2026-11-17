using BookStore.Application.Common;
using BookStore.Application.Features.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/v1/books")]
[Produces("application/json")]
public sealed class BooksController(ICatalogService catalog) : ControllerBase
{
    /// <summary>Search, filter, sort and page the catalog.</summary>
    /// <remarks>Example: GET /api/v1/books?search=camus&amp;language=fr&amp;sort=PriceAsc&amp;page=1&amp;pageSize=12</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<BookSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<PagedResult<BookSummaryDto>> Search([FromQuery] BookQuery query, CancellationToken cancellationToken) =>
        await catalog.SearchBooksAsync(query, cancellationToken);

    /// <summary>Full details of one book by its URL slug.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<BookDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<BookDetailsDto> GetBySlug(string slug, CancellationToken cancellationToken) =>
        await catalog.GetBookBySlugAsync(slug, cancellationToken);
}
