using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Platform;

namespace Polhem.Chat.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class GifDecoderTests(HeadlessSession session)
{
    private static int Alpha(GifFrame frame, int width, int x, int y) => frame.Pixels[(y * width + x) * 4 + 3];

    [Fact(DisplayName = "The package's spinner decodes into frames that differ, each with its duration and a transparent background")]
    public Task Decode_PackageIcon_HasAnimatedFrames() => session.RunAsync(() =>
    {
        using var stream = AssetLoader.Open(new Uri("avares://Polhem.Chat.Avalonia/Assets/activity.gif"));
        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        var animation = GifDecoder.Decode(memory.ToArray());

        Assert.Equal(64, animation.Width);
        Assert.Equal(64, animation.Height);
        Assert.Equal(12, animation.Frames.Count);
        Assert.All(animation.Frames, frame => Assert.Equal(TimeSpan.FromMilliseconds(70), frame.Duration));
        // A ring of dots led by the one at the top in the first frame; the corners and the middle stay empty.
        Assert.Equal(0, Alpha(animation.Frames[0], 64, 0, 0));
        Assert.Equal(0, Alpha(animation.Frames[0], 64, 32, 32));
        Assert.Equal(255, Alpha(animation.Frames[0], 64, 32, 10));
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
        // The pixels are in RGBA order, so the orange dot's red comes first.
        Assert.InRange(frame.Pixels[center], 0xC0, 0xFF);
        Assert.InRange(frame.Pixels[center + 1], 0x50, 0x9F);
        Assert.InRange(frame.Pixels[center + 2], 0x00, 0x7F);
        var corner = (127 * 128 + 0) * 4;
        Assert.NotEqual(frame.Pixels[center], frame.Pixels[corner]);
    }

    [Fact(DisplayName = "Data that is not a GIF, or that ends early, is refused")]
    public void Decode_BadData_Throws()
    {
        Assert.Throws<InvalidDataException>(() => GifDecoder.Decode("PNG"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => GifDecoder.Decode("GIF89a\x01\x00"u8.ToArray()));
    }

    [Fact(DisplayName = "The player draws the spinner in its own colors, so its dots come out orange and not blue")]
    public Task Render_PackageIcon_DrawsOrangeDots() => session.RunAsync(() =>
    {
        using var stream = AssetLoader.Open(new Uri("avares://Polhem.Chat.Avalonia/Assets/activity.gif"));
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var player = new GifPlayer { Animation = GifDecoder.Decode(memory.ToArray()), Width = 64, Height = 64 };
        var window = new Window { Width = 64, Height = 64, Background = Brushes.White, Content = player };
        window.Show();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var buffer = frame.Lock();
        var pixels = new byte[buffer.RowBytes * buffer.Size.Height];
        Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
        var (red, blue) = buffer.Format == PixelFormat.Rgba8888 ? (0, 2) : (2, 0);
        var orange = 0;
        var bluish = 0;
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            orange += pixels[offset + red] - pixels[offset + blue] > 80 ? 1 : 0;
            bluish += pixels[offset + blue] - pixels[offset + red] > 80 ? 1 : 0;
        }
        Assert.True(orange > 100, $"Only {orange} orange pixels were drawn.");
        Assert.Equal(0, bluish);
    });
}
