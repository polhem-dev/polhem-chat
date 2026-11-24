using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// One reply of the model in progress: each streamed <see cref="ChatResponseUpdate"/> passed to <see cref="Apply"/>
/// becomes entries of the conversation as it arrives, and ending the turn adds the reply to the history and a
/// <see cref="TurnSummaryEntry"/> to the entries.
/// </summary>
/// <remarks>
/// Start a turn with <see cref="ChatConversation.BeginTurn"/>. An application that runs its own loop calls
/// <see cref="BeginCall"/> before each request it sends to the model, so the turn can count them; a
/// <see cref="ChatRunner"/> counts them itself.
/// </remarks>
public sealed class ChatTurn : INotifyPropertyChanged
{
    private readonly ChatConversation _conversation;
    private readonly List<ChatResponseUpdate> _updates = [];
    private readonly List<ChatResponseUpdate> _allUpdates = [];
    private readonly List<ChatMessage> _messages = [];
    private readonly List<ToolCallEntry> _pending = [];
    private readonly List<AIContent> _decisions = [];
    private readonly long _startTimestamp;
    private UsageDetails? _usage;
    private ChatTurnResult? _result;
    private int _calls;
    private TimeSpan _waited;
    private long? _waitStart;
    private readonly List<ChatMessage> _toSend = [];
    private readonly List<AIContent> _dropped = [];
    private TaskCompletionSource? _decided;
    private ChatMessage? _taken;

    internal ChatTurn(ChatConversation conversation)
    {
        _conversation = conversation;
        _startTimestamp = conversation.TimeProvider.GetTimestamp();
        conversation.Projector.Break();
    }

    /// <summary>Occurs when <see cref="Calls"/> or <see cref="IsAwaitingApproval"/> changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the requests sent to the model in this turn so far, as <see cref="BeginCall"/> counted them.</summary>
    public int Calls => _calls;

    /// <summary>
    /// Gets the time since the turn began, or the time it took once it has ended. The time the turn spent waiting for the
    /// user to approve tool calls is left out.
    /// </summary>
    public TimeSpan Elapsed => _result?.Summary.Elapsed ?? MeasureElapsed();

    /// <summary>
    /// Gets the tool calls that wait for the user's approval, in the order they arrived. A call is added when an update
    /// with a <see cref="ToolApprovalRequestContent"/> is applied, and leaves when <see cref="Approve"/> or
    /// <see cref="Reject"/> decides it.
    /// </summary>
    public IReadOnlyList<ToolCallEntry> PendingApprovals => _pending;

    /// <summary>Gets a value indicating whether any tool call waits for the user's approval.</summary>
    public bool IsAwaitingApproval => _pending.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the user decided on every waiting call and nobody has taken the decisions with
    /// <see cref="WaitForApprovalsAsync"/> yet, which happens when the user decides while the stream is still open. An
    /// application that runs its own loop checks it, with <see cref="IsAwaitingApproval"/>, once a stream has ended.
    /// </summary>
    public bool HasDecisionsToSend => _pending.Count == 0 && _toSend.Count > 0;

    /// <summary>Gets a value indicating whether the turn has ended.</summary>
    public bool IsEnded => _result is not null;

    /// <summary>Gets the token counts reported so far in this turn, or <see langword="null"/> when there are none.</summary>
    public UsageDetails? Usage => _usage;

    /// <summary>
    /// Counts a request to the model. Call it before each request the turn sends, the first one included; the turn
    /// summary shows the count. The reply or reasoning that arrived before ends, so the next request's text starts an
    /// entry of its own.
    /// </summary>
    /// <exception cref="InvalidOperationException">The turn has ended.</exception>
    public void BeginCall()
    {
        ThrowIfEnded();
        _conversation.Projector.Break();
        _calls++;
        OnPropertyChanged(nameof(Calls));
    }

    /// <summary>
    /// Adds an entry of the application's own to the end of the entries while the turn is in progress, for example a card
    /// that asks the user something. The reply or reasoning before it ends, so text that arrives later starts an entry of
    /// its own. The entry is shown but not sent to the model, and <see cref="ChatConversation.SaveHistory"/> does not keep
    /// it.
    /// </summary>
    /// <param name="entry">The entry to add.</param>
    /// <exception cref="InvalidOperationException">The turn has ended.</exception>
    public void AddEntry(ChatEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ThrowIfEnded();
        _conversation.Projector.AddEntry(entry);
    }

    /// <summary>Adds the contents of a streamed update to the entries.</summary>
    /// <param name="update">The update.</param>
    /// <exception cref="InvalidOperationException">The turn has ended.</exception>
    public void Apply(ChatResponseUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        ThrowIfEnded();
        _updates.Add(update);
        _allUpdates.Add(update);
        foreach (var content in update.Contents)
        {
            if (content is UsageContent usage)
            {
                AddUsage(usage.Details);
                continue;
            }
            if (content is ToolApprovalRequestContent repeated
                && _conversation.Projector.FindToolCall(repeated.ToolCall.CallId) is { } seen)
            {
                DropRepeatedRequest(repeated, seen);
                continue;
            }
            _conversation.Projector.Add(content, streaming: true);
            if (content is ToolApprovalRequestContent request
                && _conversation.Projector.FindToolCall(request.ToolCall.CallId) is { Status: ToolCallStatus.AwaitingApproval } entry
                && !_pending.Contains(entry))
            {
                _pending.Add(entry);
                if (_pending.Count == 1)
                {
                    _waitStart = _conversation.TimeProvider.GetTimestamp();
                    OnPropertyChanged(nameof(IsAwaitingApproval));
                }
            }
        }
    }

    /// <summary>Approves a tool call that waits for the user's approval, so the client runs it.</summary>
    /// <param name="entry">One of the <see cref="PendingApprovals"/>.</param>
    /// <exception cref="ArgumentException">The call does not wait for approval in this turn.</exception>
    /// <exception cref="InvalidOperationException">The turn has ended.</exception>
    public void Approve(ToolCallEntry entry) => Decide(entry, approved: true, reason: null);

    /// <summary>Rejects a tool call that waits for the user's approval, so the client does not run it.</summary>
    /// <param name="entry">One of the <see cref="PendingApprovals"/>.</param>
    /// <param name="reason">Why, for the model and for <see cref="ToolCallEntry.RejectionReason"/>; optional.</param>
    /// <exception cref="ArgumentException">The call does not wait for approval in this turn.</exception>
    /// <exception cref="InvalidOperationException">The turn has ended.</exception>
    public void Reject(ToolCallEntry entry, string? reason = null) => Decide(entry, approved: false, reason);

    /// <summary>
    /// Waits until the user has decided on every call in <see cref="PendingApprovals"/>, and returns the message that
    /// carries the decisions: a user message with a <see cref="ToolApprovalResponseContent"/> for each call. The turn
    /// keeps the message in its history; an application that runs its own loop also sends it to the model with its next
    /// request. When the user decided before this was called, it returns that message at once.
    /// </summary>
    /// <param name="cancellationToken">Stops waiting. The calls stay undecided until the turn ends, and ending it rejects
    /// them.</param>
    /// <returns>The message with the decisions. When the turn ended while this waited, it holds what was decided before
    /// the end; a loop of the application's own checks <see cref="IsEnded"/> before it sends anything.</returns>
    /// <exception cref="InvalidOperationException">The turn has ended, or no call waits for approval and no decision
    /// is left to return.</exception>
    /// <exception cref="OperationCanceledException">The token was canceled.</exception>
    public Task<ChatMessage> WaitForApprovalsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfEnded();
        if (_pending.Count == 0)
        {
            return _toSend.Count > 0
                ? Task.FromResult(TakeDecisions())
                : throw new InvalidOperationException("No tool call waits for approval.");
        }
        _decided ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return TakeDecisionsAsync(_decided.Task, cancellationToken);
    }

    // NOTE: The decisions are taken by the waiter that is still there once they are made. A wait that was canceled
    // leaves them for the next one, which HasDecisionsToSend announces.
    private async Task<ChatMessage> TakeDecisionsAsync(Task decided, CancellationToken cancellationToken)
    {
        // The turn is used from one thread, so the waiter continues on the caller's context.
        await decided.WaitAsync(cancellationToken);
        // Two waiters at once get the same decisions.
        return _toSend.Count > 0 ? TakeDecisions() : _taken!;
    }

    /// <summary>The messages of the turn so far that a request continuing it sends after the history.</summary>
    internal IReadOnlyList<ChatMessage> Messages => _messages;

    /// <summary>Ends the turn after the model finished its reply.</summary>
    /// <returns>What the turn produced.</returns>
    /// <exception cref="InvalidOperationException">The turn has already ended.</exception>
    public ChatTurnResult Complete() => End(TurnOutcome.Completed, exception: null);

    /// <summary>
    /// Ends the turn because it was stopped. What arrived before is kept, in the entries and in the history, and the
    /// reply is marked as stopped.
    /// </summary>
    /// <returns>What the turn produced.</returns>
    /// <exception cref="InvalidOperationException">The turn has already ended.</exception>
    public ChatTurnResult Stop() => End(TurnOutcome.Stopped, exception: null);

    /// <summary>
    /// Ends the turn because of an exception, which is shown as an <see cref="ErrorEntry"/> with its message. What
    /// arrived before is kept.
    /// </summary>
    /// <param name="exception">The exception that ended the turn.</param>
    /// <returns>What the turn produced.</returns>
    /// <exception cref="InvalidOperationException">The turn has already ended.</exception>
    public ChatTurnResult Fail(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ThrowIfEnded();
        _conversation.Projector.AddEntry(new ErrorEntry(exception.Message));
        return End(TurnOutcome.Failed, exception);
    }

    private ChatTurnResult End(TurnOutcome outcome, Exception? exception)
    {
        ThrowIfEnded();
        var projector = _conversation.Projector;
        // IMPORTANT: Calls nobody approved are rejected, so that the history never holds a request without an answer that
        // a later request could still run.
        foreach (var entry in _pending.ToList())
        {
            DecideCore(entry, approved: false, UndecidedReason, ToolCallStatus.Canceled);
        }

        if (outcome == TurnOutcome.Stopped && _conversation.Entries.Count > 0
            && _conversation.Entries[^1] is AssistantReplyEntry { IsStreaming: true } reply)
        {
            reply.IsStopped = true;
        }
        projector.Break();
        foreach (var call in projector.ToolCalls.Where(call => call.Status == ToolCallStatus.Running))
        {
            call.Status = outcome == TurnOutcome.Failed ? ToolCallStatus.Failed : ToolCallStatus.Canceled;
        }

        var elapsed = MeasureElapsed();
        var summary = new TurnSummaryEntry(elapsed, _usage, outcome, _calls);
        projector.AddEntry(summary);

        var response = _allUpdates.ToChatResponse();
        FlushUpdates();
        var messages = _messages.ToList();
        // IMPORTANT: A call the user approved whose result never arrived would run with the next request, which the user
        // did not ask for. A result that says so closes it.
        messages.AddRange(ApprovalRecords.CloseApprovedCalls(messages));
        var last = messages.LastOrDefault(message => message.Role == ChatRole.Assistant);
        if (last is null)
        {
            last = new ChatMessage(ChatRole.Assistant, []);
            messages.Add(last);
        }
        last.Contents.Add(TurnRecord.Create(summary));

        _result = new ChatTurnResult(response, summary, exception);
        _conversation.EndTurn(this, messages);
        return _result;
    }

    internal const string UndecidedReason = "The turn ended before the user approved the call.";

    private void Decide(ToolCallEntry entry, bool approved, string? reason)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ThrowIfEnded();
        if (!_pending.Contains(entry))
        {
            throw new ArgumentException("The tool call does not wait for approval in this turn.", nameof(entry));
        }
        DecideCore(entry, approved, reason, approved ? ToolCallStatus.Running : ToolCallStatus.Rejected);
    }

    private void DecideCore(ToolCallEntry entry, bool approved, string? reason, ToolCallStatus status)
    {
        _decisions.Add(entry.ApprovalRequest!.CreateResponse(approved, reason));
        if (!approved)
        {
            entry.RejectionReason = reason;
        }
        entry.Status = status;
        _pending.Remove(entry);
        if (_pending.Count > 0)
        {
            return;
        }

        if (_waitStart is { } waitStart)
        {
            _waited += _conversation.TimeProvider.GetElapsedTime(waitStart);
            _waitStart = null;
        }
        // The decisions go into the history at once, after the reply that asked for them, so that what arrives later
        // comes after them.
        FlushUpdates();
        _toSend.Add(new ChatMessage(ChatRole.User, [.. _decisions]));
        _messages.Add(_toSend[^1]);
        _decisions.Clear();
        OnPropertyChanged(nameof(IsAwaitingApproval));
        if (_decided is { } decided)
        {
            _decided = null;
            decided.TrySetResult();
        }
    }

    /// <summary>
    /// Returns the decisions nobody has taken yet as one message. NOTE: Decisions made while the stream was still open
    /// stay until somebody waits for them, together with those made later, so a loop of the application's own sends
    /// every one of them.
    /// </summary>
    private ChatMessage TakeDecisions()
    {
        var message = _toSend.Count == 1
            ? _toSend[0]
            : new ChatMessage(ChatRole.User, [.. _toSend.SelectMany(decisions => decisions.Contents)]);
        _toSend.Clear();
        _taken = message;
        return message;
    }

    private void FlushUpdates()
    {
        if (_updates.Count == 0)
        {
            return;
        }
        // The history gets copies, so that the record End adds does not show up in the response the caller receives.
        // The combined response reports usage in ChatResponse.Usage; the history keeps the turn's own record instead.
        // A message that held nothing but a dropped request, and usage at most, is left out: a provider rejects an empty message.
        _messages.AddRange(_updates.ToChatResponse().Messages
            .Where(message => _dropped.Count == 0 || message.Contents.Any(content => content is not UsageContent && !IsDropped(content)))
            .Select(CopyForHistory));
        _updates.Clear();
    }

    internal const string RepeatedCallReason =
        "The model asked to run a call whose id this conversation used before. It was not run.";

    /// <summary>
    /// Handles a request for approval of a call id the conversation already has: the same request again is ignored, and
    /// a new request for an old id is rejected. Neither goes into the history.
    /// </summary>
    /// <remarks>
    /// WARNING: A client that runs tools keys requests by call id, and treats a call that has a result as done. A request
    /// for an id used before would either be approved without running, or break every later request of the
    /// conversation, so it is never offered to the user and never sent back.
    /// </remarks>
    private void DropRepeatedRequest(ToolApprovalRequestContent request, ToolCallEntry seen)
    {
        _dropped.Add(request);
        if (seen.ApprovalRequest?.RequestId == request.RequestId && _pending.Contains(seen))
        {
            return;
        }
        _conversation.Projector.AddRejectedRequest(request, RepeatedCallReason);
    }

    // Compared by reference: the dropped request is that very content, and another request may equal it.
    private bool IsDropped(AIContent content) => _dropped.Exists(dropped => ReferenceEquals(dropped, content));

    private ChatMessage CopyForHistory(ChatMessage message) =>
        new(message.Role, message.Contents.Where(content => content is not UsageContent && !IsDropped(content)).ToList())
        {
            AuthorName = message.AuthorName,
            CreatedAt = message.CreatedAt,
            MessageId = message.MessageId,
            AdditionalProperties = message.AdditionalProperties,
        };

    private TimeSpan MeasureElapsed()
    {
        var waited = _waited;
        if (_waitStart is { } waitStart)
        {
            waited += _conversation.TimeProvider.GetElapsedTime(waitStart);
        }
        return _conversation.TimeProvider.GetElapsedTime(_startTimestamp) - waited;
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void AddUsage(UsageDetails details)
    {
        _usage ??= new UsageDetails();
        _usage.Add(details);
    }

    private void ThrowIfEnded()
    {
        if (_result is not null)
        {
            throw new InvalidOperationException("The turn has already ended.");
        }
    }
}
