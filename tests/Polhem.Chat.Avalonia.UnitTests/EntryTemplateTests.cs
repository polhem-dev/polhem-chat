using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input.Platform;
using Microsoft.Extensions.AI;
using Polhem.Chat.UnitTests;
using Polhem.Markdown.Avalonia;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class EntryTemplateTests(HeadlessSession session)
{
    [Fact(DisplayName = "Each entry type of the model gets its default template")]
    public Task Templates_EveryEntryType_GetsItsDefault() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        conversation.Entries.Add(new UserPromptEntry("Hi"));
        conversation.Entries.Add(new ReasoningEntry("Let me think."));
        conversation.Entries.Add(new ToolCallEntry("call-1", "lookup"));
        conversation.Entries.Add(new AssistantReplyEntry("**Hello**"));
        conversation.Entries.Add(new TurnSummaryEntry(TimeSpan.FromSeconds(1), usage: null, TurnOutcome.Completed));
        conversation.Entries.Add(new ErrorEntry("Boom"));
        var view = new ChatView { Conversation = conversation };
        Show(view);

        Assert.Contains("polhemChatPrompt", EntryView(view, 0).Classes);
        Assert.Contains("polhemChatReasoning", EntryView(view, 1).Classes);
        Assert.Contains("polhemChatToolCall", EntryView(view, 2).Classes);
        Assert.Contains("polhemChatReply", EntryView(view, 3).Classes);
        Assert.Equal("**Hello**", Assert.Single(Descendants<MarkdownViewer>(EntryView(view, 3))).Markdown);
        Assert.Contains("polhemChatSummary", EntryView(view, 4).Classes);
        Assert.Contains("polhemChatError", EntryView(view, 5).Classes);
    });

    [Fact(DisplayName = "An application's own entry type is shown with the template it registers on the view")]
    public Task Templates_CustomEntryOnView_UsesHostTemplate() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        conversation.Entries.Add(new NoteEntry("Pick a color"));
        var view = new ChatView { Conversation = conversation };
        view.DataTemplates.Add(new FuncDataTemplate<NoteEntry>((note, _) => new TextBlock { Text = note.Text }));
        Show(view);

        Assert.Equal("Pick a color", Assert.IsType<TextBlock>(EntryView(view, 0)).Text);
    });

    [Fact(DisplayName = "A template the application registers further up the tree is found too")]
    public Task Templates_CustomEntryOnApplication_UsesHostTemplate() => session.RunAsync(() =>
    {
        Application.Current!.DataTemplates.Add(new FuncDataTemplate<NoteEntry>((note, _) => new TextBlock { Text = note.Text }));
        var conversation = new ChatConversation();
        conversation.Entries.Add(new NoteEntry("From the application"));
        var view = new ChatView { Conversation = conversation };
        Show(view);

        Assert.Equal("From the application", Assert.IsType<TextBlock>(EntryView(view, 0)).Text);
    });

    [Fact(DisplayName = "A template the application registers for a built-in entry type replaces the default")]
    public Task Templates_BuiltInTypeOverridden_UsesHostTemplate() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        conversation.Entries.Add(new AssistantReplyEntry("plain"));
        var view = new ChatView { Conversation = conversation };
        view.DataTemplates.Add(new FuncDataTemplate<AssistantReplyEntry>((reply, _) => new TextBlock { Text = "custom " + reply.Text }));
        Show(view);

        Assert.Equal("custom plain", Assert.IsType<TextBlock>(EntryView(view, 0)).Text);
    });

    [Fact(DisplayName = "A streamed reply grows in place, shows no copy button until it completes, and ends with a summary")]
    public Task Streaming_Reply_UpdatesInPlace() => session.RunAsync(async () =>
    {
        var start = new ScriptedChatClient.Pause();
        var middle = new ScriptedChatClient.Pause();
        var client = new ScriptedChatClient(start, ScriptedChatClient.Text("Hel"), middle, ScriptedChatClient.Text("lo"));
        var view = new ChatView { Runner = new ChatRunner(client), Prompt = "Hi" };
        var window = Show(view);

        var sending = view.SubmitAsync(null);
        await start.Reached.Task;
        Settle(window);
        Assert.True(view.IsGenerating);
        Assert.True(IsShown(Part<Button>(view, "PART_StopButton")));
        Assert.False(IsShown(Part<Button>(view, "PART_SendButton")));

        start.Release.SetResult();
        await middle.Reached.Task;
        Settle(window);
        var reply = EntryView(view, 1);
        var viewer = Assert.Single(Descendants<MarkdownViewer>(reply));
        Assert.Equal("Hel", viewer.Markdown);
        Assert.False(IsShown(CopyButton(reply)));

        middle.Release.SetResult();
        await sending;
        Settle(window);
        Assert.Same(reply, EntryView(view, 1));
        Assert.Equal("Hello", viewer.Markdown);
        Assert.True(IsShown(CopyButton(reply)));
        Assert.True(CopyButton(reply).IsEffectivelyEnabled);
        Assert.Contains("polhemChatSummary", EntryView(view, 2).Classes);
        Assert.False(view.IsGenerating);
    });

    [Fact(DisplayName = "Before the first text arrives, the reply shows that the model is thinking")]
    public Task Streaming_NoTextYet_ShowsThinking() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var reply = new AssistantReplyEntry(isStreaming: true);
        conversation.Entries.Add(reply);
        var view = new ChatView { Conversation = conversation };
        var window = Show(view);

        var thinking = Descendants<TextBlock>(EntryView(view, 0)).Single(text => text.Classes.Contains("polhemChatThinking"));
        Assert.True(IsShown(thinking));
        Assert.Equal(view.Strings.Thinking, thinking.Text);

        reply.AppendText("Now");
        Settle(window);
        Assert.False(IsShown(thinking));
    });

    [Fact(DisplayName = "A reasoning block reads Thinking while it streams and Reasoning once it is done")]
    public Task Reasoning_Header_FollowsStreaming() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var reasoning = new ReasoningEntry("Hmm", isStreaming: true);
        conversation.Entries.Add(reasoning);
        var view = new ChatView { Conversation = conversation };
        var window = Show(view);

        string ShownHeader() => Descendants<TextBlock>(EntryView(view, 0)).Single(text => IsShown(text) && text.Text is not "Hmm").Text!;
        Assert.Equal(view.Strings.Thinking, ShownHeader());

        reasoning.IsStreaming = false;
        Settle(window);
        Assert.Equal(view.Strings.Reasoning, ShownHeader());
    });

    [Fact(DisplayName = "A tool call shows its title and state, then its arguments and result")]
    public Task ToolCall_StatusAndResult_AreShown() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var call = new ToolCallEntry("call-1", "lookup")
        {
            Title = "Looking up the weather",
            Arguments = new Dictionary<string, object?> { ["city"] = "Taipei" },
        };
        conversation.Entries.Add(call);
        var view = new ChatView { Conversation = conversation };
        var window = Show(view);
        var card = EntryView(view, 0);
        Assert.Single(Descendants<Expander>(card)).IsExpanded = true;
        Settle(window);

        IEnumerable<string?> Texts() => Descendants<TextBlock>(card).Where(IsShown).Select(text => text.Text);
        Assert.Contains("Looking up the weather", Texts());
        Assert.Contains(view.Strings.ToolRunning, Texts());
        Assert.Contains("city: Taipei", Texts());
        Assert.DoesNotContain(view.Strings.ToolResult, Texts());

        call.Status = ToolCallStatus.Completed;
        call.Result = "Sunny";
        Settle(window);
        Assert.Contains(view.Strings.ToolCompleted, Texts());
        Assert.Contains("Sunny", Texts());
    });

    [Fact(DisplayName = "The turn summary shows the time, the tokens and how the turn ended")]
    public Task TurnSummary_ShowsTimeTokensAndOutcome() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var usage = new UsageDetails { InputTokenCount = 1200, OutputTokenCount = 30 };
        conversation.Entries.Add(new TurnSummaryEntry(TimeSpan.FromSeconds(1.5), usage, TurnOutcome.Stopped));
        var view = new ChatView { Conversation = conversation, Strings = new ChatViewStrings() };
        Show(view);

        Assert.Equal("1.5 s · 1,200 in · 30 out · Stopped", Assert.IsType<TextBlock>(EntryView(view, 0)).Text);
    });

    [Fact(DisplayName = "Copying a finished reply puts its Markdown source on the clipboard")]
    public Task Copy_FinishedReply_CopiesMarkdown() => session.RunAsync(async () =>
    {
        var conversation = new ChatConversation();
        var reply = new AssistantReplyEntry("# Title\n\n*text*");
        conversation.Entries.Add(reply);
        var view = new ChatView { Conversation = conversation };
        var window = Show(view);

        Assert.True(view.CopyCommand.CanExecute(reply));
        await view.CopyAsync(reply);

        Assert.Equal("# Title\n\n*text*", await window.Clipboard!.TryGetTextAsync());
    });

    [Fact(DisplayName = "A reply that is still streaming cannot be copied")]
    public Task Copy_StreamingReply_IsDisabled() => session.RunAsync(() =>
    {
        var view = new ChatView();

        Assert.False(view.CopyCommand.CanExecute(new AssistantReplyEntry("partial", isStreaming: true)));
        Assert.False(view.CopyCommand.CanExecute(null));
    });

    [Fact(DisplayName = "A reply's copy button becomes usable when the reply stops streaming, before the turn ends")]
    public Task Copy_ReplyThatStopsStreaming_EnablesItsButton() => session.RunAsync(() =>
    {
        // The host drives the turn and keeps generating, so nothing but the reply itself tells the button.
        var conversation = new ChatConversation();
        var view = new ChatView { Conversation = conversation, IsGenerating = true };
        var window = Show(view);
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "Looking it up."));
        Settle(window);
        var button = CopyButton(EntryView(view, 1));
        Assert.False(button.IsEffectivelyEnabled);

        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "lookup")]));
        Settle(window);

        Assert.True(IsShown(button));
        Assert.True(button.IsEffectivelyEnabled);
    });

    [Fact(DisplayName = "Prompts, finished reasoning and plain strings can be copied too")]
    public Task Copy_OtherEntries_AreEnabled() => session.RunAsync(() =>
    {
        var view = new ChatView();

        Assert.True(view.CopyCommand.CanExecute(new UserPromptEntry("question")));
        Assert.True(view.CopyCommand.CanExecute(new ReasoningEntry("because")));
        Assert.False(view.CopyCommand.CanExecute(new ReasoningEntry("so far", isStreaming: true)));
        Assert.True(view.CopyCommand.CanExecute("text"));
        Assert.False(view.CopyCommand.CanExecute(new ErrorEntry("failure")));
    });

    [Fact(DisplayName = "A view taken out of its window stops listening to the conversation, and picks it up again when shown")]
    public Task Detached_View_ResyncsWhenShownAgain() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var view = new ChatView { Conversation = conversation };
        var window = Show(view);
        Assert.Contains(":empty", view.Classes);

        window.Content = null;
        Settle(window);
        conversation.AddUserPrompt("While away");
        Assert.Contains(":empty", view.Classes);

        window.Content = view;
        Settle(window);
        Assert.DoesNotContain(":empty", view.Classes);
        Assert.Contains("polhemChatPrompt", EntryView(view, 0).Classes);
    });

    [Fact(DisplayName = "An empty conversation shows the empty state and its suggestions, which submit when clicked")]
    public Task EmptyState_Suggestion_Submits() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var view = new ChatView { Conversation = conversation, Suggestions = ["Summarize this", "Translate this"] };
        string? submitted = null;
        view.PromptSubmitted += (_, e) => submitted = e.Text;
        var window = Show(view);
        var emptyState = Part<StackPanel>(view, "PART_EmptyState");
        Assert.True(IsShown(emptyState));

        var suggestion = Descendants<Button>(emptyState).Single(button => Equals(button.Content, "Translate this"));
        suggestion.Command!.Execute(suggestion.CommandParameter);
        Assert.Equal("Translate this", submitted);

        conversation.AddUserPrompt("Translate this");
        Settle(window);
        Assert.False(IsShown(emptyState));
    });

    private static Button CopyButton(Control reply) =>
        Descendants<Button>(reply).Single(button => button.Classes.Contains("polhemChatCopy"));

    private sealed class NoteEntry(string text) : ChatEntry
    {
        public string Text { get; } = text;
    }
}
