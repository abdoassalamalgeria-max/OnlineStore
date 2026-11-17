using BookStore.Api.Extensions;
using BookStore.Application.Common;
using BookStore.Application.Features.Admin;
using BookStore.Domain.Ordering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/v1/admin/orders")]
[Authorize(Policy = Policies.AdminOnly)]
[Produces("application/json")]
public sealed class AdminOrdersController(IAdminOrderService orders) : ControllerBase
{
    /// <summary>Orders, newest first. Search matches the order number, the phone or the customer name.</summary>
    [HttpGet]
    public async Task<PagedResult<AdminOrderListItemDto>> List(
        CancellationToken cancellationToken,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? requiresAttention = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        await orders.ListAsync(new AdminOrderQuery(status, search, requiresAttention, page, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<AdminOrderDto> Get(Guid id, CancellationToken cancellationToken) =>
        await orders.GetAsync(id, cancellationToken);

    /// <summary>
    /// Ship (Paid → Shipped), Deliver (Shipped → Delivered), Cancel (releases stock; refund paid orders in
    /// the Chargily dashboard) or ResolveAttention (re-reserve stock after restocking). 409 if not allowed now.
    /// </summary>
    [HttpPost("{id:guid}/actions")]
    [ProducesResponseType<AdminOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<AdminOrderDto> Apply(Guid id, AdminOrderActionRequest request, CancellationToken cancellationToken) =>
        await orders.ApplyAsync(id, request, cancellationToken);
}
