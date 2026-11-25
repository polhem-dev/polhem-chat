using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// Data for <see cref="ChatRunner.MessageSending"/>: the prompt about to be sent, which a handler may rewrite, or cancel
/// to send nothing (for example to run the turn itself).
/// </summary>
public sealed class MessageSendingEventArgs : CancelEventArgs
{
    private string _text;

    /// <summary>Initializes a new instance of the <see cref="MessageSendingEventArgs"/> class.</summary>
    /// <param name="text">The prompt about to be sent.</param>
    public MessageSendingEventArgs(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
    }

    /// <summary>
    /// Gets the files that will be sent with the prompt. A handler may remove them, or replace one with another, for
    /// example a text file with its text when the provider takes no text attachments.
    /// </summary>
    public IList<DataContent> Attachments { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the prompt replaces the last one, as <see cref="ChatRunner.RegenerateAsync"/> and
    /// <see cref="ChatRunner.EditLastPromptAsync"/> send it. Its text is then the prompt as it was sent before, after
    /// the handlers of that time changed it, so a handler that adds to the text should not add again.
    /// </summary>
    public bool ReplacesLastPrompt { get; init; }

    /// <summary>Gets or sets the prompt that will be sent.</summary>
    public string Text
    {
        get => _text;
        set => _text = value ?? throw new ArgumentNullException(nameof(value));
    }
}
