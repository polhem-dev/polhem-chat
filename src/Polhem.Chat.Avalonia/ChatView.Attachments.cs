using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// Files the user attaches to a prompt: with the attach button, or by dropping them on the view. Each file is checked
/// against <see cref="AttachmentLimits"/> before it is read in full, and waits in <see cref="PendingAttachments"/> until
/// the prompt is sent.
/// </summary>
public partial class ChatView
{
    /// <summary>Defines the <see cref="AllowsAttachments"/> property.</summary>
    public static readonly StyledProperty<bool> AllowsAttachmentsProperty =
        AvaloniaProperty.Register<ChatView, bool>(nameof(AllowsAttachments));

    /// <summary>Defines the <see cref="AttachmentLimits"/> property.</summary>
    public static readonly StyledProperty<ChatAttachmentLimits> AttachmentLimitsProperty =
        AvaloniaProperty.Register<ChatView, ChatAttachmentLimits>(nameof(AttachmentLimits), defaultValue: null!, coerce: CoerceLimits);

    private const string AttachmentErrorPart = "PART_AttachmentError";

    private readonly ChatAttachmentLimits _defaultLimits = new();
    private ViewCommand? _attachCommand;
    private ViewCommand? _removeAttachmentCommand;
    private TextBlock? _attachmentError;

    /// <summary>Occurs when a file the user picked or dropped was not attached, with the reason.</summary>
    public event EventHandler<ChatAttachmentRejectedEventArgs>? AttachmentRejected;

    /// <summary>
    /// Gets or sets a value indicating whether the user can attach files to a prompt, with the attach button or by
    /// dropping them on the view. It is off by default.
    /// </summary>
    /// <remarks>
    /// Attaching is checked on the desktop. On iOS and Android the file picker may work, but the package does not test
    /// it there yet.
    /// </remarks>
    public bool AllowsAttachments
    {
        get => GetValue(AllowsAttachmentsProperty);
        set => SetValue(AllowsAttachmentsProperty, value);
    }

    /// <summary>
    /// Gets or sets how many files a prompt may carry, how large and of which types. Setting <see langword="null"/>
    /// restores the defaults of <see cref="ChatAttachmentLimits"/>.
    /// </summary>
    public ChatAttachmentLimits AttachmentLimits
    {
        get => GetValue(AttachmentLimitsProperty);
        set => SetValue(AttachmentLimitsProperty, value);
    }

    /// <summary>Gets the files attached to the prompt in the box, which are sent with it.</summary>
    public ReadOnlyObservableCollection<DataContent> PendingAttachments => _composer.PendingAttachments;

    /// <summary>Gets the command that opens the file picker and attaches the files the user picks.</summary>
    public ICommand AttachCommand => _attachCommand ??= new ViewCommand(_ => _ = PickAttachmentsAsync(), _ => AllowsAttachments);

    /// <summary>Gets the command that removes one of the <see cref="PendingAttachments"/>, its parameter.</summary>
    public ICommand RemoveAttachmentCommand => _removeAttachmentCommand ??= new ViewCommand(
        parameter => _composer.RemoveAttachment((DataContent)parameter!),
        parameter => parameter is DataContent attachment && PendingAttachments.Contains(attachment));

    /// <summary>
    /// Attaches a file to the prompt in the box, as the attach button does: the file is checked against
    /// <see cref="AttachmentLimits"/>, read only when its size allows it, and added to <see cref="PendingAttachments"/>.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>
    /// <see langword="null"/> when the file was attached, and why not otherwise; a file that is not attached also raises
    /// <see cref="AttachmentRejected"/>.
    /// </returns>
    public async Task<ChatAttachmentProblem?> AddAttachmentAsync(IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return await _composer.AddAttachmentAsync(file.Name, async () => (long?)(await file.GetBasicPropertiesAsync()).Size, file.OpenReadAsync);
    }

    /// <summary>
    /// Attaches content from a source of the application's own, such as a picture pasted from the clipboard, with the
    /// same checks as a file the user picks. The stream's length is checked first when it can tell it.
    /// </summary>
    /// <param name="fileName">The name to give the attachment; only the name is kept, without folders.</param>
    /// <param name="content">The content, read from its current position; the caller keeps ownership of it.</param>
    /// <returns>
    /// <see langword="null"/> when the content was attached, and why not otherwise; content that is not attached also
    /// raises <see cref="AttachmentRejected"/>.
    /// </returns>
    public Task<ChatAttachmentProblem?> AddAttachmentAsync(string fileName, Stream content) =>
        _composer.AddAttachmentAsync(fileName, content);

    /// <summary>Checks, reads and attaches a file, whatever it comes from.</summary>
    internal Task<ChatAttachmentProblem?> AddAttachmentAsync(string name, long? size, Func<Task<Stream>> open) =>
        _composer.AddAttachmentAsync(name, size, open);

    private static ChatAttachmentLimits CoerceLimits(AvaloniaObject owner, ChatAttachmentLimits? value) =>
        value ?? ((ChatView)owner)._defaultLimits;

    private void OnComposerAttachmentRejected(object? sender, ChatAttachmentRejectedEventArgs e) =>
        AttachmentRejected?.Invoke(this, e);

    private void ShowAttachmentError(string? message)
    {
        if (_attachmentError is not null)
        {
            _attachmentError.Text = message ?? string.Empty;
            _attachmentError.IsVisible = message is not null;
        }
    }

    private async Task PickAttachmentsAsync()
    {
        if (!AllowsAttachments || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            return;
        }
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType(Strings.Attach) { MimeTypes = [.. AttachmentLimits.AllowedMediaTypes] }],
        });
        foreach (var file in files)
        {
            await AddAttachmentAsync(file);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = AllowsAttachments && e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!AllowsAttachments || e.DataTransfer.TryGetFiles() is not { } items)
        {
            return;
        }
        foreach (var file in items.OfType<IStorageFile>())
        {
            await AddAttachmentAsync(file);
        }
    }

    private void TakeAttachmentError(INameScope scope)
    {
        _attachmentError = scope.Find<TextBlock>(AttachmentErrorPart);
        ShowAttachmentError(null);
    }
}
