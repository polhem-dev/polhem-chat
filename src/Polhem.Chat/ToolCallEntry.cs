namespace Polhem.Chat;

/// <summary>A tool (function) the model called, with its arguments and, once it arrives, its result.</summary>
public sealed class ToolCallEntry : ChatEntry
{
    private string _title;
    private IDictionary<string, object?>? _arguments;
    private ToolCallStatus _status;
    private object? _result;
    private string? _errorMessage;

    /// <summary>Initializes a new instance of the <see cref="ToolCallEntry"/> class.</summary>
    /// <param name="callId">The identifier that pairs the call with its result.</param>
    /// <param name="name">The name of the tool.</param>
    public ToolCallEntry(string callId, string name)
    {
        ArgumentNullException.ThrowIfNull(callId);
        ArgumentNullException.ThrowIfNull(name);
        CallId = callId;
        Name = name;
        _title = name;
    }

    /// <summary>Gets the identifier that pairs the call with its result.</summary>
    public string CallId { get; }

    /// <summary>Gets the name of the tool.</summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the text a front end shows for the call. It is <see cref="ChatConversation.DescribeToolCall"/>'s
    /// answer when there is one, and <see cref="Name"/> otherwise.
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value ?? Name);
    }

    /// <summary>Gets or sets the arguments the model passed.</summary>
    public IDictionary<string, object?>? Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    /// <summary>Gets or sets the state of the call.</summary>
    public ToolCallStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>Gets or sets the result the tool returned.</summary>
    public object? Result
    {
        get => _result;
        set => SetProperty(ref _result, value);
    }

    /// <summary>Gets or sets the message of the exception the tool threw, when it failed.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }
}
