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
    private readonly long _startTimestamp;
    private UsageDetails? _usage;
    private ChatTurnResult? _result;
    private int _calls;

    internal ChatTurn(ChatConversation conversation)
    {
        _conversation = conversation;
        _startTimestamp = conversation.TimeProvider.GetTimestamp();
        conversation.Projector.Break();
    }

    /// <summary>Occurs when <see cref="Calls"/> changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the requests sent to the model in this turn so far, as <see cref="BeginCall"/> counted them.</summary>
    public int Calls => _calls;

    /// <summary>Gets the time since the turn began, or the time it took once it has ended.</summary>
    public TimeSpan Elapsed => _result?.Summary.Elapsed ?? _conversation.TimeProvider.GetElapsedTime(_startTimestamp);

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Calls)));
    }

    /// <summary>Adds the contents of a streamed update to the entries.</summary>
    /// <param name="update">The update.</param>
    /// <exception cref="InvalidOperationException">The turn has ended.</exception>
    public void Apply(ChatResponseUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        ThrowIfEnded();
        _updates.Add(update);
        foreach (var content in update.Contents)
        {
            if (content is UsageContent usage)
            {
                AddUsage(usage.Details);
                continue;
            }
            _conversation.Projector.Add(content, streaming: true);
        }
    }

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

        var elapsed = _conversation.TimeProvider.GetElapsedTime(_startTimestamp);
        var summary = new TurnSummaryEntry(elapsed, _usage, outcome, _calls);
        projector.AddEntry(summary);

        var response = _updates.ToChatResponse();
        // The history gets copies, so that the record below does not show up in the response the caller receives.
        // The combined response reports usage in ChatResponse.Usage; the history keeps the turn's own record instead.
        var messages = response.Messages.Select(CopyWithoutUsage).ToList();
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

    private void AddUsage(UsageDetails details)
    {
        _usage ??= new UsageDetails();
        _usage.Add(details);
    }

    private static ChatMessage CopyWithoutUsage(ChatMessage message) =>
        new(message.Role, message.Contents.Where(content => content is not UsageContent).ToList())
        {
            AuthorName = message.AuthorName,
            CreatedAt = message.CreatedAt,
            MessageId = message.MessageId,
            AdditionalProperties = message.AdditionalProperties,
        };

    private void ThrowIfEnded()
    {
        if (_result is not null)
        {
            throw new InvalidOperationException("The turn has already ended.");
        }
    }
}
