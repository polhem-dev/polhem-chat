namespace Polhem.Chat;

/// <summary>Data for <see cref="ChatRunner.ResponseReceived"/>.</summary>
public sealed class ResponseReceivedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ResponseReceivedEventArgs"/> class.</summary>
    /// <param name="result">What the turn produced.</param>
    public ResponseReceivedEventArgs(ChatTurnResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    /// <summary>Gets what the turn produced: the response, its summary and, for a failed turn, the exception.</summary>
    public ChatTurnResult Result { get; }
}
