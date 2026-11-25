using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.Avalonia;

/// <summary>Provides data for the <see cref="ChatView.PromptSubmitted"/> event.</summary>
public sealed class PromptSubmittedEventArgs : CancelEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="PromptSubmittedEventArgs"/> class.</summary>
    /// <param name="text">The prompt the user submitted.</param>
    public PromptSubmittedEventArgs(string text)
        : this(text, [])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PromptSubmittedEventArgs"/> class with attachments.</summary>
    /// <param name="text">The prompt the user submitted; it may be empty when there are attachments.</param>
    /// <param name="attachments">The files attached to it.</param>
    public PromptSubmittedEventArgs(string text, IReadOnlyList<DataContent> attachments)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(attachments);
        Text = text;
        Attachments = attachments;
    }

    /// <summary>Gets the prompt the user submitted.</summary>
    public string Text { get; }

    /// <summary>
    /// Gets the files the user attached, already checked against <see cref="ChatView.AttachmentLimits"/>. Without a
    /// <see cref="ChatView.Runner"/>, the application passes them to <see cref="ChatConversation.AddUserPrompt(string, IReadOnlyList{DataContent})"/>.
    /// </summary>
    public IReadOnlyList<DataContent> Attachments { get; }
}
