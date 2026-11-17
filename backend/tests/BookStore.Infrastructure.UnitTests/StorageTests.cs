using System.Net;
using System.Text;
using BookStore.Application.Abstractions;
using BookStore.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace BookStore.Infrastructure.UnitTests;

public sealed class SkiaImageProcessorTests
{
    private readonly SkiaImageProcessor _processor = new();

    private static MemoryStream Image(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.DarkGreen);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return new MemoryStream(data.ToArray());
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void Large_images_are_downscaled_to_600px_webp(SKEncodedImageFormat format)
    {
        var cover = _processor.ToCover(Image(1200, 1800, format));

        Assert.Equal(600, cover.Width);
        Assert.Equal(900, cover.Height);
        Assert.Equal("image/webp", cover.ContentType);
        using var codec = SKCodec.Create(SKData.CreateCopy(cover.Data));
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
    }

    [Fact]
    public void Small_images_are_not_upscaled()
    {
        var cover = _processor.ToCover(Image(300, 450, SKEncodedImageFormat.Png));

        Assert.Equal(300, cover.Width);
        Assert.Equal(450, cover.Height);
    }

    /// <summary>
    /// A 200×100 JPEG (left half red, right half blue) with an EXIF APP1 segment whose only tag is
    /// Orientation (0x0112). SkiaSharp cannot write EXIF, so the segment is spliced in after SOI.
    /// </summary>
    private static MemoryStream JpegWithOrientation(ushort orientation)
    {
        using var bitmap = new SKBitmap(200, 100);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var red = new SKPaint { Color = SKColors.Red };
            using var blue = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(0, 0, 100, 100, red);
            canvas.DrawRect(100, 0, 100, 100, blue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 95).ToArray();

        byte[] tiff =
        [
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // big-endian header, IFD at 8
            0x00, 0x01,                                             // one entry
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01,         // Orientation, SHORT, count 1
            (byte)(orientation >> 8), (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,                                 // no next IFD
        ];
        byte[] payload = [.. "Exif\0\0"u8, .. tiff];
        var length = payload.Length + 2;
        byte[] app1 = [0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. payload];

        return new MemoryStream([.. jpeg[..2], .. app1, .. jpeg[2..]]);
    }

    private static SKColor PixelAt(ProcessedImage cover, float fx, float fy)
    {
        using var decoded = SKBitmap.Decode(cover.Data);
        return decoded.GetPixel((int)(decoded.Width * fx), (int)(decoded.Height * fy));
    }

    private static bool IsRed(SKColor c) => c.Red > 180 && c.Blue < 80;

    private static bool IsBlue(SKColor c) => c.Blue > 180 && c.Red < 80;

    [Fact]
    public void Exif_rotate_90_clockwise_is_applied()
    {
        var cover = _processor.ToCover(JpegWithOrientation(6));

        // 200×100 stored sideways → 100×200 upright; the stored left (red) half ends up on top.
        Assert.Equal((100, 200), (cover.Width, cover.Height));
        Assert.True(IsRed(PixelAt(cover, 0.5f, 0.25f)));
        Assert.True(IsBlue(PixelAt(cover, 0.5f, 0.75f)));
    }

    [Fact]
    public void Exif_rotate_90_counter_clockwise_is_applied()
    {
        var cover = _processor.ToCover(JpegWithOrientation(8));

        Assert.Equal((100, 200), (cover.Width, cover.Height));
        Assert.True(IsBlue(PixelAt(cover, 0.5f, 0.25f)));
        Assert.True(IsRed(PixelAt(cover, 0.5f, 0.75f)));
    }

    [Fact]
    public void Exif_rotate_180_is_applied()
    {
        var cover = _processor.ToCover(JpegWithOrientation(3));

        Assert.Equal((200, 100), (cover.Width, cover.Height));
        Assert.True(IsBlue(PixelAt(cover, 0.25f, 0.5f)));
        Assert.True(IsRed(PixelAt(cover, 0.75f, 0.5f)));
    }

    [Fact]
    public void Upright_jpeg_is_unchanged()
    {
        var cover = _processor.ToCover(JpegWithOrientation(1));

        Assert.Equal((200, 100), (cover.Width, cover.Height));
        Assert.True(IsRed(PixelAt(cover, 0.25f, 0.5f)));
    }

    [Theory]
    [InlineData(SKEncodedOrigin.TopRight, 200, 100)]
    [InlineData(SKEncodedOrigin.BottomLeft, 200, 100)]
    [InlineData(SKEncodedOrigin.LeftTop, 100, 200)]
    [InlineData(SKEncodedOrigin.RightBottom, 100, 200)]
    public void Mirrored_origins_keep_every_pixel_on_canvas(SKEncodedOrigin origin, int width, int height)
    {
        using var source = new SKBitmap(200, 100);
        source.Erase(SKColors.Red);

        using var upright = SkiaImageProcessor.Orient(source, origin);

        Assert.Equal((width, height), (upright.Width, upright.Height));
        // Every corner is covered: a wrong translation would leave a transparent strip.
        foreach (var (x, y) in new[] { (0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1) })
        {
            Assert.Equal(SKColors.Red, upright.GetPixel(x, y));
        }
    }

    [Fact]
    public void Non_images_are_rejected() =>
        Assert.Throws<InvalidImageException>(() => _processor.ToCover(new MemoryStream("<?php echo 'hi'; ?>"u8.ToArray())));

    [Fact]
    public void Empty_files_are_rejected() =>
        Assert.Throws<InvalidImageException>(() => _processor.ToCover(new MemoryStream()));
}

public sealed class LocalFileStorageTests : IDisposable
{
    private sealed class Env(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private readonly string _dir = Directory.CreateTempSubdirectory("bookstore-storage-").FullName;

    private LocalFileStorage Storage() => new(
        Options.Create(new StorageOptions { LocalRoot = "uploads", LocalPublicBaseUrl = "http://api.test/uploads" }),
        new Env(_dir));

    [Fact]
    public async Task Saves_returns_public_url_and_deletes()
    {
        var storage = Storage();

        var url = await storage.SaveAsync("covers/a.webp", new MemoryStream([1, 2, 3]), "image/webp", CancellationToken.None);

        Assert.Equal("http://api.test/uploads/covers/a.webp", url);
        var file = Path.Combine(_dir, "uploads", "covers", "a.webp");
        Assert.True(File.Exists(file));

        await storage.DeleteAsync(url, CancellationToken.None);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task Refuses_paths_that_escape_the_root() =>
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Storage().SaveAsync("../../evil.txt", new MemoryStream([1]), "text/plain", CancellationToken.None));

    [Fact]
    public async Task Ignores_urls_it_did_not_issue() =>
        await Storage().DeleteAsync("https://elsewhere.example/x.webp", CancellationToken.None);

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}

public sealed class SupabaseFileStorageTests
{
    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, byte[] Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Calls.Add((request, body));
            return new HttpResponseMessage(status) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }

    private static SupabaseFileStorage Storage(StubHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new StorageOptions
        {
            Provider = "Supabase",
            SupabaseUrl = "https://proj.supabase.co/",
            SupabaseServiceKey = "service-role-key",
            SupabaseBucket = "book-covers",
        }),
        NullLogger<SupabaseFileStorage>.Instance);

    [Fact]
    public async Task Uploads_with_service_key_and_returns_the_public_url()
    {
        var handler = new StubHandler(HttpStatusCode.OK);

        var url = await Storage(handler).SaveAsync("covers/x.webp", new MemoryStream([9, 9]), "image/webp", CancellationToken.None);

        Assert.Equal("https://proj.supabase.co/storage/v1/object/public/book-covers/covers/x.webp", url);
        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://proj.supabase.co/storage/v1/object/book-covers/covers/x.webp", request.RequestUri!.ToString());
        Assert.Equal("Bearer service-role-key", request.Headers.Authorization!.ToString());
        Assert.Equal("service-role-key", request.Headers.GetValues("apikey").Single());
        Assert.Equal("true", request.Headers.GetValues("x-upsert").Single());
        Assert.Equal("image/webp", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal([9, 9], body);
    }

    [Fact]
    public async Task Failed_upload_throws() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Storage(new StubHandler(HttpStatusCode.Forbidden)).SaveAsync("covers/x.webp", new MemoryStream([1]), "image/webp", CancellationToken.None));

    [Fact]
    public async Task Deletes_only_its_own_objects()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var storage = Storage(handler);

        await storage.DeleteAsync("https://proj.supabase.co/storage/v1/object/public/book-covers/covers/x.webp", CancellationToken.None);
        await storage.DeleteAsync("https://other.example/covers/x.webp", CancellationToken.None);

        var (request, _) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("https://proj.supabase.co/storage/v1/object/book-covers/covers/x.webp", request.RequestUri!.ToString());
    }
}
