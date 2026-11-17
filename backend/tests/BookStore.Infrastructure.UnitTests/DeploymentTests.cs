using System.Net;
using System.Text;
using System.Text.Json;
using BookStore.Infrastructure.Persistence;
using BookStore.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BookStore.Infrastructure.UnitTests;

public sealed class DatabaseConnectionTests
{
    private const string SessionPooler =
        "Host=aws-0-eu-central-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.abcdefgh;Password=secret";

    [Fact]
    public void Session_pooler_gets_tls_and_the_configured_pool_size()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseConnection.Build(SessionPooler, maxPoolSize: 10));

        Assert.Equal(SslMode.Require, result.SslMode);
        Assert.Equal(10, result.MaxPoolSize);
        Assert.Equal("postgres.abcdefgh", result.Username);
        Assert.Equal(5432, result.Port);
    }

    [Fact]
    public void Transaction_pooler_is_rejected()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConnection.Build(SessionPooler.Replace("Port=5432", "Port=6543", StringComparison.Ordinal), 10));

        Assert.Contains("Session pooler", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Stricter_ssl_modes_are_kept()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseConnection.Build(SessionPooler + ";SSL Mode=VerifyFull", null));

        Assert.Equal(SslMode.VerifyFull, result.SslMode);
    }

    [Fact]
    public void Local_databases_are_left_alone()
    {
        var result = new NpgsqlConnectionStringBuilder(
            DatabaseConnection.Build("Host=localhost;Port=5433;Database=bookstore;Username=bookstore;Password=x", null));

        Assert.Equal(SslMode.Prefer, result.SslMode);
        Assert.Equal(GssEncryptionMode.Disable, result.GssEncryptionMode);
        Assert.Equal(100, result.MaxPoolSize); // Npgsql default
    }
}

public sealed class SupabaseBucketTests
{
    /// <summary>Answers requests from a queue of status codes and records what was sent.</summary>
    private sealed class ScriptedHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses = new(statuses);

        public List<(HttpMethod Method, string Uri, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.Method, request.RequestUri!.ToString(), body));
            return new HttpResponseMessage(_statuses.Dequeue()) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }

    private static SupabaseFileStorage Storage(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new StorageOptions
        {
            Provider = "Supabase",
            SupabaseUrl = "https://proj.supabase.co",
            SupabaseServiceKey = "service-role-key",
            SupabaseBucket = "book-covers",
        }),
        NullLogger<SupabaseFileStorage>.Instance);

    private static void AssertSettings(string json, bool expectId)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("public").GetBoolean());
        Assert.Equal(SupabaseFileStorage.BucketFileSizeLimit, root.GetProperty("file_size_limit").GetInt64());
        Assert.Equal("image/webp", Assert.Single(root.GetProperty("allowed_mime_types").EnumerateArray()).GetString());
        Assert.Equal(expectId, root.TryGetProperty("id", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)] // older Storage versions
    public async Task Missing_bucket_is_created_public(HttpStatusCode missing)
    {
        var handler = new ScriptedHandler(missing, HttpStatusCode.OK);

        await Storage(handler).EnsureBucketAsync(CancellationToken.None);

        Assert.Equal(2, handler.Calls.Count);
        Assert.Equal((HttpMethod.Get, "https://proj.supabase.co/storage/v1/bucket/book-covers"), (handler.Calls[0].Method, handler.Calls[0].Uri));
        Assert.Equal((HttpMethod.Post, "https://proj.supabase.co/storage/v1/bucket"), (handler.Calls[1].Method, handler.Calls[1].Uri));
        AssertSettings(handler.Calls[1].Body, expectId: true);
    }

    [Fact]
    public async Task Existing_bucket_gets_its_settings_reapplied()
    {
        var handler = new ScriptedHandler(HttpStatusCode.OK, HttpStatusCode.OK);

        await Storage(handler).EnsureBucketAsync(CancellationToken.None);

        Assert.Equal((HttpMethod.Put, "https://proj.supabase.co/storage/v1/bucket/book-covers"), (handler.Calls[1].Method, handler.Calls[1].Uri));
        AssertSettings(handler.Calls[1].Body, expectId: false);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)] // wrong key: fail at the GET, never try to create
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Unexpected_read_errors_fail_the_deployment(HttpStatusCode status)
    {
        var handler = new ScriptedHandler(status);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Storage(handler).EnsureBucketAsync(CancellationToken.None));
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task Failed_create_fails_the_deployment() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Storage(new ScriptedHandler(HttpStatusCode.NotFound, HttpStatusCode.Forbidden)).EnsureBucketAsync(CancellationToken.None));
}
