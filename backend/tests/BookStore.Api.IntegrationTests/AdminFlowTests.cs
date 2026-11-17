using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookStore.Application.Common;
using BookStore.Application.Features.Admin;
using BookStore.Application.Features.Auth;
using BookStore.Application.Features.Cart;
using BookStore.Application.Features.Catalog;
using BookStore.Application.Features.Ordering;
using BookStore.Domain.Ordering;
using BookStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace BookStore.Api.IntegrationTests;

/// <summary>Admin back-office against PostgreSQL: catalog CRUD, cover upload, orders workflow, wilayas, reviews.</summary>
public sealed class AdminFlowTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(TestSettings.AdminEmail, TestSettings.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> CustomerAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Client", $"a-{Guid.NewGuid():N}@test.dz", "Secret123", null));
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<(Guid AuthorId, Guid CategoryId)> AnyAuthorAndCategoryAsync()
    {
        var anon = factory.CreateClient();
        var author = (await anon.GetFromJsonAsync<List<AuthorDto>>("/api/v1/authors", Json))![0];
        var category = (await anon.GetFromJsonAsync<List<CategoryDto>>("/api/v1/categories", Json))![0];
        return (author.Id, category.Id);
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];

    /// <summary>A valid ISBN-13 that no other test uses (979 prefix + random body + check digit).</summary>
    private static string UniqueIsbn()
    {
        var body = "979" + Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var sum = body.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        return body + ((10 - (sum % 10)) % 10);
    }

    private async Task<BookUpsertRequest> NewBookRequestAsync(string? slug = null)
    {
        var (authorId, categoryId) = await AnyAuthorAndCategoryAsync();
        return new BookUpsertRequest("Le Livre Test", slug ?? Unique("livre"), "Description.", UniqueIsbn(), 2000, 1800, 7, 200, "fr",
            new DateOnly(2020, 5, 1), "Éditions Test", true, [authorId], [categoryId]);
    }

    private async Task<AdminBookDto> CreateBookAsync(HttpClient admin, BookUpsertRequest? request = null)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/books", request ?? await NewBookRequestAsync());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AdminBookDto>(Json))!;
    }

    private async Task<(Guid OrderId, HttpClient Customer)> PaidOrderAsync(Guid bookId, int quantity = 1)
    {
        var customer = await CustomerAsync();
        await customer.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(bookId, quantity));
        var checkout = await (await customer.PostAsJsonAsync("/api/v1/checkout",
            new CheckoutRequest(new ShippingAddressRequest("Client", "0550123456", 16, "Hydra", "Rue 1"), "ar")))
            .Content.ReadFromJsonAsync<CheckoutResponse>(Json);

        string checkoutId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            checkoutId = await db.Orders.Where(o => o.Id == checkout!.OrderId).Select(o => o.PaymentCheckoutId!).SingleAsync();
        }

        var body = JsonSerializer.Serialize(new { id = $"evt_{checkoutId}", livemode = false, type = "checkout.paid", data = new { id = checkoutId, amount = checkout!.Total } });
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/payments/webhooks/chargily", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("signature", TestSettings.Sign(body));
        (await factory.CreateClient().SendAsync(request)).EnsureSuccessStatusCode();
        return (checkout.OrderId, customer);
    }

    private async Task<int> StockAsync(Guid bookId) =>
        (await (await AdminAsync()).GetFromJsonAsync<AdminBookDto>($"/api/v1/admin/books/{bookId}", Json))!.Stock;

    private static async Task<AdminOrderDto> ActAsync(HttpClient admin, Guid orderId, AdminOrderAction action, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/actions", new AdminOrderActionRequest(action, null), Json);
        Assert.Equal(expected, response.StatusCode);
        return expected == HttpStatusCode.OK ? (await response.Content.ReadFromJsonAsync<AdminOrderDto>(Json))! : null!;
    }

    // ---------------- access ----------------

    [Theory]
    [InlineData("GET", "/api/v1/admin/books")]
    [InlineData("GET", "/api/v1/admin/orders")]
    [InlineData("GET", "/api/v1/admin/reviews")]
    [InlineData("POST", "/api/v1/admin/authors")]
    [InlineData("PUT", "/api/v1/admin/wilayas/16")]
    public async Task Customers_cannot_use_admin_endpoints(string method, string url)
    {
        var customer = await CustomerAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(url, UriKind.Relative))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.SendAsync(request)).StatusCode);
    }

    // ---------------- books ----------------

    [Fact]
    public async Task Book_create_update_and_it_appears_in_the_public_catalog()
    {
        var admin = await AdminAsync();
        var request = await NewBookRequestAsync();
        var created = await CreateBookAsync(admin, request);

        Assert.Equal(CatalogRules.NormalizeIsbn(request.Isbn), created.Isbn);
        Assert.False(created.HasOrders);

        var updated = await (await admin.PutAsJsonAsync($"/api/v1/admin/books/{created.Id}",
            request with { Title = "Titre modifié", Price = 2500, SalePrice = null, Stock = 3 })).Content.ReadFromJsonAsync<AdminBookDto>(Json);
        Assert.Equal("Titre modifié", updated!.Title);
        Assert.Null(updated.SalePrice);

        var publicBook = await factory.CreateClient().GetFromJsonAsync<BookDetailsDto>($"/api/v1/books/{request.Slug}", Json);
        Assert.Equal("Titre modifié", publicBook!.Title);
        Assert.Equal(2500, publicBook.EffectivePrice);
        Assert.Equal(3, publicBook.Stock);
    }

    [Fact]
    public async Task Duplicate_slug_or_isbn_is_a_field_error()
    {
        var admin = await AdminAsync();
        var first = await NewBookRequestAsync();
        await CreateBookAsync(admin, first);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/books", (await NewBookRequestAsync(first.Slug)) with { Isbn = first.Isbn });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"slug\"", body, StringComparison.Ordinal);
        Assert.Contains("\"isbn\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Never_ordered_books_can_be_deleted_ordered_ones_cannot()
    {
        var admin = await AdminAsync();
        var unsold = await CreateBookAsync(admin);
        var sold = await CreateBookAsync(admin);
        await PaidOrderAsync(sold.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/v1/admin/books/{unsold.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync(new Uri($"/api/v1/admin/books/{sold.Id}", UriKind.Relative))).StatusCode);
        Assert.True((await admin.GetFromJsonAsync<AdminBookDto>($"/api/v1/admin/books/{sold.Id}", Json))!.HasOrders);
    }

    [Fact]
    public async Task Cover_upload_is_resized_to_webp_and_served()
    {
        var admin = await AdminAsync();
        var book = await CreateBookAsync(admin);

        using var bitmap = new SKBitmap(1000, 1500);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Teal);
        }

        using var png = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(png.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "cover.png");

        var response = await admin.PutAsync(new Uri($"/api/v1/admin/books/{book.Id}/cover", UriKind.Relative), form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<AdminBookDto>(Json))!;
        Assert.EndsWith(".webp", updated.CoverImageUrl, StringComparison.Ordinal);

        var served = await factory.CreateClient().GetAsync(new Uri(new Uri(updated.CoverImageUrl!).PathAndQuery, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        using var codec = SKCodec.Create(SKData.CreateCopy(await served.Content.ReadAsByteArrayAsync()));
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        Assert.Equal(600, codec.Info.Width);
    }

    [Fact]
    public async Task Fake_images_are_rejected()
    {
        var admin = await AdminAsync();
        var book = await CreateBookAsync(admin);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("<script>alert(1)</script>"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); // lying content type
        form.Add(file, "file", "cover.png");

        var response = await admin.PutAsync(new Uri($"/api/v1/admin/books/{book.Id}/cover", UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------- authors & categories ----------------

    [Fact]
    public async Task Authors_and_categories_crud_with_in_use_protection()
    {
        var admin = await AdminAsync();
        var author = await (await admin.PostAsJsonAsync("/api/v1/admin/authors", new AuthorUpsertRequest("Rachid Boudjedra", Unique("rachid"), null)))
            .Content.ReadFromJsonAsync<AuthorDto>(Json);
        var category = await (await admin.PostAsJsonAsync("/api/v1/admin/categories", new CategoryUpsertRequest("شعر", "Poésie", "Poetry", Unique("poesie"))))
            .Content.ReadFromJsonAsync<CategoryDto>(Json);

        var renamed = await (await admin.PutAsJsonAsync($"/api/v1/admin/authors/{author!.Id}", new AuthorUpsertRequest("R. Boudjedra", author.Slug, "Romancier")))
            .Content.ReadFromJsonAsync<AuthorDto>(Json);
        Assert.Equal("R. Boudjedra", renamed!.Name);

        var book = await CreateBookAsync(admin, (await NewBookRequestAsync()) with { AuthorIds = [author.Id], CategoryIds = [category!.Id] });
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync(new Uri($"/api/v1/admin/authors/{author.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync(new Uri($"/api/v1/admin/categories/{category.Id}", UriKind.Relative))).StatusCode);

        await admin.DeleteAsync(new Uri($"/api/v1/admin/books/{book.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/v1/admin/authors/{author.Id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/v1/admin/categories/{category.Id}", UriKind.Relative))).StatusCode);
    }

    // ---------------- orders ----------------

    [Fact]
    public async Task Paid_order_ships_and_delivers_and_shows_up_in_the_admin_list()
    {
        var admin = await AdminAsync();
        var book = await CreateBookAsync(admin);
        var (orderId, _) = await PaidOrderAsync(book.Id);

        var list = await admin.GetFromJsonAsync<PagedResult<AdminOrderListItemDto>>("/api/v1/admin/orders?status=Paid&pageSize=100", Json);
        Assert.Contains(list!.Items, o => o.Id == orderId);

        Assert.Equal(OrderStatus.Shipped, (await ActAsync(admin, orderId, AdminOrderAction.Ship)).Status);
        var delivered = await ActAsync(admin, orderId, AdminOrderAction.Deliver);
        Assert.Equal(OrderStatus.Delivered, delivered.Status);
        Assert.NotNull(delivered.DeliveredAt);

        await ActAsync(admin, orderId, AdminOrderAction.Cancel, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_cancellation_returns_the_stock()
    {
        var admin = await AdminAsync();
        var book = await CreateBookAsync(admin); // stock 7
        var (orderId, _) = await PaidOrderAsync(book.Id, quantity: 2);
        Assert.Equal(5, await StockAsync(book.Id));

        var cancelled = await ActAsync(admin, orderId, AdminOrderAction.Cancel);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(7, await StockAsync(book.Id));
    }

    [Fact]
    public async Task Pending_orders_cannot_be_shipped()
    {
        var admin = await AdminAsync();
        var book = await CreateBookAsync(admin);
        var customer = await CustomerAsync();
        await customer.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book.Id, 1));
        var checkout = await (await customer.PostAsJsonAsync("/api/v1/checkout",
            new CheckoutRequest(new ShippingAddressRequest("Client", "0550123456", 16, "Hydra", "Rue 1"), "ar"))).Content.ReadFromJsonAsync<CheckoutResponse>(Json);

        await ActAsync(admin, checkout!.OrderId, AdminOrderAction.Ship, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Orders_can_be_searched_by_order_number()
    {
        var admin = await AdminAsync();
        var book = await CreateBookAsync(admin);
        var (orderId, _) = await PaidOrderAsync(book.Id);
        var number = (await admin.GetFromJsonAsync<AdminOrderDto>($"/api/v1/admin/orders/{orderId}", Json))!.OrderNumber;

        var found = await admin.GetFromJsonAsync<PagedResult<AdminOrderListItemDto>>($"/api/v1/admin/orders?search={number.ToLowerInvariant()}", Json);

        Assert.Equal(orderId, Assert.Single(found!.Items).Id);
    }

    // ---------------- wilayas, reviews, overview ----------------

    [Fact]
    public async Task Wilaya_fee_change_applies_to_new_checkouts_and_disabled_wilayas_are_refused()
    {
        var admin = await AdminAsync();
        await admin.PutAsJsonAsync("/api/v1/admin/wilayas/11", new WilayaUpdateRequest(1500, true));
        await admin.PutAsJsonAsync("/api/v1/admin/wilayas/54", new WilayaUpdateRequest(2000, false));
        var book = await CreateBookAsync(admin);
        var customer = await CustomerAsync();
        await customer.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book.Id, 1));

        var refused = await customer.PostAsJsonAsync("/api/v1/checkout",
            new CheckoutRequest(new ShippingAddressRequest("Client", "0550123456", 54, "In Guezzam", "Rue 1"), "ar"));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var checkout = await (await customer.PostAsJsonAsync("/api/v1/checkout",
            new CheckoutRequest(new ShippingAddressRequest("Client", "0550123456", 11, "Tamanrasset", "Rue 1"), "ar"))).Content.ReadFromJsonAsync<CheckoutResponse>(Json);
        var order = await customer.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{checkout!.OrderId}", Json);
        Assert.Equal(1500, order!.ShippingFee);
    }

    [Fact]
    public async Task Admin_can_delete_a_review()
    {
        var admin = await AdminAsync();
        var book = await CreateBookAsync(admin);
        var (_, customer) = await PaidOrderAsync(book.Id);
        var slug = (await admin.GetFromJsonAsync<AdminBookDto>($"/api/v1/admin/books/{book.Id}", Json))!.Slug;
        await customer.PutAsJsonAsync($"/api/v1/books/{slug}/reviews/mine", new BookStore.Application.Features.Reviews.UpsertReviewRequest(1, "spam"));

        var reviews = await admin.GetFromJsonAsync<PagedResult<AdminReviewDto>>("/api/v1/admin/reviews?pageSize=100", Json);
        var review = reviews!.Items.Single(r => r.BookId == book.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/v1/admin/reviews/{review.Id}", UriKind.Relative))).StatusCode);

        var after = await factory.CreateClient().GetFromJsonAsync<BookStore.Application.Features.Reviews.BookReviewsDto>($"/api/v1/books/{slug}/reviews", Json);
        Assert.Equal(0, after!.Summary.Count);
    }

    [Fact]
    public async Task Overview_reports_revenue_and_orders_to_ship()
    {
        var admin = await AdminAsync();
        var before = await admin.GetFromJsonAsync<AdminOverviewDto>("/api/v1/admin/overview", Json);
        var book = await CreateBookAsync(admin);
        await PaidOrderAsync(book.Id);

        var after = await admin.GetFromJsonAsync<AdminOverviewDto>("/api/v1/admin/overview", Json);

        Assert.Equal(before!.ToShip + 1, after!.ToShip);
        Assert.Equal(before.Revenue + 1800 + 600, after.Revenue); // sale price + default fee (wilaya 16)
        Assert.True(after.OrdersByStatus["Paid"] >= 1);
    }
}
