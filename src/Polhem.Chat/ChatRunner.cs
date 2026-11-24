using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// Runs a <see cref="ChatConversation"/> against an <see cref="IChatClient"/>: sends each prompt with the history,
/// turns the streamed reply into entries, and measures each turn.
/// </summary>
/// <remarks>
/// <para>
/// To let the model call tools, pass a client wrapped in a <c>FunctionInvokingChatClient</c> and put the tools in
/// <see cref="Options"/>; the calls and their results then show up as <see cref="ToolCallEntry"/> items.
/// </para>
/// <para>
/// The runner continues on the context <see cref="SendAsync"/> is called on, so entries change on that thread. Call it
/// from the UI thread when the entries are bound to views.
/// </para>
/// </remarks>
public sealed class ChatRunner : INotifyPropertyChanged
{
    private IChatClient _client;
    private CancellationTokenSource? _stop;
    private bool _isRunning;

    /// <summary>Initializes a new instance of the <see cref="ChatRunner"/> class with an empty conversation.</summary>
    /// <param name="client">The client to send the conversation to.</param>
    public ChatRunner(IChatClient client)
        : this(client, new ChatConversation())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ChatRunner"/> class.</summary>
    /// <param name="client">The client to send the conversation to.</param>
    /// <param name="conversation">The conversation to run.</param>
    public ChatRunner(IChatClient client, ChatConversation conversation)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(conversation);
        _client = client;
        Conversation = conversation;
    }

    /// <summary>
    /// Occurs before a prompt is added and sent. A handler may change <see cref="MessageSendingEventArgs.Text"/>, or set
    /// <see cref="CancelEventArgs.Cancel"/> to send nothing.
    /// </summary>
    public event EventHandler<MessageSendingEventArgs>? MessageSending;

    /// <summary>Occurs after a turn ended, whether it completed, was stopped or failed.</summary>
    public event EventHandler<ResponseReceivedEventArgs>? ResponseReceived;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the conversation the runner drives.</summary>
    public ChatConversation Conversation { get; }

    /// <summary>
    /// Gets or sets the client to send the conversation to. Replacing it switches the model for the next turn; the
    /// history is kept.
    /// </summary>
    public IChatClient Client
    {
        get => _client;
        set => _client = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets or sets the options sent with every request: model, instructions, tools and so on.</summary>
    public ChatOptions? Options { get; set; }

    /// <summary>Gets a value indicating whether a turn is in progress.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Adds a prompt to the conversation and streams the model's reply into it. Errors do not throw: they end the turn
    /// with <see cref="TurnOutcome.Failed"/> and are shown as an <see cref="ErrorEntry"/>.
    /// </summary>
    /// <param name="text">The prompt.</param>
    /// <param name="cancellationToken">Stops the turn, like <see cref="Stop"/>.</param>
    /// <returns>
    /// What the turn produced, or <see langword="null"/> when a <see cref="MessageSending"/> handler canceled it.
    /// </returns>
    /// <exception cref="InvalidOperationException">A turn is already in progress.</exception>
    public async Task<ChatTurnResult?> SendAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (IsRunning || Conversation.CurrentTurn is not null)
        {
            throw new InvalidOperationException("A turn is already in progress.");
        }

        var sending = new MessageSendingEventArgs(text);
        MessageSending?.Invoke(this, sending);
        if (sending.Cancel)
        {
            return null;
        }

        Conversation.AddUserPrompt(sending.Text);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _stop = stop;
        IsRunning = true;
        var turn = Conversation.BeginTurn();
        ChatTurnResult result;
        try
        {
            await StreamAsync(turn, stop.Token);
            result = turn.Complete();
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            result = turn.Stop();
        }
        // NOTE: The runner talks to whichever provider the application plugged in, and each one throws its own exception
        // types. The exception is not swallowed: the turn records it in ChatTurnResult.Exception and shows its message
        // as an ErrorEntry. Letting it escape would end the turn half-way in an async handler of the UI.
        catch (Exception exception)
        {
            result = turn.Fail(exception);
        }
        finally
        {
            _stop = null;
            IsRunning = false;
        }

        ResponseReceived?.Invoke(this, new ResponseReceivedEventArgs(result));
        return result;
    }

    /// <summary>
    /// Streams the reply into the turn, and goes on after the user's approval as long as the client asks for it.
    /// </summary>
    private async Task StreamAsync(ChatTurn turn, CancellationToken cancellationToken)
    {
        var messages = Conversation.CreateRequestMessages();
        var requests = new RequestCounter();
        turn.BeginCall();
        while (true)
        {
            await foreach (var update in _client.GetStreamingResponseAsync(messages, Options, cancellationToken).WithCancellation(cancellationToken))
            {
                if (!RepeatsKnownCalls(update) && requests.StartsRequest(update))
                {
                    turn.BeginCall();
                }
                turn.Apply(update);
            }
            if (!turn.IsAwaitingApproval && !turn.HasDecisionsToSend)
            {
                break;
            }
            // The client stopped to ask for approval. The turn goes on once the user has decided, which may have
            // happened while the stream was still open, with the decisions sent after the reply so far.
            await turn.WaitForApprovalsAsync(cancellationToken);
            // WARNING: The client runs the approved calls before it asks the model again. A stop that came with the
            // decision must end the turn here, or the calls would run after the user asked to stop.
            cancellationToken.ThrowIfCancellationRequested();
            messages = [.. Conversation.CreateRequestMessages(), .. turn.Messages];
            requests.Continue();
        }
    }

    // NOTE: After the user approved calls, FunctionInvokingChatClient sends the calls it is about to run again, under a
    // response id of its own. That is not a response of the model, so it does not count as a request.
    private bool RepeatsKnownCalls(ChatResponseUpdate update) =>
        update.Contents.Count > 0
        && update.Contents.All(content => content is FunctionCallContent call && Conversation.Projector.FindToolCall(call.CallId) is not null);

    /// <summary>
    /// Stops the turn in progress. What arrived before is kept and the reply is marked as stopped. Does nothing when no
    /// turn is in progress.
    /// </summary>
    public void Stop() => _stop?.Cancel();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Tells which streamed updates start another request to the model within a turn.</summary>
    /// <remarks>
    /// NOTE: A client that runs tools, such as FunctionInvokingChatClient, sends several requests inside one stream.
    /// Each response the model sent has its own ResponseId, so a new one is another request. A client that sets none
    /// counts as one request per stream. The ids are kept for the whole turn, because a client continuing after the
    /// user's approval may send the updates of an earlier response again.
    /// </remarks>
    private sealed class RequestCounter
    {
        private readonly HashSet<string> _responses = [];
        private bool _continuing;

        /// <summary>Marks that the next stream continues the turn, so its first response is a new request.</summary>
        public void Continue() => _continuing = true;

        public bool StartsRequest(ChatResponseUpdate update)
        {
            if (update.Role is not null && update.Role != ChatRole.Assistant)
            {
                return false;
            }
            var isNew = update.ResponseId is { } responseId ? _responses.Add(responseId) : _continuing;
            if (!isNew || (!_continuing && _responses.Count <= 1))
            {
                return false;
            }
            _continuing = false;
            return true;
        }
    }
}
