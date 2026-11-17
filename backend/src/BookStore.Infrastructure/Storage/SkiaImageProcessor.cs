using BookStore.Application.Abstractions;
using SkiaSharp;

namespace BookStore.Infrastructure.Storage;

/// <summary>
/// Cover pipeline with SkiaSharp (MIT): decode → validate → downscale to 600 px width → WebP (quality 82).
/// Re-encoding drops all metadata (EXIF, GPS). Decoding is what proves the upload is an image —
/// the client's Content-Type and file name are never trusted.
/// </summary>
internal sealed class SkiaImageProcessor : IImageProcessor
{
    public const int CoverWidth = 600;
    private const int Quality = 82;

    /// <summary>Refuse decompression bombs: a tiny file that expands to a huge bitmap.</summary>
    private const long MaxPixels = 40_000_000;

    private static readonly SKEncodedImageFormat[] Allowed =
        [SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Png, SKEncodedImageFormat.Webp];

    public ProcessedImage ToCover(Stream input)
    {
        using var data = SKData.Create(input) ?? throw new InvalidImageException("The file is empty or unreadable.");
        using var codec = SKCodec.Create(data) ?? throw new InvalidImageException("The file is not a supported image.");

        if (!Allowed.Contains(codec.EncodedFormat))
        {
            throw new InvalidImageException("Only JPEG, PNG and WebP images are accepted.");
        }

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxPixels)
        {
            throw new InvalidImageException("The image dimensions are not acceptable.");
        }

        using var raw = SKBitmap.Decode(codec) ?? throw new InvalidImageException("The image could not be decoded.");
        // Phone photos store pixels sideways plus an EXIF orientation flag; the flag is lost when we
        // re-encode, so bake the rotation into the pixels first.
        using var decoded = Orient(raw, codec.EncodedOrigin);

        var width = Math.Min(CoverWidth, decoded.Width);
        var height = Math.Max(1, (int)Math.Round(decoded.Height * (width / (double)decoded.Width)));

        using var resized = width == decoded.Width
            ? decoded.Copy()
            : decoded.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (resized is null)
        {
            throw new InvalidImageException("The image could not be resized.");
        }

        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, Quality)
            ?? throw new InvalidImageException("The image could not be encoded.");

        return new ProcessedImage(encoded.ToArray(), width, height, "image/webp", "webp");
    }

    /// <summary>
    /// Returns a bitmap displayed upright for the given EXIF origin (always a new bitmap, so the
    /// caller disposes it independently). Matrices map source (x, y) to the upright position.
    /// </summary>
    internal static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width, h = source.Height;
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),     // mirror horizontally
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), // rotate 180°
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),   // mirror vertically
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),       // transpose
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),     // rotate 90° clockwise
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), // transverse
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),   // rotate 90° counter-clockwise
            _ => SKMatrix.Identity,
        };

        if (matrix == SKMatrix.Identity)
        {
            return source.Copy() ?? throw new InvalidImageException("The image could not be copied.");
        }

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var upright = new SKBitmap(swap ? source.Height : source.Width, swap ? source.Width : source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(upright);
        canvas.SetMatrix(matrix);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null); // exact pixel moves
        return upright;
    }
}
