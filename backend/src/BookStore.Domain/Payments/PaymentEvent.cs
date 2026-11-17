using BookStore.Domain.Common;

namespace BookStore.Domain.Payments;

/// <summary>
/// A payment-provider notification we have processed. The unique (Provider, ProviderEventId) index is
/// what makes webhook handling idempotent: a redelivered event is detected and skipped.
/// </summary>
public sealed class PaymentEvent : Entity
{
    private PaymentEvent()
    {
        Provider = ProviderEventId = Type = string.Empty;
    }

    public PaymentEvent(string provider, string providerEventId, string type, Guid? orderId, DateTimeOffset receivedAt)
    {
        Provider = provider;
        ProviderEventId = providerEventId;
        Type = type;
        OrderId = orderId;
        ReceivedAt = receivedAt;
    }

    public string Provider { get; private set; }
    public string ProviderEventId { get; private set; }
    public string Type { get; private set; }
    public Guid? OrderId { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
}
