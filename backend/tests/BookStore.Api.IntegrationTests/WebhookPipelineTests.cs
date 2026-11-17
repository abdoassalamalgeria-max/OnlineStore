using System.Net;
using System.Text;

namespace BookStore.Api.IntegrationTests;

/// <summary>Webhook authentication happens before any database access, so it is testable without one.</summary>
public sealed class WebhookPipelineTests(BookStoreApiFactory factory) : IClassFixture<BookStoreApiFactory>
{
    private const string Url = "/api/v1/payments/webhooks/chargily";
    private const string Body = """{"id":"evt_1","entity":"event","livemode":false,"type":"checkout.paid","data":{"id":"chk_1","amount":1000}}""";

    private readonly HttpClient _client = factory.CreateClient();

    private Task<HttpResponseMessage> PostAsync(string body, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Url, UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (signature is not null)
        {
            request.Headers.Add("signature", signature);
        }

        return _client.SendAsync(request);
    }

    [Fact]
    public async Task Missing_signature_is_rejected_with_400()
    {
        var response = await PostAsync(Body, signature: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Wrong_signature_is_rejected_with_403()
    {
        var response = await PostAsync(Body, signature: new string('a', 64));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Signature_of_a_different_body_is_rejected_with_403()
    {
        var response = await PostAsync(Body.Replace("1000", "1", StringComparison.Ordinal), TestSettings.Sign(Body));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_requires_authentication()
    {
        var response = await _client.PostAsync(new Uri("/api/v1/checkout", UriKind.Relative),
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
