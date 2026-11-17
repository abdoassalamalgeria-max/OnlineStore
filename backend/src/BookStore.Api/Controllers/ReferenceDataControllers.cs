using BookStore.Application.Features.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Produces("application/json")]
public sealed class CategoriesController(ICatalogService catalog) : ControllerBase
{
    /// <summary>All categories with their names in ar/fr/en and the number of books in each.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<CategoryDto>> GetAll(CancellationToken cancellationToken) =>
        await catalog.GetCategoriesAsync(cancellationToken);
}

[ApiController]
[Route("api/v1/authors")]
[Produces("application/json")]
public sealed class AuthorsController(ICatalogService catalog) : ControllerBase
{
    /// <summary>All authors with their book counts, ordered by name.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<AuthorDto>> GetAll(CancellationToken cancellationToken) =>
        await catalog.GetAuthorsAsync(cancellationToken);
}

[ApiController]
[Route("api/v1/wilayas")]
[Produces("application/json")]
public sealed class WilayasController(ICatalogService catalog) : ControllerBase
{
    /// <summary>The 58 wilayas with their delivery fee (DZD), for the checkout address form.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<WilayaDto>> GetAll(CancellationToken cancellationToken) =>
        await catalog.GetWilayasAsync(cancellationToken);
}
