namespace Polhem.Chat;

/// <summary>A prompt the user sent.</summary>
public sealed class UserPromptEntry : ChatEntry
{
    /// <summary>Initializes a new instance of the <see cref="UserPromptEntry"/> class.</summary>
    /// <param name="text">The text of the prompt.</param>
    public UserPromptEntry(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Gets the text of the prompt.</summary>
    public string Text { get; }
}
