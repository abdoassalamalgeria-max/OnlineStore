using BookStore.Api.Extensions;
using BookStore.Application.Common;
using BookStore.Application.Features.Ordering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
[Produces("application/json")]
public sealed class OrdersController(ICheckoutService checkout, IOrderQueryService orders) : ControllerBase
{
    /// <summary>
    /// Turns the cart into an order, reserves the stock for 30 minutes, and returns the Chargily payment
    /// page URL to redirect the customer to. The order becomes Paid only when Chargily's webhook confirms it.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CheckoutResponse>> Checkout(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var result = await checkout.PlaceOrderAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = result.OrderId }, result);
    }

    /// <summary>The signed-in user's orders, newest first.</summary>
    [HttpGet("orders")]
    public async Task<PagedResult<OrderSummaryDto>> ListOrders(CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 10) =>
        await orders.ListForUserAsync(User.GetUserId(), page, pageSize, cancellationToken);

    /// <summary>One of the signed-in user's orders (used by the payment success/failure pages).</summary>
    [HttpGet("orders/{id:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<OrderDto> GetOrder(Guid id, CancellationToken cancellationToken) =>
        await orders.GetForUserAsync(User.GetUserId(), id, cancellationToken);
}
