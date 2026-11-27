using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>A prompt the user sent, with the files attached to it and the context sent with it.</summary>
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
        : this(text, attachments, [])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UserPromptEntry"/> class with attachments and context.</summary>
    /// <param name="text">The text of the prompt; it may be empty when there are attachments.</param>
    /// <param name="attachments">The files sent with the prompt.</param>
    /// <param name="context">The text sent with the prompt that is not shown as its text.</param>
    public UserPromptEntry(string text, IReadOnlyList<DataContent> attachments, IReadOnlyList<PromptContext> context)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(context);
        Text = text;
        Attachments = attachments;
        Context = context;
    }

    /// <summary>Gets the text of the prompt.</summary>
    public string Text { get; }

    /// <summary>Gets the files sent with the prompt, in the order they were attached; empty when there are none.</summary>
    public IReadOnlyList<DataContent> Attachments { get; }

    /// <summary>
    /// Gets the text sent with the prompt that a view shows by its label only, in the order it was added; empty when
    /// there is none.
    /// </summary>
    public IReadOnlyList<PromptContext> Context { get; }
}
