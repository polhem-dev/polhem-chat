using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.Blazor;

/// <summary>
/// The prompt box under the conversation and what the user does with it: submitting a prompt or a suggestion, stopping a
/// reply, sending the last prompt again or changing it, and attaching files with the attach button, by dropping them on
/// the view or by pasting a picture.
/// </summary>
public sealed partial class ChatView
{
    private ElementReference _promptBox;
    private string? _promptParameter;
    private bool _applyingPrompt;
    private bool _promptBound;
    private Task _reading = Task.CompletedTask;
    private ChatAttachmentLimits? _limitsParameter;

    /// <summary>
    /// Gets or sets the text in the prompt box. The view raises <see cref="PromptChanged"/> as the user types and when it
    /// changes the text itself, so it binds two-way with <c>@bind-Prompt</c>.
    /// </summary>
    [Parameter]
    public string? Prompt { get; set; }

    /// <summary>Gets or sets the callback raised when the text in the prompt box changes.</summary>
    [Parameter]
    public EventCallback<string?> PromptChanged { get; set; }

    /// <summary>
    /// Gets or sets the prompts offered while the conversation is empty. Clicking one submits it, as if the user had typed
    /// it, without the files attached to the box.
    /// </summary>
    [Parameter]
    public IEnumerable<string>? Suggestions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user can send the last prompt again or change it. By default,
    /// <see langword="null"/>, they can when the view has a <see cref="Runner"/>, and cannot when the application drives
    /// the conversation itself: what the tools of the replaced turns did is not undone, and only the application knows
    /// whether that is safe.
    /// </summary>
    [Parameter]
    public bool? AllowsRewind { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user can attach files to a prompt, with the attach button, by dropping
    /// them on the view or by pasting a picture in the prompt box. It is off by default.
    /// </summary>
    /// <remarks>
    /// On Blazor Server the browser streams each file to the server in pieces, so the hub's
    /// <c>MaximumReceiveMessageSize</c> does not limit it; <see cref="AttachmentLimits"/> does. The files wait in the
    /// server's memory until the prompt is sent.
    /// </remarks>
    [Parameter]
    public bool AllowsAttachments { get; set; }

    /// <summary>
    /// Gets or sets how many files a prompt may carry, how large and of which types. <see langword="null"/> uses the
    /// defaults of <see cref="ChatAttachmentLimits"/>.
    /// </summary>
    [Parameter]
    public ChatAttachmentLimits? AttachmentLimits { get; set; }

    /// <summary>
    /// Gets or sets the callback raised when the user submits a prompt, before anything is sent. Set
    /// <see cref="CancelEventArgs.Cancel"/> to keep the prompt in the box and send nothing; the view reads it when the
    /// handler returns or reaches its first <see langword="await"/>, so set it before that. Without a
    /// <see cref="Runner"/>, this is where the application sends the prompt.
    /// </summary>
    [Parameter]
    public EventCallback<PromptSubmittedEventArgs> PromptSubmitted { get; set; }

    /// <summary>
    /// Gets or sets the callback raised when the user asks to stop the reply in progress, with the stop button or Esc.
    /// Without a <see cref="Runner"/>, this is where the application stops its own turn.
    /// </summary>
    [Parameter]
    public EventCallback StopRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback raised when the user asks to send the last prompt again, without a <see cref="Runner"/>.
    /// The application calls <see cref="ChatConversation.RewindLastPrompt"/> and sends the prompt it returns.
    /// </summary>
    [Parameter]
    public EventCallback RegenerateRequested { get; set; }

    /// <summary>Gets or sets the callback raised when a file the user picked, dropped or pasted was not attached.</summary>
    [Parameter]
    public EventCallback<ChatAttachmentRejectedEventArgs> AttachmentRejected { get; set; }

    /// <summary>Gets a value indicating whether the prompt box holds the last prompt for the user to change.</summary>
    public bool IsEditingPrompt => _composer.IsEditingPrompt;

    /// <summary>Gets the files attached to the prompt in the box, which are sent with it.</summary>
    public ReadOnlyObservableCollection<DataContent> PendingAttachments => _composer.PendingAttachments;

    // The picker offers the types the limits allow; the content is still checked, whatever the picker let through.
    private string AcceptedTypes => string.Join(',', _composer.AttachmentLimits.AllowedMediaTypes);

    /// <summary>
    /// Attaches a file the user picked with an <see cref="InputFile"/> of the application's own: the file is checked
    /// against <see cref="AttachmentLimits"/>, read only when its size allows it, and added to
    /// <see cref="PendingAttachments"/>.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>
    /// <see langword="null"/> when the file was attached, and why not otherwise; a file that is not attached also raises
    /// <see cref="AttachmentRejected"/>.
    /// </returns>
    /// <remarks>Call it on the component's dispatcher, as any member of a component: <c>InvokeAsync</c> from elsewhere.</remarks>
    public Task<ChatAttachmentProblem?> AddAttachmentAsync(IBrowserFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        // One byte past the limit lets the composer tell a file that is too large; the browser's stream refuses to go past
        // what it is allowed, so it never reads more than that.
        var limit = _composer.AttachmentLimits.MaxFileSize;
        var allowed = limit < long.MaxValue ? Math.Max(limit, 0) + 1 : long.MaxValue;
        return _composer.AddAttachmentAsync(
            file.Name, file.Size, () => Task.FromResult<Stream>(new BrowserFileStream(file.OpenReadStream(allowed))));
    }

    /// <summary>
    /// Attaches content from a source of the application's own, with the same checks as a file the user picks. The
    /// stream's length is checked first when it can tell it.
    /// </summary>
    /// <param name="fileName">The name to give the attachment; only the name is kept, without folders.</param>
    /// <param name="content">The content, read from its current position; the caller keeps ownership of it.</param>
    /// <returns>
    /// <see langword="null"/> when the content was attached, and why not otherwise; content that is not attached also
    /// raises <see cref="AttachmentRejected"/>.
    /// </returns>
    /// <remarks>Call it on the component's dispatcher, as any member of a component: <c>InvokeAsync</c> from elsewhere.</remarks>
    public Task<ChatAttachmentProblem?> AddAttachmentAsync(string fileName, Stream content) =>
        _composer.AddAttachmentAsync(fileName, content);

    private void ApplyComposingParameters()
    {
        _composer.AllowsRewind = AllowsRewind;
        if (!ReferenceEquals(AttachmentLimits, _limitsParameter))
        {
            _limitsParameter = AttachmentLimits;
            _composer.AttachmentLimits = AttachmentLimits;
        }
        // NOTE: A parent that binds the text (it passes both the text and the callback) holds it, and may change what the
        // user typed, as by cutting it short, so the box follows it whenever they differ. Otherwise the user's typing
        // changes the text without the parameter, so the text follows the parameter only when the parameter itself
        // changes; a parent that renders again without binding it does not empty the box.
        var differs = _promptBound
            ? !string.Equals(Prompt, _composer.Prompt, StringComparison.Ordinal)
            : !string.Equals(Prompt, _promptParameter, StringComparison.Ordinal);
        if (differs)
        {
            _promptParameter = Prompt;
            _applyingPrompt = true;
            _composer.Prompt = Prompt;
            _applyingPrompt = false;
        }
    }

    private void ConnectComposer()
    {
        _composer.PromptSubmitted += OnComposerPromptSubmitted;
        _composer.StopRequested += OnComposerStopRequested;
        _composer.RegenerateRequested += OnComposerRegenerateRequested;
        _composer.AttachmentRejected += OnComposerAttachmentRejected;
        ((INotifyCollectionChanged)_composer.PendingAttachments).CollectionChanged += OnPendingAttachmentsChanged;
    }

    private void DisconnectComposer()
    {
        _composer.PromptSubmitted -= OnComposerPromptSubmitted;
        _composer.StopRequested -= OnComposerStopRequested;
        _composer.RegenerateRequested -= OnComposerRegenerateRequested;
        _composer.AttachmentRejected -= OnComposerAttachmentRejected;
        ((INotifyCollectionChanged)_composer.PendingAttachments).CollectionChanged -= OnPendingAttachmentsChanged;
    }

    // The composer tells the parent of the new text (OnComposerPropertyChanged).
    private void SetPrompt(string? text) => _composer.Prompt = text;

    // The suggestions go away with the empty state, so the keyboard focus moves to the box rather than the page.
    private async Task SubmitSuggestionAsync(string suggestion)
    {
        await _promptBox.FocusAsync();
        await _composer.SubmitAsync(suggestion);
    }

    // One button sends, or stops while a reply is in progress, so the keyboard focus stays on it as it changes.
    private Task SendOrStopAsync()
    {
        if (_composer.IsGenerating)
        {
            _composer.Stop();
            return Task.CompletedTask;
        }
        return _composer.SubmitAsync();
    }

    // NOTE: The script hands over the text the box holds as the user presses Enter, which may be ahead of the last input
    // event the view received.
    private void SubmitFromBox(string text) => Observe(InvokeAsync(async () =>
    {
        if (_disposed)
        {
            return;
        }
        _composer.Prompt = text;
        await _composer.SubmitAsync();
    }));

    // Esc stops the reply in progress, or else ends the edit, as in the Avalonia view.
    private void OnEscape() => Observe(InvokeAsync(() =>
    {
        if (_disposed)
        {
            return;
        }
        if (_composer.IsGenerating)
        {
            _composer.Stop();
        }
        else
        {
            _composer.CancelEdit();
        }
    }));

    private async Task EditLastPromptAsync(UserPromptEntry prompt)
    {
        _composer.EditLastPrompt(prompt);
        if (_composer.IsEditingPrompt)
        {
            await _promptBox.FocusAsync();
        }
    }

    // NOTE: All the files are asked for, so a long list is not refused as a whole; the composer refuses each file past
    // the count of the limits, with a message that names it. One batch is read after the other, so a client that sends
    // many batches at once holds one file in memory at a time, not one per batch.
    private Task OnFilesPickedAsync(InputFileChangeEventArgs e)
    {
        var files = e.GetMultipleFiles(e.FileCount);
        _reading = ReadAfterAsync(_reading, files);
        return _reading;
    }

    private async Task ReadAfterAsync(Task previous, IReadOnlyList<IBrowserFile> files)
    {
        // A batch that failed has reported its failure already.
        await previous.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        foreach (var file in files)
        {
            if (_disposed)
            {
                return;
            }
            await AddAttachmentAsync(file);
        }
    }

    // The text changed by typing, or by the composer itself: emptied by a submission, taken for an edit, given back.
    // A text the parent gave goes back to it only from a later change.
    private void NotifyPrompt()
    {
        if (!_applyingPrompt)
        {
            Observe(PromptChanged.InvokeAsync(_composer.Prompt));
        }
    }

    // NOTE: The composer raises its events synchronously and reads Cancel when they return. A handler of the application
    // that awaits goes on in the background; its failure is reported to the renderer like an event handler's.
    private void OnComposerPromptSubmitted(object? sender, PromptSubmittedEventArgs e)
    {
        Observe(PromptSubmitted.InvokeAsync(e));
        if (!e.Cancel)
        {
            Observe(ScrollToLatestAsync());
        }
    }

    private void OnComposerStopRequested(object? sender, EventArgs e) => Observe(StopRequested.InvokeAsync());

    private void OnComposerRegenerateRequested(object? sender, EventArgs e) => Observe(RegenerateRequested.InvokeAsync());

    private void OnComposerAttachmentRejected(object? sender, ChatAttachmentRejectedEventArgs e) =>
        Observe(AttachmentRejected.InvokeAsync(e));

    private void OnPendingAttachmentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => _throttle.Request();

    private void Observe(Task task)
    {
        if (!task.IsCompletedSuccessfully)
        {
            _ = task.ContinueWith(
                failed => InvokeAsync(() => DispatchExceptionAsync(failed.Exception!.GetBaseException())),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }
}
