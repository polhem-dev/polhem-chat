namespace Polhem.Chat;

/// <summary>Reasoning the model showed before or between its replies. Front ends usually show it collapsed.</summary>
public sealed class ReasoningEntry : ChatEntry
{
    private string _text;
    private bool _isStreaming;

    /// <summary>Initializes a new instance of the <see cref="ReasoningEntry"/> class.</summary>
    /// <param name="text">The reasoning received so far.</param>
    /// <param name="isStreaming">Whether more reasoning is still expected.</param>
    public ReasoningEntry(string text = "", bool isStreaming = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        _isStreaming = isStreaming;
    }

    /// <summary>Gets or sets the reasoning received so far.</summary>
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value ?? string.Empty);
    }

    /// <summary>Gets or sets a value indicating whether more reasoning is still expected.</summary>
    public bool IsStreaming
    {
        get => _isStreaming;
        set => SetProperty(ref _isStreaming, value);
    }

    /// <summary>Appends streamed reasoning to <see cref="Text"/>.</summary>
    /// <param name="text">The text to append.</param>
    public void AppendText(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Text = _text + text;
        }
    }
}
