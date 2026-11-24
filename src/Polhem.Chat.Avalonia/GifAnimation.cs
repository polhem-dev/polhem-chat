namespace Polhem.Chat.Avalonia;

/// <summary>The decoded frames of a GIF.</summary>
/// <param name="Width">The width of every frame, in pixels.</param>
/// <param name="Height">The height of every frame, in pixels.</param>
/// <param name="Frames">The frames, in order.</param>
internal sealed record GifAnimation(int Width, int Height, IReadOnlyList<GifFrame> Frames);
