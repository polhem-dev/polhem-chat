namespace Polhem.Chat;

/// <summary>Text the model wrote, as Markdown. While it streams, <see cref="Text"/> grows with every update.</summary>
public sealed class AssistantReplyEntry : ChatEntry
{
    private string _text;
    private bool _isStreaming;
    private bool _isStopped;

    /// <summary>Initializes a new instance of the <see cref="AssistantReplyEntry"/> class.</summary>
    /// <param name="text">The text received so far.</param>
    /// <param name="isStreaming">Whether more text is still expected.</param>
    public AssistantReplyEntry(string text = "", bool isStreaming = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        _isStreaming = isStreaming;
    }

    /// <summary>Gets or sets the Markdown text received so far.</summary>
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value ?? string.Empty);
    }

    /// <summary>Gets or sets a value indicating whether more text is still expected.</summary>
    public bool IsStreaming
    {
        get => _isStreaming;
        set => SetProperty(ref _isStreaming, value);
    }

    /// <summary>Gets or sets a value indicating whether the reply ended because the turn was stopped.</summary>
    public bool IsStopped
    {
        get => _isStopped;
        set => SetProperty(ref _isStopped, value);
    }

    /// <summary>Appends streamed text to <see cref="Text"/>.</summary>
    /// <param name="text">The text to append.</param>
    public void AppendText(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Text = _text + text;
        }
    }
}
