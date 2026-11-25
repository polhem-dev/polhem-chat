using Avalonia.Media.Imaging;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.Avalonia;

/// <summary>Small pictures of attached images.</summary>
internal static class AttachmentPreview
{
    public const int ThumbnailWidth = 96;

    /// <summary>
    /// Decodes a picture at a small width, so a large image is never decoded at full size; returns
    /// <see langword="null"/> for an attachment that is not a picture, or one that does not decode.
    /// </summary>
    public static Bitmap? Thumbnail(DataContent? attachment)
    {
        if (attachment is null || !attachment.HasTopLevelMediaType("image"))
        {
            return null;
        }
        try
        {
            using var stream = new MemoryStream(attachment.Data.ToArray(), writable: false);
            return Bitmap.DecodeToWidth(stream, ThumbnailWidth);
        }
        // The content starts like a picture (the attachment check makes sure), but its body may still be broken. NOTE:
        // Avalonia.Skia reports a body it cannot decode with a NullReferenceException (checked with 12.0), the other
        // types with the exceptions a stream or an argument throws.
        catch (Exception exception) when (exception is NullReferenceException or ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            return null;
        }
    }
}
