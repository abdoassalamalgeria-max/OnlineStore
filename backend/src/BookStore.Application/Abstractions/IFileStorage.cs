namespace BookStore.Application.Abstractions;

/// <summary>
/// Public file storage for cover images. Implementations: local disk (development) and Supabase Storage
/// (production), selected by configuration. Uploads happen only from the API; keys never reach browsers.
/// </summary>
public interface IFileStorage
{
    /// <summary>Stores the file at <paramref name="path"/> (e.g. "covers/nedjma-1a2b3c4d.webp") and returns its public URL.</summary>
    Task<string> SaveAsync(string path, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Deletes a file previously returned by <see cref="SaveAsync"/>. URLs this storage did not issue are ignored.</summary>
    Task DeleteAsync(string publicUrl, CancellationToken cancellationToken);
}

/// <summary>Decodes, validates, resizes and re-encodes uploaded images.</summary>
public interface IImageProcessor
{
    /// <summary>
    /// Turns any supported upload (JPEG, PNG, WebP) into a cover: at most 600 px wide, WebP, metadata stripped.
    /// Throws <see cref="InvalidImageException"/> when the bytes are not a usable image.
    /// </summary>
    ProcessedImage ToCover(Stream input);
}

public sealed record ProcessedImage(byte[] Data, int Width, int Height, string ContentType, string Extension);

/// <summary>The upload is not a supported image (or is too large in pixels). Mapped to 400.</summary>
public sealed class InvalidImageException(string message) : Exception(message);
