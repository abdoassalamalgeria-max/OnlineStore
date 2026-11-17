using BookStore.Api.Extensions;
using BookStore.Application.Features.Auth;
using BookStore.Application.Features.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/v1/books/{slug}/reviews")]
[Produces("application/json")]
public sealed class ReviewsController(IReviewService reviews, IAuthService auth) : ControllerBase
{
    /// <summary>Rating summary and reviews of a book, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<BookReviewsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<BookReviewsDto> List(string slug, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 10) =>
        await reviews.GetForBookAsync(slug, page, pageSize, cancellationToken);

    /// <summary>Whether the signed-in user may review this book (they bought it), and their review if any.</summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<MyReviewDto> Mine(string slug, CancellationToken cancellationToken) =>
        await reviews.GetMineAsync(User.GetUserId(), slug, cancellationToken);

    /// <summary>Creates or updates the signed-in user's review. 403 if they have not bought the book.</summary>
    [HttpPut("mine")]
    [Authorize]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ReviewDto> Upsert(string slug, UpsertReviewRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var user = await auth.GetUserAsync(userId, cancellationToken); // current name, not the token's
        return await reviews.UpsertMineAsync(userId, user.FullName, slug, request, cancellationToken);
    }

    [HttpDelete("mine")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string slug, CancellationToken cancellationToken)
    {
        await reviews.DeleteMineAsync(User.GetUserId(), slug, cancellationToken);
        return NoContent();
    }
}
