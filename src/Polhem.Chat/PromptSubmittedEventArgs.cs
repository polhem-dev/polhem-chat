using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// Provides data for the <see cref="ChatComposer.PromptSubmitted"/> event, which a view raises again as its own.
/// </summary>
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

    /// <summary>
    /// Gets a value indicating whether the prompt replaces the last one, which the user took into the box to change
    /// (<see cref="ChatComposer.EditLastPrompt"/>, or a view's edit button). Without a <see cref="ChatComposer.Runner"/>, the application calls
    /// <see cref="ChatConversation.RewindLastPrompt"/> before it sends the prompt.
    /// </summary>
    public bool IsEdit { get; init; }

    /// <summary>Gets the prompt the user submitted.</summary>
    public string Text { get; }

    /// <summary>
    /// Gets the files the user attached, already checked against <see cref="ChatComposer.AttachmentLimits"/>. Without a
    /// <see cref="ChatComposer.Runner"/>, the application passes them to <see cref="ChatConversation.AddUserPrompt(string, IReadOnlyList{DataContent})"/>.
    /// </summary>
    public IReadOnlyList<DataContent> Attachments { get; }
}
