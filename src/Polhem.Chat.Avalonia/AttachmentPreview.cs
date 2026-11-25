using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.Avalonia;

/// <summary>Small pictures of attached images.</summary>
internal static class AttachmentPreview
{
    public const int ThumbnailWidth = 96;

    private static readonly ConditionalWeakTable<DataContent, Bitmap?> s_cache = [];

    /// <summary>
    /// Returns a small picture of an attachment, or <see langword="null"/> for one that is not a PNG, JPEG, GIF or WebP
    /// picture, is larger than <see cref="ChatPicturePreview"/> allows, or does not decode. Each attachment is decoded once.
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
        if (!ChatPicturePreview.CanPreview(attachment)
            || !ChatPicturePreview.TryReadSize(attachment.Data.Span, out var pictureWidth, out var pictureHeight))
        {
            return null;
        }
        var size = (Width: pictureWidth, Height: pictureHeight);
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
