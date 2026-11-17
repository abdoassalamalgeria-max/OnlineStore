using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookStore.Application.Common;
using BookStore.Application.Features.Account;
using BookStore.Application.Features.Auth;
using BookStore.Application.Features.Cart;
using BookStore.Application.Features.Catalog;
using BookStore.Application.Features.Ordering;
using BookStore.Application.Features.Reviews;
using BookStore.Domain.Ordering;
using BookStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Api.IntegrationTests;

/// <summary>Reviews (buyers only), order history and account settings against PostgreSQL.</summary>
public sealed class ReviewsAndAccountTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private sealed record Customer(HttpClient Client, string Email, AuthResponse Auth);

    private async Task<Customer> CustomerAsync(string name = "Lectrice Test")
    {
        var client = factory.CreateClient();
        var email = $"r-{Guid.NewGuid():N}@test.dz";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(name, email, "Secret123", null));
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new Customer(client, email, auth);
    }

    private async Task<Guid> BookIdAsync(string slug)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Books.Where(b => b.Slug == slug).ExecuteUpdateAsync(s => s.SetProperty(b => b.Stock, 50));
        return await db.Books.Where(b => b.Slug == slug).Select(b => b.Id).SingleAsync();
    }

    /// <summary>Places an order for the book and, unless <paramref name="pay"/> is false, confirms it via a signed webhook.</summary>
    private async Task<Guid> BuyAsync(Customer customer, string slug, bool pay = true)
    {
        await customer.Client.PostAsJsonAsync("/api/v1/cart/items", new CartLineRequest(await BookIdAsync(slug), 1));
        var checkout = await customer.Client.PostAsJsonAsync("/api/v1/checkout",
            new CheckoutRequest(new ShippingAddressRequest("Lectrice", "0550123456", 31, "Oran", "Rue 1"), "fr"));
        var order = (await checkout.Content.ReadFromJsonAsync<CheckoutResponse>(Json))!;
        if (!pay)
        {
            return order.OrderId;
        }

        string checkoutId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            checkoutId = await db.Orders.Where(o => o.Id == order.OrderId).Select(o => o.PaymentCheckoutId!).SingleAsync();
        }

        var body = JsonSerializer.Serialize(new
        {
            id = $"evt_{checkoutId}",
            entity = "event",
            livemode = false,
            type = "checkout.paid",
            data = new { id = checkoutId, amount = order.Total, status = "paid" },
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/payments/webhooks/chargily", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("signature", TestSettings.Sign(body));
        (await factory.CreateClient().SendAsync(request)).EnsureSuccessStatusCode();
        return order.OrderId;
    }

    // ---------------- reviews ----------------

    [Fact]
    public async Task Customers_who_did_not_buy_the_book_cannot_review_it()
    {
        var customer = await CustomerAsync();

        var mine = await customer.Client.GetFromJsonAsync<MyReviewDto>("/api/v1/books/nedjma/reviews/mine", Json);
        var put = await customer.Client.PutAsJsonAsync("/api/v1/books/nedjma/reviews/mine", new UpsertReviewRequest(5, "Superbe"));

        Assert.False(mine!.CanReview);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task An_unpaid_order_does_not_count_as_a_purchase()
    {
        var customer = await CustomerAsync();
        await BuyAsync(customer, "la-grande-maison", pay: false);

        var put = await customer.Client.PutAsJsonAsync("/api/v1/books/la-grande-maison/reviews/mine", new UpsertReviewRequest(4, null));

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task A_buyer_can_review_edit_and_delete_and_ratings_are_aggregated()
    {
        var first = await CustomerAsync("Amina");
        var second = await CustomerAsync("Karim");
        await BuyAsync(first, "ce-que-le-jour-doit-a-la-nuit");
        await BuyAsync(second, "ce-que-le-jour-doit-a-la-nuit");

        Assert.True((await first.Client.GetFromJsonAsync<MyReviewDto>("/api/v1/books/ce-que-le-jour-doit-a-la-nuit/reviews/mine", Json))!.CanReview);

        var created = await first.Client.PutAsJsonAsync("/api/v1/books/ce-que-le-jour-doit-a-la-nuit/reviews/mine", new UpsertReviewRequest(3, "Bien"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        await first.Client.PutAsJsonAsync("/api/v1/books/ce-que-le-jour-doit-a-la-nuit/reviews/mine", new UpsertReviewRequest(5, "Magnifique")); // edit, not duplicate
        await second.Client.PutAsJsonAsync("/api/v1/books/ce-que-le-jour-doit-a-la-nuit/reviews/mine", new UpsertReviewRequest(4, null));

        var listing = await factory.CreateClient().GetFromJsonAsync<BookReviewsDto>("/api/v1/books/ce-que-le-jour-doit-a-la-nuit/reviews", Json);
        Assert.Equal(2, listing!.Summary.Count);
        Assert.Equal(4.5, listing.Summary.Average);
        Assert.Equal([0, 0, 0, 1, 1], listing.Summary.Distribution);
        Assert.Contains(listing.Reviews.Items, r => r.AuthorName == "Amina" && r.Comment == "Magnifique");

        var book = await factory.CreateClient().GetFromJsonAsync<BookDetailsDto>("/api/v1/books/ce-que-le-jour-doit-a-la-nuit", Json);
        Assert.Equal(4.5, book!.AverageRating);
        Assert.Equal(2, book.ReviewCount);

        Assert.Equal(HttpStatusCode.NoContent, (await first.Client.DeleteAsync(new Uri("/api/v1/books/ce-que-le-jour-doit-a-la-nuit/reviews/mine", UriKind.Relative))).StatusCode);
        var after = await factory.CreateClient().GetFromJsonAsync<BookReviewsDto>("/api/v1/books/ce-que-le-jour-doit-a-la-nuit/reviews", Json);
        Assert.Equal(1, after!.Summary.Count);
    }

    [Fact]
    public async Task Reviewing_requires_sign_in_and_validates_the_rating()
    {
        var anonymous = await factory.CreateClient().PutAsJsonAsync("/api/v1/books/nedjma/reviews/mine", new UpsertReviewRequest(5, null));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var buyer = await CustomerAsync();
        await BuyAsync(buyer, "le-fils-du-pauvre");
        var invalid = await buyer.Client.PutAsJsonAsync("/api/v1/books/le-fils-du-pauvre/reviews/mine", new UpsertReviewRequest(9, null));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Books_without_reviews_report_no_average()
    {
        var listing = await factory.CreateClient().GetFromJsonAsync<BookReviewsDto>("/api/v1/books/al-nabi/reviews", Json);

        Assert.Null(listing!.Summary.Average);
        Assert.Equal(0, listing.Summary.Count);
    }

    // ---------------- orders ----------------

    [Fact]
    public async Task Order_history_lists_only_my_orders_newest_first()
    {
        var me = await CustomerAsync();
        var first = await BuyAsync(me, "animal-farm");
        var second = await BuyAsync(me, "sapiens", pay: false);
        var someoneElse = await CustomerAsync();
        await BuyAsync(someoneElse, "animal-farm");

        var page = await me.Client.GetFromJsonAsync<PagedResult<OrderSummaryDto>>("/api/v1/orders", Json);

        Assert.Equal(2, page!.TotalCount);
        Assert.Equal([second, first], page.Items.Select(o => o.Id));
        Assert.Equal(OrderStatus.Pending, page.Items[0].Status);
        Assert.Equal(OrderStatus.Paid, page.Items[1].Status);
        Assert.Equal("Animal Farm", Assert.Single(page.Items[1].Titles));
    }

    // ---------------- account ----------------

    [Fact]
    public async Task Profile_can_be_updated_and_new_reviews_use_the_new_name()
    {
        var me = await CustomerAsync("Ancien Nom");
        await BuyAsync(me, "la-colline-oubliee");

        var updated = await me.Client.PutAsJsonAsync("/api/v1/account/profile", new UpdateProfileRequest("Nouveau Nom", "0661234567"));
        var user = await updated.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.Equal("Nouveau Nom", user!.FullName);
        Assert.Equal("0661234567", user.PhoneNumber);

        var review = await (await me.Client.PutAsJsonAsync("/api/v1/books/la-colline-oubliee/reviews/mine", new UpsertReviewRequest(4, null)))
            .Content.ReadFromJsonAsync<ReviewDto>(Json);
        Assert.Equal("Nouveau Nom", review!.AuthorName);
    }

    [Fact]
    public async Task Changing_the_password_signs_out_other_sessions()
    {
        var me = await CustomerAsync();
        var otherDevice = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(me.Email, "Secret123"));
        var otherSession = (await otherDevice.Content.ReadFromJsonAsync<AuthResponse>(Json))!;

        var wrong = await me.Client.PostAsJsonAsync("/api/v1/account/password", new ChangePasswordRequest("Wrong123", "NewSecret456"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("currentPassword", await wrong.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var changed = await me.Client.PostAsJsonAsync("/api/v1/account/password", new ChangePasswordRequest("Secret123", "NewSecret456"));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var fresh = (await changed.Content.ReadFromJsonAsync<AuthResponse>(Json))!;

        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(otherSession.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(me.Auth.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(fresh.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(me.Email, "NewSecret456"))).StatusCode);
    }
}
