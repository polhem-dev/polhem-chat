using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// Sending the last prompt again: as it was (regenerate), or changed in the prompt box (edit). Both replace the last
/// prompt and everything after it.
/// </summary>
public partial class ChatView
{
    /// <summary>Defines the <see cref="AllowsRewind"/> property.</summary>
    public static readonly StyledProperty<bool?> AllowsRewindProperty =
        AvaloniaProperty.Register<ChatView, bool?>(nameof(AllowsRewind));

    private const string EditingPseudoClass = ":editing";

    private ViewCommand? _regenerateCommand;
    private ViewCommand? _editLastPromptCommand;
    private ViewCommand? _cancelEditCommand;
    private (string? Prompt, DataContent[] Attachments)? _beforeEdit;
    private UserPromptEntry? _editing;

    /// <summary>
    /// Occurs when the user asks to send the last prompt again, without a <see cref="Runner"/>. The application calls
    /// <see cref="ChatConversation.RewindLastPrompt"/> and sends the prompt it returns.
    /// </summary>
    public event EventHandler? RegenerateRequested;

    /// <summary>
    /// Gets or sets a value indicating whether the user can send the last prompt again or change it. By default,
    /// <see langword="null"/>, they can when the view has a <see cref="Runner"/>, and cannot when the application drives
    /// the conversation itself: what the tools of the replaced turns did is not undone, and only the application knows
    /// whether that is safe.
    /// </summary>
    public bool? AllowsRewind
    {
        get => GetValue(AllowsRewindProperty);
        set => SetValue(AllowsRewindProperty, value);
    }

    /// <summary>Gets a value indicating whether the prompt box holds the last prompt for the user to change.</summary>
    public bool IsEditingPrompt => _beforeEdit is not null;

    /// <summary>
    /// Gets the command that sends the last prompt again in place of its turn. Its parameter, when there is one, must be
    /// the last reply, which is where the default template shows it.
    /// </summary>
    public ICommand RegenerateCommand => _regenerateCommand ??= new ViewCommand(_ => Regenerate(), CanRegenerate);

    /// <summary>
    /// Gets the command that puts the last prompt, its parameter, in the prompt box for the user to change; submitting
    /// then replaces it and everything after it.
    /// </summary>
    public ICommand EditLastPromptCommand => _editLastPromptCommand ??= new ViewCommand(
        parameter => BeginEdit((UserPromptEntry)parameter!),
        parameter => CanRewind && parameter is UserPromptEntry entry && ReferenceEquals(entry, LastOf<UserPromptEntry>()));

    /// <summary>Gets the command that stops changing the last prompt and gives the box back what it held before.</summary>
    public ICommand CancelEditCommand => _cancelEditCommand ??= new ViewCommand(_ => CancelEdit(), _ => IsEditingPrompt);

    private bool CanRewind =>
        (AllowsRewind ?? _activeRunner is not null) && !IsGenerating && LastOf<UserPromptEntry>() is not null;

    private T? LastOf<T>() where T : ChatEntry => _shown?.Entries.OfType<T>().LastOrDefault();

    private bool CanRegenerate(object? parameter)
    {
        if (!CanRewind)
        {
            return false;
        }
        if (parameter is null)
        {
            return true;
        }
        // The last reply, and only when it answers the last prompt.
        var entries = _shown!.Entries;
        return parameter is AssistantReplyEntry reply
            && ReferenceEquals(reply, LastOf<AssistantReplyEntry>())
            && entries.IndexOf(reply) > entries.IndexOf(LastOf<UserPromptEntry>()!);
    }

    private void Regenerate()
    {
        CancelEdit();
        if (_activeRunner is { } runner)
        {
            _ = runner.RegenerateAsync(CancellationToken.None);
            return;
        }
        RegenerateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BeginEdit(UserPromptEntry entry)
    {
        _beforeEdit ??= (Prompt, [.. _pendingAttachments]);
        _editing = entry;
        SetCurrentValue(PromptProperty, entry.Text);
        _pendingAttachments.Clear();
        foreach (var attachment in entry.Attachments)
        {
            _pendingAttachments.Add(attachment);
        }
        UpdateEditing();
        FocusPrompt();
    }

    private void CancelEdit()
    {
        if (_beforeEdit is not { } before)
        {
            return;
        }
        _beforeEdit = null;
        _editing = null;
        SetCurrentValue(PromptProperty, before.Prompt);
        _pendingAttachments.Clear();
        foreach (var attachment in before.Attachments)
        {
            _pendingAttachments.Add(attachment);
        }
        UpdateEditing();
    }

    /// <summary>Ends editing as the changed prompt is sent; the box was emptied by the submission.</summary>
    private void EndEdit()
    {
        _beforeEdit = null;
        _editing = null;
        UpdateEditing();
    }

    /// <summary>
    /// Edits the last prompt again after the runner's sending handler canceled the changed one, which went back into the
    /// box; the box held nothing else before.
    /// </summary>
    private void ResumeEdit()
    {
        if (LastOf<UserPromptEntry>() is { } last)
        {
            _beforeEdit = (string.Empty, []);
            _editing = last;
            UpdateEditing();
        }
    }

    private void UpdateEditing()
    {
        PseudoClasses.Set(EditingPseudoClass, IsEditingPrompt);
        _cancelEditCommand?.RaiseCanExecuteChanged();
    }

    private void UpdateRewind()
    {
        // The edit belongs to the prompt it took: once that is no longer the last prompt of the conversation shown (a
        // suggestion was sent, the conversation was replaced or cleared), it ends.
        if (IsEditingPrompt && !IsGenerating && (!CanRewind || !ReferenceEquals(_editing, LastOf<UserPromptEntry>())))
        {
            CancelEdit();
        }
        _regenerateCommand?.RaiseCanExecuteChanged();
        _editLastPromptCommand?.RaiseCanExecuteChanged();
    }

    private void FocusPrompt() =>
        this.GetVisualDescendants().OfType<ChatPromptBox>().FirstOrDefault(box => box.TemplatedParent == this)?.Focus();
}
