using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookStore.Application.Common;
using BookStore.Application.Features.Catalog;

namespace BookStore.Api.IntegrationTests;

/// <summary>Catalog endpoints against a real, seeded PostgreSQL (30 books, 5 categories, 58 wilayas).</summary>
public sealed class CatalogEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    private async Task<PagedResult<BookSummaryDto>> SearchAsync(string query)
    {
        var result = await _client.GetFromJsonAsync<PagedResult<BookSummaryDto>>($"/api/v1/books{query}", Json);
        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public async Task Lists_first_page_of_seeded_catalog()
    {
        var page = await SearchAsync("");

        Assert.Equal(30, page.TotalCount);
        Assert.Equal(BookQuery.DefaultPageSize, page.Items.Count);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public async Task Pages_do_not_overlap()
    {
        var first = await SearchAsync("?page=1&pageSize=10&sort=Title");
        var second = await SearchAsync("?page=2&pageSize=10&sort=Title");

        Assert.Empty(first.Items.Select(b => b.Id).Intersect(second.Items.Select(b => b.Id)));
    }

    [Theory]
    [InlineData("camus", 2)]          // author name, Latin, case-insensitive
    [InlineData("CAMUS", 2)]
    [InlineData("مستغانمي", 2)]        // author name, Arabic
    [InlineData("ذاكرة", 1)]           // title, Arabic
    [InlineData("nedj", 1)]            // partial title
    [InlineData("100%", 0)]            // LIKE wildcards are matched literally
    public async Task Searches_titles_and_author_names(string term, int expected)
    {
        var page = await SearchAsync($"?search={Uri.EscapeDataString(term)}");

        Assert.Equal(expected, page.TotalCount);
    }

    [Fact]
    public async Task Filters_by_category_author_and_language()
    {
        Assert.Equal(5, (await SearchAsync("?category=history")).TotalCount);
        Assert.Equal(2, (await SearchAsync("?author=george-orwell")).TotalCount);

        var french = await SearchAsync("?language=fr&pageSize=48");
        Assert.Equal(10, french.TotalCount);
        Assert.All(french.Items, b => Assert.Equal("fr", b.Language));
    }

    [Fact]
    public async Task Filters_by_effective_price_range_and_sorts_ascending()
    {
        var page = await SearchAsync("?minPrice=1000&maxPrice=1500&sort=PriceAsc&pageSize=48");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, b => Assert.InRange(b.EffectivePrice, 1000, 1500));
        Assert.Equal(page.Items.OrderBy(b => b.EffectivePrice).Select(b => b.Id), page.Items.Select(b => b.Id));
    }

    [Fact]
    public async Task Featured_and_in_stock_filters_apply()
    {
        var featured = await SearchAsync("?featured=true&pageSize=48");
        Assert.All(featured.Items, b => Assert.True(b.IsFeatured));
        Assert.Equal(7, featured.TotalCount);

        var inStock = await SearchAsync("?inStock=true&pageSize=48");
        Assert.All(inStock.Items, b => Assert.True(b.InStock));
        Assert.Equal(28, inStock.TotalCount);
    }

    [Fact]
    public async Task Returns_book_details_by_slug()
    {
        var book = await _client.GetFromJsonAsync<BookDetailsDto>("/api/v1/books/l-etranger", Json);

        Assert.NotNull(book);
        Assert.Equal("L'Étranger", book.Title);
        Assert.Equal("albert-camus", Assert.Single(book.Authors).Slug);
        Assert.Contains(book.Categories, c => c.Slug == "classics");
        Assert.Equal(13, book.Isbn.Length);
    }

    [Fact]
    public async Task Unknown_slug_returns_404_problem_details()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/books/no-such-book", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Invalid_query_returns_400_with_field_errors()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/books?pageSize=500&language=de", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("pageSize", out _));
        Assert.True(errors.TryGetProperty("language", out _));
    }

    [Fact]
    public async Task Reference_data_is_available()
    {
        var categories = await _client.GetFromJsonAsync<List<CategoryDto>>("/api/v1/categories", Json);
        var authors = await _client.GetFromJsonAsync<List<AuthorDto>>("/api/v1/authors", Json);
        var wilayas = await _client.GetFromJsonAsync<List<WilayaDto>>("/api/v1/wilayas", Json);

        Assert.Equal(5, categories!.Count);
        Assert.Equal(24, authors!.Count);
        Assert.Equal(58, wilayas!.Count);
        Assert.Equal("الجزائر", wilayas.Single(w => w.Code == 16).NameAr);
    }

    [Fact]
    public async Task Readiness_is_healthy_with_a_database()
    {
        var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
