using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Windows.Input;
using Microsoft.Extensions.AI;
using Polhem.Chat;

namespace ChatGallery;

/// <summary>
/// A conversation the host drives itself, the way an application with a tool loop of its own uses the view: it adds the
/// prompt, starts a turn, applies each streamed update, and ends the turn. Before each turn ends it adds a
/// <see cref="ChoiceEntry"/>, an entry type of its own, with <see cref="ChatTurn.AddEntry"/>.
/// </summary>
public sealed class HostSession : INotifyPropertyChanged
{
    private readonly IChatClient _client = DemoTools.WithTools(new DemoChatClient());
    private CancellationTokenSource? _stop;
    private bool _isGenerating;

    public HostSession() => ChooseCommand = new DelegateCommand(choice => _ = SendAsync((string)choice!));

    public event PropertyChangedEventHandler? PropertyChanged;

    public ChatConversation Conversation { get; } = new() { DescribeToolCall = ToolTitles.Describe };

    /// <summary>Gets a value indicating whether a reply is in progress, which the view shows with its stop button.</summary>
    public bool IsGenerating
    {
        get => _isGenerating;
        private set
        {
            if (_isGenerating != value)
            {
                _isGenerating = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Gets the command the buttons of a <see cref="ChoiceEntry"/> run, with the picked answer.</summary>
    public ICommand ChooseCommand { get; }

    public async Task SendAsync(string prompt)
    {
        if (IsGenerating)
        {
            return;
        }
        foreach (var open in Conversation.Entries.OfType<ChoiceEntry>())
        {
            open.IsAnswered = true;
        }

        Conversation.AddUserPrompt(prompt);
        var turn = Conversation.BeginTurn();
        using var stop = new CancellationTokenSource();
        _stop = stop;
        IsGenerating = true;
        try
        {
            // The host keeps the messages of the turn itself, to send them again after the user's approval.
            var messages = Conversation.SaveHistory().ToList();
            while (true)
            {
                // An application with its own tool loop calls BeginCall before each request.
                turn.BeginCall();
                var updates = new List<ChatResponseUpdate>();
                await foreach (var update in _client.GetStreamingResponseAsync(messages, DemoTools.Options, stop.Token))
                {
                    updates.Add(update);
                    turn.Apply(update);
                }
                // The user may decide while the stream is still open, so the host checks for decisions not sent yet too.
                if (!turn.IsAwaitingApproval && !turn.HasDecisionsToSend)
                {
                    break;
                }
                // The client asked to run a tool that needs approval. The view shows the card; the host waits for the
                // user's decisions and sends them after the reply so far.
                var decisions = await turn.WaitForApprovalsAsync(stop.Token);
                messages.AddRange(updates.ToChatResponse().Messages);
                messages.Add(decisions);
            }
            // The card belongs to this turn, so it goes in before the turn ends and its summary follows it.
            turn.AddEntry(DemoReplies.Choices(prompt));
            turn.Complete();
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            turn.Stop();
        }
        // FunctionInvokingChatClient throws InvalidOperationException when the history does not match what it expects.
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            turn.Fail(exception);
        }
        finally
        {
            _stop = null;
            IsGenerating = false;
        }
    }

    public void Stop() => _stop?.Cancel();

    /// <summary>
    /// Saves the history as JSON and loads it back, the way an application stores a conversation between sessions. The
    /// turn summaries come back; the choice cards do not, because they are entries of the host's and not messages.
    /// </summary>
    public void ReloadHistory()
    {
        if (IsGenerating)
        {
            return;
        }
        // The type information comes from the source-generated contracts of Microsoft.Extensions.AI, not from reflection,
        // so this also works when the app is trimmed (iOS).
        var contract = (JsonTypeInfo<IList<ChatMessage>>)AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(IList<ChatMessage>));
        var json = JsonSerializer.Serialize([.. Conversation.SaveHistory()], contract);
        Conversation.LoadHistory(JsonSerializer.Deserialize(json, contract) ?? []);
    }

    public void Clear()
    {
        if (!IsGenerating)
        {
            Conversation.Clear();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
