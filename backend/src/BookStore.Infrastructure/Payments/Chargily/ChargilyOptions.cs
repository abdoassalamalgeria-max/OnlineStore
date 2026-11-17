using Microsoft.Extensions.Options;

namespace BookStore.Infrastructure.Payments.Chargily;

/// <summary>
/// Bound from "Chargily". The secret key comes from user-secrets / env (Chargily__SecretKey) only.
/// Test and live are fully separate: different base URL and keys, selected by <see cref="Mode"/>.
/// </summary>
public sealed class ChargilyOptions
{
    public const string Section = "Chargily";
    public const string TestBaseUrl = "https://pay.chargily.net/test/api/v2/";
    public const string LiveBaseUrl = "https://pay.chargily.net/api/v2/";

    /// <summary>"test" or "live".</summary>
    public string Mode { get; set; } = "test";

    /// <summary>API secret key: test_sk_... in test mode, live_sk_... in live mode. Also signs webhooks.</summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Public URL of POST /api/v1/payments/webhooks/chargily (e.g. a cloudflared tunnel in development).
    /// Sent with each checkout; when empty, the webhook configured in the Chargily dashboard is used.
    /// </summary>
    public string? WebhookUrl { get; set; }

    public bool IsLive => string.Equals(Mode, "live", StringComparison.OrdinalIgnoreCase);
    public string BaseUrl => IsLive ? LiveBaseUrl : TestBaseUrl;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}

/// <summary>Refuses to start with a live key in test mode (or the reverse), the classic costly mix-up.</summary>
internal sealed class ChargilyOptionsValidator : IValidateOptions<ChargilyOptions>
{
    public ValidateOptionsResult Validate(string? name, ChargilyOptions options)
    {
        if (options.Mode is not ("test" or "live"))
        {
            return ValidateOptionsResult.Fail("Chargily:Mode must be 'test' or 'live'.");
        }

        if (!options.IsConfigured)
        {
            return ValidateOptionsResult.Success; // payments disabled; catalog still works
        }

        var expectedPrefix = options.IsLive ? "live_sk_" : "test_sk_";
        return options.SecretKey!.StartsWith(expectedPrefix, StringComparison.Ordinal)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Chargily:Mode is '{options.Mode}' but the secret key does not start with '{expectedPrefix}'.");
    }
}
