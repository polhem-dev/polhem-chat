using Microsoft.Extensions.AI;
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
        var view = new ChatView { Conversation = conversation, Strings = new ChatViewStrings() };
        var window = Show(view);
        conversation.AddUserPrompt("Hi");
        Assert.Empty(view.ProgressText);

        view.IsGenerating = true;
        var turn = conversation.BeginTurn();
        turn.BeginCall();
        Settle(window);
        Assert.StartsWith("Call 1 · ", view.ProgressText, StringComparison.Ordinal);

        turn.BeginCall();
        Settle(window);
        Assert.StartsWith("Call 2 · ", view.ProgressText, StringComparison.Ordinal);

        turn.Complete();
        view.IsGenerating = false;
        Settle(window);
        Assert.Empty(view.ProgressText);
    });

    [Fact(DisplayName = "A turn that does not count its requests shows only its seconds")]
    public Task HostDriven_NoBeginCall_ShowsSeconds() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var view = new ChatView { Conversation = conversation, Strings = new ChatViewStrings() };
        var window = Show(view);

        view.IsGenerating = true;
        conversation.BeginTurn();
        Settle(window);

        Assert.Equal(view.Strings.FormatProgress(0, TimeSpan.Zero), view.ProgressText);
    });

    [Fact(DisplayName = "With a runner, the progress line shows the first request while the reply streams")]
    public Task Runner_ShowsProgressWhileRunning() => session.RunAsync(async () =>
    {
        var pause = new ScriptedChatClient.Pause();
        var client = new ScriptedChatClient(pause, ScriptedChatClient.Text("Hello"));
        var view = new ChatView { Runner = new ChatRunner(client), Strings = new ChatViewStrings(), Prompt = "Hi" };
        var window = Show(view);

        var sending = view.SubmitAsync(null);
        await pause.Reached.Task;
        Settle(window);
        Assert.StartsWith("Call 1 · ", view.ProgressText, StringComparison.Ordinal);

        pause.Release.SetResult();
        await sending;
        Settle(window);
        Assert.Empty(view.ProgressText);
    });

    [Fact(DisplayName = "The summary names the calls when there were several, and the cached input when there was some")]
    public void DescribeTurn_CallsAndCachedTokens()
    {
        var strings = new ChatViewStrings();
        var usage = new UsageDetails { InputTokenCount = 54758, CachedInputTokenCount = 47224, OutputTokenCount = 5350 };

        var several = EntryConverters.DescribeTurn(new TurnSummaryEntry(TimeSpan.FromSeconds(45), usage, TurnOutcome.Completed, 8), strings);
        var single = EntryConverters.DescribeTurn(
            new TurnSummaryEntry(TimeSpan.FromSeconds(2), new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 }, TurnOutcome.Completed, 1),
            strings);

        Assert.Equal("45.0 s · 8 calls · 54,758 in (47,224 cached) · 5,350 out", several);
        Assert.Equal("2.0 s · 10 in · 5 out", single);
    }

    [Fact(DisplayName = "The Traditional Chinese texts format the calls, the cached input and the progress")]
    public void Strings_ZhHant_FormatTheNewTexts()
    {
        var strings = ChatViewStrings.ForCulture(System.Globalization.CultureInfo.GetCultureInfo("zh-TW"));

        Assert.Equal("8 次呼叫", strings.FormatCalls(8));
        Assert.Equal("輸入 1,000（快取 800）· 輸出 20", strings.FormatCachedTokens(1000, 800, 20));
        Assert.Equal("第 2 次呼叫 · 12 秒", strings.FormatProgress(2, TimeSpan.FromSeconds(12.7)));
    }
}
