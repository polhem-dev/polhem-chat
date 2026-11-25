using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// Decides whether an attached picture is safe to show as a small preview, from its first bytes and without decoding it.
/// A view shows a preview only for the pictures this allows.
/// </summary>
/// <remarks>
/// WARNING: A file of a few kilobytes can declare a picture that takes gigabytes to decode, and an attachment may come
/// from a loaded history that <see cref="ChatAttachmentLimits"/> never checked. A view, or a browser that a view hands
/// the picture to, decodes it at full size before it scales it down, so the size has to be checked first.
/// </remarks>
public static class ChatPicturePreview
{
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

    /// <summary>Reads the width and height of a PNG, GIF, JPEG or WebP picture from its first bytes.</summary>
    /// <param name="data">The picture's bytes.</param>
    /// <param name="width">The width, or 0 when the size cannot be read.</param>
    /// <param name="height">The height, or 0 when the size cannot be read.</param>
    /// <returns><see langword="true"/> when the bytes start like one of those formats and declare a positive size.</returns>
    public static bool TryReadSize(ReadOnlySpan<byte> data, out int width, out int height)
    {
        if (ImageHeader.ReadSize(data) is { } size)
        {
            (width, height) = size;
            return true;
        }
        (width, height) = (0, 0);
        return false;
    }

    /// <summary>
    /// Returns whether an attachment is a PNG, JPEG, GIF or WebP picture whose declared size is within
    /// <see cref="MaxPixels"/> and <see cref="MaxAspectRatio"/>.
    /// </summary>
    /// <param name="attachment">The attachment.</param>
    /// <returns><see langword="true"/> when a view may show a preview of it.</returns>
    public static bool CanPreview(DataContent attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return attachment.MediaType is ChatAttachmentLimits.Png or ChatAttachmentLimits.Jpeg or ChatAttachmentLimits.Gif or ChatAttachmentLimits.WebP
            && TryReadSize(attachment.Data.Span, out var width, out var height)
            && (long)width * height <= MaxPixels
            && Math.Max(width, height) <= (long)MaxAspectRatio * Math.Min(width, height);
    }
}
