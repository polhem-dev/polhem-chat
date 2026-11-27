namespace Polhem.Chat;

/// <summary>
/// Text sent to the model with a prompt that the user did not type, such as the selected text of a document or the file
/// the user is working on. A view shows its <see cref="Label"/> on the prompt, like a file, and never its text.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="ChatRunner.MessageSending"/> handler adds it to <see cref="MessageSendingEventArgs.Context"/>. It is kept
/// with its prompt in the history, so <see cref="ChatRunner.RegenerateAsync"/> and
/// <see cref="ChatRunner.EditLastPromptAsync"/> send it again and <see cref="ChatConversation.LoadHistory"/> shows it
/// again.
/// </para>
/// <para>
/// The model receives it as text before the prompt, in the same message, so it works with providers that take no text
/// attachments.
/// </para>
/// </remarks>
public sealed class PromptContext
{
    /// <summary>Initializes a new instance of the <see cref="PromptContext"/> class.</summary>
    /// <param name="label">The short name a view shows on the prompt, such as "Selection" or a file name.</param>
    /// <param name="text">The text the model receives.</param>
    /// <exception cref="ArgumentException">The label is blank.</exception>
    public PromptContext(string label, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(text);
        Label = label;
        Text = text;
    }

    /// <summary>Gets the short name a view shows on the prompt.</summary>
    public string Label { get; }

    /// <summary>Gets the text the model receives.</summary>
    public string Text { get; }
}
