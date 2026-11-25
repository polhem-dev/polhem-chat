using System.Buffers.Binary;

namespace Polhem.Chat.Avalonia;

/// <summary>Reads the size of a PNG, GIF, JPEG or WebP picture from its first bytes, without decoding it.</summary>
internal static class ImageHeader
{
    /// <summary>Returns the width and height of the picture, or <see langword="null"/> when they cannot be read.</summary>
    public static (int Width, int Height)? ReadSize(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 24 && data[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Positive(BinaryPrimitives.ReadInt32BigEndian(data[16..]), BinaryPrimitives.ReadInt32BigEndian(data[20..]));
        }
        if (data.Length >= 10 && (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8)))
        {
            return Positive(BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), BinaryPrimitives.ReadUInt16LittleEndian(data[8..]));
        }
        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8)
        {
            return ReadJpeg(data);
        }
        if (data.Length >= 30 && data.StartsWith("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return ReadWebP(data);
        }
        return null;
    }

    private static (int, int)? Positive(long width, long height) =>
        width is > 0 and <= int.MaxValue && height is > 0 and <= int.MaxValue ? ((int)width, (int)height) : null;

    private static (int, int)? ReadJpeg(ReadOnlySpan<byte> data)
    {
        var position = 2;
        while (position + 9 < data.Length)
        {
            if (data[position] != 0xFF)
            {
                return null;
            }
            var marker = data[position + 1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }
            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 2)..]);
            // Start-of-frame markers carry the size; 0xC4, 0xC8 and 0xCC are other segments in the same range.
            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
            {
                return Positive(BinaryPrimitives.ReadUInt16BigEndian(data[(position + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(data[(position + 5)..]));
            }
            if (length < 2)
            {
                return null;
            }
            position += 2 + length;
        }
        return null;
    }

    private static (int, int)? ReadWebP(ReadOnlySpan<byte> data)
    {
        var chunk = data[12..16];
        if (chunk.SequenceEqual("VP8X"u8))
        {
            return Positive(1 + ReadUInt24(data[24..]), 1 + ReadUInt24(data[27..]));
        }
        if (chunk.SequenceEqual("VP8L"u8) && data[20] == 0x2F)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[21..]);
            return Positive(1 + (bits & 0x3FFF), 1 + ((bits >> 14) & 0x3FFF));
        }
        if (chunk.SequenceEqual("VP8 "u8))
        {
            return Positive(BinaryPrimitives.ReadUInt16LittleEndian(data[26..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(data[28..]) & 0x3FFF);
        }
        return null;
    }

    private static long ReadUInt24(ReadOnlySpan<byte> data) => data[0] | (data[1] << 8) | (data[2] << 16);
}
