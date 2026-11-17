using System.Security.Cryptography;
using BookStore.Application.Abstractions;
using BookStore.Domain.Ordering;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookStore.Application.Features.Ordering;

public interface ICheckoutService
{
    Task<CheckoutResponse> PlaceOrderAsync(Guid userId, CheckoutRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Cart → order → hosted payment page:
///   1. one transaction: price the cart from the catalog, reserve stock atomically, insert the order;
///   2. create the Chargily checkout (outside the transaction: never hold DB locks during an HTTP call);
///   3. store the checkout id on the order and empty the cart.
/// If step 2 fails the order is marked Failed and its stock released; the cart is left untouched.
/// </summary>
public sealed partial class CheckoutService(
    IAppDbContext db,
    StockLedger stock,
    IPaymentGateway gateway,
    IValidator<CheckoutRequest> validator,
    IOptions<CheckoutOptions> options,
    TimeProvider clock,
    ILogger<CheckoutService> logger) : ICheckoutService
{
    public async Task<CheckoutResponse> PlaceOrderAsync(Guid userId, CheckoutRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var address = request.ShippingAddress;

        var wilaya = await db.Wilayas.AsNoTracking().FirstOrDefaultAsync(w => w.Code == address.WilayaCode, cancellationToken);
        if (wilaya is null || !wilaya.IsDeliveryAvailable)
        {
            throw Invalid("ShippingAddress.WilayaCode", "Delivery to this wilaya is not available.");
        }

        var order = await db.InTransactionAsync(async ct =>
        {
            var lines = await db.Carts.AsNoTracking()
                .Where(c => c.UserId == userId)
                .SelectMany(c => c.Items)
                .Select(i => new StockLine(i.BookId, i.Quantity))
                .ToListAsync(ct);
            if (lines.Count == 0)
            {
                throw Invalid("Cart", "The cart is empty.");
            }

            var ids = lines.Select(l => l.BookId).ToList();
            var books = await db.Books.AsNoTracking()
                .Where(b => ids.Contains(b.Id))
                .Select(b => new { b.Id, b.Title, b.EffectivePrice })
                .ToDictionaryAsync(b => b.Id, ct);

            var missing = ids.Where(id => !books.ContainsKey(id)).ToList();
            var unavailable = missing.Count > 0 ? missing : await stock.TryReserveAsync(lines, ct);
            if (unavailable.Count > 0)
            {
                // Throwing rolls back any lines already reserved in this transaction.
                throw new InsufficientStockException(unavailable);
            }

            var placed = Order.Place(
                NewOrderNumber(clock.GetUtcNow()),
                userId,
                new ShippingAddress(address.FullName.Trim(), address.Phone.Trim(), wilaya.Code, wilaya.NameAr,
                    address.Commune.Trim(), address.AddressLine.Trim()),
                lines.Select(l => new OrderItem(l.BookId, books[l.BookId].Title, books[l.BookId].EffectivePrice, l.Quantity)),
                wilaya.ShippingFee,
                clock.GetUtcNow(),
                TimeSpan.FromMinutes(options.Value.ReservationMinutes));
            db.Orders.Add(placed);
            await db.SaveChangesAsync(ct);
            return placed;
        }, cancellationToken);

        PaymentCheckout checkout;
        try
        {
            var baseUrl = options.Value.FrontendBaseUrl.TrimEnd('/');
            checkout = await gateway.CreateCheckoutAsync(new PaymentCheckoutRequest(
                order.Id,
                order.OrderNumber,
                order.Total,
                request.Locale,
                new Uri($"{baseUrl}/{request.Locale}/checkout/success?order={order.Id}"),
                new Uri($"{baseUrl}/{request.Locale}/checkout/failure?order={order.Id}")), cancellationToken);
        }
        catch (Exception ex) when (ex is PaymentGatewayException or HttpRequestException or TaskCanceledException)
        {
            LogGatewayFailure(logger, ex, order.OrderNumber);
            await AbandonAsync(order.Id, CancellationToken.None);
            throw ex as PaymentGatewayException ?? new PaymentGatewayException("The payment provider is unavailable.", ex);
        }

        order.AttachPayment(gateway.Provider, checkout.CheckoutId, checkout.PaymentUrl.ToString());
        var cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        cart?.Clear();
        await db.SaveChangesAsync(cancellationToken);

        return new CheckoutResponse(order.Id, order.OrderNumber, order.Total, checkout.PaymentUrl.ToString());
    }

    /// <summary>The checkout could not be created: fail the order and give the stock back.</summary>
    private async Task AbandonAsync(Guid orderId, CancellationToken cancellationToken)
    {
        db.ResetChangeTracker();
        await db.InTransactionAsync(async ct =>
        {
            var order = await db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == orderId, ct);
            if (order.FailPayment(cancelledByCustomer: false, "Payment checkout could not be created."))
            {
                await stock.ReleaseAsync(order.Items.Select(i => new StockLine(i.BookId, i.Quantity)), ct);
            }

            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    /// <summary>"BK-20261116-7Q4XK9": date for humans, 6 random chars (unambiguous alphabet) for uniqueness.</summary>
    private static string NewOrderNumber(DateTimeOffset now)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> suffix = stackalloc char[6];
        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return $"BK-{now:yyyyMMdd}-{suffix}";
    }

    private static ValidationException Invalid(string property, string message) =>
        new([new ValidationFailure(property, message)]);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not create a payment checkout for order {OrderNumber}")]
    private static partial void LogGatewayFailure(ILogger logger, Exception exception, string orderNumber);
}
