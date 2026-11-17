using BookStore.Api.Extensions;
using BookStore.Application.Features.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

/// <summary>The signed-in customer's cart. Guests keep theirs in the browser and use /preview and /merge.</summary>
[ApiController]
[Route("api/v1/cart")]
[Authorize]
[Produces("application/json")]
public sealed class CartController(ICartService carts) : ControllerBase
{
    [HttpGet]
    public async Task<CartDto> Get(CancellationToken cancellationToken) =>
        await carts.GetAsync(User.GetUserId(), cancellationToken);

    /// <summary>Adds a book (quantities add up if it is already in the cart).</summary>
    [HttpPost("items")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<CartDto> Add(CartLineRequest request, CancellationToken cancellationToken) =>
        await carts.AddAsync(User.GetUserId(), request, cancellationToken);

    /// <summary>Sets a line's quantity; 0 removes it.</summary>
    [HttpPut("items/{bookId:guid}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<CartDto> Update(Guid bookId, UpdateCartLineRequest request, CancellationToken cancellationToken) =>
        await carts.UpdateAsync(User.GetUserId(), bookId, request, cancellationToken);

    [HttpDelete("items/{bookId:guid}")]
    public async Task<CartDto> Remove(Guid bookId, CancellationToken cancellationToken) =>
        await carts.RemoveAsync(User.GetUserId(), bookId, cancellationToken);

    /// <summary>Called right after sign-in with the guest cart from browser storage.</summary>
    [HttpPost("merge")]
    public async Task<CartDto> Merge(GuestCartRequest request, CancellationToken cancellationToken) =>
        await carts.MergeAsync(User.GetUserId(), request, cancellationToken);

    /// <summary>Prices a guest cart (no account needed, nothing is saved).</summary>
    [HttpPost("preview")]
    [AllowAnonymous]
    public async Task<CartDto> Preview(GuestCartRequest request, CancellationToken cancellationToken) =>
        await carts.PreviewAsync(request, cancellationToken);
}
