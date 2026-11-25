using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>A prompt the user sent, with the files attached to it.</summary>
public sealed class UserPromptEntry : ChatEntry
{
    /// <summary>Initializes a new instance of the <see cref="UserPromptEntry"/> class.</summary>
    /// <param name="text">The text of the prompt.</param>
    public UserPromptEntry(string text)
        : this(text, [])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UserPromptEntry"/> class with attachments.</summary>
    /// <param name="text">The text of the prompt; it may be empty when there are attachments.</param>
    /// <param name="attachments">The files sent with the prompt.</param>
    public UserPromptEntry(string text, IReadOnlyList<DataContent> attachments)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(attachments);
        Text = text;
        Attachments = attachments;
    }

    /// <summary>Gets the text of the prompt.</summary>
    public string Text { get; }

    /// <summary>Gets the files sent with the prompt, in the order they were attached; empty when there are none.</summary>
    public IReadOnlyList<DataContent> Attachments { get; }
}
