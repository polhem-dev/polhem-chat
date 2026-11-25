using System.Collections.ObjectModel;
using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// Files the user attaches to a prompt. Each file is checked against <see cref="AttachmentLimits"/> before it is read in
/// full, and waits in <see cref="PendingAttachments"/> until the prompt is sent.
/// </summary>
public sealed partial class ChatComposer
{
    private readonly ObservableCollection<DataContent> _pendingAttachments = [];
    private string? _attachmentError;

    /// <summary>Occurs when a file the user picked or dropped was not attached, with the reason.</summary>
    public event EventHandler<ChatAttachmentRejectedEventArgs>? AttachmentRejected;

    /// <summary>Gets the files attached to the prompt in the box, which are sent with it.</summary>
    public ReadOnlyObservableCollection<DataContent> PendingAttachments => field ??= new(_pendingAttachments);

    /// <summary>
    /// Gets the message for the last file that was not attached, formatted with <see cref="Strings"/>, or
    /// <see langword="null"/> once a file is attached or the prompt is sent.
    /// </summary>
    public string? AttachmentError
    {
        get => _attachmentError;
        private set => SetField(ref _attachmentError, value);
    }

    /// <summary>
    /// Attaches a file to the prompt in the box. Its size is asked first and checked against
    /// <see cref="AttachmentLimits"/>; the file is read only when its size allows it, and the reading stops past the limit,
    /// so a file that grows or misreports its size is never loaded in full.
    /// </summary>
    /// <param name="fileName">The name of the file; only the name is kept, without folders.</param>
    /// <param name="readSize">Returns the size the file reports, or <see langword="null"/> when it cannot tell.</param>
    /// <param name="open">Opens the file for reading; the composer closes the stream.</param>
    /// <returns>
    /// <see langword="null"/> when the file was attached, and why not otherwise; a file that is not attached also sets
    /// <see cref="AttachmentError"/> and raises <see cref="AttachmentRejected"/>.
    /// </returns>
    /// <remarks>
    /// A file that goes away or is refused while its size is read or while it is read is refused as
    /// <see cref="ChatAttachmentProblem.Unreadable"/>: either delegate may throw an <see cref="IOException"/>,
    /// <see cref="UnauthorizedAccessException"/>, <see cref="InvalidOperationException"/>,
    /// <see cref="NotSupportedException"/> or <see cref="ArgumentException"/>.
    /// </remarks>
    public async Task<ChatAttachmentProblem?> AddAttachmentAsync(string fileName, Func<Task<long?>> readSize, Func<Task<Stream>> open)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(readSize);
        ArgumentNullException.ThrowIfNull(open);
        var name = NameOnly(fileName);
        long? size;
        try
        {
            size = await readSize();
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            return Reject(name, ChatAttachmentProblem.Unreadable);
        }

        var limits = AttachmentLimits;
        // IMPORTANT: The size the file reports is checked before anything is read, and the reading stops past the limit,
        // so a file that grows or lies about its size is never loaded in full.
        if (size is { } reported && limits.CheckSize(reported, _pendingAttachments) is { } early)
        {
            return Reject(name, early);
        }

        byte[]? content;
        try
        {
            await using var stream = await open();
            content = await ReadUpToAsync(stream, limits.MaxFileSize);
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            content = null;
        }
        if (content is null)
        {
            return Reject(name, ChatAttachmentProblem.Unreadable);
        }
        if (content.Length > limits.MaxFileSize)
        {
            return Reject(name, ChatAttachmentProblem.TooLarge);
        }

        var check = limits.Check(name, content, [.. _pendingAttachments]);
        if (check.Attachment is not { } attachment)
        {
            return Reject(name, check.Problem!.Value);
        }
        _pendingAttachments.Add(attachment);
        AttachmentError = null;
        return null;
    }

    /// <summary>
    /// Attaches a file whose size is known, or <see langword="null"/>, with the checks of
    /// <see cref="AddAttachmentAsync(string, Func{Task{long?}}, Func{Task{Stream}})"/>.
    /// </summary>
    /// <param name="fileName">The name of the file; only the name is kept, without folders.</param>
    /// <param name="size">The size the file reports, or <see langword="null"/> when it cannot tell.</param>
    /// <param name="open">Opens the file for reading; the composer closes the stream.</param>
    /// <returns><see langword="null"/> when the file was attached, and why not otherwise.</returns>
    public Task<ChatAttachmentProblem?> AddAttachmentAsync(string fileName, long? size, Func<Task<Stream>> open) =>
        AddAttachmentAsync(fileName, () => Task.FromResult(size), open);

    /// <summary>
    /// Attaches content from a source of the application's own, such as a picture pasted from the clipboard, with the
    /// same checks as a file the user picks. The stream's length is checked first when it can tell it.
    /// </summary>
    /// <param name="fileName">The name to give the attachment; only the name is kept, without folders.</param>
    /// <param name="content">The content, read from its current position; the caller keeps ownership of it.</param>
    /// <returns>
    /// <see langword="null"/> when the content was attached, and why not otherwise; content that is not attached also
    /// sets <see cref="AttachmentError"/> and raises <see cref="AttachmentRejected"/>.
    /// </returns>
    public Task<ChatAttachmentProblem?> AddAttachmentAsync(string fileName, Stream content)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);
        long? size = content.CanSeek ? content.Length - content.Position : null;
        return AddAttachmentAsync(fileName, size, () => Task.FromResult<Stream>(new NonClosingStream(content)));
    }

    /// <summary>Removes one of the <see cref="PendingAttachments"/>.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <returns><see langword="true"/> when it was attached and is removed.</returns>
    public bool RemoveAttachment(DataContent attachment) => _pendingAttachments.Remove(attachment);

    // NOTE: The message on screen shows the name as the file has it, hidden characters escaped, so the user sees that a
    // name tried to pass for another. The event and the model get the cleaned name.
    private static string NameOnly(string name) => name[(name.LastIndexOfAny(['/', '\\']) + 1)..];

    // NOTE: A file can go away, or a platform's storage refuse it, between the moment the user picks or drops it and the
    // moment it is read; each storage provider reports that its own way. The file is then refused as unreadable instead
    // of ending the drop or the picking of the files after it.
    private static bool IsReadFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException;

    private static async Task<byte[]> ReadUpToAsync(Stream stream, long limit)
    {
        // One byte past the limit is enough to know the file is too large. The limit may be anything up to
        // long.MaxValue, and an array cannot be larger than Array.MaxLength.
        var cap = (int)Math.Min(Math.Max(limit, 0), Array.MaxLength - 1) + 1;
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while (buffer.Length < cap && (read = await stream.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, cap - buffer.Length)))) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, read));
        }
        return buffer.ToArray();
    }

    private ChatAttachmentProblem Reject(string fileName, ChatAttachmentProblem problem)
    {
        // NOTE: Notified even when the message is the same as the last one, so a view that hid it shows it again.
        _attachmentError = Strings.FormatAttachmentProblem(fileName, problem, AttachmentLimits);
        OnPropertyChanged(nameof(AttachmentError));
        AttachmentRejected?.Invoke(this, new ChatAttachmentRejectedEventArgs(ChatAttachmentLimits.FileNameOf(fileName), problem));
        return problem;
    }
}
