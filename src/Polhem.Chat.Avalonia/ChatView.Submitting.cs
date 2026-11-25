using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace Polhem.Chat.Avalonia;

/// <summary>What the user does with the view: submitting a prompt, stopping a reply, copying, and deciding on tool calls.</summary>
public partial class ChatView
{
    /// <summary>
    /// Submits a prompt through the composer: raises <see cref="PromptSubmitted"/>, then sends it through the
    /// <see cref="Runner"/> if there is one.
    /// </summary>
    /// <param name="text">The prompt, or <see langword="null"/> for the text in the prompt box.</param>
    internal Task SubmitAsync(string? text) => _composer.SubmitAsync(text);

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

    private bool IsPendingApproval(object? parameter) => parameter is ToolCallEntry entry && _composer.CanDecide(entry);

    private void Decide(object? parameter, bool approved)
    {
        if (parameter is not ToolCallEntry entry)
        {
            return;
        }
        if (approved)
        {
            _composer.Approve(entry);
        }
        else
        {
            _composer.Reject(entry);
        }
    }

    private void Stop() => _composer.Stop();

    private void OnComposerPromptSubmitted(object? sender, PromptSubmittedEventArgs e)
    {
        PromptSubmitted?.Invoke(this, e);
        if (!e.Cancel)
        {
            ScrollToLatest();
        }
    }

    private void OnComposerStopRequested(object? sender, EventArgs e) => StopRequested?.Invoke(this, e);

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
            _composer.CancelEdit();
        }
    }

    private void OnSubmitRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = SubmitAsync(null);
    }
}
