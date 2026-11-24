using Polhem.Chat;

namespace ChatGallery;

/// <summary>
/// An entry type of the gallery's own: a question with answers to pick from, which the host adds at the end of a turn. The
/// view shows it with the template <see cref="HostDrivenDemo"/> registers.
/// </summary>
public sealed class ChoiceEntry(string question, IReadOnlyList<string> choices) : ChatEntry
{
    private bool _isAnswered;

    public string Question { get; } = question;

    public IReadOnlyList<string> Choices { get; } = choices;

    /// <summary>Gets or sets a value indicating whether an answer was picked, after which the buttons are disabled.</summary>
    public bool IsAnswered
    {
        get => _isAnswered;
        set => SetProperty(ref _isAnswered, value);
    }
}
