using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookStore.Application.Features.Auth;
using BookStore.Application.Features.Cart;
using BookStore.Application.Features.Catalog;
using BookStore.Application.Features.Ordering;
using BookStore.Domain.Ordering;
using BookStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Api.IntegrationTests;

/// <summary>
/// Cart → checkout → Chargily webhook, end to end against PostgreSQL, with Chargily faked in memory.
/// Each test registers its own customer and uses its own book so tests stay independent.
/// </summary>
public sealed class CheckoutFlowTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static readonly ShippingAddressRequest Address = new("Amine Benali", "0550123456", 16, "Bab Ezzouar", "Cité 1, Bt 3");

    // ---------------- helpers ----------------

    private async Task<HttpClient> CustomerAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Client Test", $"c-{Guid.NewGuid():N}@test.dz", "Secret123", null));
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    /// <summary>Gives a seeded book a known stock and returns its id.</summary>
    private async Task<Guid> BookWithStockAsync(string slug, int stock)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Books.Where(b => b.Slug == slug).ExecuteUpdateAsync(s => s.SetProperty(b => b.Stock, stock));
        return await db.Books.Where(b => b.Slug == slug).Select(b => b.Id).SingleAsync();
    }

    private async Task<int> StockOfAsync(Guid bookId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Books.Where(b => b.Id == bookId).Select(b => b.Stock).SingleAsync();
    }

    private async Task<string> CheckoutIdOfAsync(Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.Where(o => o.Id == orderId).Select(o => o.PaymentCheckoutId!).SingleAsync();
    }

    private static async Task<CheckoutResponse> CheckoutAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/checkout", new CheckoutRequest(Address, "ar"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CheckoutResponse>(Json))!;
    }

    private static async Task<OrderDto> OrderAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{id}", Json))!;

    private static async Task<CartDto> CartAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<CartDto>("/api/v1/cart", Json))!;

    private async Task<HttpResponseMessage> WebhookAsync(string eventId, string type, string checkoutId, int amount)
    {
        var body = JsonSerializer.Serialize(new
        {
            id = eventId,
            entity = "event",
            livemode = false,
            type,
            data = new { id = checkoutId, entity = "checkout", amount, status = type.Split('.')[1] },
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/payments/webhooks/chargily", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("signature", TestSettings.Sign(body));
        return await factory.CreateClient().SendAsync(request);
    }

    // ---------------- cart ----------------

    [Fact]
    public async Task Cart_add_update_remove()
    {
        var client = await CustomerAsync();
        var book = await BookWithStockAsync("animal-farm", 10);

        var added = await (await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 2))).Content.ReadFromJsonAsync<CartDto>(Json);
        Assert.Equal(2, Assert.Single(added!.Items).Quantity);

        var updated = await (await client.PutAsJsonAsync($"/api/v1/cart/items/{book}", new UpdateCartLineRequest(5))).Content.ReadFromJsonAsync<CartDto>(Json);
        Assert.Equal(5, updated!.ItemCount);
        Assert.Equal(updated.Items[0].UnitPrice * 5, updated.Subtotal);

        var removed = await (await client.DeleteAsync(new Uri($"/api/v1/cart/items/{book}", UriKind.Relative))).Content.ReadFromJsonAsync<CartDto>(Json);
        Assert.Empty(removed!.Items);
    }

    [Fact]
    public async Task Guest_cart_preview_and_merge_at_sign_in()
    {
        var book = await BookWithStockAsync("the-old-man-and-the-sea", 10);
        var guest = new GuestCartRequest([new CartLineRequest(book, 2), new CartLineRequest(book, 1), new CartLineRequest(Guid.NewGuid(), 1)]);

        var preview = await (await factory.CreateClient().PostAsJsonAsync("/api/v1/cart/preview", guest)).Content.ReadFromJsonAsync<CartDto>(Json);
        Assert.Equal(3, Assert.Single(preview!.Items).Quantity); // duplicates merged, unknown book dropped

        var client = await CustomerAsync();
        await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 1));
        var merged = await (await client.PostAsJsonAsync("/api/v1/cart/merge", guest)).Content.ReadFromJsonAsync<CartDto>(Json);
        Assert.Equal(4, Assert.Single(merged!.Items).Quantity);
    }

    // ---------------- checkout & webhook ----------------

    [Fact]
    public async Task Checkout_reserves_stock_empties_cart_and_returns_payment_url()
    {
        var client = await CustomerAsync();
        var book = await BookWithStockAsync("nedjma", 10);
        await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 3));

        var checkout = await CheckoutAsync(client);

        Assert.StartsWith("https://pay.chargily.test/checkouts/", checkout.PaymentUrl, StringComparison.Ordinal);
        Assert.Equal(7, await StockOfAsync(book));
        Assert.Empty((await CartAsync(client)).Items);

        var order = await OrderAsync(client, checkout.OrderId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(order.Subtotal + 600, order.Total); // default wilaya fee
        Assert.Equal(checkout.PaymentUrl, order.PaymentUrl);
        Assert.Equal("الجزائر", order.ShippingAddress.WilayaName);
    }

    [Fact]
    public async Task Paid_webhook_marks_the_order_paid_exactly_once()
    {
        var client = await CustomerAsync();
        var book = await BookWithStockAsync("la-grande-maison", 5);
        await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 1));
        var checkout = await CheckoutAsync(client);
        var checkoutId = await CheckoutIdOfAsync(checkout.OrderId);

        var first = await WebhookAsync("evt_paid_" + checkoutId, "checkout.paid", checkoutId, checkout.Total);
        var again = await WebhookAsync("evt_paid_" + checkoutId, "checkout.paid", checkoutId, checkout.Total);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("Applied", await first.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains("Duplicate", await again.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var order = await OrderAsync(client, checkout.OrderId);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.NotNull(order.PaidAt);
        Assert.Null(order.PaymentUrl);
        Assert.Equal(4, await StockOfAsync(book)); // reserved once, never released
    }

    [Fact]
    public async Task Failed_webhook_releases_stock_and_restores_the_cart()
    {
        var client = await CustomerAsync();
        var book = await BookWithStockAsync("le-fils-du-pauvre", 5);
        await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 2));
        var checkout = await CheckoutAsync(client);
        Assert.Equal(3, await StockOfAsync(book));

        await WebhookAsync("evt_fail_" + checkout.OrderId, "checkout.failed", await CheckoutIdOfAsync(checkout.OrderId), checkout.Total);

        Assert.Equal(OrderStatus.Failed, (await OrderAsync(client, checkout.OrderId)).Status);
        Assert.Equal(5, await StockOfAsync(book));
        Assert.Equal(2, Assert.Single((await CartAsync(client)).Items).Quantity);
    }

    [Fact]
    public async Task Canceled_webhook_cancels_the_order()
    {
        var client = await CustomerAsync();
        var book = await BookWithStockAsync("la-colline-oubliee", 5);
        await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 1));
        var checkout = await CheckoutAsync(client);

        await WebhookAsync("evt_cancel_" + checkout.OrderId, "checkout.canceled", await CheckoutIdOfAsync(checkout.OrderId), checkout.Total);

        Assert.Equal(OrderStatus.Cancelled, (await OrderAsync(client, checkout.OrderId)).Status);
        Assert.Equal(5, await StockOfAsync(book));
    }

    [Fact]
    public async Task Checkout_fails_with_409_when_stock_is_insufficient()
    {
        var client = await CustomerAsync();
        var book = await BookWithStockAsync("l-amour-la-fantasia", 1);
        await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 2));

        var response = await client.PostAsJsonAsync("/api/v1/checkout", new CheckoutRequest(Address, "ar"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(book.ToString(), await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(1, await StockOfAsync(book)); // nothing reserved
        Assert.Single((await CartAsync(client)).Items); // cart untouched
    }

    [Fact]
    public async Task Two_customers_racing_for_the_last_copy_only_one_wins()
    {
        var book = await BookWithStockAsync("rijal-fi-al-shams", 1);
        var alice = await CustomerAsync();
        var bob = await CustomerAsync();
        await alice.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 1));
        await bob.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 1));

        var results = await Task.WhenAll(
            alice.PostAsJsonAsync("/api/v1/checkout", new CheckoutRequest(Address, "ar")),
            bob.PostAsJsonAsync("/api/v1/checkout", new CheckoutRequest(Address, "fr")));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(0, await StockOfAsync(book));
    }

    [Fact]
    public async Task Gateway_failure_returns_502_releases_stock_and_keeps_the_cart()
    {
        var client = await CustomerAsync();
        var book = await BookWithStockAsync("meursault-contre-enquete", 4);
        await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 1));
        factory.Chargily.FailNextCreate = true;

        var response = await client.PostAsJsonAsync("/api/v1/checkout", new CheckoutRequest(Address, "ar"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(4, await StockOfAsync(book));
        Assert.Single((await CartAsync(client)).Items);
    }

    [Fact]
    public async Task Empty_cart_cannot_be_checked_out()
    {
        var client = await CustomerAsync();

        var response = await client.PostAsJsonAsync("/api/v1/checkout", new CheckoutRequest(Address, "ar"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customers_cannot_see_each_others_orders()
    {
        var owner = await CustomerAsync();
        var book = await BookWithStockAsync("sapiens", 5);
        await owner.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 1));
        var checkout = await CheckoutAsync(owner);

        var stranger = await CustomerAsync();
        var response = await stranger.GetAsync(new Uri($"/api/v1/orders/{checkout.OrderId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------- expiry ----------------

    private async Task ForceExpiryAsync(Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Orders.Where(o => o.Id == orderId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var lifecycle = scope.ServiceProvider.GetRequiredService<IOrderLifecycleService>();
        await lifecycle.ExpireOverdueOrdersAsync(100, CancellationToken.None);
    }

    [Fact]
    public async Task Unpaid_order_expires_and_gives_stock_and_cart_back()
    {
        var client = await CustomerAsync();
        var book = await BookWithStockAsync("pride-and-prejudice", 3);
        await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 2));
        var checkout = await CheckoutAsync(client);

        await ForceExpiryAsync(checkout.OrderId); // fake Chargily still says "pending"

        Assert.Equal(OrderStatus.Cancelled, (await OrderAsync(client, checkout.OrderId)).Status);
        Assert.Equal(3, await StockOfAsync(book));
        Assert.Single((await CartAsync(client)).Items);
    }

    [Fact]
    public async Task Overdue_order_that_was_actually_paid_is_confirmed_not_expired()
    {
        var client = await CustomerAsync();
        var book = await BookWithStockAsync("nineteen-eighty-four", 3);
        await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 1));
        var checkout = await CheckoutAsync(client);
        factory.Chargily.SetStatus(await CheckoutIdOfAsync(checkout.OrderId), "paid"); // webhook "lost"

        await ForceExpiryAsync(checkout.OrderId);

        Assert.Equal(OrderStatus.Paid, (await OrderAsync(client, checkout.OrderId)).Status);
        Assert.Equal(2, await StockOfAsync(book));
    }

    [Fact]
    public async Task Late_payment_after_expiry_re_reserves_stock()
    {
        var client = await CustomerAsync();
        var book = await BookWithStockAsync("la-peste", 3);
        await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 1));
        var checkout = await CheckoutAsync(client);
        await ForceExpiryAsync(checkout.OrderId);
        Assert.Equal(3, await StockOfAsync(book));

        await WebhookAsync("evt_late_" + checkout.OrderId, "checkout.paid", await CheckoutIdOfAsync(checkout.OrderId), checkout.Total);

        Assert.Equal(OrderStatus.Paid, (await OrderAsync(client, checkout.OrderId)).Status);
        Assert.Equal(2, await StockOfAsync(book));
        Assert.Empty((await CartAsync(client)).Items); // the restored copy was bought after all
    }

    [Fact]
    public async Task Catalog_shows_reserved_stock_as_unavailable()
    {
        var client = await CustomerAsync();
        var book = await BookWithStockAsync("al-ayyam", 1);
        await client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(book, 1));
        await CheckoutAsync(client);

        var details = await factory.CreateClient().GetFromJsonAsync<BookDetailsDto>("/api/v1/books/al-ayyam", Json);

        Assert.False(details!.InStock);
    }
}
