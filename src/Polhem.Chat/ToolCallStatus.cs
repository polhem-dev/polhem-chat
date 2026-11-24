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

    /// <summary>
    /// The turn was stopped before the result arrived, or ended before the user decided on a call that needed their
    /// approval.
    /// </summary>
    Canceled,

    /// <summary>
    /// The call needs the user's approval and they have not decided yet. It runs only once they approve it; see
    /// <see cref="ChatTurn.Approve"/>.
    /// </summary>
    AwaitingApproval,

    /// <summary>
    /// The call did not run because someone said no: the user rejected it, or the application refused it.
    /// <see cref="ToolCallEntry.RejectionReason"/> tells why, when a reason was given.
    /// </summary>
    Rejected,
}
