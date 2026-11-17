using BookStore.Application.Abstractions;
using BookStore.Application.Common;
using BookStore.Domain.Ordering;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Application.Features.Ordering;

public interface IOrderQueryService
{
    /// <summary>One of the user's own orders. Other users' orders are reported as not found (no id probing).</summary>
    Task<OrderDto> GetForUserAsync(Guid userId, Guid orderId, CancellationToken cancellationToken);

    /// <summary>The user's orders, newest first.</summary>
    Task<PagedResult<OrderSummaryDto>> ListForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class OrderQueryService(IAppDbContext db, TimeProvider clock) : IOrderQueryService
{
    public async Task<OrderDto> GetForUserAsync(Guid userId, Guid orderId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId && o.UserId == userId)
            .Select(o => new OrderDto(
                o.Id,
                o.OrderNumber,
                o.Status,
                o.Subtotal,
                o.ShippingFee,
                o.Total,
                o.PlacedAt,
                o.PaidAt,
                o.ExpiresAt,
                o.Status == OrderStatus.Pending && o.ExpiresAt > now ? o.PaymentUrl : null,
                new ShippingAddressDto(
                    o.ShippingAddress.FullName,
                    o.ShippingAddress.Phone,
                    o.ShippingAddress.WilayaCode,
                    o.ShippingAddress.WilayaName,
                    o.ShippingAddress.Commune,
                    o.ShippingAddress.AddressLine),
                o.Items.Select(i => new OrderItemDto(i.BookId, i.Title, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return order ?? throw new NotFoundException("Order", orderId);
    }

    public async Task<PagedResult<OrderSummaryDto>> ListForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var orders = db.Orders.AsNoTracking().Where(o => o.UserId == userId);

        var total = await orders.CountAsync(cancellationToken);
        var items = await orders
            .OrderByDescending(o => o.PlacedAt).ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.OrderNumber,
                o.Status,
                o.Total,
                o.PlacedAt,
                o.Items.Sum(i => i.Quantity),
                o.Items.OrderBy(i => i.Title).Select(i => i.Title).Take(3).ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderSummaryDto>(items, page, pageSize, total);
    }
}
