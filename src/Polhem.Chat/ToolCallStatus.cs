namespace Polhem.Chat;

/// <summary>The state of a <see cref="ToolCallEntry"/>.</summary>
public enum ToolCallStatus
{
    /// <summary>The model asked for the call and no result has arrived yet.</summary>
    Running,

    /// <summary>The call returned a result.</summary>
    Completed,

    /// <summary>The call threw, or the turn failed before its result arrived.</summary>
    Failed,

    /// <summary>The turn was stopped before the result arrived.</summary>
    Canceled,
}
