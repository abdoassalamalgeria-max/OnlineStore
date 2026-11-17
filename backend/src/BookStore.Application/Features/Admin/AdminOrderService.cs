using BookStore.Application.Abstractions;
using BookStore.Application.Common;
using BookStore.Application.Features.Ordering;
using BookStore.Domain.Common;
using BookStore.Domain.Ordering;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Application.Features.Admin;

public sealed record AdminOrderQuery(OrderStatus? Status, string? Search, bool? RequiresAttention, int Page = 1, int PageSize = 20);

public sealed record AdminOrderListItemDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    int Total,
    DateTimeOffset PlacedAt,
    string CustomerName,
    string Phone,
    string WilayaName,
    int ItemCount,
    bool RequiresAttention);

public sealed record AdminOrderDto(
    Guid Id,
    string OrderNumber,
    Guid UserId,
    OrderStatus Status,
    int Subtotal,
    int ShippingFee,
    int Total,
    DateTimeOffset PlacedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? ShippedAt,
    DateTimeOffset? DeliveredAt,
    bool StockReserved,
    bool RequiresAttention,
    string? StatusNote,
    string? PaymentProvider,
    string? PaymentCheckoutId,
    ShippingAddressDto ShippingAddress,
    IReadOnlyList<OrderItemDto> Items);

public enum AdminOrderAction
{
    Ship,
    Deliver,
    Cancel,
    /// <summary>Re-reserve stock for a paid order flagged "requires attention" (after restocking).</summary>
    ResolveAttention,
}

public sealed record AdminOrderActionRequest(AdminOrderAction Action, string? Note);

public sealed class AdminOrderActionRequestValidator : AbstractValidator<AdminOrderActionRequest>
{
    public AdminOrderActionRequestValidator()
    {
        RuleFor(r => r.Action).IsInEnum();
        RuleFor(r => r.Note).MaximumLength(500);
    }
}

public interface IAdminOrderService
{
    Task<PagedResult<AdminOrderListItemDto>> ListAsync(AdminOrderQuery query, CancellationToken cancellationToken);
    Task<AdminOrderDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<AdminOrderDto> ApplyAsync(Guid id, AdminOrderActionRequest request, CancellationToken cancellationToken);
}

/// <summary>Order back-office: shipping workflow and cancellations (which give the stock back).</summary>
public sealed class AdminOrderService(
    IAppDbContext db,
    StockLedger stock,
    TimeProvider clock,
    IValidator<AdminOrderActionRequest> validator) : IAdminOrderService
{
    public async Task<PagedResult<AdminOrderListItemDto>> ListAsync(AdminOrderQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var orders = db.Orders.AsNoTracking();

        if (query.Status is not null)
        {
            orders = orders.Where(o => o.Status == query.Status);
        }

        if (query.RequiresAttention is true)
        {
            orders = orders.Where(o => o.RequiresAttention);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Order number (exact, case-insensitive), phone (partial) or customer name (partial, case-insensitive).
            var term = query.Search.Trim();
            var upper = term.ToUpperInvariant();
            var lower = term.ToLowerInvariant();
            orders = orders.Where(o =>
                o.OrderNumber == upper ||
                o.ShippingAddress.Phone.Contains(term) ||
                o.ShippingAddress.FullName.ToLower().Contains(lower));
        }

        var total = await orders.CountAsync(cancellationToken);
        var items = await orders
            .OrderByDescending(o => o.PlacedAt).ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new AdminOrderListItemDto(
                o.Id, o.OrderNumber, o.Status, o.Total, o.PlacedAt,
                o.ShippingAddress.FullName, o.ShippingAddress.Phone, o.ShippingAddress.WilayaName,
                o.Items.Sum(i => i.Quantity), o.RequiresAttention))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminOrderListItemDto>(items, page, pageSize, total);
    }

    public async Task<AdminOrderDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new AdminOrderDto(
                o.Id, o.OrderNumber, o.UserId, o.Status, o.Subtotal, o.ShippingFee, o.Total,
                o.PlacedAt, o.ExpiresAt, o.PaidAt, o.ShippedAt, o.DeliveredAt,
                o.StockReserved, o.RequiresAttention, o.StatusNote, o.PaymentProvider, o.PaymentCheckoutId,
                new ShippingAddressDto(o.ShippingAddress.FullName, o.ShippingAddress.Phone, o.ShippingAddress.WilayaCode,
                    o.ShippingAddress.WilayaName, o.ShippingAddress.Commune, o.ShippingAddress.AddressLine),
                o.Items.Select(i => new OrderItemDto(i.BookId, i.Title, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
        return order ?? throw new NotFoundException("Order", id);
    }

    public async Task<AdminOrderDto> ApplyAsync(Guid id, AdminOrderActionRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        try
        {
            await db.InTransactionAsync(async ct =>
            {
                var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
                    ?? throw new NotFoundException("Order", id);
                var lines = order.Items.Select(i => new StockLine(i.BookId, i.Quantity)).ToList();
                var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

                switch (request.Action)
                {
                    case AdminOrderAction.Ship:
                        order.Ship(clock.GetUtcNow());
                        break;
                    case AdminOrderAction.Deliver:
                        order.Deliver(clock.GetUtcNow());
                        break;
                    case AdminOrderAction.Cancel:
                        if (order.CancelByAdmin(note ?? "Cancelled by admin."))
                        {
                            await stock.ReleaseAsync(lines, ct);
                        }

                        break;
                    case AdminOrderAction.ResolveAttention:
                        if (!order.RequiresAttention)
                        {
                            break;
                        }

                        if ((await stock.TryReserveAsync(lines, ct)).Count > 0)
                        {
                            // Throwing rolls back the partial reservation.
                            throw new DomainException("Still not enough stock for this order. Restock the books first, or cancel and refund.");
                        }

                        order.ResolveAttention(note ?? "Stock re-reserved by admin.");
                        break;
                }

                await db.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A webhook or the expiry job changed the order at the same moment.
            throw new ConflictException("The order changed while you were editing it. Reload and try again.");
        }

        return await GetAsync(id, cancellationToken);
    }
}
