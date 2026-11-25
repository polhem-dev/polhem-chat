using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.Avalonia;

/// <summary>What the user does with the view: submitting a prompt, stopping a reply, copying, and deciding on tool calls.</summary>
public partial class ChatView
{
    /// <summary>
    /// Submits a prompt: raises <see cref="PromptSubmitted"/>, then sends it through the <see cref="Runner"/> if there is
    /// one. Nothing happens while a reply is in progress or when the prompt is blank.
    /// </summary>
    /// <param name="text">The prompt, or <see langword="null"/> for the text in the prompt box.</param>
    internal async Task SubmitAsync(string? text)
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
            SetCurrentValue(PromptProperty, string.Empty);
            _pendingAttachments.Clear();
            ShowAttachmentError(null);
        }
        ScrollToLatest();

        var ended = editing ? EndEdit() : default;
        if (_activeRunner is not { } runner)
        {
            return;
        }

        var result = editing
            ? await runner.EditLastPromptAsync(text, attachments, CancellationToken.None)
            : await runner.SendAsync(text, attachments, CancellationToken.None);
        if (result is null && fromBox)
        {
            GiveBack(text, attachments, editing ? ended : null);
        }
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
        SetCurrentValue(PromptProperty, text);
        foreach (var attachment in attachments)
        {
            _pendingAttachments.Add(attachment);
        }
        if (edit is { } ended)
        {
            ResumeEdit(ended);
        }
    }

    /// <summary>Copies the text of an entry, or a string, to the clipboard.</summary>
    /// <param name="parameter">An entry or a string.</param>
    internal async Task CopyAsync(object? parameter)
    {
        if (CopyText(parameter) is { } text && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private static string? CopyText(object? parameter) => parameter switch
    {
        AssistantReplyEntry { IsStreaming: false } reply when reply.Text.Length > 0 => reply.Text,
        UserPromptEntry prompt => prompt.Text,
        ReasoningEntry { IsStreaming: false } reasoning => reasoning.Text,
        string text when text.Length > 0 => text,
        _ => null,
    };

    private bool IsPendingApproval(object? parameter) =>
        parameter is ToolCallEntry entry && _shown?.CurrentTurn?.PendingApprovals.Contains(entry) == true;

    private void Decide(object? parameter, bool approved)
    {
        if (parameter is not ToolCallEntry entry || _shown?.CurrentTurn is not { } turn)
        {
            return;
        }
        if (approved)
        {
            turn.Approve(entry);
        }
        else
        {
            turn.Reject(entry);
        }
    }

    private void Stop()
    {
        _activeRunner?.Stop();
        StopRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsGenerating)
        {
            e.Handled = true;
            Stop();
        }
        else if (e.Key == Key.Escape && IsEditingPrompt)
        {
            e.Handled = true;
            CancelEdit();
        }
    }

    private void OnSubmitRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = SubmitAsync(null);
    }
}
