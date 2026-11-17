using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.Application.Features.Admin;
using BookStore.Application.Features.Auth;
using Microsoft.AspNetCore.Hosting;

namespace BookStore.Api.IntegrationTests;

/// <summary>Register/login/refresh/logout and role checks against a real PostgreSQL.</summary>
public sealed class AuthEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@test.dz";

    private async Task<AuthResponse> RegisterAsync(string email, string password = "Secret123")
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Amine Test", email, password, "0550123456"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<HttpResponseMessage> GetWithTokenAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(refreshToken));

    [Fact]
    public async Task Register_signs_in_as_customer_and_me_returns_the_profile()
    {
        var email = UniqueEmail();
        var auth = await RegisterAsync(email);

        Assert.Equal([Roles.Customer], auth.User.Roles);
        Assert.True(auth.AccessTokenExpiresAt > DateTimeOffset.UtcNow);

        var me = await (await GetWithTokenAsync("/api/v1/auth/me", auth.AccessToken)).Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(email, me!.Email);
        Assert.Equal("0550123456", me.PhoneNumber);
    }

    [Fact]
    public async Task Duplicate_email_returns_409()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Other", email, "Secret123", null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_succeeds_with_the_right_password_and_fails_generically_otherwise()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var ok = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Secret123"));
        var wrongPassword = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Wrong123"));
        var unknownUser = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(UniqueEmail(), "Secret123"));

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        // Same title and detail either way, so responses don't reveal which emails are registered
        // (the traceId naturally differs per request).
        var a = await wrongPassword.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        var b = await unknownUser.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal(a!.Title, b!.Title);
        Assert.Equal(a.Detail, b.Detail);
    }

    [Fact]
    public async Task Account_locks_after_five_failed_logins()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        for (var i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Wrong123"));
        }

        var correctButLocked = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Secret123"));
        Assert.Equal(HttpStatusCode.Unauthorized, correctButLocked.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_old_one_stops_working()
    {
        var auth = await RegisterAsync(UniqueEmail());

        var first = await RefreshAsync(auth.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = (await first.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(auth.RefreshToken, rotated.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(auth.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family()
    {
        var auth = await RegisterAsync(UniqueEmail());
        var rotated = (await (await RefreshAsync(auth.RefreshToken)).Content.ReadFromJsonAsync<AuthResponse>())!;

        // Attacker replays the old token → the legitimate new token is revoked too.
        await RefreshAsync(auth.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(rotated.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Parallel_refresh_within_the_grace_window_does_not_log_the_user_out()
    {
        using var lenient = factory.WithWebHostBuilder(b => b.UseSetting("Jwt:RefreshReuseGraceSeconds", "60"));
        var client = lenient.CreateClient();
        var register = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Amine Test", UniqueEmail(), "Secret123", null));
        var auth = (await register.Content.ReadFromJsonAsync<AuthResponse>())!;

        var winner = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken));
        var loser = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken));
        var rotated = (await winner.Content.ReadFromJsonAsync<AuthResponse>())!;

        Assert.Equal(HttpStatusCode.Unauthorized, loser.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(rotated.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var auth = await RegisterAsync(UniqueEmail());

        var logout = await _client.PostAsJsonAsync("/api/v1/auth/logout", new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(auth.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Seeded_admin_can_open_the_dashboard_but_a_customer_cannot()
    {
        var adminLogin = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(TestSettings.AdminEmail, TestSettings.AdminPassword));
        var admin = (await adminLogin.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Contains(Roles.Admin, admin.User.Roles);

        var overview = await GetWithTokenAsync("/api/v1/admin/overview", admin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        var stats = await overview.Content.ReadFromJsonAsync<AdminOverviewDto>();
        Assert.Equal(30, stats!.Books);
        Assert.Equal(2, stats.OutOfStockBooks);

        var customer = await RegisterAsync(UniqueEmail());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await GetWithTokenAsync("/api/v1/admin/overview", customer.AccessToken)).StatusCode);
    }
}
