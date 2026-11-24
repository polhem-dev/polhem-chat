namespace Polhem.Chat.Avalonia;

/// <summary>One whole picture of a GIF and how long it shows.</summary>
/// <param name="Pixels">BGRA pixels with premultiplied alpha, row by row.</param>
/// <param name="Duration">How long the frame shows.</param>
internal sealed record GifFrame(byte[] Pixels, TimeSpan Duration);
