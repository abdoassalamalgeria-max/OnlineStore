using System.Net;
using System.Text;
using System.Text.Json;
using BookStore.Application.Abstractions;
using BookStore.Infrastructure.Payments.Chargily;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace BookStore.Api.IntegrationTests;

/// <summary>Settings shared by every test host.</summary>
internal static class TestSettings
{
    public const string JwtSigningKey = "test-signing-key-that-is-at-least-32-characters-long";
    public const string JwtIssuer = "bookstore-api";
    public const string JwtAudience = "bookstore-frontend";
    public const string AdminEmail = "admin@test.dz";
    public const string AdminPassword = "AdminPass123";
    public const string ChargilySecret = "test_sk_integration_tests_secret";

    public static void Apply(IWebHostBuilder builder, FakeChargily chargily)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
        builder.UseSetting("Seed:Admin:Email", AdminEmail);
        builder.UseSetting("Seed:Admin:Password", AdminPassword);
        builder.UseSetting("RateLimiting:AuthPermitsPerMinute", "10000");
        builder.UseSetting("Jwt:RefreshReuseGraceSeconds", "0"); // replay tests expect immediate family revocation
        builder.UseSetting("Chargily:Mode", "test");
        builder.UseSetting("Chargily:SecretKey", ChargilySecret);
        builder.UseSetting("Checkout:ExpirySweepEnabled", "false"); // tests trigger expiry explicitly
        // Uploaded covers go to a throwaway folder and are served by the test server itself.
        builder.UseSetting("Storage:Provider", "Local");
        builder.UseSetting("Storage:LocalRoot", Path.Combine(Path.GetTempPath(), "bookstore-test-uploads"));
        builder.UseSetting("Storage:LocalPublicBaseUrl", "http://localhost/uploads");

        // Chargily is never called for real: its HttpClient talks to an in-memory fake.
        builder.ConfigureTestServices(services =>
            services.AddHttpClient<IPaymentGateway, ChargilyPaymentGateway>()
                .ConfigurePrimaryHttpMessageHandler(() => chargily));
    }

    /// <summary>Signs a webhook body exactly as Chargily does.</summary>
    public static string Sign(string body) => ChargilyPaymentGateway.ComputeSignature(ChargilySecret, body);
}

/// <summary>
/// In-memory stand-in for the Chargily API: creates checkouts, reports statuses, can be told to fail.
/// </summary>
public sealed class FakeChargily : HttpMessageHandler
{
    private readonly Dictionary<string, string> _statuses = [];
    private int _counter;

    /// <summary>When true, the next checkout creation returns 503.</summary>
    public bool FailNextCreate { get; set; }

    public void SetStatus(string checkoutId, string status)
    {
        lock (_statuses)
        {
            _statuses[checkoutId] = status;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Post && path.EndsWith("/checkouts", StringComparison.Ordinal))
        {
            if (FailNextCreate)
            {
                FailNextCreate = false;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            _ = await request.Content!.ReadAsStringAsync(cancellationToken);
            var id = $"chk_{Interlocked.Increment(ref _counter):D6}_{Guid.NewGuid():N}";
            SetStatus(id, "pending");
            return Json(new { id, status = "pending", checkout_url = $"https://pay.chargily.test/checkouts/{id}/pay" });
        }

        if (request.Method == HttpMethod.Get && path.Contains("/checkouts/", StringComparison.Ordinal))
        {
            var id = path[(path.LastIndexOf('/') + 1)..];
            lock (_statuses)
            {
                return _statuses.TryGetValue(id, out var status)
                    ? Json(new { id, status })
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json(object body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
}

/// <summary>
/// Hosts the API in memory WITHOUT a database: the connection string points at a closed port and
/// startup migration is off. For pipeline tests (health, ProblemDetails, OpenAPI, auth, webhook signature).
/// </summary>
public sealed class BookStoreApiFactory : WebApplicationFactory<Program>
{
    public FakeChargily Chargily { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TestSettings.Apply(builder, Chargily);
        builder.UseSetting("ConnectionStrings:Default", "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1");
        builder.UseSetting("Database:MigrateOnStartup", "false");
    }
}

/// <summary>
/// Hosts the API against a real, throwaway PostgreSQL database, migrated and seeded once per test class.
/// <list type="bullet">
/// <item>If BOOKSTORE_TEST_DB is set (a connection string to any PostgreSQL server whose user may create
/// databases, e.g. a local install), a uniquely named database is created there and dropped afterwards.</item>
/// <item>Otherwise a postgres:17-alpine container is started with Testcontainers (Docker required).</item>
/// </list>
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ExternalDatabaseVariable = "BOOKSTORE_TEST_DB";

    private readonly string? _external = Environment.GetEnvironmentVariable(ExternalDatabaseVariable);
    private PostgreSqlContainer? _container;
    private string _connectionString = string.Empty;
    private string? _databaseName;

    public FakeChargily Chargily { get; } = new();

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_external))
        {
            // EF's Migrate() creates the database on first start-up.
            _databaseName = $"bookstore_test_{Guid.NewGuid():N}";
            _connectionString = new NpgsqlConnectionStringBuilder(_external) { Database = _databaseName }.ConnectionString;
            return;
        }

        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        else if (_databaseName is not null)
        {
            NpgsqlConnection.ClearAllPools();
            var admin = new NpgsqlConnectionStringBuilder(_external) { Database = "postgres" }.ConnectionString;
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TestSettings.Apply(builder, Chargily);
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:SeedDemoData", "true");
    }
}
