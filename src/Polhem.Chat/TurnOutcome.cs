namespace Polhem.Chat;

/// <summary>How a turn ended.</summary>
public enum TurnOutcome
{
    /// <summary>The model finished its reply.</summary>
    Completed,

    /// <summary>The turn was stopped; what arrived before is kept.</summary>
    Stopped,

    /// <summary>The turn ended with an exception.</summary>
    Failed,
}
