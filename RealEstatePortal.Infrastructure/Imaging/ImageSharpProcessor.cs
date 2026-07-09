using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Application.Common.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace RealEstatePortal.Infrastructure.Imaging;

public class ImageSharpProcessor : IImageProcessor
{
    private const int DisplayMaxEdge = 1600;
    private const int ThumbnailMaxEdge = 600;

    public async Task<ProcessedImage> ProcessAsync(byte[] input, CancellationToken cancellationToken = default)
    {
        // Image.Load throws on non-image input — this doubles as validation.
        using var image = Image.Load(input);

        // Strip metadata (phone photos carry GPS/EXIF — a real privacy leak).
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        var display = await ToWebpAsync(image, DisplayMaxEdge, quality: 80, cancellationToken);
        var thumbnail = await ToWebpAsync(image, ThumbnailMaxEdge, quality: 75, cancellationToken);

        return new ProcessedImage(display, thumbnail);
    }

    private static async Task<byte[]> ToWebpAsync(Image source, int maxEdge, int quality, CancellationToken ct)
    {
        using var clone = source.Clone(ctx =>
        {
            // Only shrink — never upscale a small image (avoids quality loss).
            if (source.Width > maxEdge || source.Height > maxEdge)
            {
                ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxEdge, maxEdge)
                });
            }
        });

        using var ms = new MemoryStream();
        await clone.SaveAsWebpAsync(ms, new WebpEncoder { Quality = quality }, ct);
        return ms.ToArray();
    }
}