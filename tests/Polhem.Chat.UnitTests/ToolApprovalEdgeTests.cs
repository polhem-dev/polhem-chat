using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.UnitTests;

/// <summary>
/// Approvals at the edges: a turn that ends after the user approved, decisions made while the stream is open, and call
/// ids that come back. Each case once let a call run, or ask the model again, when it should not have.
/// </summary>
public class ToolApprovalEdgeTests
{
    private static FunctionCallContent Call(string callId, string path) =>
        new(callId, "delete_file", new Dictionary<string, object?> { ["path"] = path });

    private static (AIFunction Tool, List<string> Deleted) DeleteTool()
    {
        var deleted = new List<string>();
        var tool = new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string path) => { deleted.Add(path); return "deleted"; }, "delete_file"));
        return (tool, deleted);
    }

    [Fact(DisplayName = "A call approved in a turn that ended before it ran does not run with the next prompt")]
    public async Task ApprovedThenFailed_DoesNotRunLater()
    {
        var (tool, deleted) = DeleteTool();
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Delete my notes");
        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("ficc_c1", Call("c1", "notes.txt"))]));
        var entry = turn.PendingApprovals[0];
        turn.Approve(entry);
        turn.Fail(new InvalidOperationException("The connection dropped."));

        // The turn failed before the result arrived, which is what Failed means for any running call.
        Assert.Equal(ToolCallStatus.Failed, entry.Status);
        var model = new ScriptModel(_ => [new ChatResponseUpdate(ChatRole.Assistant, "OK.")]);
        using var client = new FunctionInvokingChatClient(model);
        var runner = new ChatRunner(client, conversation) { Options = new ChatOptions { Tools = [tool] } };
        var result = await runner.SendAsync("Never mind");

        Assert.Empty(deleted);
        Assert.Equal(TurnOutcome.Completed, result!.Summary.Outcome);
    }

    [Fact(DisplayName = "A history saved after an approval but before the result loads with the call closed, and it does not run later")]
    public async Task ApprovedWithoutResult_LoadsClosed()
    {
        var (tool, deleted) = DeleteTool();
        var request = new ToolApprovalRequestContent("ficc_c1", Call("c1", "notes.txt"));
        var saved = new List<ChatMessage>
        {
            new(ChatRole.User, "Delete my notes"),
            new(ChatRole.Assistant, [request]),
            new(ChatRole.User, [request.CreateResponse(true)]),
        };
        var contract = (JsonTypeInfo<List<ChatMessage>>)AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(List<ChatMessage>));
        var conversation = new ChatConversation();
        conversation.LoadHistory(JsonSerializer.Deserialize(JsonSerializer.Serialize(saved, contract), contract)!);

        Assert.Equal(ToolCallStatus.Canceled, conversation.Entries.OfType<ToolCallEntry>().Single().Status);
        Assert.Equal(ChatRole.Tool, conversation.History[^1].Role);

        var reloaded = new ChatConversation();
        reloaded.LoadHistory(JsonSerializer.Deserialize(JsonSerializer.Serialize(conversation.SaveHistory().ToList(), contract), contract)!);
        Assert.Equal(ToolCallStatus.Canceled, reloaded.Entries.OfType<ToolCallEntry>().Single().Status);
        Assert.Equal(conversation.History.Count, reloaded.History.Count);

        using var client = new FunctionInvokingChatClient(new ScriptModel(_ => [new ChatResponseUpdate(ChatRole.Assistant, "OK.")]));
        var runner = new ChatRunner(client, reloaded) { Options = new ChatOptions { Tools = [tool] } };
        await runner.SendAsync("Never mind");
        Assert.Empty(deleted);
    }

    [Fact(DisplayName = "Stopping in the same moment as approving ends the turn before the call runs")]
    public async Task ApproveThenStop_DoesNotRun()
    {
        var (tool, deleted) = DeleteTool();
        var model = new ScriptModel(_ => [new ChatResponseUpdate(ChatRole.Assistant, [Call("c1", "notes.txt")]) { ResponseId = "r1" }]);
        using var client = new FunctionInvokingChatClient(model);
        var runner = new ChatRunner(client) { Options = new ChatOptions { Tools = [tool] } };
        runner.Conversation.PropertyChanged += (_, _) =>
        {
            if (runner.Conversation.CurrentTurn is { } turn)
            {
                turn.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ChatTurn.IsAwaitingApproval) && turn.IsAwaitingApproval)
                    {
                        turn.Approve(turn.PendingApprovals[0]);
                        runner.Stop();
                    }
                };
            }
        };

        var result = await runner.SendAsync("Delete my notes");

        Assert.Equal(TurnOutcome.Stopped, result!.Summary.Outcome);
        Assert.Empty(deleted);
        await runner.SendAsync("Never mind");
        Assert.Empty(deleted);
    }

    [Fact(DisplayName = "Decisions made while the stream is open, before and after another request arrives, go to the model once and together")]
    public async Task DecideDuringStream_SendsEachDecisionOnce()
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("ficc_a", Call("a", "a.txt"))]));
        turn.Approve(turn.PendingApprovals[0]);
        Assert.True(turn.HasDecisionsToSend);
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("ficc_b", Call("b", "b.txt"))]));
        Assert.False(turn.HasDecisionsToSend);
        turn.Reject(turn.PendingApprovals[0]);

        var message = await turn.WaitForApprovalsAsync();

        Assert.Equal(["ficc_a", "ficc_b"], message.Contents.OfType<ToolApprovalResponseContent>().Select(response => response.RequestId));
        Assert.False(turn.HasDecisionsToSend);
        Assert.Throws<InvalidOperationException>(() => { _ = turn.WaitForApprovalsAsync(); });
    }

    [Fact(DisplayName = "With the real FunctionInvokingChatClient, a request decided during the stream does not make the runner ask the model again")]
    public async Task Runner_DecideDuringStream_AsksOnce()
    {
        var (tool, deleted) = DeleteTool();
        var model = new ScriptModel(messages => messages[^1].Role == ChatRole.Tool
            ? [new ChatResponseUpdate(ChatRole.Assistant, "Done.") { ResponseId = "r2" }]
            : [
                new ChatResponseUpdate(ChatRole.Assistant, [Call("a", "a.txt")]) { ResponseId = "r1" },
                new ChatResponseUpdate(ChatRole.Assistant, [Call("b", "b.txt")]) { ResponseId = "r1" },
            ]);
        using var client = new FunctionInvokingChatClient(model);
        var runner = new ChatRunner(client) { Options = new ChatOptions { Tools = [tool] } };
        runner.Conversation.PropertyChanged += (_, _) =>
        {
            if (runner.Conversation.CurrentTurn is { } turn)
            {
                turn.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ChatTurn.IsAwaitingApproval) && turn.IsAwaitingApproval)
                    {
                        turn.Approve(turn.PendingApprovals[0]);
                    }
                };
            }
        };

        var result = await runner.SendAsync("Delete two files");

        Assert.Equal(["a.txt", "b.txt"], deleted.Order());
        Assert.Equal(2, model.Requests.Count);
        Assert.Equal(TurnOutcome.Completed, result!.Summary.Outcome);
    }

    [Fact(DisplayName = "A request whose call id was seen before gets a card of its own, with the arguments it asks about")]
    public void Request_ReusedCallId_ShowsItsOwnArguments()
    {
        var conversation = new ChatConversation();
        var first = conversation.BeginTurn();
        first.Apply(new ChatResponseUpdate(ChatRole.Assistant, [Call("c1", "harmless.txt")]));
        first.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("c1", "read")]));
        first.Complete();

        var second = conversation.BeginTurn();
        second.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("ficc_c1", Call("c1", "secrets.txt"))]));

        var cards = conversation.Entries.OfType<ToolCallEntry>().ToList();
        Assert.Equal(2, cards.Count);
        Assert.Equal("harmless.txt", cards[0].Arguments!["path"]);
        Assert.Same(cards[1], second.PendingApprovals.Single());
        Assert.Equal("secrets.txt", cards[1].Arguments!["path"]);
        Assert.Equal(ToolCallStatus.Completed, cards[0].Status);
    }

    /// <summary>A model that answers each request with the updates a function picks from the messages.</summary>
    private sealed class ScriptModel(Func<List<ChatMessage>, ChatResponseUpdate[]> reply) : IChatClient
    {
        public List<List<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var list = messages.ToList();
            Requests.Add(list);
            foreach (var update in reply(list))
            {
                await Task.Yield();
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
