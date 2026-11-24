using Avalonia.Controls;
using Microsoft.Extensions.AI;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>
/// By default the conversation shows what is written for the user; the reasoning and the tool calls show only as the line
/// under it while a turn runs.
/// </summary>
[Collection(HeadlessCollection.Name)]
public class ActivityLineTests(HeadlessSession session)
{
    private static TextBlock Waiting(ChatView view) => Part<TextBlock>(view, "PART_Waiting");

    [Fact(DisplayName = "By default the reasoning and the tool calls take no place in the conversation; ShowsActivity brings them back")]
    public Task Default_HidesActivityEntries() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        conversation.Entries.Add(new UserPromptEntry("Hi"));
        conversation.Entries.Add(new ReasoningEntry("Let me think."));
        conversation.Entries.Add(new ToolCallEntry("call-1", "lookup"));
        conversation.Entries.Add(new AssistantReplyEntry("Hello"));
        var view = new ChatView { Conversation = conversation };
        var window = Show(view);
        var entries = Part<ItemsControl>(view, "PART_Entries");
        bool[] Shown() => [.. Enumerable.Range(0, 4).Select(index => entries.ContainerFromIndex(index)!.IsVisible)];

        Assert.Equal([true, false, false, true], Shown());

        conversation.Entries.Add(new ToolCallEntry("call-2", "lookup"));
        Settle(window);
        Assert.False(entries.ContainerFromIndex(4)!.IsVisible);

        view.ShowsActivity = true;
        Settle(window);
        Assert.Equal([true, true, true, true], Shown());
    });

    [Fact(DisplayName = "The line says the model is thinking, then names the running tool, and gives way to a reply that is being written")]
    public Task Default_ActivityFollowsTheTurn() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var view = new ChatView { Conversation = conversation, Strings = new ChatViewStrings() };
        var window = Show(view);
        string? Line()
        {
            Settle(window);
            return IsShown(Waiting(view)) ? Waiting(view).Text : null;
        }

        conversation.AddUserPrompt("Hi");
        view.IsGenerating = true;
        var turn = conversation.BeginTurn();
        Assert.Equal("Thinking…", Line());

        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent("Let me look.")]));
        Assert.Equal("Thinking…", Line());

        conversation.DescribeToolCall = _ => "Looking up the weather";
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "lookup")]));
        Assert.Equal("Looking up the weather…", Line());

        turn.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("call-1", "sunny")]));
        Assert.Equal("Thinking…", Line());

        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "It is sunny."));
        Assert.Null(Line());

        turn.Complete();
        view.IsGenerating = false;
        Assert.Null(Line());
    });

    [Fact(DisplayName = "The animated icon plays and the toggle for the details shows while a reply is in progress")]
    public Task Spinner_ShowsWhileGenerating() => session.RunAsync(() =>
    {
        var view = new ChatView { Conversation = new ChatConversation() };
        var window = Show(view);
        var spinner = Part<Border>(view, "PART_Spinner");
        Assert.False(IsShown(spinner));
        Assert.False(view.ActivityIconPlayer.IsPlaying);

        view.IsGenerating = true;
        Settle(window);
        Assert.True(IsShown(spinner));
        Assert.Same(view.ActivityIconPlayer, spinner.Child);
        Assert.True(view.ActivityIconPlayer.IsPlaying);
        view.ActivityIconPlayer.Advance();
        Assert.Equal(1, view.ActivityIconPlayer.CurrentFrame);
        var toggle = Part<global::Avalonia.Controls.Primitives.ToggleButton>(view, "PART_ActivityToggle");
        Assert.True(IsShown(toggle));
        toggle.IsChecked = true;
        Assert.True(view.ShowsActivity);

        view.IsGenerating = false;
        Settle(window);
        Assert.False(IsShown(spinner));
        Assert.False(view.ActivityIconPlayer.IsPlaying);
        // Open, the toggle stays, so the details can be closed again.
        Assert.True(IsShown(toggle));
        toggle.IsChecked = false;
        Settle(window);
        Assert.False(IsShown(toggle));
        Assert.False(view.ShowsActivity);
        // Open, the toggle stays, so the details can be closed again.
        Assert.True(IsShown(toggle));
        toggle.IsChecked = false;
        Settle(window);
        Assert.False(IsShown(toggle));
        Assert.False(view.ShowsActivity);
    });

    [Fact(DisplayName = "The progress line adds the output tokens the turn has reported so far")]
    public Task Progress_AddsOutputTokens() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var view = new ChatView { Conversation = conversation, Strings = new ChatViewStrings() };
        var window = Show(view);
        view.IsGenerating = true;
        var turn = conversation.BeginTurn();
        turn.BeginCall();

        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = 900, OutputTokenCount = 1234 })]));
        view.UpdateProgress();
        Settle(window);

        Assert.Equal("0s · call 1 · ↓ 1,234 tokens", view.ProgressText);
    });
}
