using Avalonia.Controls;
using Microsoft.Extensions.AI;
using Polhem.Chat.UnitTests;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class WaitingIndicatorTests(HeadlessSession session)
{
    private static TextBlock Waiting(ChatView view) => Part<TextBlock>(view, "PART_Waiting");

    [Fact(DisplayName = "With a runner, the view shows that the model is thinking until the first text arrives")]
    public Task Runner_BeforeFirstText_ShowsWaiting() => session.RunAsync(async () =>
    {
        var start = new ScriptedChatClient.Pause();
        var middle = new ScriptedChatClient.Pause();
        var client = new ScriptedChatClient(start, ScriptedChatClient.Text("Hel"), middle, ScriptedChatClient.Text("lo"));
        var view = new ChatView { Runner = new ChatRunner(client), Prompt = "Hi" };
        var window = Show(view);
        Assert.False(IsShown(Waiting(view)));

        var sending = view.SubmitAsync(null);
        await start.Reached.Task;
        Settle(window);
        Assert.True(IsShown(Waiting(view)));
        Assert.Equal(view.Strings.Thinking, Waiting(view).Text);

        start.Release.SetResult();
        await middle.Reached.Task;
        Settle(window);
        Assert.False(IsShown(Waiting(view)));

        middle.Release.SetResult();
        await sending;
        Settle(window);
        Assert.False(IsShown(Waiting(view)));
    });

    [Fact(DisplayName = "Driven by the application, the view shows the wait before reasoning arrives and between a tool's result and the next response")]
    public Task HostDriven_BetweenEntries_ShowsWaiting() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var view = new ChatView { Conversation = conversation };
        var window = Show(view);
        bool Shown()
        {
            Settle(window);
            return IsShown(Waiting(view));
        }

        conversation.AddUserPrompt("Hi");
        Assert.False(Shown());

        view.IsGenerating = true;
        var turn = conversation.BeginTurn();
        Assert.True(Shown());

        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent("Let me look.")]));
        Assert.False(Shown());

        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "lookup")]));
        Assert.False(Shown());

        turn.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("call-1", "found")]));
        Assert.True(Shown());

        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "Done."));
        Assert.False(Shown());

        turn.Complete();
        Assert.True(Shown());

        view.IsGenerating = false;
        Assert.False(Shown());
    });

    [Fact(DisplayName = "A conversation shown while a reply is in progress starts with the wait shown")]
    public Task HostDriven_ConversationSetWhileGenerating_ShowsWaiting() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var view = new ChatView { IsGenerating = true, Conversation = conversation };
        Show(view);

        Assert.True(IsShown(Waiting(view)));
    });
}
