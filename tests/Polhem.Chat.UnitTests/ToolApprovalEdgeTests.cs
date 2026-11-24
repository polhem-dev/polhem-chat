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

    [Fact(DisplayName = "A request for a call id used before is rejected as it arrives, shows its own arguments and stays out of the history")]
    public void Request_ReusedCallId_IsRejected()
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
        Assert.Equal(ToolCallStatus.Completed, cards[0].Status);
        Assert.Equal("secrets.txt", cards[1].Arguments!["path"]);
        Assert.Equal(ToolCallStatus.Rejected, cards[1].Status);
        Assert.False(second.IsAwaitingApproval);
        Assert.False(second.HasDecisionsToSend);

        second.Apply(new ChatResponseUpdate(ChatRole.Assistant, "I cannot do that."));
        second.Complete();
        Assert.DoesNotContain(conversation.History.SelectMany(m => m.Contents), c => c is ToolApprovalRequestContent);
    }

    [Fact(DisplayName = "The same request sent twice in a stream shows one card and is answered once")]
    public async Task Request_SentTwice_AnsweredOnce()
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();
        var request = new ToolApprovalRequestContent("ficc_c1", Call("c1", "notes.txt"));
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [request]));
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("ficc_c1", Call("c1", "notes.txt"))]));

        Assert.Single(conversation.Entries.OfType<ToolCallEntry>());
        var waiting = turn.WaitForApprovalsAsync();
        turn.Approve(turn.PendingApprovals.Single());
        Assert.Single((await waiting).Contents);
        Assert.Single(conversation.Entries.OfType<ToolCallEntry>());
    }

    [Fact(DisplayName = "A call stopped while it runs is closed with a result only, so the history holds its call once")]
    public async Task StopWhileRunning_ClosesWithResultOnly()
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("ficc_c1", Call("c1", "notes.txt"))]));
        var waiting = turn.WaitForApprovalsAsync();
        turn.Approve(turn.PendingApprovals[0]);
        await waiting;
        // The client sends the approved call again before it runs it; the turn stops while it runs.
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [Call("c1", "notes.txt")]) { ResponseId = "ficc" });
        turn.Stop();

        var contents = conversation.History.SelectMany(m => m.Contents).ToList();
        Assert.Single(contents.OfType<FunctionCallContent>());
        Assert.Single(contents.OfType<FunctionResultContent>());
        Assert.Equal(ChatRole.Tool, conversation.History[^1].Role);
        Assert.Contains(conversation.History[^2].Contents, c => c is FunctionCallContent);
    }

    [Fact(DisplayName = "Two waiters at once get the same decisions")]
    public async Task TwoWaiters_GetTheSameDecisions()
    {
        var turn = new ChatConversation().BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("ficc_c1", Call("c1", "notes.txt"))]));
        var first = turn.WaitForApprovalsAsync();
        var second = turn.WaitForApprovalsAsync();

        turn.Approve(turn.PendingApprovals[0]);

        Assert.Same(await first, await second);
        Assert.Single((await first).Contents);
    }

    [Fact(DisplayName = "A wait that was canceled leaves the decisions for the next wait")]
    public async Task CanceledWait_KeepsTheDecisions()
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("ficc_c1", Call("c1", "notes.txt"))]));
        using var cancel = new CancellationTokenSource();
        var first = turn.WaitForApprovalsAsync(cancel.Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        turn.Approve(turn.PendingApprovals[0]);
        await Task.Yield();

        Assert.True(turn.HasDecisionsToSend);
        var message = await turn.WaitForApprovalsAsync();
        Assert.True(Assert.IsType<ToolApprovalResponseContent>(Assert.Single(message.Contents)).Approved);
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
