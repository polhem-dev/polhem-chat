using Avalonia.Platform;

namespace Polhem.Chat.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class GifDecoderTests(HeadlessSession session)
{
    private static int Alpha(GifFrame frame, int width, int x, int y) => frame.Pixels[(y * width + x) * 4 + 3];

    [Fact(DisplayName = "The package's activity icon decodes into frames that differ, each with its duration and a transparent background")]
    public Task Decode_PackageIcon_HasAnimatedFrames() => session.RunAsync(() =>
    {
        using var stream = AssetLoader.Open(new Uri("avares://Polhem.Chat.Avalonia/Assets/activity.gif"));
        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        var animation = GifDecoder.Decode(memory.ToArray());

        Assert.Equal(32, animation.Width);
        Assert.Equal(32, animation.Height);
        Assert.Equal(12, animation.Frames.Count);
        Assert.All(animation.Frames, frame => Assert.Equal(TimeSpan.FromMilliseconds(80), frame.Duration));
        Assert.Equal(0, Alpha(animation.Frames[0], 32, 31, 0));
        Assert.Equal(255, Alpha(animation.Frames[0], 32, 6, 6));
        Assert.NotEqual(animation.Frames[0].Pixels, animation.Frames[6].Pixels);
    });

    [Fact(DisplayName = "A GIF from another encoder, whose codes grow past the first size, decodes to its picture")]
    public void Decode_ImageIoGif_ReadsThePicture()
    {
        var animation = GifDecoder.Decode(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "imageio.gif")));
        var frame = Assert.Single(animation.Frames);

        Assert.Equal(128, animation.Width);
        // The first dot of the 32-pixel icon, scaled four times, covers the point (26, 26); the corner stays empty.
        var center = (26 * 128 + 26) * 4;
        Assert.InRange(frame.Pixels[center + 2], 0xC0, 0xFF);
        Assert.InRange(frame.Pixels[center + 1], 0x50, 0x9F);
        var corner = (127 * 128 + 0) * 4;
        Assert.NotEqual(frame.Pixels[center + 2], frame.Pixels[corner + 2]);
    }

    [Fact(DisplayName = "Data that is not a GIF, or that ends early, is refused")]
    public void Decode_BadData_Throws()
    {
        Assert.Throws<InvalidDataException>(() => GifDecoder.Decode("PNG"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => GifDecoder.Decode("GIF89a\x01\x00"u8.ToArray()));
    }
}
