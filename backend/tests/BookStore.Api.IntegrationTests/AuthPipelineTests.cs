using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BookStore.Api.IntegrationTests;

/// <summary>Authentication/authorization wiring that can be checked without a database.</summary>
public sealed class AuthPipelineTests(BookStoreApiFactory factory) : IClassFixture<BookStoreApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpResponseMessage> GetAsync(string url, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    [Theory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/admin/overview")]
    public async Task Protected_endpoints_return_401_problem_without_a_token(string url)
    {
        var response = await GetAsync(url, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var forged = TestTokens.Create("Admin", signingKey: "a-completely-different-key-of-sufficient-length!!");

        var response = await GetAsync("/api/v1/admin/overview", forged);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var expired = TestTokens.Create("Admin", expires: DateTime.UtcNow.AddMinutes(-5));

        var response = await GetAsync("/api/v1/admin/overview", expired);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_is_forbidden_from_admin_endpoints()
    {
        var response = await GetAsync("/api/v1/admin/overview", TestTokens.Create("Customer"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Register_validates_input_before_touching_the_database()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "", email = "not-an-email", password = "short", phoneNumber = "12345" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"email\"", body, StringComparison.Ordinal);
        Assert.Contains("\"password\"", body, StringComparison.Ordinal);
        Assert.Contains("\"phoneNumber\"", body, StringComparison.Ordinal);
        Assert.Contains("\"fullName\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_document_declares_the_bearer_scheme()
    {
        var json = await _client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Contains("\"bearer\"", json, StringComparison.Ordinal);
    }
}
