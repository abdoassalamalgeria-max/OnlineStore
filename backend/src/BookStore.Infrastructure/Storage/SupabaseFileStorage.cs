using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookStore.Infrastructure.Storage;

/// <summary>
/// Supabase Storage via its REST API (no SDK). Uploads use the service_role key, which stays on this
/// server; files go to a public bucket, so browsers load covers directly from Supabase's CDN.
///   upload: POST   {url}/storage/v1/object/{bucket}/{path}   (x-upsert)
///   delete: DELETE {url}/storage/v1/object/{bucket}/{path}
///   public: GET    {url}/storage/v1/object/public/{bucket}/{path}
/// </summary>
internal sealed partial class SupabaseFileStorage(
    HttpClient http,
    IOptions<StorageOptions> options,
    ILogger<SupabaseFileStorage> logger) : IFileStorage
{
    private readonly StorageOptions _options = options.Value;

    private string PublicPrefix => $"{_options.SupabaseUrl!.TrimEnd('/')}/storage/v1/object/public/{_options.SupabaseBucket}/";

    public async Task<string> SaveAsync(string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ObjectUri(path)) { Content = new StreamContent(content) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Headers.Add("x-upsert", "true");
        // File names are unique per upload, so they can be cached for a year.
        request.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromDays(365), Public = true };
        Authorize(request);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            LogFailure(logger, "upload", path, (int)response.StatusCode, body.Length > 300 ? body[..300] : body);
            throw new InvalidOperationException($"Supabase Storage upload failed ({(int)response.StatusCode}).");
        }

        return PublicPrefix + path;
    }

    public async Task DeleteAsync(string publicUrl, CancellationToken cancellationToken)
    {
        if (!publicUrl.StartsWith(PublicPrefix, StringComparison.Ordinal))
        {
            return; // not one of ours (e.g. an external URL)
        }

        var path = publicUrl[PublicPrefix.Length..];
        using var request = new HttpRequestMessage(HttpMethod.Delete, ObjectUri(path));
        Authorize(request);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // An orphaned file costs a little storage; it must not fail the admin's action.
            LogFailure(logger, "delete", path, (int)response.StatusCode, string.Empty);
        }
    }

    /// <summary>Largest object the bucket accepts. Processed covers are ~10–60 KB WebP.</summary>
    internal const long BucketFileSizeLimit = 2 * 1024 * 1024;

    /// <summary>
    /// Creates the covers bucket if it does not exist, or re-applies its settings if it does
    /// (public, WebP only, 2 MB). Idempotent; run as part of the deployment step.
    ///   read:   GET  {url}/storage/v1/bucket/{id}
    ///   create: POST {url}/storage/v1/bucket        { id, name, public, file_size_limit, allowed_mime_types }
    ///   update: PUT  {url}/storage/v1/bucket/{id}   { public, file_size_limit, allowed_mime_types }
    /// </summary>
    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var bucket = _options.SupabaseBucket;
        var settings = new Dictionary<string, object>
        {
            ["public"] = true,
            ["file_size_limit"] = BucketFileSizeLimit,
            ["allowed_mime_types"] = new[] { "image/webp" },
        };

        using var get = new HttpRequestMessage(HttpMethod.Get, BucketUri(bucket));
        Authorize(get);
        using var existing = await http.SendAsync(get, cancellationToken);

        HttpRequestMessage write;
        if (existing.IsSuccessStatusCode)
        {
            write = new HttpRequestMessage(HttpMethod.Put, BucketUri(bucket)) { Content = JsonContent.Create(settings) };
        }
        else if ((int)existing.StatusCode is 400 or 404)
        {
            // Older Storage versions answer a missing bucket with 400 { statusCode: "404" }.
            settings["id"] = bucket;
            settings["name"] = bucket;
            write = new HttpRequestMessage(HttpMethod.Post, BucketUri(null)) { Content = JsonContent.Create(settings) };
        }
        else
        {
            throw await StorageError("read bucket", bucket, existing, cancellationToken);
        }

        using (write)
        {
            Authorize(write);
            using var response = await http.SendAsync(write, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw await StorageError(write.Method == HttpMethod.Post ? "create bucket" : "update bucket", bucket, response, cancellationToken);
            }

            LogBucketReady(logger, bucket, write.Method == HttpMethod.Post ? "created" : "updated");
        }
    }

    private async Task<InvalidOperationException> StorageError(
        string operation, string bucket, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        LogFailure(logger, operation, bucket, (int)response.StatusCode, body.Length > 300 ? body[..300] : body);
        return new InvalidOperationException(
            $"Supabase Storage {operation} '{bucket}' failed ({(int)response.StatusCode}). Check Storage:SupabaseUrl and the service_role key.");
    }

    private Uri BucketUri(string? bucket) =>
        new($"{_options.SupabaseUrl!.TrimEnd('/')}/storage/v1/bucket" + (bucket is null ? string.Empty : "/" + Uri.EscapeDataString(bucket)));

    private Uri ObjectUri(string path) =>
        new($"{_options.SupabaseUrl!.TrimEnd('/')}/storage/v1/object/{_options.SupabaseBucket}/{path}");

    private void Authorize(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SupabaseServiceKey);
        request.Headers.Add("apikey", _options.SupabaseServiceKey);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Supabase Storage {Operation} of {Path} failed with {Status}: {Body}")]
    private static partial void LogFailure(ILogger logger, string operation, string path, int status, string body);

    [LoggerMessage(Level = LogLevel.Information, Message = "Supabase Storage bucket {Bucket} {Outcome} (public, image/webp)")]
    private static partial void LogBucketReady(ILogger logger, string bucket, string outcome);
}
