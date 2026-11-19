using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookStore.Application.Common;
using BookStore.Application.Features.Admin;
using BookStore.Application.Features.Auth;

namespace BookStore.Api.IntegrationTests;

/// <summary>Admin user management against PostgreSQL: list/search, edit, suspend/reactivate, access rules.</summary>
public sealed class AdminUsersTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
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

    private async Task<(HttpClient Client, AuthResponse Auth, string Email)> CustomerAsync(string name = "Client Test", string? phone = null)
    {
        var client = factory.CreateClient();
        var email = $"u-{Guid.NewGuid():N}@test.dz";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(name, email, "Secret123", phone));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth, email);
    }

    private static async Task<PagedResult<AdminUserListItemDto>> ListAsync(HttpClient admin, string query) =>
        (await admin.GetFromJsonAsync<PagedResult<AdminUserListItemDto>>($"/api/v1/admin/users?{query}", Json))!;

    [Fact]
    public async Task Lists_and_searches_users_by_name_email_and_phone()
    {
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var (_, auth, email) = await CustomerAsync($"Amina {tag}", "0661234567");

        Assert.Contains((await ListAsync(admin, $"search={tag}")).Items, u => u.Id == auth.User.Id);
        var byEmail = Assert.Single((await ListAsync(admin, $"search={Uri.EscapeDataString(email)}")).Items);
        Assert.Equal($"Amina {tag}", byEmail.FullName);
        Assert.Contains("Customer", byEmail.Roles);
        Assert.False(byEmail.IsSuspended);
        Assert.Equal(0, byEmail.OrderCount);
        Assert.Contains((await ListAsync(admin, "search=0661234567")).Items, u => u.Id == auth.User.Id);

        // The admin is listed too, with the Admin role.
        var self = Assert.Single((await ListAsync(admin, $"search={Uri.EscapeDataString(TestSettings.AdminEmail)}")).Items);
        Assert.Contains("Admin", self.Roles);
    }

    [Fact]
    public async Task Admin_edits_personal_details_and_the_user_signs_in_with_the_new_email()
    {
        var admin = await AdminAsync();
        var (_, auth, _) = await CustomerAsync();
        var newEmail = $"new-{Guid.NewGuid():N}@test.dz";

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/users/{auth.User.Id}",
            new AdminUpdateUserRequest("  Karim Benali ", newEmail, "0550123456"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<AdminUserDto>(Json))!;
        Assert.Equal(("Karim Benali", newEmail, "0550123456"), (updated.FullName, updated.Email, updated.PhoneNumber));

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(newEmail, "Secret123"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Email_of_another_account_or_invalid_input_is_a_field_error()
    {
        var admin = await AdminAsync();
        var (_, first, firstEmail) = await CustomerAsync();
        var (_, second, _) = await CustomerAsync();

        var taken = await admin.PutAsJsonAsync($"/api/v1/admin/users/{second.User.Id}",
            new AdminUpdateUserRequest("Name", firstEmail.ToUpperInvariant(), null));
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Contains("\"email\"", await taken.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var invalid = await admin.PutAsJsonAsync($"/api/v1/admin/users/{first.User.Id}",
            new AdminUpdateUserRequest("", "not-an-email", "123"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var body = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("\"fullName\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"phoneNumber\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Suspension_blocks_sign_in_and_refresh_and_reactivation_restores_it()
    {
        var admin = await AdminAsync();
        var (_, auth, email) = await CustomerAsync();
        var anon = factory.CreateClient();

        var suspended = (await (await admin.PostAsync($"/api/v1/admin/users/{auth.User.Id}/suspend", null))
            .Content.ReadFromJsonAsync<AdminUserDto>(Json))!;
        Assert.True(suspended.IsSuspended);
        Assert.Null(suspended.LockedOutUntil);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Secret123"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken))).StatusCode);
        Assert.Contains((await ListAsync(admin, "suspended=true")).Items, u => u.Id == auth.User.Id);
        Assert.DoesNotContain((await ListAsync(admin, "suspended=false&pageSize=50")).Items, u => u.Id == auth.User.Id);

        var reactivated = (await (await admin.PostAsync($"/api/v1/admin/users/{auth.User.Id}/reactivate", null))
            .Content.ReadFromJsonAsync<AdminUserDto>(Json))!;
        Assert.False(reactivated.IsSuspended);
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Secret123"))).StatusCode);
    }

    [Fact]
    public async Task Admin_accounts_cannot_be_suspended()
    {
        var admin = await AdminAsync();
        var self = Assert.Single((await ListAsync(admin, $"search={Uri.EscapeDataString(TestSettings.AdminEmail)}")).Items);

        var response = await admin.PostAsync($"/api/v1/admin/users/{self.Id}/suspend", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Customers_and_guests_cannot_reach_user_management()
    {
        var (customer, auth, _) = await CustomerAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsync($"/api/v1/admin/users/{auth.User.Id}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Unknown_user_is_404_and_bad_paging_is_400()
    {
        var admin = await AdminAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/users/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/users?pageSize=500")).StatusCode);
    }
}
