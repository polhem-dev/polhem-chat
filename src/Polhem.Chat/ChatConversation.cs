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
/// loop drives it directly instead: <see cref="AddUserPrompt"/>, then <see cref="BeginTurn"/> and
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
    private readonly EntryProjector _projector;
    private ChatTurn? _currentTurn;

    /// <summary>Initializes a new instance of the <see cref="ChatConversation"/> class.</summary>
    public ChatConversation()
    {
        _projector = new EntryProjector(this);
    }

    /// <summary>Occurs when <see cref="CurrentTurn"/> changes.</summary>
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
        _history.Add(new ChatMessage(ChatRole.User, text));
        var entry = new UserPromptEntry(text);
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

    /// <summary>Replaces the conversation with a saved history.</summary>
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
    }

    /// <summary>
    /// Returns a copy of the history for the application to store. The time and token counts of each turn travel in a
    /// <see cref="UsageContent"/> at the end of the turn, which <see cref="LoadHistory"/> turns back into a
    /// <see cref="TurnSummaryEntry"/>. Serialize it with <see cref="AIJsonUtilities.DefaultOptions"/> to keep every
    /// content type.
    /// </summary>
    /// <returns>The messages, oldest first.</returns>
    public IReadOnlyList<ChatMessage> SaveHistory() => [.. _history];

    /// <summary>Removes every message and entry.</summary>
    /// <exception cref="InvalidOperationException">A turn is in progress.</exception>
    public void Clear()
    {
        ThrowIfTurnInProgress();
        _history.Clear();
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
            if (text.Length > 0)
            {
                _projector.AddEntry(new UserPromptEntry(text));
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
