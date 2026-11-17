using BookStore.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BookStore.Infrastructure.Storage;

/// <summary>Stores files on the API's disk and serves them at /uploads (development, single server).</summary>
internal sealed class LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment environment) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.LocalRoot));
    private readonly string _baseUrl = options.Value.LocalPublicBaseUrl.TrimEnd('/');

    public string Root => _root;

    public async Task<string> SaveAsync(string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var fullPath = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, cancellationToken);
        return $"{_baseUrl}/{path}";
    }

    public Task DeleteAsync(string publicUrl, CancellationToken cancellationToken)
    {
        if (publicUrl.StartsWith(_baseUrl + "/", StringComparison.Ordinal))
        {
            var fullPath = Resolve(publicUrl[(_baseUrl.Length + 1)..]);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Maps a storage path into the root, refusing anything that escapes it ("../").</summary>
    private string Resolve(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, path));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid storage path.", nameof(path));
        }

        return fullPath;
    }
}
