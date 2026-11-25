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
