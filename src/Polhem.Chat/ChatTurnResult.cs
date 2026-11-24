using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>What a turn produced.</summary>
public sealed class ChatTurnResult
{
    internal ChatTurnResult(ChatResponse response, TurnSummaryEntry summary, Exception? exception)
    {
        Response = response;
        Summary = summary;
        Exception = exception;
    }

    /// <summary>
    /// Gets the response combined from the updates that arrived. For a stopped or failed turn it holds what arrived
    /// before the end.
    /// </summary>
    public ChatResponse Response { get; }

    /// <summary>Gets the summary entry added at the end of the turn: time, tokens and outcome.</summary>
    public TurnSummaryEntry Summary { get; }

    /// <summary>Gets the exception that ended a failed turn, or <see langword="null"/>.</summary>
    public Exception? Exception { get; }
}
