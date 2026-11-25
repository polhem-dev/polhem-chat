using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>Submitting a prompt, stopping a reply, deciding on tool calls, and what the model is doing meanwhile.</summary>
public sealed partial class ChatComposer
{
    /// <summary>
    /// Submits a prompt: raises <see cref="PromptSubmitted"/>, then sends it through the <see cref="Runner"/> if there is
    /// one. Nothing happens while a reply is in progress or when the prompt is blank.
    /// </summary>
    /// <param name="text">
    /// The prompt, which goes without the files waiting in the box; or <see langword="null"/> for the text in the prompt
    /// box, which takes them along.
    /// </param>
    /// <returns>
    /// A task that completes once the turn has ended when there is a <see cref="Runner"/>, and once
    /// <see cref="PromptSubmitted"/> has been raised otherwise.
    /// </returns>
    /// <remarks>
    /// When the runner's sending handler cancels a prompt from the box, the prompt goes back into the box, unless the
    /// user has typed something new meanwhile.
    /// </remarks>
    public async Task SubmitAsync(string? text = null)
    {
        var fromBox = text is null;
        text ??= Prompt ?? string.Empty;
        // The files wait in the box, so only a prompt from the box carries them; a suggestion goes alone.
        IReadOnlyList<DataContent> attachments = fromBox ? [.. _pendingAttachments] : [];
        if (IsGenerating || (string.IsNullOrWhiteSpace(text) && attachments.Count == 0))
        {
            return;
        }

        var editing = fromBox && IsEditingPrompt;
        var submitted = new PromptSubmittedEventArgs(text, attachments) { IsEdit = editing };
        PromptSubmitted?.Invoke(this, submitted);
        if (submitted.Cancel)
        {
            return;
        }
        if (fromBox)
        {
            EmptyBox();
        }

        var ended = editing ? EndEdit() : default;
        if (_runner is { } runner)
        {
            await SendThroughAsync(runner, text, attachments, fromBox, editing ? ended : null);
        }
    }

    /// <summary>
    /// Stops the reply in progress through the <see cref="Runner"/>, if there is one, and raises
    /// <see cref="StopRequested"/>.
    /// </summary>
    public void Stop()
    {
        _runner?.Stop();
        StopRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Returns whether a tool call waits for the user's approval in the turn in progress.</summary>
    /// <param name="entry">The tool call.</param>
    /// <returns><see langword="true"/> when the user can approve or reject it now.</returns>
    public bool CanDecide(ToolCallEntry? entry) =>
        entry is not null && _shown?.CurrentTurn?.PendingApprovals.Contains(entry) == true;

    /// <summary>Approves a tool call that waits for the user's approval, so the client runs it.</summary>
    /// <param name="entry">The tool call.</param>
    public void Approve(ToolCallEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _shown?.CurrentTurn?.Approve(entry);
    }

    /// <summary>Rejects a tool call that waits for the user's approval, so the client does not run it.</summary>
    /// <param name="entry">The tool call.</param>
    public void Reject(ToolCallEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _shown?.CurrentTurn?.Reject(entry);
    }

    /// <summary>
    /// Describes what the model is doing while a reply is in progress, for a line under the entries; or returns
    /// <see langword="null"/> when an entry already shows it. It is the title of a running tool, or
    /// <see cref="ChatStrings.Thinking"/> while the model reasons or nothing has arrived yet. A reply that is being
    /// written shows itself.
    /// </summary>
    /// <param name="showsActivity">
    /// Whether the view shows reasoning and tool calls as entries, which then show their own progress.
    /// </param>
    /// <returns>The text of the line, or <see langword="null"/> for none.</returns>
    public string? DescribeActivity(bool showsActivity)
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
        if (showsActivity)
        {
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

    private async Task SendThroughAsync(
        ChatRunner runner,
        string text,
        IReadOnlyList<DataContent> attachments,
        bool fromBox,
        (UserPromptEntry? Entry, (string? Prompt, DataContent[] Attachments)? Before)? edit)
    {
        var result = edit is not null
            ? await runner.EditLastPromptAsync(text, attachments, CancellationToken.None)
            : await runner.SendAsync(text, attachments, CancellationToken.None);
        if (result is null && fromBox)
        {
            GiveBack(text, attachments, edit);
        }
    }

    private void EmptyBox()
    {
        Prompt = string.Empty;
        _pendingAttachments.Clear();
        AttachmentError = null;
    }

    /// <summary>
    /// Gives a prompt the runner's sending handler canceled back to the box, instead of losing it, unless the user has
    /// typed something new meanwhile; an edit goes on.
    /// </summary>
    private void GiveBack(
        string text,
        IReadOnlyList<DataContent> attachments,
        (UserPromptEntry? Entry, (string? Prompt, DataContent[] Attachments)? Before)? edit)
    {
        if (!string.IsNullOrEmpty(Prompt) || _pendingAttachments.Count > 0)
        {
            return;
        }
        Prompt = text;
        foreach (var attachment in attachments)
        {
            _pendingAttachments.Add(attachment);
        }
        if (edit is { } ended)
        {
            ResumeEdit(ended);
        }
    }
}
