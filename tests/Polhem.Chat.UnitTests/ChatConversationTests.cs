using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.UnitTests;

public class ChatConversationTests
{
    [Fact(DisplayName = "An application running its own loop drives the conversation without a runner")]
    public void BeginTurn_HostRunsLoop_BuildsEntriesAndHistory()
    {
        var time = new ManualTimeProvider();
        var conversation = new ChatConversation { TimeProvider = time };

        conversation.AddUserPrompt("Draft a form");
        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "add_field", new Dictionary<string, object?>())]));
        turn.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("call-1", "added")]));
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "Done."));
        time.Advance(TimeSpan.FromMilliseconds(1500));
        var result = turn.Complete();

        Assert.True(turn.IsEnded);
        Assert.Null(conversation.CurrentTurn);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), result.Summary.Elapsed);
        Assert.Equal(
            [typeof(UserPromptEntry), typeof(ToolCallEntry), typeof(AssistantReplyEntry), typeof(TurnSummaryEntry)],
            conversation.Entries.Select(entry => entry.GetType()));
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant], conversation.History.Select(message => message.Role));
    }

    [Fact(DisplayName = "A turn that has ended refuses further updates")]
    public void Apply_AfterEnd_Throws()
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();
        turn.Complete();

        Assert.Throws<InvalidOperationException>(() => turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "Late")));
        Assert.Throws<InvalidOperationException>(() => turn.Complete());
    }

    [Fact(DisplayName = "The conversation cannot change while a turn is in progress")]
    public void Changes_DuringTurn_Throw()
    {
        var conversation = new ChatConversation();
        conversation.BeginTurn();

        Assert.Throws<InvalidOperationException>(() => conversation.AddUserPrompt("Hi"));
        Assert.Throws<InvalidOperationException>(() => conversation.AppendMessage(new ChatMessage(ChatRole.User, "Hi")));
        Assert.Throws<InvalidOperationException>(() => conversation.BeginTurn());
        Assert.Throws<InvalidOperationException>(() => conversation.Clear());
        Assert.Throws<InvalidOperationException>(() => conversation.LoadHistory([]));
    }

    [Fact(DisplayName = "Text from a new turn starts a new reply instead of continuing the last one")]
    public void BeginTurn_AfterAppendedReply_StartsNewReply()
    {
        var conversation = new ChatConversation();
        conversation.AppendMessage(new ChatMessage(ChatRole.Assistant, "Welcome."));

        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "Next."));
        turn.Complete();

        Assert.Equal(["Welcome.", "Next."], conversation.Entries.OfType<AssistantReplyEntry>().Select(reply => reply.Text));
    }

    [Fact(DisplayName = "A saved history, serialized to JSON and loaded again, shows the same entries")]
    public void SaveHistory_RoundTripThroughJson_RestoresEntries()
    {
        var time = new ManualTimeProvider();
        var original = new ChatConversation { TimeProvider = time };
        original.AddUserPrompt("Weather?");
        var turn = original.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent("Need the tool.")]));
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_weather", new Dictionary<string, object?> { ["city"] = "Taipei" })]));
        turn.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("call-1", "Sunny")]));
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "It is sunny."));
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = 40, OutputTokenCount = 8, TotalTokenCount = 48 })]));
        time.Advance(TimeSpan.FromSeconds(2));
        turn.Complete();

        var json = JsonSerializer.Serialize(original.SaveHistory(), AIJsonUtilities.DefaultOptions);
        var messages = JsonSerializer.Deserialize<List<ChatMessage>>(json, AIJsonUtilities.DefaultOptions)!;
        var loaded = new ChatConversation();
        loaded.LoadHistory(messages);

        Assert.Equal(original.Entries.Select(entry => entry.GetType()), loaded.Entries.Select(entry => entry.GetType()));
        Assert.Equal("Weather?", loaded.Entries.OfType<UserPromptEntry>().Single().Text);
        Assert.Equal("Need the tool.", loaded.Entries.OfType<ReasoningEntry>().Single().Text);
        var call = loaded.Entries.OfType<ToolCallEntry>().Single();
        Assert.Equal(ToolCallStatus.Completed, call.Status);
        Assert.Equal("get_weather", call.Name);
        Assert.Equal("It is sunny.", loaded.Entries.OfType<AssistantReplyEntry>().Single().Text);
        var summary = loaded.Entries.OfType<TurnSummaryEntry>().Single();
        Assert.Equal(TimeSpan.FromSeconds(2), summary.Elapsed);
        Assert.Equal(TurnOutcome.Completed, summary.Outcome);
        Assert.Equal(48, summary.Usage!.TotalTokenCount);
        Assert.All(loaded.Entries.OfType<AssistantReplyEntry>(), reply => Assert.False(reply.IsStreaming));
    }

    [Fact(DisplayName = "A stopped turn keeps its outcome through a save and a load")]
    public void SaveHistory_StoppedTurn_KeepsOutcome()
    {
        var original = new ChatConversation();
        original.AddUserPrompt("Go");
        var turn = original.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "Partial"));
        turn.Stop();

        var loaded = new ChatConversation();
        loaded.LoadHistory(original.SaveHistory());

        Assert.Equal(TurnOutcome.Stopped, loaded.Entries.OfType<TurnSummaryEntry>().Single().Outcome);
    }

    [Fact(DisplayName = "A turn that produced no message still keeps its summary in the history")]
    public void Fail_BeforeAnyContent_KeepsSummary()
    {
        var original = new ChatConversation();
        original.AddUserPrompt("Go");
        original.BeginTurn().Fail(new InvalidOperationException("Offline."));

        var loaded = new ChatConversation();
        loaded.LoadHistory(original.SaveHistory());

        Assert.Equal(TurnOutcome.Failed, loaded.Entries.OfType<TurnSummaryEntry>().Single().Outcome);
        Assert.Equal([ChatRole.User], original.CreateRequestMessages().Select(message => message.Role));
    }

    [Fact(DisplayName = "A history from elsewhere loads: system messages are kept but not shown, and foreign usage is ignored")]
    public void LoadHistory_ForeignMessages_ShowsConversation()
    {
        var conversation = new ChatConversation();

        conversation.LoadHistory(
        [
            new ChatMessage(ChatRole.System, "You are terse."),
            new ChatMessage(ChatRole.User, "Hi"),
            new ChatMessage(ChatRole.Assistant, [new TextContent("Hello."), new UsageContent(new UsageDetails { TotalTokenCount = 3 })]),
        ]);

        Assert.Equal([typeof(UserPromptEntry), typeof(AssistantReplyEntry)], conversation.Entries.Select(entry => entry.GetType()));
        Assert.Equal(3, conversation.History.Count);
    }

    [Fact(DisplayName = "Loading replaces what the conversation had")]
    public void LoadHistory_ReplacesExisting()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Old");

        conversation.LoadHistory([new ChatMessage(ChatRole.User, "New")]);

        Assert.Equal("New", ((UserPromptEntry)Assert.Single(conversation.Entries)).Text);
        Assert.Single(conversation.History);
    }

    [Fact(DisplayName = "Saving returns a copy that later turns do not change")]
    public void SaveHistory_ReturnsCopy()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("One");

        var saved = conversation.SaveHistory();
        conversation.AddUserPrompt("Two");

        Assert.Single(saved);
    }

    [Fact(DisplayName = "An application can add entries of its own type")]
    public void Entries_CustomEntry_Shown()
    {
        var conversation = new ChatConversation();
        var question = new QuestionEntry("Which layout?");

        conversation.Entries.Add(question);

        Assert.Same(question, Assert.Single(conversation.Entries));
        Assert.Empty(conversation.History);
    }

    [Fact(DisplayName = "Appending to a reply raises PropertyChanged for its text")]
    public void AppendText_RaisesPropertyChanged()
    {
        var reply = new AssistantReplyEntry("A", isStreaming: true);
        var changed = new List<string?>();
        reply.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        reply.AppendText("B");
        reply.AppendText(string.Empty);

        Assert.Equal("AB", reply.Text);
        Assert.Equal([nameof(AssistantReplyEntry.Text)], changed);
    }

    private sealed class QuestionEntry(string question) : ChatEntry
    {
        public string Question { get; } = question;
    }
}
