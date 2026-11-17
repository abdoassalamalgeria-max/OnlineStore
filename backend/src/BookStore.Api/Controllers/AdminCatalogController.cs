using BookStore.Api.Extensions;
using BookStore.Application.Common;
using BookStore.Application.Features.Admin;
using BookStore.Application.Features.Catalog;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

/// <summary>Catalog administration. Every action requires the Admin role.</summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public sealed class AdminCatalogController(IAdminCatalogService catalog) : ControllerBase
{
    /// <summary>Largest accepted cover upload (before resizing).</summary>
    public const long MaxCoverBytes = 5 * 1024 * 1024;

    // ---------------- Books ----------------

    [HttpGet("books")]
    public async Task<PagedResult<AdminBookListItemDto>> ListBooks(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] bool? lowStock = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        await catalog.ListBooksAsync(new AdminBookQuery(search, page, pageSize, lowStock), cancellationToken);

    [HttpGet("books/{id:guid}")]
    public async Task<AdminBookDto> GetBook(Guid id, CancellationToken cancellationToken) =>
        await catalog.GetBookAsync(id, cancellationToken);

    [HttpPost("books")]
    [ProducesResponseType<AdminBookDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminBookDto>> CreateBook(BookUpsertRequest request, CancellationToken cancellationToken)
    {
        var book = await catalog.CreateBookAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetBook), new { id = book.Id }, book);
    }

    [HttpPut("books/{id:guid}")]
    [ProducesResponseType<AdminBookDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<AdminBookDto> UpdateBook(Guid id, BookUpsertRequest request, CancellationToken cancellationToken) =>
        await catalog.UpdateBookAsync(id, request, cancellationToken);

    /// <summary>Deletes a book that was never ordered (409 otherwise: set its stock to 0 instead).</summary>
    [HttpDelete("books/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteBook(Guid id, CancellationToken cancellationToken)
    {
        await catalog.DeleteBookAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Uploads a cover (JPEG/PNG/WebP, max 5 MB). It is resized to 600 px and stored as WebP.</summary>
    [HttpPut("books/{id:guid}/cover")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxCoverBytes + 64 * 1024)]
    [ProducesResponseType<AdminBookDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<AdminBookDto> UploadCover(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is 0 or > MaxCoverBytes)
        {
            throw new ValidationException([new ValidationFailure("file", "The cover must be an image of at most 5 MB.")]);
        }

        await using var stream = file.OpenReadStream();
        return await catalog.SetCoverAsync(id, stream, cancellationToken);
    }

    [HttpDelete("books/{id:guid}/cover")]
    public async Task<AdminBookDto> RemoveCover(Guid id, CancellationToken cancellationToken) =>
        await catalog.RemoveCoverAsync(id, cancellationToken);

    // ---------------- Authors ----------------

    [HttpPost("authors")]
    [ProducesResponseType<AuthorDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthorDto>> CreateAuthor(AuthorUpsertRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await catalog.CreateAuthorAsync(request, cancellationToken));

    [HttpPut("authors/{id:guid}")]
    public async Task<AuthorDto> UpdateAuthor(Guid id, AuthorUpsertRequest request, CancellationToken cancellationToken) =>
        await catalog.UpdateAuthorAsync(id, request, cancellationToken);

    [HttpDelete("authors/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAuthor(Guid id, CancellationToken cancellationToken)
    {
        await catalog.DeleteAuthorAsync(id, cancellationToken);
        return NoContent();
    }

    // ---------------- Categories ----------------

    [HttpPost("categories")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryDto>> CreateCategory(CategoryUpsertRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await catalog.CreateCategoryAsync(request, cancellationToken));

    [HttpPut("categories/{id:guid}")]
    public async Task<CategoryDto> UpdateCategory(Guid id, CategoryUpsertRequest request, CancellationToken cancellationToken) =>
        await catalog.UpdateCategoryAsync(id, request, cancellationToken);

    [HttpDelete("categories/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        await catalog.DeleteCategoryAsync(id, cancellationToken);
        return NoContent();
    }

    // ---------------- Wilayas & reviews ----------------

    /// <summary>Sets a wilaya's delivery fee (DZD) and whether delivery is available there.</summary>
    [HttpPut("wilayas/{code:int}")]
    public async Task<WilayaDto> UpdateWilaya(int code, WilayaUpdateRequest request, CancellationToken cancellationToken) =>
        await catalog.UpdateWilayaAsync(code, request, cancellationToken);

    [HttpGet("reviews")]
    public async Task<PagedResult<AdminReviewDto>> ListReviews(CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        await catalog.ListReviewsAsync(page, pageSize, cancellationToken);

    [HttpDelete("reviews/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteReview(Guid id, CancellationToken cancellationToken)
    {
        await catalog.DeleteReviewAsync(id, cancellationToken);
        return NoContent();
    }
}
