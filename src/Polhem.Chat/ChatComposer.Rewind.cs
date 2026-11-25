using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// Sending the last prompt again: as it was (regenerate), or changed in the prompt box (edit). Both replace the last
/// prompt and everything after it.
/// </summary>
public sealed partial class ChatComposer
{
    private (string? Prompt, DataContent[] Attachments)? _beforeEdit;
    private UserPromptEntry? _editing;

    /// <summary>
    /// Occurs when the user asks to send the last prompt again, without a <see cref="Runner"/>. The application calls
    /// <see cref="ChatConversation.RewindLastPrompt"/> and sends the prompt it returns.
    /// </summary>
    public event EventHandler? RegenerateRequested;

    /// <summary>
    /// Gets or sets a value indicating whether the user can send the last prompt again or change it. By default,
    /// <see langword="null"/>, they can when there is a <see cref="Runner"/>, and cannot when the application drives the
    /// conversation itself: what the tools of the replaced turns did is not undone, and only the application knows
    /// whether that is safe.
    /// </summary>
    public bool? AllowsRewind
    {
        get => _allowsRewind;
        set
        {
            if (value == _allowsRewind)
            {
                return;
            }
            _allowsRewind = value;
            OnPropertyChanged();
            UpdateRewind();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the user can send the last prompt again or change it now: rewinding is allowed, no
    /// reply is in progress and the conversation has a prompt.
    /// </summary>
    public bool CanRewind => (AllowsRewind ?? _runner is not null) && !IsGenerating && LastPrompt is not null;

    /// <summary>Gets a value indicating whether the prompt box holds the last prompt for the user to change.</summary>
    public bool IsEditingPrompt => _beforeEdit is not null;

    // NOTE: The prompt the conversation would replace, not the last prompt entry shown: an application may add entries
    // of that type itself, or remove one, and editing must never replace another prompt than the one the user took.
    private UserPromptEntry? LastPrompt => _shown?.LastPrompt;

    /// <summary>Returns whether the last prompt can be sent again in place of its turn.</summary>
    /// <param name="reply">
    /// The reply the user asks to regenerate, which must be the last one and answer the last prompt; or
    /// <see langword="null"/> for the last prompt whatever its reply.
    /// </param>
    /// <returns><see langword="true"/> when <see cref="Regenerate"/> would send it.</returns>
    public bool CanRegenerate(AssistantReplyEntry? reply = null)
    {
        if (!CanRewind)
        {
            return false;
        }
        if (reply is null)
        {
            return true;
        }
        // The last reply, and only when it answers the last prompt.
        var entries = _shown!.Entries;
        return ReferenceEquals(reply, entries.OfType<AssistantReplyEntry>().LastOrDefault())
            && entries.IndexOf(reply) > entries.IndexOf(LastPrompt!);
    }

    /// <summary>
    /// Sends the last prompt again in place of its turn, through the <see cref="Runner"/>; without one, raises
    /// <see cref="RegenerateRequested"/>. An edit in progress is canceled first. Nothing happens when
    /// <see cref="CanRegenerate"/> is <see langword="false"/>.
    /// </summary>
    public void Regenerate()
    {
        if (!CanRegenerate())
        {
            return;
        }
        CancelEdit();
        if (_runner is { } runner)
        {
            _ = runner.RegenerateAsync(CancellationToken.None);
            return;
        }
        RegenerateRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Returns whether a prompt can be taken into the box to be changed.</summary>
    /// <param name="entry">The prompt, which must be the conversation's last.</param>
    /// <returns><see langword="true"/> when <see cref="EditLastPrompt"/> would take it.</returns>
    public bool CanEditLastPrompt(UserPromptEntry? entry) =>
        CanRewind && entry is not null && ReferenceEquals(entry, LastPrompt);

    /// <summary>
    /// Puts the last prompt and its files in the box for the user to change; submitting then replaces it and everything
    /// after it. What the box held before comes back with <see cref="CancelEdit"/>. Nothing happens when
    /// <see cref="CanEditLastPrompt"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="entry">The last prompt.</param>
    public void EditLastPrompt(UserPromptEntry entry)
    {
        if (!CanEditLastPrompt(entry))
        {
            return;
        }
        _beforeEdit ??= (Prompt, [.. _pendingAttachments]);
        _editing = entry;
        Prompt = entry.Text;
        _pendingAttachments.Clear();
        foreach (var attachment in entry.Attachments)
        {
            _pendingAttachments.Add(attachment);
        }
        OnPropertyChanged(nameof(IsEditingPrompt));
    }

    /// <summary>Stops changing the last prompt and gives the box back what it held before.</summary>
    public void CancelEdit()
    {
        if (_beforeEdit is not { } before)
        {
            return;
        }
        _beforeEdit = null;
        _editing = null;
        Prompt = before.Prompt;
        _pendingAttachments.Clear();
        foreach (var attachment in before.Attachments)
        {
            _pendingAttachments.Add(attachment);
        }
        OnPropertyChanged(nameof(IsEditingPrompt));
    }

    /// <summary>
    /// Ends editing as the changed prompt is sent; the box was emptied by the submission. Returns what the box held
    /// before the edit, for <see cref="ResumeEdit"/>.
    /// </summary>
    private (UserPromptEntry? Entry, (string? Prompt, DataContent[] Attachments)? Before) EndEdit()
    {
        var ended = (_editing, _beforeEdit);
        _beforeEdit = null;
        _editing = null;
        OnPropertyChanged(nameof(IsEditingPrompt));
        return ended;
    }

    /// <summary>
    /// Edits the prompt again after the runner's sending handler canceled the changed one, which went back into the box,
    /// keeping what the box held before the edit for a later cancel.
    /// </summary>
    private void ResumeEdit((UserPromptEntry? Entry, (string? Prompt, DataContent[] Attachments)? Before) ended)
    {
        if (ended.Entry is { } entry && ReferenceEquals(entry, LastPrompt))
        {
            _beforeEdit = ended.Before;
            _editing = entry;
            OnPropertyChanged(nameof(IsEditingPrompt));
        }
    }

    private void UpdateRewind()
    {
        // The edit belongs to the prompt it took: once that is no longer the last prompt of the conversation (a suggestion
        // was sent, the conversation was replaced or cleared), it ends.
        if (IsEditingPrompt && !IsGenerating && (!CanRewind || !ReferenceEquals(_editing, LastPrompt)))
        {
            CancelEdit();
        }
        OnPropertyChanged(nameof(CanRewind));
    }
}
