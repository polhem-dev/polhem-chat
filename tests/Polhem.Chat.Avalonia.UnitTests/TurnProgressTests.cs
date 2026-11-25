using Polhem.Chat.UnitTests;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>The line that shows a turn in progress, and the summary that ends it.</summary>
[Collection(HeadlessCollection.Name)]
public class TurnProgressTests(HeadlessSession session)
{
    [Fact(DisplayName = "Driven by the application, the progress line shows the request the turn is on and goes away with the turn")]
    public Task HostDriven_ProgressFollowsBeginCall() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var view = new ChatView { Conversation = conversation, Strings = new ChatStrings() };
        var window = Show(view);
        conversation.AddUserPrompt("Hi");
        Assert.Empty(view.ProgressText);

        view.IsGenerating = true;
        var turn = conversation.BeginTurn();
        turn.BeginCall();
        Settle(window);
        Assert.Contains(" · call 1", view.ProgressText, StringComparison.Ordinal);

        turn.BeginCall();
        Settle(window);
        Assert.Contains(" · call 2", view.ProgressText, StringComparison.Ordinal);

        turn.Complete();
        view.IsGenerating = false;
        Settle(window);
        Assert.Empty(view.ProgressText);
    });

    [Fact(DisplayName = "A turn that does not count its requests shows only its seconds")]
    public Task HostDriven_NoBeginCall_ShowsSeconds() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var view = new ChatView { Conversation = conversation, Strings = new ChatStrings() };
        var window = Show(view);

        view.IsGenerating = true;
        conversation.BeginTurn();
        Settle(window);

        Assert.Equal(view.Strings.FormatProgress(0, TimeSpan.Zero, 0), view.ProgressText);
    });

    [Fact(DisplayName = "With a runner, the progress line shows the first request while the reply streams")]
    public Task Runner_ShowsProgressWhileRunning() => session.RunAsync(async () =>
    {
        var pause = new ScriptedChatClient.Pause();
        var client = new ScriptedChatClient(pause, ScriptedChatClient.Text("Hello"));
        var view = new ChatView { Runner = new ChatRunner(client), Strings = new ChatStrings(), Prompt = "Hi" };
        var window = Show(view);

        var sending = view.SubmitAsync(null);
        await pause.Reached.Task;
        Settle(window);
        Assert.Contains(" · call 1", view.ProgressText, StringComparison.Ordinal);

        pause.Release.SetResult();
        await sending;
        Settle(window);
        Assert.Empty(view.ProgressText);
    });
}
