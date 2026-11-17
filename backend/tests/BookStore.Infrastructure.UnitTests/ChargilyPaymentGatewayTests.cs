using System.Net;
using System.Text;
using System.Text.Json;
using BookStore.Application.Abstractions;
using BookStore.Infrastructure.Payments.Chargily;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BookStore.Infrastructure.UnitTests;

public sealed class ChargilyPaymentGatewayTests
{
    private const string Secret = "test_sk_unit_test_secret_key";

    /// <summary>Captures the outgoing request and answers with a canned response.</summary>
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static ChargilyPaymentGateway Gateway(
        StubHandler? handler = null,
        string mode = "test",
        string? secret = Secret,
        string? webhookUrl = "https://hooks.example.dz/api/v1/payments/webhooks/chargily")
    {
        var options = new ChargilyOptions { Mode = mode, SecretKey = secret, WebhookUrl = webhookUrl };
        var http = new HttpClient(handler ?? new StubHandler(HttpStatusCode.OK, "{}")) { BaseAddress = new Uri(options.BaseUrl) };
        return new ChargilyPaymentGateway(http, Options.Create(options), NullLogger<ChargilyPaymentGateway>.Instance);
    }

    private static PaymentCheckoutRequest CheckoutRequest() => new(
        Guid.NewGuid(), "BK-20261116-ABC234", 3100, "ar",
        new Uri("http://localhost:3000/ar/checkout/success?order=1"),
        new Uri("http://localhost:3000/ar/checkout/failure?order=1"));

    private static string Event(string type = "checkout.paid", bool livemode = false, string checkoutId = "01hj5n17yqywe2afsrb8gz8xyz", int amount = 3100) =>
        JsonSerializer.Serialize(new
        {
            id = "01hj5n17yqywe2afsrb8gz8evt",
            entity = "event",
            livemode,
            type,
            data = new { id = checkoutId, entity = "checkout", amount, status = "paid" },
        });

    // ---------------- Signature ----------------

    [Fact]
    public void Accepts_a_correct_signature()
    {
        var body = Event();
        Assert.True(Gateway().IsSignatureValid(body, ChargilyPaymentGateway.ComputeSignature(Secret, body)));
    }

    [Fact]
    public void Signature_comparison_ignores_hex_case_and_whitespace()
    {
        var body = Event();
        var signature = " " + ChargilyPaymentGateway.ComputeSignature(Secret, body).ToUpperInvariant() + " ";
        Assert.True(Gateway().IsSignatureValid(body, signature));
    }

    [Fact]
    public void Rejects_a_signature_made_with_another_key()
    {
        var body = Event();
        Assert.False(Gateway().IsSignatureValid(body, ChargilyPaymentGateway.ComputeSignature("test_sk_other", body)));
    }

    [Fact]
    public void Rejects_a_tampered_body()
    {
        var body = Event(amount: 3100);
        var signature = ChargilyPaymentGateway.ComputeSignature(Secret, body);
        Assert.False(Gateway().IsSignatureValid(Event(amount: 1), signature));
    }

    [Fact]
    public void Signature_is_over_the_exact_bytes_so_reformatted_json_fails()
    {
        var body = Event();
        var signature = ChargilyPaymentGateway.ComputeSignature(Secret, body);
        var reformatted = JsonSerializer.Serialize(JsonDocument.Parse(body).RootElement, new JsonSerializerOptions { WriteIndented = true });
        Assert.False(Gateway().IsSignatureValid(reformatted, signature));
    }

    [Fact]
    public void Nothing_validates_when_no_secret_is_configured()
    {
        var body = Event();
        Assert.False(Gateway(secret: null).IsSignatureValid(body, ChargilyPaymentGateway.ComputeSignature(Secret, body)));
    }

    [Fact]
    public void Signature_matches_a_known_hmac_sha256_vector()
    {
        // HMAC-SHA256(key="key", "The quick brown fox jumps over the lazy dog") — RFC 4231-style reference value.
        Assert.Equal(
            "f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8",
            ChargilyPaymentGateway.ComputeSignature("key", "The quick brown fox jumps over the lazy dog"));
    }

    // ---------------- Webhook parsing ----------------

    [Theory]
    [InlineData("checkout.paid", PaymentStatus.Paid)]
    [InlineData("checkout.failed", PaymentStatus.Failed)]
    [InlineData("checkout.canceled", PaymentStatus.Canceled)]
    [InlineData("checkout.expired", PaymentStatus.Expired)]
    public void Parses_checkout_events(string type, PaymentStatus expected)
    {
        var notification = Gateway().ParseNotification(Event(type));

        Assert.NotNull(notification);
        Assert.Equal(expected, notification.Status);
        Assert.Equal("01hj5n17yqywe2afsrb8gz8evt", notification.EventId);
        Assert.Equal("01hj5n17yqywe2afsrb8gz8xyz", notification.CheckoutId);
        Assert.Equal(3100, notification.Amount);
    }

    [Fact]
    public void Ignores_unknown_event_types() => Assert.Null(Gateway().ParseNotification(Event("customer.created")));

    [Fact]
    public void Ignores_live_events_in_test_mode() => Assert.Null(Gateway(mode: "test").ParseNotification(Event(livemode: true)));

    [Fact]
    public void Ignores_test_events_in_live_mode() =>
        Assert.Null(Gateway(mode: "live", secret: "live_sk_x").ParseNotification(Event(livemode: false)));

    [Fact]
    public void Ignores_malformed_json() => Assert.Null(Gateway().ParseNotification("{not json"));

    // ---------------- Checkout creation ----------------

    [Fact]
    public async Task Creates_a_checkout_with_the_documented_fields()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"id":"01hj5n17","status":"pending","checkout_url":"https://pay.chargily.dz/test/checkouts/01hj5n17/pay"}""");

        var checkout = await Gateway(handler).CreateCheckoutAsync(CheckoutRequest(), CancellationToken.None);

        Assert.Equal("01hj5n17", checkout.CheckoutId);
        Assert.Equal("https://pay.chargily.dz/test/checkouts/01hj5n17/pay", checkout.PaymentUrl.ToString());

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://pay.chargily.net/test/api/v2/checkouts", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal(Secret, handler.Request.Headers.Authorization.Parameter);

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var root = body.RootElement;
        Assert.Equal(3100, root.GetProperty("amount").GetInt32());
        Assert.Equal("dzd", root.GetProperty("currency").GetString());
        Assert.Equal("ar", root.GetProperty("locale").GetString());
        Assert.StartsWith("http://localhost:3000/ar/checkout/success", root.GetProperty("success_url").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("http://localhost:3000/ar/checkout/failure", root.GetProperty("failure_url").GetString(), StringComparison.Ordinal);
        Assert.Equal("https://hooks.example.dz/api/v1/payments/webhooks/chargily", root.GetProperty("webhook_endpoint").GetString());
        Assert.Contains("BK-20261116-ABC234", root.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Omits_webhook_endpoint_when_not_configured()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"x","checkout_url":"https://pay.chargily.dz/x"}""");

        await Gateway(handler, webhookUrl: null).CreateCheckoutAsync(CheckoutRequest(), CancellationToken.None);

        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.False(body.RootElement.TryGetProperty("webhook_endpoint", out _));
    }

    [Fact]
    public async Task Live_mode_uses_the_live_base_url()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"id":"x","checkout_url":"https://pay.chargily.dz/x"}""");

        await Gateway(handler, mode: "live", secret: "live_sk_x").CreateCheckoutAsync(CheckoutRequest(), CancellationToken.None);

        Assert.Equal("https://pay.chargily.net/api/v2/checkouts", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Api_errors_become_payment_gateway_exceptions()
    {
        var handler = new StubHandler(HttpStatusCode.UnprocessableEntity, """{"message":"The amount must be at least 75."}""");

        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            Gateway(handler).CreateCheckoutAsync(CheckoutRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Refuses_to_call_chargily_without_a_secret_key() =>
        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            Gateway(secret: null).CreateCheckoutAsync(CheckoutRequest(), CancellationToken.None));

    [Theory]
    [InlineData("paid", PaymentStatus.Paid)]
    [InlineData("pending", PaymentStatus.Pending)]
    [InlineData("processing", PaymentStatus.Pending)]
    [InlineData("failed", PaymentStatus.Failed)]
    [InlineData("canceled", PaymentStatus.Canceled)]
    [InlineData("expired", PaymentStatus.Expired)]
    [InlineData("something-new", PaymentStatus.Unknown)]
    public async Task Reads_checkout_status(string status, PaymentStatus expected)
    {
        var handler = new StubHandler(HttpStatusCode.OK, $$"""{"id":"abc","status":"{{status}}"}""");

        Assert.Equal(expected, await Gateway(handler).GetCheckoutStatusAsync("abc", CancellationToken.None));
        Assert.Equal("https://pay.chargily.net/test/api/v2/checkouts/abc", handler.Request!.RequestUri!.ToString());
    }
}

public sealed class ChargilyOptionsValidatorTests
{
    private static bool IsValid(string mode, string? key) =>
        new ChargilyOptionsValidator().Validate(null, new ChargilyOptions { Mode = mode, SecretKey = key }).Succeeded;

    [Fact]
    public void Test_key_in_test_mode_is_valid() => Assert.True(IsValid("test", "test_sk_abc"));

    [Fact]
    public void Live_key_in_live_mode_is_valid() => Assert.True(IsValid("live", "live_sk_abc"));

    [Fact]
    public void Live_key_in_test_mode_is_rejected() => Assert.False(IsValid("test", "live_sk_abc"));

    [Fact]
    public void Test_key_in_live_mode_is_rejected() => Assert.False(IsValid("live", "test_sk_abc"));

    [Fact]
    public void No_key_is_allowed_payments_are_just_disabled() => Assert.True(IsValid("test", null));

    [Fact]
    public void Unknown_mode_is_rejected() => Assert.False(IsValid("prod", "live_sk_abc"));
}
