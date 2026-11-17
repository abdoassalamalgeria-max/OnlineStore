using BookStore.Application.Abstractions;
using BookStore.Domain.Ordering;
using BookStore.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CartEntity = BookStore.Domain.Shopping.Cart;

namespace BookStore.Application.Features.Ordering;

public enum NotificationOutcome
{
    Applied,
    Duplicate,
    Ignored,
    OrderNotFound,
}

public interface IOrderLifecycleService
{
    /// <summary>Applies a verified payment notification exactly once (idempotent on the provider event id).</summary>
    Task<NotificationOutcome> ApplyPaymentNotificationAsync(PaymentNotification notification, CancellationToken cancellationToken);

    /// <summary>Cancels unpaid orders whose reservation has lapsed, releasing their stock. Returns how many were expired.</summary>
    Task<int> ExpireOverdueOrdersAsync(int batchSize, CancellationToken cancellationToken);
}

/// <summary>
/// Every payment-driven status change goes through here, inside a transaction, so the order status,
/// the stock and the processed-event record always change together. Concurrent writers (webhook vs
/// expiry job) are serialised by the order's xmin row version: the loser re-reads and re-decides.
/// </summary>
public sealed partial class OrderLifecycleService(
    IAppDbContext db,
    StockLedger stock,
    IPaymentGateway gateway,
    TimeProvider clock,
    ILogger<OrderLifecycleService> logger) : IOrderLifecycleService
{
    private const int MaxConcurrencyRetries = 3;

    /// <summary>If the provider cannot tell us an overdue order's status for this long, expire it anyway.</summary>
    private static readonly TimeSpan GiveUpWaitingForProvider = TimeSpan.FromHours(2);

    public async Task<NotificationOutcome> ApplyPaymentNotificationAsync(PaymentNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            return await WithConcurrencyRetryAsync(ct => ApplyOnceAsync(notification, ct), cancellationToken);
        }
        catch (DbUpdateException) when (notification.EventId.Length > 0)
        {
            // Two deliveries of the same event may have raced; the unique index lets only one through.
            db.ResetChangeTracker();
            if (await IsRecordedAsync(notification.EventId, cancellationToken))
            {
                return NotificationOutcome.Duplicate;
            }

            throw;
        }
    }

    public async Task<int> ExpireOverdueOrdersAsync(int batchSize, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var overdue = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Pending && o.ExpiresAt <= now)
            .OrderBy(o => o.ExpiresAt)
            .Select(o => new { o.Id, o.PaymentCheckoutId, o.ExpiresAt })
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var order in overdue)
        {
            // The webhook may have been lost: ask the provider before cancelling a possibly paid order.
            var status = PaymentStatus.Unknown;
            if (order.PaymentCheckoutId is not null)
            {
                try
                {
                    status = await gateway.GetCheckoutStatusAsync(order.PaymentCheckoutId, cancellationToken);
                }
                catch (PaymentGatewayException ex)
                {
                    LogStatusCheckFailed(logger, ex, order.Id);
                    if (now - order.ExpiresAt < GiveUpWaitingForProvider)
                    {
                        continue; // try again on the next run
                    }
                }
            }

            if (status == PaymentStatus.Paid)
            {
                await ApplyPaymentNotificationAsync(
                    new PaymentNotification($"poll:{order.PaymentCheckoutId}:paid", order.PaymentCheckoutId!, PaymentStatus.Paid, Amount: 0, LiveMode: false),
                    cancellationToken);
                continue;
            }

            if (await WithConcurrencyRetryAsync(ct => ExpireOnceAsync(order.Id, ct), cancellationToken))
            {
                expired++;
            }
        }

        return expired;
    }

    private async Task<NotificationOutcome> ApplyOnceAsync(PaymentNotification notification, CancellationToken cancellationToken) =>
        await db.InTransactionAsync(async ct =>
        {
            if (await IsRecordedAsync(notification.EventId, ct))
            {
                return NotificationOutcome.Duplicate;
            }

            var order = await db.Orders.Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.PaymentProvider == gateway.Provider && o.PaymentCheckoutId == notification.CheckoutId, ct);

            db.PaymentEvents.Add(new PaymentEvent(gateway.Provider, notification.EventId, notification.Status.ToString(), order?.Id, clock.GetUtcNow()));

            var outcome = order is null
                ? NotificationOutcome.OrderNotFound
                : await ApplyToOrderAsync(order, notification, ct);

            await db.SaveChangesAsync(ct);
            return outcome;
        }, cancellationToken);

    private async Task<NotificationOutcome> ApplyToOrderAsync(Order order, PaymentNotification notification, CancellationToken ct)
    {
        var lines = order.Items.Select(i => new StockLine(i.BookId, i.Quantity)).ToList();

        switch (notification.Status)
        {
            case PaymentStatus.Paid:
                // Polled confirmations carry no amount (0); webhook amounts must cover the order total.
                if (notification.Amount != 0 && notification.Amount < order.Total)
                {
                    LogAmountMismatch(logger, order.OrderNumber, notification.Amount, order.Total);
                }

                if (order.Status == OrderStatus.Pending)
                {
                    order.MarkPaid(clock.GetUtcNow(), stockSecured: true);
                    LogPaid(logger, order.OrderNumber);
                    return NotificationOutcome.Applied;
                }

                if (order.Status is OrderStatus.Cancelled or OrderStatus.Failed)
                {
                    // Late payment for an order whose stock was already released: try to take it again.
                    var secured = (await stock.TryReserveAsync(lines, ct)).Count == 0;
                    order.MarkPaid(clock.GetUtcNow(), secured);
                    await RemoveFromCartAsync(order, ct);
                    LogLatePayment(logger, order.OrderNumber, secured);
                    return NotificationOutcome.Applied;
                }

                return NotificationOutcome.Duplicate;

            case PaymentStatus.Failed or PaymentStatus.Canceled or PaymentStatus.Expired:
                var cancelled = notification.Status != PaymentStatus.Failed;
                if (order.FailPayment(cancelled, $"Payment {notification.Status.ToString().ToLowerInvariant()} at provider."))
                {
                    await stock.ReleaseAsync(lines, ct);
                    await RestoreCartAsync(order, ct);
                    return NotificationOutcome.Applied;
                }

                return NotificationOutcome.Ignored;

            default:
                return NotificationOutcome.Ignored;
        }
    }

    private async Task<bool> ExpireOnceAsync(Guid orderId, CancellationToken cancellationToken) =>
        await db.InTransactionAsync(async ct =>
        {
            var order = await db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == orderId, ct);
            var released = order.Expire(clock.GetUtcNow());
            if (released)
            {
                await stock.ReleaseAsync(order.Items.Select(i => new StockLine(i.BookId, i.Quantity)), ct);
                await RestoreCartAsync(order, ct);
            }

            await db.SaveChangesAsync(ct);
            return order.Status == OrderStatus.Cancelled && released;
        }, cancellationToken);

    /// <summary>Puts a failed/expired order's books back in the customer's cart so they can retry in one click.</summary>
    private async Task RestoreCartAsync(Order order, CancellationToken ct)
    {
        var cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == order.UserId, ct);
        if (cart is null)
        {
            cart = new CartEntity(order.UserId);
            db.Carts.Add(cart);
        }

        foreach (var item in order.Items)
        {
            cart.Add(item.BookId, item.Quantity);
        }

        cart.UpdatedAt = clock.GetUtcNow();
    }

    /// <summary>A late payment succeeded: the books restored to the cart were bought after all.</summary>
    private async Task RemoveFromCartAsync(Order order, CancellationToken ct)
    {
        var cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == order.UserId, ct);
        if (cart is null)
        {
            return;
        }

        foreach (var item in order.Items)
        {
            cart.Remove(item.BookId);
        }
    }

    private Task<bool> IsRecordedAsync(string eventId, CancellationToken ct) =>
        db.PaymentEvents.AnyAsync(e => e.Provider == gateway.Provider && e.ProviderEventId == eventId, ct);

    private async Task<T> WithConcurrencyRetryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // Someone else changed the order between our read and write: start over with fresh data.
                db.ResetChangeTracker();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderNumber} paid")]
    private static partial void LogPaid(ILogger logger, string orderNumber);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Late payment for order {OrderNumber}; stock secured: {Secured}")]
    private static partial void LogLatePayment(ILogger logger, string orderNumber, bool secured);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Order {OrderNumber}: paid amount {Paid} is below the total {Total}")]
    private static partial void LogAmountMismatch(ILogger logger, string orderNumber, int paid, int total);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not check payment status of overdue order {OrderId}")]
    private static partial void LogStatusCheckFailed(ILogger logger, Exception exception, Guid orderId);
}
