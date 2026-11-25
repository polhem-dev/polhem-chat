using System.Buffers.Binary;

namespace Polhem.Chat;

/// <summary>
/// Reads the size of a PNG, GIF, JPEG or WebP picture from its header, without decoding it. For a GIF, the size covers
/// every frame as well as the logical screen.
/// </summary>
internal static class ImageHeader
{
    /// <summary>Returns the width and height of the picture, or <see langword="null"/> when they cannot be read.</summary>
    public static (int Width, int Height)? ReadSize(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 24 && data[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Positive(BinaryPrimitives.ReadInt32BigEndian(data[16..]), BinaryPrimitives.ReadInt32BigEndian(data[20..]));
        }
        if (data.Length >= 13 && (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8)))
        {
            return ReadGif(data);
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

    // WARNING: A frame may reach past the logical screen the header declares, and browsers enlarge the picture to fit the
    // first frame: a GIF of a few dozen bytes that declares 64 x 64 and a 20000 x 20000 frame decodes to over a gigabyte.
    // So every frame descriptor counts, not only the screen.
    private static (int, int)? ReadGif(ReadOnlySpan<byte> data)
    {
        long width = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        long height = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]);
        var position = 13 + ColorTableSize(data[10]);
        while (position < data.Length)
        {
            var block = data[position];
            if (block == 0x2C && position + 10 <= data.Length)
            {
                var frame = data[(position + 1)..];
                width = Math.Max(width, BinaryPrimitives.ReadUInt16LittleEndian(frame) + (long)BinaryPrimitives.ReadUInt16LittleEndian(frame[4..]));
                height = Math.Max(height, BinaryPrimitives.ReadUInt16LittleEndian(frame[2..]) + (long)BinaryPrimitives.ReadUInt16LittleEndian(frame[6..]));
                // The descriptor, its color table and the code size byte come before the frame's data.
                position += 10 + ColorTableSize(data[position + 9]) + 1;
            }
            else if (block == 0x21)
            {
                // An extension: its introducer and label, then its data.
                position += 2;
            }
            else
            {
                // The trailer, a cut descriptor, or a block that is not GIF: nothing after it is drawn.
                break;
            }
            if (!SkipSubBlocks(data, ref position))
            {
                break;
            }
        }
        return Positive(width, height);
    }

    private static int ColorTableSize(byte packed) => (packed & 0x80) != 0 ? 3 << ((packed & 0x07) + 1) : 0;

    private static bool SkipSubBlocks(ReadOnlySpan<byte> data, ref int position)
    {
        while (position < data.Length)
        {
            var size = data[position];
            position += 1 + size;
            if (size == 0)
            {
                return true;
            }
        }
        return false;
    }

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
