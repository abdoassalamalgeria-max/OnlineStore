using BookStore.Application.Abstractions;
using BookStore.Application.Features.Ordering;
using Microsoft.Extensions.Logging;

namespace BookStore.Application.Features.Payments;

/// <summary>Webhook request rejected before processing: 400 (no signature) or 403 (bad signature).</summary>
public sealed class WebhookRejectedException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public interface IPaymentWebhookService
{
    Task<NotificationOutcome> HandleAsync(string rawBody, string? signature, CancellationToken cancellationToken);
}

/// <summary>
/// Entry point for provider webhooks. Authenticity first (signature over the raw body), then parsing,
/// then <see cref="IOrderLifecycleService"/> applies it exactly once. The success page never marks
/// orders as paid; only this path (or the expiry job's status poll) does.
/// </summary>
public sealed partial class PaymentWebhookService(
    IPaymentGateway gateway,
    IOrderLifecycleService lifecycle,
    ILogger<PaymentWebhookService> logger) : IPaymentWebhookService
{
    public async Task<NotificationOutcome> HandleAsync(string rawBody, string? signature, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            throw new WebhookRejectedException(400, "Missing signature header.");
        }

        if (!gateway.IsSignatureValid(rawBody, signature))
        {
            LogBadSignature(logger);
            throw new WebhookRejectedException(403, "Invalid signature.");
        }

        var notification = gateway.ParseNotification(rawBody);
        if (notification is null)
        {
            return NotificationOutcome.Ignored;
        }

        var outcome = await lifecycle.ApplyPaymentNotificationAsync(notification, cancellationToken);
        LogHandled(logger, notification.EventId, notification.Status, outcome);
        return outcome;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected payment webhook with an invalid signature")]
    private static partial void LogBadSignature(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Payment webhook {EventId} ({Status}): {Outcome}")]
    private static partial void LogHandled(ILogger logger, string eventId, PaymentStatus status, NotificationOutcome outcome);
}
