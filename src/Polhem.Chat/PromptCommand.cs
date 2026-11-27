namespace Polhem.Chat;

/// <summary>
/// A prompt the host offers in the prompt box: typing <c>/</c> at the start of the box lists the commands whose
/// <see cref="Name"/> matches what follows, and choosing one puts its <see cref="Text"/> in the box, or sends it.
/// </summary>
/// <remarks>
/// A view lists the commands of its <c>Commands</c> property, which the host may replace or, when it is an observable
/// collection, change at any time, for example from prompts the user saved.
/// </remarks>
public sealed class PromptCommand
{
    /// <summary>Initializes a new instance of the <see cref="PromptCommand"/> class.</summary>
    /// <param name="name">The name the user types after the slash, such as "shorter"; it holds no space and no slash.</param>
    /// <param name="text">The prompt the command puts in the box or sends.</param>
    /// <exception cref="ArgumentException">
    /// The name is blank, holds white space or starts with a slash, or the text is blank.
    /// </exception>
    public PromptCommand(string name, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (name.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("A command name cannot hold white space.", nameof(name));
        }
        if (name[0] == '/')
        {
            throw new ArgumentException("A command name is given without its slash.", nameof(name));
        }
        Name = name;
        Text = text;
    }

    /// <summary>Gets the name the user types after the slash.</summary>
    public string Name { get; }

    /// <summary>Gets the prompt the command puts in the box or sends.</summary>
    public string Text { get; }

    /// <summary>Gets the line a view shows under the name in the list, such as what the command asks for.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets a value indicating whether choosing the command sends its <see cref="Text"/> at once, as if the user had typed
    /// it and pressed Enter. By default, <see langword="false"/>, the text goes into the box for the user to finish.
    /// </summary>
    public bool SendsImmediately { get; init; }
}
