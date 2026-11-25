using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Unicode;
using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// What a prompt may carry: how many files, how large, and of which types. <see cref="Check"/> reads the type of a file
/// from its content and turns an allowed file into the <see cref="DataContent"/> sent to the model.
/// </summary>
/// <remarks>
/// <para>
/// The types are an allowlist. A file's type is read from its first bytes for pictures and PDF, and a name that says
/// otherwise is rejected, so a program named like a picture is not sent. Text types are taken from the name and must be
/// valid UTF-8 without NUL characters. The model receives the name of the file only, never the folders of its path.
/// </para>
/// <para>
/// A file's content is data from outside, and a model may follow instructions written in it. The package sends it as it
/// is, as a <see cref="DataContent"/>, and never writes it into the prompt's text.
/// </para>
/// </remarks>
public class ChatAttachmentLimits
{
    /// <summary>The media type of PNG pictures.</summary>
    public const string Png = "image/png";

    /// <summary>The media type of JPEG pictures.</summary>
    public const string Jpeg = "image/jpeg";

    /// <summary>The media type of GIF pictures.</summary>
    public const string Gif = "image/gif";

    /// <summary>The media type of WebP pictures.</summary>
    public const string WebP = "image/webp";

    /// <summary>The media type of PDF documents.</summary>
    public const string Pdf = "application/pdf";

    /// <summary>The media type of plain text.</summary>
    public const string PlainText = "text/plain";

    /// <summary>The media type of Markdown.</summary>
    public const string Markdown = "text/markdown";

    /// <summary>The media type of CSV.</summary>
    public const string Csv = "text/csv";

    /// <summary>The media type of JSON.</summary>
    public const string Json = "application/json";

    private static readonly Dictionary<string, string> s_extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = Png,
        [".jpg"] = Jpeg,
        [".jpeg"] = Jpeg,
        [".jpe"] = Jpeg,
        [".jfif"] = Jpeg,
        [".gif"] = Gif,
        [".webp"] = WebP,
        [".pdf"] = Pdf,
        [".txt"] = PlainText,
        [".md"] = Markdown,
        [".markdown"] = Markdown,
        [".csv"] = Csv,
        [".json"] = Json,
    };

    private static readonly HashSet<string> s_textTypes = [PlainText, Markdown, Csv, Json];

    private static readonly SearchValues<char> s_digits = SearchValues.Create("0123456789");

    /// <summary>Gets or sets the largest file, in bytes. The default is 5 MB.</summary>
    /// <remarks>
    /// Providers have limits of their own, often lower for pictures than for documents. The default stays within the
    /// picture limit of common providers (Anthropic takes pictures up to 5 MB); a file over a provider's limit passes here
    /// and fails the turn there, so set the limit of the provider in use.
    /// </remarks>
    public long MaxFileSize { get; set; } = 5L * 1024 * 1024;

    /// <summary>Gets or sets how many files one prompt may carry. The default is 5.</summary>
    public int MaxCount { get; set; } = 5;

    /// <summary>Gets or sets the largest size of the files of one prompt together, in bytes. The default is 20 MB.</summary>
    public long MaxTotalSize { get; set; } = 20L * 1024 * 1024;

    /// <summary>
    /// Gets the media types a prompt may carry. The default is PNG, JPEG, GIF, WebP and PDF, which most models with vision
    /// read. Text types (<see cref="PlainText"/>, <see cref="Markdown"/>, <see cref="Csv"/>, <see cref="Json"/>) are known
    /// but not allowed by default, because many providers do not take them as attachments: add them when the application
    /// sends them in a form its provider takes.
    /// </summary>
    public ISet<string> AllowedMediaTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Png, Jpeg, Gif, WebP, Pdf };

    /// <summary>
    /// Checks a file's size against the limits before it is read, so a file too large is never loaded.
    /// </summary>
    /// <param name="size">The size the file reports, in bytes.</param>
    /// <param name="pending">The attachments the prompt carries already.</param>
    /// <returns>Why the file cannot be attached, or <see langword="null"/> when its size and the count allow it.</returns>
    public ChatAttachmentProblem? CheckSize(long size, IReadOnlyCollection<DataContent> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        if (pending.Count >= MaxCount)
        {
            return ChatAttachmentProblem.TooMany;
        }
        if (size > MaxFileSize)
        {
            return ChatAttachmentProblem.TooLarge;
        }
        // Compared by subtraction, so a limit near long.MaxValue cannot overflow the sum.
        var total = pending.Sum(attachment => (long)attachment.Data.Length);
        return size > MaxTotalSize - total ? ChatAttachmentProblem.TotalTooLarge : null;
    }

    /// <summary>
    /// Checks a file and, when the limits allow it, returns it as an attachment whose media type was read from the
    /// content.
    /// </summary>
    /// <param name="fileName">The name of the file, with or without its folders; only the name is kept.</param>
    /// <param name="content">The whole content of the file.</param>
    /// <param name="pending">The attachments the prompt carries already.</param>
    /// <returns>The attachment, or why there is none.</returns>
    public ChatAttachmentCheck Check(string fileName, ReadOnlySpan<byte> content, IReadOnlyCollection<DataContent> pending)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var name = FileNameOf(fileName);
        if (name.Length == 0)
        {
            return ChatAttachmentCheck.Rejected(name, ChatAttachmentProblem.Unreadable);
        }
        if (CheckSize(content.Length, pending) is { } problem)
        {
            return ChatAttachmentCheck.Rejected(name, problem);
        }
        if (content.IsEmpty)
        {
            return ChatAttachmentCheck.Rejected(name, ChatAttachmentProblem.Unreadable);
        }

        var (mediaType, typeProblem) = ReadType(name, content);
        if (typeProblem is { } wrongType)
        {
            return ChatAttachmentCheck.Rejected(name, wrongType);
        }
        if (!AllowedMediaTypes.Contains(mediaType!))
        {
            return ChatAttachmentCheck.Rejected(name, ChatAttachmentProblem.TypeNotAllowed);
        }
        return ChatAttachmentCheck.Allowed(name, new DataContent(content.ToArray(), mediaType!) { Name = name });
    }

    /// <summary>Reads the media type of a file from its content and its name, or why it has none it may have.</summary>
    private static (string? MediaType, ChatAttachmentProblem? Problem) ReadType(string name, ReadOnlySpan<byte> content)
    {
        var extension = TypeExtension(name);
        var named = s_extensions.GetValueOrDefault(extension);
        // A name with an extension this package does not know says a type it cannot check, such as photo.png.exe.
        if (named is null && extension.Length > 0)
        {
            return (null, ChatAttachmentProblem.TypeNotAllowed);
        }
        if (Sniff(content) is { } sniffed)
        {
            return named is null || named == sniffed ? (sniffed, null) : (null, ChatAttachmentProblem.ContentMismatch);
        }
        if (named is not null && s_textTypes.Contains(named))
        {
            return IsText(content) ? (named, null) : (null, ChatAttachmentProblem.Unreadable);
        }
        // A known binary type whose content does not start the way that type does, or a name with no known type.
        return (null, named is null ? ChatAttachmentProblem.TypeNotAllowed : ChatAttachmentProblem.ContentMismatch);
    }

    /// <summary>
    /// Returns the name a file is sent under: without the folders of its path, whichever separator it uses, without
    /// characters that draw nothing or reorder the text, and without trailing dots and spaces.
    /// </summary>
    /// <param name="path">A file name or path.</param>
    /// <returns>The name; empty when nothing is left.</returns>
    /// <remarks>
    /// A name from another system, or one written to look like a path, must not carry its folders to the model, and a
    /// name must not use hidden characters or a trailing dot (<c>photo.png.exe.</c>) to pass for another.
    /// </remarks>
    public static string FileNameOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var name = path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
        // NOTE: Compatibility forms become their plain letters, and the characters that look like a dot become one, so
        // that "invoice\u2024exe" or a name in mathematical letters says what it reads as.
        name = name.Normalize(NormalizationForm.FormKC).Replace('\u3002', '.').Replace('\uFF61', '.');
        name = string.Concat(name.EnumerateRunes()
            .Where(rune => Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator))
            .Select(rune => rune.ToString())).TrimStart();
        // Spaces of any kind and dots, in any order, until none is left at the end; the result does not change when
        // cleaned again.
        var end = name.Length;
        while (end > 0 && (name[end - 1] == '.' || char.IsWhiteSpace(name[end - 1])))
        {
            end--;
        }
        return name[..end];
    }

    /// <summary>
    /// Returns the extension that says the type of a file. Parts after the last dots made of ASCII digits only, as in
    /// "Scan 2026.11.25", are not extensions and are passed over, so "photo.exe.1" still says ".exe". Any other
    /// character counts, since it may be a letter of another script or a symbol that passes for one.
    /// </summary>
    private static string TypeExtension(string name)
    {
        var end = name.Length;
        while (true)
        {
            var dot = name.LastIndexOf('.', end - 1, end);
            if (dot < 0 || dot == end - 1)
            {
                return string.Empty;
            }
            var extension = name.AsSpan(dot, end - dot);
            if (extension[1..].ContainsAnyExcept(s_digits) || dot == 0)
            {
                return extension.ToString();
            }
            end = dot;
        }
    }

    private static string? Sniff(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }
        if (content.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return Jpeg;
        }
        if (content.StartsWith("GIF87a"u8) || content.StartsWith("GIF89a"u8))
        {
            return Gif;
        }
        if (content.Length >= 12 && content.StartsWith("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8))
        {
            return WebP;
        }
        return content.StartsWith("%PDF-"u8) ? Pdf : null;
    }

    private static bool IsText(ReadOnlySpan<byte> content) =>
        !content.Contains((byte)0) && Utf8.IsValid(content);
}
