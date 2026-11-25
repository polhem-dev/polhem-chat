using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// A conversation in two views: the history of <see cref="ChatMessage"/>s that is sent to the model, and the
/// <see cref="Entries"/> a user interface shows.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="ChatRunner"/> drives a conversation with an <see cref="IChatClient"/>. An application that runs its own
/// loop drives it directly instead: <see cref="AddUserPrompt(string)"/>, then <see cref="BeginTurn"/> and
/// <see cref="ChatTurn.Apply"/> for each streamed update, then <see cref="ChatTurn.Complete"/>.
/// </para>
/// <para>
/// The conversation is not thread-safe. Use it from one thread, normally the UI thread, because the entries are bound
/// to views.
/// </para>
/// </remarks>
public sealed class ChatConversation : INotifyPropertyChanged
{
    private readonly List<ChatMessage> _history = [];

    // NOTE: Each prompt's message and the entry that shows it, so that rewinding cuts both lists at the same prompt,
    // whatever entries the application added or removed. A message here is a prompt; one that only answers approval
    // requests never is.
    private readonly Dictionary<ChatMessage, UserPromptEntry> _prompts = [];
    private readonly EntryProjector _projector;
    private ChatTurn? _currentTurn;

    /// <summary>Initializes a new instance of the <see cref="ChatConversation"/> class.</summary>
    public ChatConversation()
    {
        _projector = new EntryProjector(this);
    }

    /// <summary>
    /// Occurs when <see cref="CurrentTurn"/> changes, and with <see cref="LastPrompt"/> when
    /// <see cref="RewindLastPrompt"/> changed which prompt is the last one.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the entries a user interface shows, oldest first. Applications may add entries of their own types; they are
    /// shown but not sent to the model.
    /// </summary>
    public ObservableCollection<ChatEntry> Entries { get; } = [];

    /// <summary>
    /// Gets or sets the function that gives a tool call the text a front end shows for it, in place of the tool's name.
    /// Returning <see langword="null"/> keeps the name.
    /// </summary>
    public Func<FunctionCallContent, string?>? DescribeToolCall { get; set; }

    /// <summary>Gets the turn in progress, or <see langword="null"/> when there is none.</summary>
    public ChatTurn? CurrentTurn => _currentTurn;

    /// <summary>Gets the history as it is sent to the model, oldest first.</summary>
    public IReadOnlyList<ChatMessage> History => _history;

    internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    internal EntryProjector Projector => _projector;

    /// <summary>Adds a prompt from the user to the history and to the entries.</summary>
    /// <param name="text">The text of the prompt.</param>
    /// <returns>The entry that shows the prompt.</returns>
    /// <exception cref="InvalidOperationException">A turn is in progress.</exception>
    public UserPromptEntry AddUserPrompt(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ThrowIfTurnInProgress();
        var message = new ChatMessage(ChatRole.User, text);
        _history.Add(message);
        var entry = new UserPromptEntry(text);
        _prompts[message] = entry;
        _projector.AddEntry(entry);
        return entry;
    }

    /// <summary>Adds a prompt from the user with files attached to the history and to the entries.</summary>
    /// <param name="text">The text of the prompt; it may be empty when there are attachments.</param>
    /// <param name="attachments">The files, sent after the text in the same message.</param>
    /// <returns>The entry that shows the prompt.</returns>
    /// <exception cref="ArgumentException">Both the text and the attachments are empty.</exception>
    /// <exception cref="InvalidOperationException">A turn is in progress.</exception>
    public UserPromptEntry AddUserPrompt(string text, IReadOnlyList<DataContent> attachments)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(attachments);
        // A provider may reject a text block of nothing but white space, and the files say all there is to say.
        if (attachments.Count > 0 && string.IsNullOrWhiteSpace(text))
        {
            text = string.Empty;
        }
        if (text.Length == 0 && attachments.Count == 0)
        {
            throw new ArgumentException("A prompt needs text or attachments.", nameof(text));
        }
        ThrowIfTurnInProgress();
        var contents = new List<AIContent>(attachments.Count + 1);
        if (text.Length > 0)
        {
            contents.Add(new TextContent(text));
        }
        contents.AddRange(attachments);
        var message = new ChatMessage(ChatRole.User, contents);
        _history.Add(message);
        var entry = new UserPromptEntry(text, [.. attachments]);
        _prompts[message] = entry;
        _projector.AddEntry(entry);
        return entry;
    }

    /// <summary>
    /// Adds a message to the history and shows its contents as entries, without sending anything. System messages are
    /// kept in the history and not shown.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <exception cref="InvalidOperationException">A turn is in progress.</exception>
    public void AppendMessage(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfTurnInProgress();
        _history.Add(message);
        Project(message);
    }

    /// <summary>Starts a turn whose streamed updates become entries as they arrive.</summary>
    /// <returns>The turn, which must be ended with <see cref="ChatTurn.Complete"/>, <see cref="ChatTurn.Stop"/> or
    /// <see cref="ChatTurn.Fail"/>.</returns>
    /// <exception cref="InvalidOperationException">Another turn is in progress.</exception>
    public ChatTurn BeginTurn()
    {
        ThrowIfTurnInProgress();
        _currentTurn = new ChatTurn(this);
        OnCurrentTurnChanged();
        return _currentTurn;
    }

    /// <summary>
    /// Replaces the conversation with a saved history. A tool call the history asked approval for and never answered is
    /// rejected, and one that was approved but has no result is closed with a result that says it did not finish, so a
    /// later request runs neither; both are added to the history.
    /// </summary>
    /// <param name="messages">A history saved with <see cref="SaveHistory"/>, or any list of messages.</param>
    /// <exception cref="InvalidOperationException">A turn is in progress.</exception>
    public void LoadHistory(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        Clear();
        foreach (var message in messages)
        {
            AppendMessage(message);
        }
        RejectUnansweredApprovals();
        foreach (var message in ApprovalRecords.CloseApprovedCalls(_history))
        {
            AppendMessage(message);
        }
    }

    /// <summary>
    /// Returns a copy of the history for the application to store. The time and token counts of each turn travel in a
    /// <see cref="UsageContent"/> at the end of the turn, which <see cref="LoadHistory"/> turns back into a
    /// <see cref="TurnSummaryEntry"/>. Serialize it with <see cref="AIJsonUtilities.DefaultOptions"/> to keep every
    /// content type.
    /// </summary>
    /// <returns>The messages, oldest first.</returns>
    public IReadOnlyList<ChatMessage> SaveHistory() => [.. _history];

    /// <summary>
    /// Removes the user's last prompt and everything after it, from the history and from the entries, so that it can be
    /// sent again as it was or changed. Entries of the application's own after the prompt go too.
    /// </summary>
    /// <returns>The message of the prompt that was removed, with its text and attachments, or <see langword="null"/> when
    /// the conversation has no prompt.</returns>
    /// <remarks>
    /// A prompt is a message added with <see cref="AddUserPrompt(string)"/> or shown from a loaded history; a user message
    /// that only answers approval requests is not one, and neither is a <see cref="UserPromptEntry"/> the application
    /// added to the entries itself. What the tools of the removed turns did is not undone; the model only stops seeing
    /// it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">A turn is in progress.</exception>
    public ChatMessage? RewindLastPrompt()
    {
        ThrowIfTurnInProgress();
        var index = LastPromptIndex();
        if (index < 0)
        {
            return null;
        }
        var prompt = _history[index];
        var promptEntry = _prompts[prompt];
        foreach (var removed in _history.Skip(index))
        {
            _prompts.Remove(removed);
        }
        _history.RemoveRange(index, _history.Count - index);

        // The entries go from the prompt's own entry on. An application that removed that entry itself has left no
        // place to cut at, and its entries stay as they are.
        _projector.Break();
        var first = Entries.IndexOf(promptEntry);
        for (var entry = Entries.Count - 1; first >= 0 && entry >= first; entry--)
        {
            _projector.Forget(Entries[entry]);
            Entries.RemoveAt(entry);
        }
        // The entries may not have changed (the application removed the prompt's entry), so a front end that follows
        // them would not learn that another prompt is now the last one.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastPrompt)));
        return prompt;
    }

    /// <summary>Returns the index in the history of the user's last prompt, or -1.</summary>
    internal int LastPromptIndex() => _history.FindLastIndex(_prompts.ContainsKey);

    /// <summary>
    /// Gets the entry of the prompt <see cref="RewindLastPrompt"/> would remove, or <see langword="null"/> when there is
    /// none or the application removed its entry. A front end offers to send again or change only this prompt: a
    /// <see cref="UserPromptEntry"/> the application added to the entries itself is not one.
    /// </summary>
    /// <remarks>It is computed when read; changes to the entries or the history raise no notification for it.</remarks>
    public UserPromptEntry? LastPrompt =>
        LastPromptIndex() is var index and >= 0 && _prompts[_history[index]] is var entry && Entries.Contains(entry) ? entry : null;

    /// <summary>Removes every message and entry.</summary>
    /// <exception cref="InvalidOperationException">A turn is in progress.</exception>
    public void Clear()
    {
        ThrowIfTurnInProgress();
        _history.Clear();
        _prompts.Clear();
        _projector.Reset();
        _projector.Break();
        Entries.Clear();
    }

    /// <summary>
    /// Returns the history to send to the model: the summaries this package keeps in it are left out, and so are
    /// messages that had nothing else.
    /// </summary>
    internal List<ChatMessage> CreateRequestMessages()
    {
        var messages = new List<ChatMessage>(_history.Count);
        foreach (var message in _history)
        {
            if (!message.Contents.Any(content => content is UsageContent))
            {
                messages.Add(message);
                continue;
            }
            var contents = message.Contents.Where(content => content is not UsageContent).ToList();
            if (contents.Count > 0)
            {
                messages.Add(new ChatMessage(message.Role, contents)
                {
                    AuthorName = message.AuthorName,
                    CreatedAt = message.CreatedAt,
                    MessageId = message.MessageId,
                    AdditionalProperties = message.AdditionalProperties,
                });
            }
        }
        return messages;
    }

    // IMPORTANT: A history saved while a call waited for approval ends with the request and no answer. It is answered
    // with a rejection here, so that a request sent later cannot run a call the user never approved.
    private void RejectUnansweredApprovals()
    {
        var answered = _history.SelectMany(message => message.Contents).OfType<ToolApprovalResponseContent>()
            .Select(response => response.RequestId).ToHashSet(StringComparer.Ordinal);
        var unanswered = _history.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>()
            .Where(request => !answered.Contains(request.RequestId)).ToList();
        if (unanswered.Count == 0)
        {
            return;
        }
        var rejections = unanswered.Select(request => (AIContent)request.CreateResponse(false, ChatTurn.UndecidedReason)).ToList();
        _history.Add(new ChatMessage(ChatRole.User, rejections));
        foreach (var request in unanswered)
        {
            if (_projector.FindToolCall(request.ToolCall.CallId) is { Status: ToolCallStatus.AwaitingApproval } entry)
            {
                entry.RejectionReason = ChatTurn.UndecidedReason;
                entry.Status = ToolCallStatus.Canceled;
            }
        }
    }

    internal void EndTurn(ChatTurn turn, IEnumerable<ChatMessage> messages)
    {
        _history.AddRange(messages);
        if (ReferenceEquals(_currentTurn, turn))
        {
            _currentTurn = null;
            OnCurrentTurnChanged();
        }
    }

    private void Project(ChatMessage message)
    {
        if (message.Role == ChatRole.System)
        {
            return;
        }
        if (message.Role == ChatRole.User)
        {
            var text = message.Text;
            var attachments = message.Contents.OfType<DataContent>().ToList();
            if (text.Length > 0 || attachments.Count > 0)
            {
                var entry = new UserPromptEntry(text, attachments);
                _prompts[message] = entry;
                _projector.AddEntry(entry);
            }
            foreach (var response in message.Contents.OfType<ToolApprovalResponseContent>())
            {
                _projector.ApplyApprovalResponse(response);
            }
            return;
        }
        _projector.Break();
        foreach (var content in message.Contents)
        {
            if (content is UsageContent usage)
            {
                if (TurnRecord.Read(usage) is { } summary)
                {
                    _projector.AddEntry(summary);
                }
                continue;
            }
            _projector.Add(content, streaming: false);
        }
        _projector.Break();
    }

    private void OnCurrentTurnChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTurn)));

    private void ThrowIfTurnInProgress()
    {
        if (_currentTurn is not null)
        {
            throw new InvalidOperationException("A turn is in progress. End it before changing the conversation.");
        }
    }
}
