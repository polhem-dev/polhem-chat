using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.UnitTests;

/// <summary>Tool calls that wait for the user's approval, in a turn the application runs and in a runner's turn.</summary>
public class ToolApprovalTests
{
    private static FunctionCallContent DeleteCall(string callId = "c1") =>
        new(callId, "delete_file", new Dictionary<string, object?> { ["path"] = "notes.txt" });

    private static ToolApprovalRequestContent Request(string callId = "c1") => new("r-" + callId, DeleteCall(callId));

    private static ChatResponseUpdate Update(params AIContent[] contents) => new(ChatRole.Assistant, [.. contents]);

    [Fact(DisplayName = "An approval request shows as a tool call that waits, and approving it marks it running")]
    public async Task Approve_HostDriven_ReturnsTheDecisionMessage()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Delete my notes");
        var turn = conversation.BeginTurn();
        var changes = new List<string?>();
        turn.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        turn.Apply(Update(new TextContent("Deleting."), Request()));
        var entry = Assert.Single(turn.PendingApprovals);
        Assert.Equal(ToolCallStatus.AwaitingApproval, entry.Status);
        Assert.Equal("delete_file", entry.Name);
        Assert.Equal("notes.txt", entry.Arguments!["path"]);
        Assert.NotNull(entry.ApprovalRequest);
        Assert.True(turn.IsAwaitingApproval);

        var waiting = turn.WaitForApprovalsAsync();
        Assert.False(waiting.IsCompleted);
        turn.Approve(entry);
        var message = await waiting;

        Assert.Equal(ToolCallStatus.Running, entry.Status);
        Assert.False(turn.IsAwaitingApproval);
        Assert.Equal([nameof(ChatTurn.IsAwaitingApproval), nameof(ChatTurn.IsAwaitingApproval)], changes);
        Assert.Equal(ChatRole.User, message.Role);
        var response = Assert.IsType<ToolApprovalResponseContent>(Assert.Single(message.Contents));
        Assert.True(response.Approved);
        Assert.Equal("r-c1", response.RequestId);

        turn.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("c1", "deleted")]));
        turn.Complete();
        Assert.Equal(ToolCallStatus.Completed, entry.Status);

        // The history keeps the reply before the request, the decisions, then what came after.
        var history = conversation.History;
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.User, ChatRole.Tool], history.Select(m => m.Role));
        Assert.Contains(history[1].Contents, content => content is ToolApprovalRequestContent);
        Assert.Contains(history[2].Contents, content => content is ToolApprovalResponseContent);
    }

    [Fact(DisplayName = "A rejected call stays rejected when the client answers it with a result of its own")]
    public async Task Reject_KeepsRejectedAfterTheResult()
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();
        turn.Apply(Update(Request()));
        var entry = turn.PendingApprovals[0];

        var waiting = turn.WaitForApprovalsAsync();
        turn.Reject(entry, "Not that file.");
        var response = Assert.IsType<ToolApprovalResponseContent>(Assert.Single((await waiting).Contents));
        turn.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("c1", "Tool call invocation rejected. Not that file.")]));
        turn.Complete();

        Assert.False(response.Approved);
        Assert.Equal("Not that file.", response.Reason);
        Assert.Equal(ToolCallStatus.Rejected, entry.Status);
        Assert.Equal("Not that file.", entry.RejectionReason);
    }

    [Fact(DisplayName = "The decisions are sent only once every pending call is decided")]
    public async Task Decide_SeveralCalls_WaitsForAll()
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();
        turn.Apply(Update(Request("c1"), Request("c2")));
        var waiting = turn.WaitForApprovalsAsync();

        turn.Approve(turn.PendingApprovals[0]);
        Assert.False(waiting.IsCompleted);
        Assert.Single(turn.PendingApprovals);
        turn.Reject(turn.PendingApprovals[0]);

        Assert.Equal([true, false], (await waiting).Contents.OfType<ToolApprovalResponseContent>().Select(r => r.Approved));
    }

    [Fact(DisplayName = "Ending a turn rejects the calls nobody decided, in the entries and in the history")]
    public void End_WithPendingCalls_RejectsThem()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Delete my notes");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(Request()));
        var entry = turn.PendingApprovals[0];

        turn.Stop();

        Assert.Equal(ToolCallStatus.Canceled, entry.Status);
        Assert.False(turn.IsAwaitingApproval);
        var answer = conversation.History.SelectMany(m => m.Contents).OfType<ToolApprovalResponseContent>().Single();
        Assert.False(answer.Approved);
        Assert.Equal(ChatRole.User, conversation.History[^1].Role);
    }

    [Fact(DisplayName = "Decisions made before anyone waits for them are returned by the next wait")]
    public async Task Wait_AfterDeciding_ReturnsTheDecisions()
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();
        turn.Apply(Update(Request()));
        turn.Reject(turn.PendingApprovals[0]);

        var message = await turn.WaitForApprovalsAsync();

        Assert.False(Assert.IsType<ToolApprovalResponseContent>(Assert.Single(message.Contents)).Approved);
        Assert.Throws<InvalidOperationException>(() => { _ = turn.WaitForApprovalsAsync(); });
    }

    [Fact(DisplayName = "Deciding a call that does not wait for approval throws, and so does waiting with nothing to decide")]
    public void Decide_NotPending_Throws()
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();

        Assert.Throws<ArgumentException>(() => turn.Approve(new ToolCallEntry("x", "tool")));
        Assert.Throws<InvalidOperationException>(() => { _ = turn.WaitForApprovalsAsync(); });
        turn.Apply(Update(Request()));
        var entry = turn.PendingApprovals[0];
        turn.Approve(entry);
        Assert.Throws<ArgumentException>(() => turn.Reject(entry));
    }

    [Fact(DisplayName = "The time spent waiting for approval is not part of the turn's time")]
    public void Elapsed_LeavesOutTheWait()
    {
        var time = new ManualTimeProvider();
        var conversation = new ChatConversation { TimeProvider = time };
        var turn = conversation.BeginTurn();
        time.Advance(TimeSpan.FromSeconds(2));
        turn.Apply(Update(Request()));
        time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(2), turn.Elapsed);

        turn.Approve(turn.PendingApprovals[0]);
        time.Advance(TimeSpan.FromSeconds(3));
        var result = turn.Complete();

        Assert.Equal(TimeSpan.FromSeconds(5), result.Summary.Elapsed);
    }

    [Fact(DisplayName = "A history with decided approvals loads back with the same states")]
    public void LoadHistory_RestoresTheDecisions()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Delete two files");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(Request("c1"), Request("c2")));
        turn.Approve(turn.PendingApprovals[0]);
        turn.Reject(turn.PendingApprovals[0], "Keep it.");
        turn.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("c1", "deleted"), new FunctionResultContent("c2", "rejected")]));
        turn.Complete();

        var loaded = new ChatConversation();
        loaded.LoadHistory(RoundTrip(conversation.SaveHistory()));

        var calls = loaded.Entries.OfType<ToolCallEntry>().ToList();
        Assert.Equal([ToolCallStatus.Completed, ToolCallStatus.Rejected], calls.Select(call => call.Status));
        Assert.Equal("Keep it.", calls[1].RejectionReason);
        Assert.DoesNotContain(loaded.Entries, entry => entry is UserPromptEntry { Text: "" });
        Assert.Single(loaded.Entries.OfType<UserPromptEntry>());
    }

    [Fact(DisplayName = "A history that ends with an unanswered request loads with the call rejected, so no later request runs it")]
    public void LoadHistory_UnansweredRequest_IsRejected()
    {
        var saved = new List<ChatMessage>
        {
            new(ChatRole.User, "Delete my notes"),
            new(ChatRole.Assistant, [Request()]),
        };

        var conversation = new ChatConversation();
        conversation.LoadHistory(RoundTrip(saved));

        var entry = conversation.Entries.OfType<ToolCallEntry>().Single();
        Assert.Equal(ToolCallStatus.Canceled, entry.Status);
        var answer = Assert.IsType<ToolApprovalResponseContent>(Assert.Single(conversation.History[^1].Contents));
        Assert.False(answer.Approved);
        Assert.Equal("r-c1", answer.RequestId);
    }

    [Fact(DisplayName = "A request for an MCP server's tool is titled with the server and the tool")]
    public void Apply_McpRequest_NamesServerAndTool()
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new ToolApprovalRequestContent("r1", new McpServerToolCallContent("m1", "search", "docs"))));

        Assert.Equal("docs/search", turn.PendingApprovals[0].Title);
    }

    [Fact(DisplayName = "With the real FunctionInvokingChatClient, an approved call runs once and its result reaches the model")]
    public async Task Runner_Approve_RunsTheTool()
    {
        var deleted = new List<string>();
        var tool = new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string path) => { deleted.Add(path); return "deleted"; }, "delete_file"));
        var model = new ToolModel();
        using var client = new FunctionInvokingChatClient(model);
        var runner = new ChatRunner(client) { Options = new ChatOptions { Tools = [tool] } };
        runner.Conversation.PropertyChanged += (_, _) => Decide(runner, approve: true);

        var result = await runner.SendAsync("Delete my notes");

        Assert.Equal(["notes.txt"], deleted);
        Assert.Equal(TurnOutcome.Completed, result!.Summary.Outcome);
        Assert.Equal(ToolCallStatus.Completed, runner.Conversation.Entries.OfType<ToolCallEntry>().Single().Status);
        Assert.Equal("Done.", runner.Conversation.Entries.OfType<AssistantReplyEntry>().Last().Text);
        Assert.Contains(model.Requests[^1].SelectMany(m => m.Contents), c => c is FunctionResultContent { Result: not null });
        Assert.Equal(2, result.Summary.Calls);
    }

    [Fact(DisplayName = "With the real FunctionInvokingChatClient, a rejected call never runs and stays rejected")]
    public async Task Runner_Reject_DoesNotRunTheTool()
    {
        var deleted = new List<string>();
        var tool = new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string path) => { deleted.Add(path); return "deleted"; }, "delete_file"));
        var model = new ToolModel();
        using var client = new FunctionInvokingChatClient(model);
        var runner = new ChatRunner(client) { Options = new ChatOptions { Tools = [tool] } };
        runner.Conversation.PropertyChanged += (_, _) => Decide(runner, approve: false);

        await runner.SendAsync("Delete my notes");

        Assert.Empty(deleted);
        // The rejection went to the model, which answered it.
        Assert.Equal(2, model.Requests.Count);
        Assert.Contains(model.Requests[^1].SelectMany(m => m.Contents), c => c is FunctionResultContent);
        Assert.Equal(ToolCallStatus.Rejected, runner.Conversation.Entries.OfType<ToolCallEntry>().Single().Status);
    }

    [Fact(DisplayName = "Stopping a runner while a call waits for approval rejects it, and it never runs")]
    public async Task Runner_StopWhileWaiting_RejectsTheCall()
    {
        var deleted = new List<string>();
        var tool = new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string path) => { deleted.Add(path); return "deleted"; }, "delete_file"));
        var model = new ToolModel();
        using var client = new FunctionInvokingChatClient(model);
        var runner = new ChatRunner(client) { Options = new ChatOptions { Tools = [tool] } };
        var sending = runner.SendAsync("Delete my notes");
        Assert.False(sending.IsCompleted);
        Assert.True(runner.IsRunning);

        runner.Stop();
        var result = await sending;

        Assert.Equal(TurnOutcome.Stopped, result!.Summary.Outcome);
        Assert.Equal(ToolCallStatus.Canceled, runner.Conversation.Entries.OfType<ToolCallEntry>().Single().Status);
        Assert.Single(model.Requests);

        // The next turn sends the rejection, so the client does not run the call then either.
        await runner.SendAsync("Never mind");
        Assert.Empty(deleted);
    }

    private static void Decide(ChatRunner runner, bool approve)
    {
        if (runner.Conversation.CurrentTurn is not { } turn)
        {
            return;
        }
        turn.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChatTurn.IsAwaitingApproval) && turn.IsAwaitingApproval)
            {
                foreach (var entry in turn.PendingApprovals.ToList())
                {
                    if (approve)
                    {
                        turn.Approve(entry);
                    }
                    else
                    {
                        turn.Reject(entry);
                    }
                }
            }
        };
    }

    private static List<ChatMessage> RoundTrip(IEnumerable<ChatMessage> messages)
    {
        var contract = (JsonTypeInfo<List<ChatMessage>>)AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(List<ChatMessage>));
        return JsonSerializer.Deserialize(JsonSerializer.Serialize(messages.ToList(), contract), contract)!;
    }

    /// <summary>
    /// A model that asks to delete a file, and answers "Done." once the history holds the result of the call.
    /// </summary>
    private sealed class ToolModel : IChatClient
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
            await Task.Yield();
            var responseId = "resp" + Requests.Count;
            if (list[^1].Role == ChatRole.Tool)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, "Done.") { ResponseId = responseId };
                yield break;
            }
            if (list[^1].Text == "Never mind")
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, "OK.") { ResponseId = responseId };
                yield break;
            }
            yield return new ChatResponseUpdate(ChatRole.Assistant, [DeleteCall()]) { ResponseId = responseId };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
