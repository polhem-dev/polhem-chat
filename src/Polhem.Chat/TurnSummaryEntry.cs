using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>The time a turn took, the tokens it used, and how it ended.</summary>
public sealed class TurnSummaryEntry : ChatEntry
{
    /// <summary>Initializes a new instance of the <see cref="TurnSummaryEntry"/> class.</summary>
    /// <param name="elapsed">The time from the start of the turn to its end.</param>
    /// <param name="usage">The token counts the model reported, if any.</param>
    /// <param name="outcome">How the turn ended.</param>
    public TurnSummaryEntry(TimeSpan elapsed, UsageDetails? usage, TurnOutcome outcome)
        : this(elapsed, usage, outcome, calls: 0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TurnSummaryEntry"/> class with the requests it sent.</summary>
    /// <param name="elapsed">The time the turn took.</param>
    /// <param name="usage">The token counts the model reported, or <see langword="null"/> when it reported none.</param>
    /// <param name="outcome">How the turn ended.</param>
    /// <param name="calls">The requests the turn sent to the model; 0 when they were not counted.</param>
    public TurnSummaryEntry(TimeSpan elapsed, UsageDetails? usage, TurnOutcome outcome, int calls)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(calls);
        Elapsed = elapsed;
        Usage = usage;
        Outcome = outcome;
        Calls = calls;
    }

    /// <summary>Gets the time from the start of the turn to its end.</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Gets the token counts the model reported, summed over every request of the turn (a tool loop makes several), or
    /// <see langword="null"/> when it reported none.
    /// </summary>
    public UsageDetails? Usage { get; }

    /// <summary>Gets how the turn ended.</summary>
    public TurnOutcome Outcome { get; }

    /// <summary>Gets the requests the turn sent to the model; 0 when they were not counted.</summary>
    public int Calls { get; }
}
