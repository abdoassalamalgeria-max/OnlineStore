using System.Net;

namespace BookStore.Api.IntegrationTests;

public sealed class PipelineTests(BookStoreApiFactory factory) : IClassFixture<BookStoreApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_returns_200_without_touching_the_database()
    {
        var response = await _client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_returns_503_when_the_database_is_unreachable()
    {
        var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OpenApi_document_is_served_in_development()
    {
        var response = await _client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
