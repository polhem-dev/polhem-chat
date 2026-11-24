using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Polhem.Chat.Avalonia;

/// <summary>Plays the frames of a decoded GIF, scaled to its size, while <see cref="IsPlaying"/> is set.</summary>
internal sealed class GifPlayer : Control
{
    private readonly DispatcherTimer _timer = new();
    private WriteableBitmap[] _frames = [];
    private GifAnimation? _animation;
    private int _current;
    private bool _isPlaying;
    private bool _isAttached;

    public GifPlayer()
    {
        _timer.Tick += (_, _) => Advance();
    }

    /// <summary>Gets or sets the animation to play.</summary>
    public GifAnimation? Animation
    {
        get => _animation;
        set
        {
            if (ReferenceEquals(_animation, value))
            {
                return;
            }
            DisposeFrames();
            _animation = value;
            _frames = value is null ? [] : [.. value.Frames.Select(frame => CreateBitmap(value, frame))];
            _current = 0;
            InvalidateVisual();
            Schedule();
        }
    }

    /// <summary>Gets or sets a value indicating whether the frames advance; stopped, the first frame shows.</summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying == value)
            {
                return;
            }
            _isPlaying = value;
            _current = 0;
            InvalidateVisual();
            Schedule();
        }
    }

    /// <summary>Gets the index of the frame that shows.</summary>
    internal int CurrentFrame => _current;

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_frames.Length == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var frame = _frames[_current];
        var scale = Math.Min(Bounds.Width / frame.PixelSize.Width, Bounds.Height / frame.PixelSize.Height);
        var size = new Size(frame.PixelSize.Width * scale, frame.PixelSize.Height * scale);
        var origin = new Point((Bounds.Width - size.Width) / 2, (Bounds.Height - size.Height) / 2);
        context.DrawImage(frame, new Rect(origin, size));
    }

    /// <summary>Shows the next frame.</summary>
    internal void Advance()
    {
        if (_frames.Length == 0)
        {
            return;
        }
        _current = (_current + 1) % _frames.Length;
        InvalidateVisual();
        Schedule();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        Schedule();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        Schedule();
        base.OnDetachedFromVisualTree(e);
    }

    // NOTE: The timer runs only while the control is in a visual tree and playing, so a closed view does not keep it
    // alive; each frame keeps its own duration.
    private void Schedule()
    {
        _timer.Stop();
        if (_isAttached && _isPlaying && _animation is { Frames.Count: > 1 } animation)
        {
            _timer.Interval = animation.Frames[_current].Duration;
            _timer.Start();
        }
    }

    private static WriteableBitmap CreateBitmap(GifAnimation animation, GifFrame frame)
    {
        var bitmap = new WriteableBitmap(new PixelSize(animation.Width, animation.Height), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var buffer = bitmap.Lock();
        var rowLength = animation.Width * 4;
        for (var row = 0; row < animation.Height; row++)
        {
            Marshal.Copy(frame.Pixels, row * rowLength, buffer.Address + row * buffer.RowBytes, rowLength);
        }
        return bitmap;
    }

    private void DisposeFrames()
    {
        foreach (var frame in _frames)
        {
            frame.Dispose();
        }
        _frames = [];
    }
}
