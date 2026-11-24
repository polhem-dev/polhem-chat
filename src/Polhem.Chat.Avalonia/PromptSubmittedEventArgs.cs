using System.ComponentModel;

namespace Polhem.Chat.Avalonia;

/// <summary>Provides data for the <see cref="ChatView.PromptSubmitted"/> event.</summary>
/// <param name="text">The prompt the user submitted.</param>
public sealed class PromptSubmittedEventArgs(string text) : CancelEventArgs
{
    /// <summary>Gets the prompt the user submitted.</summary>
    public string Text { get; } = text;
}
