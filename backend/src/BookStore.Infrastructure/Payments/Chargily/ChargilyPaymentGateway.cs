using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookStore.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookStore.Infrastructure.Payments.Chargily;

/// <summary>
/// Chargily Pay V2 (https://dev.chargily.com/pay-v2): hosted checkout for EDAHABIA and CIB cards.
/// Typed HttpClient; base address and bearer key come from <see cref="ChargilyOptions"/>.
/// </summary>
internal sealed partial class ChargilyPaymentGateway(
    HttpClient http,
    IOptions<ChargilyOptions> options,
    ILogger<ChargilyPaymentGateway> logger) : IPaymentGateway
{
    public const string ProviderName = "chargily";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ChargilyOptions _options = options.Value;

    public string Provider => ProviderName;

    public async Task<PaymentCheckout> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var body = new CreateCheckoutBody(
            Amount: request.Amount,
            Currency: "dzd",
            SuccessUrl: request.SuccessUrl.ToString(),
            FailureUrl: request.FailureUrl.ToString(),
            WebhookEndpoint: string.IsNullOrWhiteSpace(_options.WebhookUrl) ? null : _options.WebhookUrl,
            Locale: request.Locale,
            Description: $"Order {request.OrderNumber}");

        using var message = new HttpRequestMessage(HttpMethod.Post, "checkouts") { Content = JsonContent.Create(body, options: Json) };
        Authorize(message);

        using var response = await http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            LogApiError(logger, "create checkout", (int)response.StatusCode, Truncate(error));
            throw new PaymentGatewayException($"Chargily rejected the checkout ({(int)response.StatusCode}).");
        }

        var checkout = await response.Content.ReadFromJsonAsync<CheckoutResponse>(Json, cancellationToken);
        if (checkout?.Id is null || !Uri.TryCreate(checkout.CheckoutUrl, UriKind.Absolute, out var url))
        {
            throw new PaymentGatewayException("Chargily returned an unexpected checkout response.");
        }

        return new PaymentCheckout(checkout.Id, url);
    }

    public async Task<PaymentStatus> GetCheckoutStatusAsync(string checkoutId, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var message = new HttpRequestMessage(HttpMethod.Get, $"checkouts/{Uri.EscapeDataString(checkoutId)}");
        Authorize(message);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogApiError(logger, "retrieve checkout", (int)response.StatusCode, string.Empty);
                throw new PaymentGatewayException($"Chargily returned {(int)response.StatusCode} for checkout {checkoutId}.");
            }

            var checkout = await response.Content.ReadFromJsonAsync<CheckoutResponse>(Json, cancellationToken);
            return MapStatus(checkout?.Status);
        }
        catch (HttpRequestException ex)
        {
            throw new PaymentGatewayException("Chargily is unreachable.", ex);
        }
    }

    /// <summary>
    /// Chargily signs each webhook: header "signature" = hex(HMAC-SHA256(secret key, raw body)).
    /// Compared in constant time so the check leaks nothing about the expected value.
    /// </summary>
    public bool IsSignatureValid(string rawBody, string signature)
    {
        if (!_options.IsConfigured)
        {
            return false;
        }

        var expected = ComputeSignature(_options.SecretKey!, rawBody);
        var given = signature.Trim().ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(given));
    }

    public PaymentNotification? ParseNotification(string rawBody)
    {
        WebhookEvent? webhook;
        try
        {
            webhook = JsonSerializer.Deserialize<WebhookEvent>(rawBody, Json);
        }
        catch (JsonException ex)
        {
            LogUnparseable(logger, ex);
            return null;
        }

        if (webhook?.Id is null || webhook.Data?.Id is null)
        {
            return null;
        }

        // An event from the other environment (live event hitting a test deployment, or vice versa) is ignored.
        var liveMode = webhook.Livemode is { ValueKind: JsonValueKind.True }
            || (webhook.Livemode is { ValueKind: JsonValueKind.String } s && s.GetString() == "true");
        if (liveMode != _options.IsLive)
        {
            LogModeMismatch(logger, webhook.Id, liveMode);
            return null;
        }

        var status = webhook.Type switch
        {
            "checkout.paid" => PaymentStatus.Paid,
            "checkout.failed" => PaymentStatus.Failed,
            "checkout.canceled" => PaymentStatus.Canceled,
            "checkout.expired" => PaymentStatus.Expired,
            _ => PaymentStatus.Unknown,
        };
        if (status == PaymentStatus.Unknown)
        {
            return null;
        }

        var amount = webhook.Data.Amount is { ValueKind: JsonValueKind.Number } a && a.TryGetInt32(out var value) ? value : 0;
        return new PaymentNotification(webhook.Id, webhook.Data.Id, status, amount, liveMode);
    }

    /// <summary>Exposed for tests: the signature Chargily would send for <paramref name="body"/>.</summary>
    internal static string ComputeSignature(string secretKey, string body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(body)));

    internal static PaymentStatus MapStatus(string? status) => status switch
    {
        "paid" => PaymentStatus.Paid,
        "failed" => PaymentStatus.Failed,
        "canceled" => PaymentStatus.Canceled,
        "expired" => PaymentStatus.Expired,
        "pending" or "processing" => PaymentStatus.Pending,
        _ => PaymentStatus.Unknown,
    };

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new PaymentGatewayException("Chargily is not configured (Chargily:SecretKey is missing).");
        }
    }

    private void Authorize(HttpRequestMessage message) =>
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

    private static string Truncate(string text) => text.Length <= 500 ? text : text[..500];

    private sealed record CreateCheckoutBody(
        int Amount,
        string Currency,
        string SuccessUrl,
        string FailureUrl,
        string? WebhookEndpoint,
        string Locale,
        string Description);

    private sealed record CheckoutResponse(string? Id, string? Status, string? CheckoutUrl);

    private sealed record WebhookEvent(string? Id, string? Type, JsonElement? Livemode, WebhookData? Data);

    private sealed record WebhookData(string? Id, JsonElement? Amount, string? Status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Chargily {Operation} failed with {Status}: {Body}")]
    private static partial void LogApiError(ILogger logger, string operation, int status, string body);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chargily webhook body could not be parsed")]
    private static partial void LogUnparseable(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring Chargily event {EventId}: livemode={LiveMode} does not match the configured mode")]
    private static partial void LogModeMismatch(ILogger logger, string eventId, bool liveMode);
}
