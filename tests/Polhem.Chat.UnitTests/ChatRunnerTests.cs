using Microsoft.Extensions.AI;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.UnitTests;

public class ChatRunnerTests
{
    private static (ChatRunner Runner, ManualTimeProvider Time) CreateRunner(ScriptedChatClient client)
    {
        var time = new ManualTimeProvider();
        var conversation = new ChatConversation { TimeProvider = time };
        return (new ChatRunner(client, conversation), time);
    }

    [Fact(DisplayName = "Streamed text becomes one reply that grows with each update and ends with a summary")]
    public async Task SendAsync_StreamedText_BuildsOneReply()
    {
        var (runner, _) = CreateRunner(new ScriptedChatClient(Text("Hello"), Text(", "), Text("world")));

        var result = await runner.SendAsync("Hi");

        Assert.NotNull(result);
        Assert.Collection(runner.Conversation.Entries,
            entry => Assert.Equal("Hi", Assert.IsType<UserPromptEntry>(entry).Text),
            entry =>
            {
                var reply = Assert.IsType<AssistantReplyEntry>(entry);
                Assert.Equal("Hello, world", reply.Text);
                Assert.False(reply.IsStreaming);
                Assert.False(reply.IsStopped);
            },
            entry => Assert.Equal(TurnOutcome.Completed, Assert.IsType<TurnSummaryEntry>(entry).Outcome));
        Assert.Equal("Hello, world", result.Response.Text);
    }

    [Fact(DisplayName = "The reply is marked as streaming while updates arrive")]
    public async Task SendAsync_WhileStreaming_ReplyIsStreaming()
    {
        bool? streamingMidway = null;
        ChatRunner? runner = null;
        var client = new ScriptedChatClient(
            Text("partial"),
            new Run(() => streamingMidway = runner!.Conversation.Entries.OfType<AssistantReplyEntry>().Single().IsStreaming),
            Text(" text"));
        (runner, _) = CreateRunner(client);

        await runner.SendAsync("Hi");

        Assert.True(streamingMidway);
        Assert.False(runner.Conversation.Entries.OfType<AssistantReplyEntry>().Single().IsStreaming);
    }

    [Fact(DisplayName = "Reasoning and text become separate entries in the order they arrive")]
    public async Task SendAsync_ReasoningThenText_SeparateEntries()
    {
        var (runner, _) = CreateRunner(new ScriptedChatClient(
            Contents(new TextReasoningContent("Let me ")),
            Contents(new TextReasoningContent("think.")),
            Text("Answer.")));

        await runner.SendAsync("Question");

        var entries = runner.Conversation.Entries;
        Assert.Equal("Let me think.", Assert.IsType<ReasoningEntry>(entries[1]).Text);
        Assert.False(((ReasoningEntry)entries[1]).IsStreaming);
        Assert.Equal("Answer.", Assert.IsType<AssistantReplyEntry>(entries[2]).Text);
    }

    [Fact(DisplayName = "A tool call shows its arguments and then its result, and text after it starts a new reply")]
    public async Task SendAsync_ToolCallAndResult_CompletesToolCallEntry()
    {
        var arguments = new Dictionary<string, object?> { ["city"] = "Taipei" };
        ToolCallStatus? statusBeforeResult = null;
        ChatRunner? runner = null;
        var client = new ScriptedChatClient(
            Text("Checking."),
            Contents(new FunctionCallContent("call-1", "get_weather", arguments)),
            new Run(() => statusBeforeResult = runner!.Conversation.Entries.OfType<ToolCallEntry>().Single().Status),
            ToolContents(new FunctionResultContent("call-1", "Sunny")),
            Text("It is sunny."));
        (runner, _) = CreateRunner(client);

        await runner.SendAsync("Weather?");

        var entries = runner.Conversation.Entries;
        Assert.Equal(
            [typeof(UserPromptEntry), typeof(AssistantReplyEntry), typeof(ToolCallEntry), typeof(AssistantReplyEntry), typeof(TurnSummaryEntry)],
            entries.Select(entry => entry.GetType()));
        var call = (ToolCallEntry)entries[2];
        Assert.Equal(ToolCallStatus.Running, statusBeforeResult);
        Assert.Equal(ToolCallStatus.Completed, call.Status);
        Assert.Equal("get_weather", call.Name);
        Assert.Equal("get_weather", call.Title);
        Assert.Equal("Taipei", call.Arguments!["city"]);
        Assert.Equal("Sunny", call.Result);
        Assert.Equal("It is sunny.", ((AssistantReplyEntry)entries[3]).Text);
    }

    [Fact(DisplayName = "A tool that threw is shown as failed with the exception's message")]
    public async Task SendAsync_ToolResultWithException_FailsToolCallEntry()
    {
        var (runner, _) = CreateRunner(new ScriptedChatClient(
            Contents(new FunctionCallContent("call-1", "lookup", new Dictionary<string, object?>())),
            ToolContents(new FunctionResultContent("call-1", "Error") { Exception = new InvalidOperationException("Not found.") })));

        await runner.SendAsync("Look it up");

        var call = runner.Conversation.Entries.OfType<ToolCallEntry>().Single();
        Assert.Equal(ToolCallStatus.Failed, call.Status);
        Assert.Equal("Not found.", call.ErrorMessage);
    }

    [Fact(DisplayName = "The application's description replaces the tool name as the title")]
    public async Task SendAsync_DescribeToolCall_SetsTitle()
    {
        var (runner, _) = CreateRunner(new ScriptedChatClient(
            Contents(new FunctionCallContent("call-1", "get_weather", new Dictionary<string, object?> { ["city"] = "Taipei" }))));
        runner.Conversation.DescribeToolCall = call => $"Checking the weather in {call.Arguments!["city"]}";

        await runner.SendAsync("Weather?");

        Assert.Equal("Checking the weather in Taipei", runner.Conversation.Entries.OfType<ToolCallEntry>().Single().Title);
    }

    [Fact(DisplayName = "The summary sums the usage of every request in the turn and measures the time it took")]
    public async Task SendAsync_UsageAndTime_RecordedInSummary()
    {
        ManualTimeProvider? time = null;
        var client = new ScriptedChatClient(
            Contents(new UsageContent(new UsageDetails { InputTokenCount = 10, OutputTokenCount = 2, TotalTokenCount = 12 })),
            new Run(() => time!.Advance(TimeSpan.FromSeconds(3))),
            Text("Done."),
            Contents(new UsageContent(new UsageDetails { InputTokenCount = 20, OutputTokenCount = 5, TotalTokenCount = 25 })));
        (var runner, time) = CreateRunner(client);

        var result = await runner.SendAsync("Go");

        var summary = result!.Summary;
        Assert.Same(summary, runner.Conversation.Entries[^1]);
        Assert.Equal(TimeSpan.FromSeconds(3), summary.Elapsed);
        Assert.Equal(30, summary.Usage!.InputTokenCount);
        Assert.Equal(7, summary.Usage.OutputTokenCount);
        Assert.Equal(37, summary.Usage.TotalTokenCount);
    }

    [Fact(DisplayName = "A turn without usage reports none")]
    public async Task SendAsync_NoUsage_SummaryUsageIsNull()
    {
        var (runner, _) = CreateRunner(new ScriptedChatClient(Text("Done.")));

        var result = await runner.SendAsync("Go");

        Assert.Null(result!.Summary.Usage);
    }

    [Fact(DisplayName = "An error the model reports in its stream becomes an error entry")]
    public async Task SendAsync_ErrorContent_AddsErrorEntry()
    {
        var (runner, _) = CreateRunner(new ScriptedChatClient(
            Contents(new ErrorContent("Rate limited.") { ErrorCode = "429", Details = "Retry later." })));

        await runner.SendAsync("Go");

        var error = runner.Conversation.Entries.OfType<ErrorEntry>().Single();
        Assert.Equal("Rate limited.", error.Message);
        Assert.Equal("429", error.ErrorCode);
        Assert.Equal("Retry later.", error.Details);
    }

    [Fact(DisplayName = "Stopping keeps what arrived, marks the reply as stopped and keeps it in the history")]
    public async Task Stop_DuringStream_KeepsPartialReply()
    {
        var pause = new Pause();
        var (runner, _) = CreateRunner(new ScriptedChatClient(Text("Half an ans"), pause, Text("wer.")));

        var sending = runner.SendAsync("Go");
        await pause.Reached.Task;
        Assert.True(runner.IsRunning);
        runner.Stop();
        var result = await sending;

        Assert.Equal(TurnOutcome.Stopped, result!.Summary.Outcome);
        Assert.Null(result.Exception);
        var reply = runner.Conversation.Entries.OfType<AssistantReplyEntry>().Single();
        Assert.Equal("Half an ans", reply.Text);
        Assert.True(reply.IsStopped);
        Assert.False(reply.IsStreaming);
        Assert.False(runner.IsRunning);
        Assert.Null(runner.Conversation.CurrentTurn);
        Assert.Contains(runner.Conversation.History, message => message.Role == ChatRole.Assistant && message.Text == "Half an ans");
    }

    [Fact(DisplayName = "The caller's cancellation token stops the turn like Stop")]
    public async Task SendAsync_CallerCancels_Stops()
    {
        var pause = new Pause();
        var (runner, _) = CreateRunner(new ScriptedChatClient(Text("Partial"), pause));
        using var cancellation = new CancellationTokenSource();

        var sending = runner.SendAsync("Go", cancellation.Token);
        await pause.Reached.Task;
        await cancellation.CancelAsync();
        var result = await sending;

        Assert.Equal(TurnOutcome.Stopped, result!.Summary.Outcome);
    }

    [Fact(DisplayName = "Stopping while a tool runs marks the call as canceled")]
    public async Task Stop_WhileToolRuns_CancelsToolCall()
    {
        var pause = new Pause();
        var (runner, _) = CreateRunner(new ScriptedChatClient(
            Contents(new FunctionCallContent("call-1", "slow", new Dictionary<string, object?>())),
            pause));

        var sending = runner.SendAsync("Go");
        await pause.Reached.Task;
        runner.Stop();
        await sending;

        Assert.Equal(ToolCallStatus.Canceled, runner.Conversation.Entries.OfType<ToolCallEntry>().Single().Status);
    }

    [Fact(DisplayName = "An exception from the client ends the turn as failed and is shown with its message")]
    public async Task SendAsync_ClientThrows_FailsTurn()
    {
        var failure = new HttpRequestException("The service is unavailable.");
        var (runner, _) = CreateRunner(new ScriptedChatClient(
            Text("Start"),
            Contents(new FunctionCallContent("call-1", "lookup", new Dictionary<string, object?>())),
            new Throw(failure)));

        var result = await runner.SendAsync("Go");

        Assert.Equal(TurnOutcome.Failed, result!.Summary.Outcome);
        Assert.Same(failure, result.Exception);
        Assert.Equal("The service is unavailable.", runner.Conversation.Entries.OfType<ErrorEntry>().Single().Message);
        Assert.Equal(ToolCallStatus.Failed, runner.Conversation.Entries.OfType<ToolCallEntry>().Single().Status);
        Assert.False(runner.IsRunning);
        Assert.Null(runner.Conversation.CurrentTurn);
    }

    [Fact(DisplayName = "A cancellation the runner did not ask for, such as a timeout, fails the turn")]
    public async Task SendAsync_ClientTimesOut_FailsTurn()
    {
        var (runner, _) = CreateRunner(new ScriptedChatClient(new Throw(new TaskCanceledException("Timed out."))));

        var result = await runner.SendAsync("Go");

        Assert.Equal(TurnOutcome.Failed, result!.Summary.Outcome);
    }

    [Fact(DisplayName = "Canceling MessageSending sends nothing and adds nothing")]
    public async Task SendAsync_MessageSendingCanceled_SendsNothing()
    {
        var client = new ScriptedChatClient(Text("Never"));
        var (runner, _) = CreateRunner(client);
        runner.MessageSending += (_, e) => e.Cancel = true;

        var result = await runner.SendAsync("Go");

        Assert.Null(result);
        Assert.Empty(runner.Conversation.Entries);
        Assert.Empty(runner.Conversation.History);
        Assert.Empty(client.Requests);
    }

    [Fact(DisplayName = "MessageSending can rewrite the prompt before it is shown and sent")]
    public async Task SendAsync_MessageSendingRewrites_SendsRewrittenPrompt()
    {
        var client = new ScriptedChatClient(Text("OK"));
        var (runner, _) = CreateRunner(client);
        runner.MessageSending += (_, e) => e.Text = e.Text.ToUpperInvariant();

        await runner.SendAsync("shout");

        Assert.Equal("SHOUT", ((UserPromptEntry)runner.Conversation.Entries[0]).Text);
        Assert.Equal("SHOUT", client.Requests.Single().Single().Text);
    }

    [Fact(DisplayName = "ResponseReceived reports the result of every turn, including a failed one")]
    public async Task SendAsync_TurnEnds_RaisesResponseReceived()
    {
        var (runner, _) = CreateRunner(new ScriptedChatClient(new Throw(new InvalidOperationException("Broken."))));
        ChatTurnResult? received = null;
        runner.ResponseReceived += (_, e) => received = e.Result;

        var result = await runner.SendAsync("Go");

        Assert.Same(result, received);
    }

    [Fact(DisplayName = "IsRunning raises PropertyChanged when a turn starts and ends")]
    public async Task SendAsync_RaisesIsRunningChanges()
    {
        var (runner, _) = CreateRunner(new ScriptedChatClient(Text("OK")));
        var values = new List<bool>();
        runner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChatRunner.IsRunning))
            {
                values.Add(runner.IsRunning);
            }
        };

        await runner.SendAsync("Go");

        Assert.Equal([true, false], values);
    }

    [Fact(DisplayName = "Sending while a turn is in progress throws")]
    public async Task SendAsync_WhileRunning_Throws()
    {
        var pause = new Pause();
        var (runner, _) = CreateRunner(new ScriptedChatClient(pause));

        var sending = runner.SendAsync("First");
        await pause.Reached.Task;

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.SendAsync("Second"));
        runner.Stop();
        await sending;
    }

    [Fact(DisplayName = "The next request carries the history, without the summaries kept in it")]
    public async Task SendAsync_SecondTurn_SendsHistoryWithoutSummaries()
    {
        var client = new ScriptedChatClient(
            Text("Answer."),
            Contents(new UsageContent(new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 })));
        var (runner, _) = CreateRunner(client);

        await runner.SendAsync("First");
        await runner.SendAsync("Second");

        var request = client.Requests[1];
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.User], request.Select(message => message.Role));
        Assert.Equal(["First", "Answer.", "Second"], request.Select(message => message.Text));
        Assert.DoesNotContain(request.SelectMany(message => message.Contents), content => content is UsageContent);
    }

    [Fact(DisplayName = "The response handed to the caller does not carry the summary kept in the history")]
    public async Task SendAsync_Result_ResponseHasNoSummaryRecord()
    {
        var (runner, _) = CreateRunner(new ScriptedChatClient(Text("Answer.")));

        var result = await runner.SendAsync("Go");

        Assert.DoesNotContain(result!.Response.Messages.SelectMany(message => message.Contents), content => content is UsageContent);
    }

    [Fact(DisplayName = "Replacing the client sends the next turn to the new one")]
    public async Task Client_Replaced_NextTurnUsesNewClient()
    {
        var first = new ScriptedChatClient(Text("From the first."));
        var second = new ScriptedChatClient(Text("From the second."));
        var (runner, _) = CreateRunner(first);

        await runner.SendAsync("One");
        runner.Client = second;
        await runner.SendAsync("Two");

        Assert.Single(first.Requests);
        Assert.Single(second.Requests);
        Assert.Equal(3, second.Requests[0].Count);
    }

    [Fact(DisplayName = "The options are sent with every request")]
    public async Task SendAsync_Options_Passed()
    {
        ChatOptions? seen = null;
        var client = new OptionsRecorder(options => seen = options);
        var runner = new ChatRunner(client) { Options = new ChatOptions { ModelId = "model-x" } };

        await runner.SendAsync("Go");

        Assert.Equal("model-x", seen!.ModelId);
    }

    [Fact(DisplayName = "An empty prompt is refused")]
    public async Task SendAsync_EmptyPrompt_Throws()
    {
        var (runner, _) = CreateRunner(new ScriptedChatClient());

        await Assert.ThrowsAsync<ArgumentException>(() => runner.SendAsync("  "));
    }

    private sealed class OptionsRecorder(Action<ChatOptions?> record) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            record(options);
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "OK");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
