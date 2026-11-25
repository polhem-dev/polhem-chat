using Microsoft.Extensions.AI;

namespace Polhem.Chat.UnitTests;

/// <summary>Which attached pictures a view may show as a preview, read from their first bytes.</summary>
public class ChatPicturePreviewTests
{
    [Theory(DisplayName = "The header gives the size of each picture type")]
    [InlineData("gif", 3, 2)]
    [InlineData("jpeg", 640, 480)]
    [InlineData("webp-vp8x", 300, 200)]
    public void ImageHeader_ReadsEachType(string kind, int width, int height)
    {
        byte[] data = kind switch
        {
            "gif" => [.. "GIF89a"u8, 3, 0, 2, 0, 0, 0, 0],
            "jpeg" => [0xFF, 0xD8, 0xFF, 0xE0, 0, 4, 0, 0, 0xFF, 0xC0, 0, 11, 8, 0x01, 0xE0, 0x02, 0x80, 3, 0, 0],
            _ => [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. "VP8X"u8, 10, 0, 0, 0, 0, 0, 0, 0, 0x2B, 0x01, 0x00, 0xC7, 0x00, 0x00, 0, 0],
        };

        Assert.True(ChatPicturePreview.TryReadSize(data, out var readWidth, out var readHeight));
        Assert.Equal((width, height), (readWidth, readHeight));
    }

    [Theory(DisplayName = "The header gives the size of the other WebP kinds and of a JPEG with fill bytes")]
    [InlineData("webp-vp8l", 400, 300)]
    [InlineData("webp-vp8", 640, 480)]
    [InlineData("jpeg-fill", 640, 480)]
    public void TryReadSize_OtherLayouts(string kind, int width, int height)
    {
        var vp8lBits = (uint)(width - 1) | ((uint)(height - 1) << 14);
        byte[] data = kind switch
        {
            "webp-vp8l" => [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. "VP8L"u8, 0, 0, 0, 0, 0x2F,
                (byte)vp8lBits, (byte)(vp8lBits >> 8), (byte)(vp8lBits >> 16), (byte)(vp8lBits >> 24), 0, 0, 0, 0, 0],
            "webp-vp8" => [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. "VP8 "u8, 0, 0, 0, 0, 0, 0, 0, 0x9D, 0x01, 0x2A,
                (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8)],
            _ => [0xFF, 0xD8, 0xFF, 0xFF, 0xE0, 0, 4, 0, 0, 0xFF, 0xC0, 0, 11, 8, 0x01, 0xE0, 0x02, 0x80, 3, 0, 0],
        };

        Assert.True(ChatPicturePreview.TryReadSize(data, out var readWidth, out var readHeight));
        Assert.Equal((width, height), (readWidth, readHeight));
    }

    [Theory(DisplayName = "A header that is cut short, broken or of another format gives no size")]
    [InlineData("short-png")]
    [InlineData("jpeg-no-marker")]
    [InlineData("jpeg-bad-length")]
    [InlineData("jpeg-no-frame")]
    [InlineData("webp-unknown-chunk")]
    [InlineData("png-zero-width")]
    [InlineData("text")]
    public void TryReadSize_Broken_GivesNothing(string kind)
    {
        byte[] data = kind switch
        {
            "short-png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0],
            "jpeg-no-marker" => [0xFF, 0xD8, 0x00, 0xE0, 0, 4, 0, 0, 0, 0, 0, 0],
            "jpeg-bad-length" => [0xFF, 0xD8, 0xFF, 0xE0, 0, 1, 0, 0, 0, 0, 0, 0],
            "jpeg-no-frame" => [0xFF, 0xD8, 0xFF, 0xE0, 0, 2, 0xFF, 0xE1, 0, 2, 0, 0],
            "webp-unknown-chunk" => [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. "ABCD"u8, .. new byte[16]],
            "png-zero-width" => [.. AttachmentTests.Png[..16], 0, 0, 0, 0, .. AttachmentTests.Png[20..]],
            _ => "Hello, this is not a picture."u8.ToArray(),
        };

        Assert.False(ChatPicturePreview.TryReadSize(data, out var width, out var height));
        Assert.Equal((0, 0), (width, height));
    }

    [Theory(DisplayName = "A picture larger than the limits, a long strip or another type gets no preview")]
    [InlineData(ChatAttachmentLimits.Png, 12000, 12000, false)]
    [InlineData(ChatAttachmentLimits.Png, 16, 999_999, false)]
    [InlineData(ChatAttachmentLimits.Png, 100, 1000, true)]
    [InlineData("image/bmp", 100, 100, false)]
    public void CanPreview_ChecksTypeSizeAndShape(string mediaType, int width, int height, bool allowed)
    {
        var png = (byte[])AttachmentTests.Png.Clone();
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20), height);

        Assert.Equal(allowed, ChatPicturePreview.CanPreview(new DataContent(png, mediaType)));
    }
}
