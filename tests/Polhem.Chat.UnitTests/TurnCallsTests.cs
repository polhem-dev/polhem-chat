using System.Text.Json;
using Microsoft.Extensions.AI;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.UnitTests;

/// <summary>The requests a turn sent and the time it has taken, as the turn counts them and the summary keeps them.</summary>
public class TurnCallsTests
{
    private static Yield Response(string responseId, string text) =>
        new(new ChatResponseUpdate(ChatRole.Assistant, text) { ResponseId = responseId });

    [Fact(DisplayName = "An application that runs its own loop counts each request with BeginCall, and the summary keeps the count")]
    public void BeginCall_HostDriven_CountsIntoTheSummary()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        var changes = new List<string?>();
        turn.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        turn.BeginCall();
        turn.BeginCall();
        var result = turn.Complete();

        Assert.Equal(2, turn.Calls);
        Assert.Equal([nameof(ChatTurn.Calls), nameof(ChatTurn.Calls)], changes);
        Assert.Equal(2, result.Summary.Calls);
        Assert.Throws<InvalidOperationException>(turn.BeginCall);
    }

    [Fact(DisplayName = "A turn whose requests nobody counted has a summary with no calls")]
    public void Complete_NoBeginCall_HasNoCalls()
    {
        var conversation = new ChatConversation();
        var result = conversation.BeginTurn().Complete();

        Assert.Equal(0, result.Summary.Calls);
    }

    [Fact(DisplayName = "The runner counts each response the model sent, so a client that runs tools counts every request")]
    public async Task SendAsync_SeveralResponses_CountsEach()
    {
        var client = new ScriptedChatClient(
            Response("r1", "Looking"),
            ToolContents(new FunctionResultContent("c1", "found")),
            Response("r2", "Found it"),
            Response("r2", "."));
        var runner = new ChatRunner(client);

        var result = await runner.SendAsync("Hi");

        Assert.Equal(2, result!.Summary.Calls);
    }

    [Fact(DisplayName = "The runner counts one request for a client that sets no response ids")]
    public async Task SendAsync_NoResponseIds_CountsOne()
    {
        var runner = new ChatRunner(new ScriptedChatClient(Text("Hello"), Text(" there")));

        var result = await runner.SendAsync("Hi");

        Assert.Equal(1, result!.Summary.Calls);
    }

    [Fact(DisplayName = "The time of a turn grows while it runs and stays at what it took once it ends")]
    public void Elapsed_FollowsTheClockUntilTheTurnEnds()
    {
        var time = new ManualTimeProvider();
        var conversation = new ChatConversation { TimeProvider = time };
        var turn = conversation.BeginTurn();

        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(3), turn.Elapsed);
        turn.Complete();
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(3), turn.Elapsed);
    }

    [Fact(DisplayName = "The conversation says when a turn begins and when it ends")]
    public void CurrentTurn_RaisesPropertyChanged()
    {
        var conversation = new ChatConversation();
        var changes = new List<ChatTurn?>();
        conversation.PropertyChanged += (_, e) =>
        {
            Assert.Equal(nameof(ChatConversation.CurrentTurn), e.PropertyName);
            changes.Add(conversation.CurrentTurn);
        };

        var turn = conversation.BeginTurn();
        turn.Stop();

        Assert.Equal([turn, null], changes);
    }

    [Fact(DisplayName = "The calls of each turn survive saving the history as JSON and loading it again")]
    public void SaveAndLoadHistory_KeepsTheCalls()
    {
        var original = new ChatConversation();
        original.AddUserPrompt("Hi");
        var turn = original.BeginTurn();
        turn.BeginCall();
        turn.BeginCall();
        turn.BeginCall();
        turn.Complete();
        var options = AIJsonUtilities.DefaultOptions;
        var json = JsonSerializer.Serialize(original.SaveHistory(), options.GetTypeInfo(typeof(IReadOnlyList<ChatMessage>)));

        var loaded = new ChatConversation();
        loaded.LoadHistory((List<ChatMessage>)JsonSerializer.Deserialize(json, options.GetTypeInfo(typeof(List<ChatMessage>)))!);

        Assert.Equal(3, Assert.Single(loaded.Entries.OfType<TurnSummaryEntry>()).Calls);
    }

    [Fact(DisplayName = "A summary cannot have a negative number of calls")]
    public void TurnSummaryEntry_NegativeCalls_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new TurnSummaryEntry(TimeSpan.Zero, usage: null, TurnOutcome.Completed, -1));
}
