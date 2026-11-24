using System.ComponentModel;
using Avalonia.Controls;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// Following the state of the shown entries: whether a reply can be copied yet, and whether the view shows that a reply is
/// on its way because no entry does (between submitting a prompt and the first reasoning or text, and between a tool's
/// result and the next response).
/// </summary>
public partial class ChatView
{
    private const string WaitingPseudoClass = ":waiting";

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
        if (e.PropertyName is nameof(AssistantReplyEntry.IsStreaming) or nameof(ToolCallEntry.Status))
        {
            UpdateWaiting();
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
    /// Whether the view shows that a reply is on its way: a reply is in progress and the last entry does not show progress
    /// itself, as a streaming reply or reasoning block and a running tool call do.
    /// </summary>
    internal bool IsWaiting => IsGenerating && _shown?.Entries.LastOrDefault() switch
    {
        AssistantReplyEntry { IsStreaming: true } => false,
        ReasoningEntry { IsStreaming: true } => false,
        ToolCallEntry { Status: ToolCallStatus.Running } => false,
        _ => true,
    };

    private void UpdateWaiting() => PseudoClasses.Set(WaitingPseudoClass, IsWaiting);
}
