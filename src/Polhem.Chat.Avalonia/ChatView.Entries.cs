using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// Following the state of the shown entries: whether a reply can be copied yet, which entries show, and what the line
/// under them says the model is doing.
/// </summary>
public partial class ChatView
{
    private const string WaitingPseudoClass = ":waiting";
    private const string WaitingPart = "PART_Waiting";

    private TextBlock? _waitingText;

    // NOTE: Whether an entry can be copied depends on its streaming state, which the entry changes without the view
    // being told. A button asks a command again only when the command says so, so the view watches the entries and tells
    // the copy command when one stops streaming; otherwise the copy button of a reply stays disabled after it completes.
    private void Watch(IEnumerable<ChatEntry> entries)
    {
        foreach (var entry in entries)
        {
            entry.PropertyChanged += OnEntryPropertyChanged;
            _watchedEntries.Add(entry);
        }
    }

    private void UnwatchAll()
    {
        foreach (var entry in _watchedEntries)
        {
            entry.PropertyChanged -= OnEntryPropertyChanged;
        }
        _watchedEntries.Clear();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The line follows a running tool's title and a reply's first text as well.
        if (e.PropertyName is nameof(AssistantReplyEntry.IsStreaming) or nameof(ToolCallEntry.Status) or nameof(ToolCallEntry.Title)
            || (e.PropertyName == nameof(AssistantReplyEntry.Text) && IsGenerating))
        {
            UpdateWaiting();
        }
        if (e.PropertyName == nameof(ToolCallEntry.Status))
        {
            UpdateEntryVisibility();
            _approveCommand.RaiseCanExecuteChanged();
            _rejectCommand.RaiseCanExecuteChanged();
        }
        // A streaming reply changes its text with every update; asking every copy button again for each of those would
        // cost more than it tells, and a reply cannot be copied before it stops streaming anyway.
        if (e.PropertyName == nameof(AssistantReplyEntry.IsStreaming)
            || (e.PropertyName == nameof(AssistantReplyEntry.Text) && CopyText(sender) is not null))
        {
            _copyCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// What the line under the entries says the model is doing while a reply is in progress, or <see langword="null"/>
    /// when an entry already shows it: the title of a running tool, or <see cref="ChatStrings.Thinking"/> while the
    /// model reasons or nothing has arrived yet. A reply that is being written shows itself.
    /// </summary>
    internal string? Activity
    {
        get
        {
            if (!IsGenerating)
            {
                return null;
            }
            if (_shown?.CurrentTurn is { IsAwaitingApproval: true })
            {
                return Strings.AwaitingApproval + "…";
            }
            var last = _shown?.Entries.LastOrDefault();
            if (ShowsActivity)
            {
                // The reasoning and tool call entries show their own progress.
                return last is AssistantReplyEntry { IsStreaming: true } or ReasoningEntry { IsStreaming: true }
                    or ToolCallEntry { Status: ToolCallStatus.Running }
                    ? null
                    : Strings.Thinking;
            }
            return last switch
            {
                AssistantReplyEntry { IsStreaming: true, Text.Length: > 0 } => null,
                ToolCallEntry { Status: ToolCallStatus.Running } call => call.Title + "…",
                _ => Strings.Thinking,
            };
        }
    }

    /// <summary>Gets a value indicating whether the line under the entries says what the model is doing.</summary>
    internal bool IsWaiting => Activity is not null;

    private void UpdateWaiting()
    {
        var activity = Activity;
        PseudoClasses.Set(WaitingPseudoClass, activity is not null);
        if (_waitingText is not null)
        {
            _waitingText.Text = activity ?? string.Empty;
        }
    }

    // A call that waits for the user's approval is shown however ShowsActivity is set: hiding it would leave the turn
    // waiting for a decision nobody can make. So is a call of a tool the application gave a template of its own.
    private bool IsActivity(object? item) =>
        item is ReasoningEntry
        || (item is ToolCallEntry { Status: not ToolCallStatus.AwaitingApproval } && this.FindDataTemplate(item) is not ToolCallTemplate);

    // NOTE: Hidden entries stay in the conversation and its history; only their containers are hidden, so they take no
    // room and no spacing in the list.
    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e) =>
        e.Container.IsVisible = ShowsActivity || !IsActivity(_entries?.ItemFromContainer(e.Container));

    private void UpdateEntryVisibility()
    {
        if (_entries is null)
        {
            return;
        }
        foreach (var container in _entries.GetRealizedContainers())
        {
            container.IsVisible = ShowsActivity || !IsActivity(_entries.ItemFromContainer(container));
        }
    }
}
