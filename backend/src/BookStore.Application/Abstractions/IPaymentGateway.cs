namespace BookStore.Application.Abstractions;

/// <summary>
/// A hosted-payment-page provider (Chargily Pay today). Adding a provider = another implementation;
/// checkout and webhook processing only see this interface.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Short provider id stored on orders and payment events, e.g. "chargily".</summary>
    string Provider { get; }

    /// <summary>Creates a hosted checkout and returns where to send the customer.</summary>
    Task<PaymentCheckout> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken);

    /// <summary>Asks the provider for a checkout's current state (used before expiring an unpaid order).</summary>
    Task<PaymentStatus> GetCheckoutStatusAsync(string checkoutId, CancellationToken cancellationToken);

    /// <summary>True if <paramref name="signature"/> authenticates <paramref name="rawBody"/> (byte-exact request body).</summary>
    bool IsSignatureValid(string rawBody, string signature);

    /// <summary>Parses a verified webhook body. Returns null for payloads this app does not handle.</summary>
    PaymentNotification? ParseNotification(string rawBody);
}

public sealed record PaymentCheckoutRequest(
    Guid OrderId,
    string OrderNumber,
    int Amount,
    string Locale,
    Uri SuccessUrl,
    Uri FailureUrl);

public sealed record PaymentCheckout(string CheckoutId, Uri PaymentUrl);

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Canceled,
    Expired,
    Unknown,
}

/// <summary>A webhook event reduced to what the app needs.</summary>
public sealed record PaymentNotification(string EventId, string CheckoutId, PaymentStatus Status, int Amount, bool LiveMode);

/// <summary>The provider could not be reached or rejected the request. Mapped to 502.</summary>
public sealed class PaymentGatewayException(string message, Exception? inner = null) : Exception(message, inner);
