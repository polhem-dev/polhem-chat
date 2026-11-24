using System.Globalization;
using Avalonia.Controls;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class ChatViewStringsTests(HeadlessSession session)
{
    public static TheoryData<string> Cultures => ["en-US", "zh-TW"];

    [Theory(DisplayName = "Traditional Chinese cultures get the Chinese texts and other cultures the English ones")]
    [InlineData("zh-TW", "送出")]
    [InlineData("zh-HK", "送出")]
    [InlineData("zh-Hant", "送出")]
    [InlineData("en-US", "Send")]
    [InlineData("fr-FR", "Send")]
    [InlineData("ja-JP", "Send")]
    public void ForCulture_PicksLanguage(string culture, string send) =>
        Assert.Equal(send, ChatViewStrings.ForCulture(CultureInfo.GetCultureInfo(culture)).Send);

    [Theory(DisplayName = "Every text has a value in each shipped language")]
    [MemberData(nameof(Cultures))]
    public void ForCulture_EveryText_HasValue(string culture)
    {
        var strings = ChatViewStrings.ForCulture(CultureInfo.GetCultureInfo(culture));

        var empty = typeof(ChatViewStrings).GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .Where(property => string.IsNullOrWhiteSpace((string?)property.GetValue(strings)))
            .Select(property => property.Name);

        Assert.Empty(empty);
    }

    [Fact(DisplayName = "The texts come from the culture asked for, whatever the culture of the thread")]
    public void ForCulture_IgnoresThreadCulture()
    {
        var saved = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal("停止", ChatViewStrings.ForCulture(CultureInfo.GetCultureInfo("zh-TW")).Stop);
        }
        finally
        {
            CultureInfo.CurrentUICulture = saved;
        }
    }

    [Fact(DisplayName = "The turn summary formats numbers and times with the culture of the texts")]
    public void DescribeTurn_FormatsWithCulture()
    {
        var summary = new TurnSummaryEntry(TimeSpan.FromSeconds(2.25), new() { InputTokenCount = 1500, OutputTokenCount = 42 }, TurnOutcome.Failed);

        Assert.Equal("2.3 s · 1,500 in · 42 out · Failed", EntryConverters.DescribeTurn(summary, new ChatViewStrings()));
        Assert.Equal("2.3 秒 · 輸入 1,500 · 輸出 42 · 失敗",
            EntryConverters.DescribeTurn(summary, ChatViewStrings.ForCulture(CultureInfo.GetCultureInfo("zh-TW"))));
    }

    [Fact(DisplayName = "A view takes the UI culture of the thread that creates it")]
    public Task View_DefaultStrings_FollowCreatingCulture() => session.RunAsync(() =>
    {
        var saved = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-TW");
            var view = new ChatView();
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Show(view);

            Assert.Equal("送出", Part<Button>(view, "PART_SendButton").Content);
        }
        finally
        {
            CultureInfo.CurrentUICulture = saved;
        }
    });

    [Fact(DisplayName = "An application replaces a text by assigning its own strings, and null restores the defaults")]
    public Task View_HostStrings_ReplaceDefaults() => session.RunAsync(() =>
    {
        var view = new ChatView { Strings = new ChatViewStrings { Send = "Ask", PromptPlaceholder = "Your question" } };
        var window = Show(view);

        Assert.Equal("Ask", Part<Button>(view, "PART_SendButton").Content);
        Assert.Equal("Your question", Part<ChatPromptBox>(view, "PART_PromptBox").PlaceholderText);

        view.Strings = null!;
        Settle(window);
        Assert.Equal(ChatViewStrings.ForCulture(CultureInfo.CurrentUICulture).Send, Part<Button>(view, "PART_SendButton").Content);
        Assert.NotNull(view.Strings);
    });
}
