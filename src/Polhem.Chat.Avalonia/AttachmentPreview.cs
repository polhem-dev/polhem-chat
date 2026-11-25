using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.Avalonia;

/// <summary>Small pictures of attached images.</summary>
internal static class AttachmentPreview
{
    public const int ThumbnailWidth = 96;

    /// <summary>
    /// The most pixels a picture may have to get a preview: 4096 × 4096, 64 MB decoded. A picture is decoded at full size
    /// before it is scaled down unless its format can decode smaller, which PNG and GIF cannot.
    /// </summary>
    public const long MaxPixels = 4096L * 4096;

    /// <summary>
    /// The most one side of a picture may be longer than the other to get a preview. A long strip is cheap to store
    /// and still decodes at full size first.
    /// </summary>
    public const int MaxAspectRatio = 20;

    private static readonly ConditionalWeakTable<DataContent, Bitmap?> s_cache = [];

    /// <summary>
    /// Returns a small picture of an attachment, or <see langword="null"/> for one that is not a PNG, JPEG, GIF or WebP
    /// picture, has more than <see cref="MaxPixels"/> pixels, or does not decode. Each attachment is decoded once.
    /// </summary>
    /// <remarks>
    /// WARNING: The size is read from the picture's header before anything is decoded. A file of a few kilobytes can
    /// declare a picture that takes gigabytes to decode, and the attachment may come from a history the limits never
    /// checked.
    /// </remarks>
    public static Bitmap? Thumbnail(DataContent? attachment)
    {
        if (attachment is null)
        {
            return null;
        }
        return s_cache.GetValue(attachment, Decode);
    }

    private static Bitmap? Decode(DataContent attachment)
    {
        if (attachment.MediaType is not (ChatAttachmentLimits.Png or ChatAttachmentLimits.Jpeg or ChatAttachmentLimits.Gif or ChatAttachmentLimits.WebP)
            || ImageHeader.ReadSize(attachment.Data.Span) is not { } size
            || (long)size.Width * size.Height > MaxPixels
            || Math.Max(size.Width, size.Height) > (long)MaxAspectRatio * Math.Min(size.Width, size.Height))
        {
            return null;
        }
        // The longer side gets the preview's size: a tall picture decoded to a small width would keep all its rows.
        var width = size.Width >= size.Height
            ? Math.Min(ThumbnailWidth, size.Width)
            : Math.Max(1, (int)((long)size.Width * Math.Min(ThumbnailWidth, size.Height) / size.Height));
        try
        {
            using var stream = new MemoryStream(attachment.Data.ToArray(), writable: false);
            return Bitmap.DecodeToWidth(stream, width);
        }
        // The header reads, but the body may still be broken. NOTE: Avalonia.Skia reports a body it cannot decode with a
        // NullReferenceException (checked with 12.0), the other failures with the exceptions a stream or an argument
        // throws.
        catch (Exception exception) when (exception is NullReferenceException or ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            return null;
        }
    }
}
