namespace Polhem.Chat.Avalonia;

/// <summary>
/// Reads the frames of an animated GIF into whole pictures, each with how long it shows. Avalonia draws only the first
/// frame of a GIF, so the activity line decodes them itself.
/// </summary>
/// <remarks>
/// Covers what an animated icon uses: global and local color tables, transparency, interlacing and the frame disposal
/// methods. Plain-text extensions and the aspect ratio are ignored.
/// </remarks>
internal static class GifDecoder
{
    private const int MaxCodes = 4096;

    /// <summary>Decodes a GIF.</summary>
    /// <param name="data">The file's bytes.</param>
    /// <returns>The frames, as BGRA pixels with premultiplied alpha, in order.</returns>
    /// <exception cref="InvalidDataException">The data is not a GIF this decoder can read.</exception>
    public static GifAnimation Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var reader = new Reader(data);
        var signature = reader.ReadAscii(6);
        if (signature is not ("GIF87a" or "GIF89a"))
        {
            throw new InvalidDataException("The data is not a GIF.");
        }
        var width = reader.ReadUInt16();
        var height = reader.ReadUInt16();
        if (width == 0 || height == 0)
        {
            throw new InvalidDataException("The GIF has no size.");
        }
        var packed = reader.ReadByte();
        reader.ReadByte(); // Background color index: a transparent canvas is used instead.
        reader.ReadByte(); // Pixel aspect ratio.
        var globalColors = (packed & 0x80) != 0 ? ReadColorTable(reader, 2 << (packed & 7)) : null;

        var canvas = new uint[width * height];
        var frames = new List<GifFrame>();
        var delay = 0;
        var disposal = 0;
        var transparentIndex = -1;
        while (true)
        {
            var block = reader.ReadByte();
            if (block == 0x3B)
            {
                break;
            }
            if (block == 0x21)
            {
                var label = reader.ReadByte();
                if (label == 0xF9)
                {
                    reader.ReadByte(); // Block size, always 4.
                    var flags = reader.ReadByte();
                    disposal = (flags >> 2) & 7;
                    delay = reader.ReadUInt16() * 10;
                    var index = reader.ReadByte();
                    transparentIndex = (flags & 1) != 0 ? index : -1;
                    reader.SkipSubBlocks();
                }
                else
                {
                    reader.SkipSubBlocks();
                }
                continue;
            }
            if (block != 0x2C)
            {
                throw new InvalidDataException($"The GIF has an unknown block 0x{block:X2}.");
            }

            var left = reader.ReadUInt16();
            var top = reader.ReadUInt16();
            var frameWidth = reader.ReadUInt16();
            var frameHeight = reader.ReadUInt16();
            var framePacked = reader.ReadByte();
            var colors = (framePacked & 0x80) != 0 ? ReadColorTable(reader, 2 << (framePacked & 7)) : globalColors;
            if (colors is null)
            {
                throw new InvalidDataException("A GIF frame has no color table.");
            }
            var minimumCodeSize = reader.ReadByte();
            var indices = DecodeLzw(reader.ReadSubBlocks(), minimumCodeSize, frameWidth * frameHeight);
            var interlaced = (framePacked & 0x40) != 0;

            var before = disposal == 3 ? (uint[])canvas.Clone() : null;
            for (var row = 0; row < frameHeight; row++)
            {
                var y = top + (interlaced ? InterlacedRow(row, frameHeight) : row);
                if (y >= height)
                {
                    continue;
                }
                for (var column = 0; column < frameWidth; column++)
                {
                    var x = left + column;
                    var index = indices[row * frameWidth + column];
                    if (x < width && index != transparentIndex && index < colors.Length)
                    {
                        canvas[y * width + x] = colors[index];
                    }
                }
            }
            frames.Add(new GifFrame(ToPremultipliedBgra(canvas), TimeSpan.FromMilliseconds(delay > 0 ? delay : 100)));

            if (disposal == 2)
            {
                for (var y = top; y < Math.Min(top + frameHeight, height); y++)
                {
                    Array.Clear(canvas, y * width + left, Math.Max(0, Math.Min(frameWidth, width - left)));
                }
            }
            else if (before is not null)
            {
                canvas = before;
            }
            delay = 0;
            disposal = 0;
            transparentIndex = -1;
        }
        if (frames.Count == 0)
        {
            throw new InvalidDataException("The GIF has no frames.");
        }
        return new GifAnimation(width, height, frames);
    }

    private static uint[] ReadColorTable(Reader reader, int count)
    {
        var colors = new uint[count];
        for (var index = 0; index < count; index++)
        {
            var red = reader.ReadByte();
            var green = reader.ReadByte();
            var blue = reader.ReadByte();
            colors[index] = 0xFF000000u | ((uint)red << 16) | ((uint)green << 8) | blue;
        }
        return colors;
    }

    // Rows of an interlaced frame arrive in four passes: every 8th row from 0, every 8th from 4, every 4th from 2, every
    // 2nd from 1.
    private static int InterlacedRow(int row, int height)
    {
        ReadOnlySpan<(int Start, int Step)> passes = [(0, 8), (4, 8), (2, 4), (1, 2)];
        foreach (var (start, step) in passes)
        {
            var rows = (height - start + step - 1) / step;
            if (row < rows)
            {
                return start + row * step;
            }
            row -= rows;
        }
        return row;
    }

    private static byte[] DecodeLzw(byte[] data, int minimumCodeSize, int pixelCount)
    {
        if (minimumCodeSize is < 1 or > 11)
        {
            throw new InvalidDataException("A GIF frame has an invalid code size.");
        }
        var output = new byte[pixelCount];
        var prefix = new short[MaxCodes];
        var suffix = new byte[MaxCodes];
        var stack = new byte[MaxCodes + 1];
        var clear = 1 << minimumCodeSize;
        var end = clear + 1;
        var codeSize = minimumCodeSize + 1;
        var next = end + 1;
        var previous = -1;
        byte first = 0;
        var written = 0;
        var bitBuffer = 0;
        var bitCount = 0;
        var position = 0;

        for (var code = 0; code < clear; code++)
        {
            suffix[code] = (byte)code;
        }
        while (written < pixelCount)
        {
            while (bitCount < codeSize)
            {
                if (position >= data.Length)
                {
                    return output;
                }
                bitBuffer |= data[position++] << bitCount;
                bitCount += 8;
            }
            var code = bitBuffer & ((1 << codeSize) - 1);
            bitBuffer >>= codeSize;
            bitCount -= codeSize;

            if (code == clear)
            {
                codeSize = minimumCodeSize + 1;
                next = end + 1;
                previous = -1;
                continue;
            }
            if (code == end)
            {
                break;
            }
            if (previous == -1)
            {
                if (code >= clear)
                {
                    throw new InvalidDataException("A GIF frame's data is corrupt.");
                }
                output[written++] = suffix[code];
                previous = code;
                first = (byte)code;
                continue;
            }

            var current = code;
            var top = 0;
            if (code >= next)
            {
                if (code > next)
                {
                    throw new InvalidDataException("A GIF frame's data is corrupt.");
                }
                stack[top++] = first;
                current = previous;
            }
            while (current >= clear)
            {
                stack[top++] = suffix[current];
                current = prefix[current];
            }
            first = suffix[current];
            stack[top++] = first;
            while (top > 0 && written < pixelCount)
            {
                output[written++] = stack[--top];
            }
            if (next < MaxCodes)
            {
                prefix[next] = (short)previous;
                suffix[next] = first;
                next++;
                if (next == 1 << codeSize && codeSize < 12)
                {
                    codeSize++;
                }
            }
            previous = code;
        }
        return output;
    }

    private static byte[] ToPremultipliedBgra(uint[] canvas)
    {
        // Every pixel is opaque or fully transparent, so premultiplying leaves the colors as they are.
        var pixels = new byte[canvas.Length * 4];
        for (var index = 0; index < canvas.Length; index++)
        {
            var color = canvas[index];
            pixels[index * 4] = (byte)color;
            pixels[index * 4 + 1] = (byte)(color >> 8);
            pixels[index * 4 + 2] = (byte)(color >> 16);
            pixels[index * 4 + 3] = (byte)(color >> 24);
        }
        return pixels;
    }

    private sealed class Reader(byte[] data)
    {
        private int _position;

        public byte ReadByte() =>
            _position < data.Length ? data[_position++] : throw new InvalidDataException("The GIF ends too early.");

        public int ReadUInt16() => ReadByte() | (ReadByte() << 8);

        public string ReadAscii(int length)
        {
            var characters = new char[length];
            for (var index = 0; index < length; index++)
            {
                characters[index] = (char)ReadByte();
            }
            return new string(characters);
        }

        public byte[] ReadSubBlocks()
        {
            var bytes = new List<byte>();
            for (var size = ReadByte(); size > 0; size = ReadByte())
            {
                for (var index = 0; index < size; index++)
                {
                    bytes.Add(ReadByte());
                }
            }
            return [.. bytes];
        }

        public void SkipSubBlocks()
        {
            for (var size = ReadByte(); size > 0; size = ReadByte())
            {
                _position += size;
            }
        }
    }
}
