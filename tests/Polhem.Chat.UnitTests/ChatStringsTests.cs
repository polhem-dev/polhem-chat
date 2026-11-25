using System.Globalization;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.UnitTests;

/// <summary>The texts both views show, and the lines formatted from them.</summary>
public class ChatStringsTests
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
        Assert.Equal(send, ChatStrings.ForCulture(CultureInfo.GetCultureInfo(culture)).Send);

    [Theory(DisplayName = "Every text has a value in each shipped language")]
    [MemberData(nameof(Cultures))]
    public void ForCulture_EveryText_HasValue(string culture)
    {
        var strings = ChatStrings.ForCulture(CultureInfo.GetCultureInfo(culture));

        var empty = typeof(ChatStrings).GetProperties()
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

            Assert.Equal("停止", ChatStrings.ForCulture(CultureInfo.GetCultureInfo("zh-TW")).Stop);
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

        Assert.Equal("2.3 s · 1,500 in · 42 out · Failed", new ChatStrings().DescribeTurn(summary));
        Assert.Equal("2.3 秒 · 輸入 1,500 · 輸出 42 · 失敗",
            ChatStrings.ForCulture(CultureInfo.GetCultureInfo("zh-TW")).DescribeTurn(summary));
    }

    [Fact(DisplayName = "The summary names the calls when there were several, and the cached input when there was some")]
    public void DescribeTurn_CallsAndCachedTokens()
    {
        var strings = new ChatStrings();
        var usage = new UsageDetails { InputTokenCount = 54758, CachedInputTokenCount = 47224, OutputTokenCount = 5350 };

        var several = strings.DescribeTurn(new TurnSummaryEntry(TimeSpan.FromSeconds(45), usage, TurnOutcome.Completed, 8));
        var single = strings.DescribeTurn(
            new TurnSummaryEntry(TimeSpan.FromSeconds(2), new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 }, TurnOutcome.Completed, 1));

        Assert.Equal("45.0 s · 8 calls · 54,758 in (47,224 cached) · 5,350 out", several);
        Assert.Equal("2.0 s · 10 in · 5 out", single);
    }

    [Fact(DisplayName = "The Traditional Chinese texts format the calls, the cached input and the progress")]
    public void Strings_ZhHant_FormatTheNewTexts()
    {
        var strings = ChatStrings.ForCulture(CultureInfo.GetCultureInfo("zh-TW"));

        Assert.Equal("8 次呼叫", strings.FormatCalls(8));
        Assert.Equal("輸入 1,000（快取 800）· 輸出 20", strings.FormatCachedTokens(1000, 800, 20));
        Assert.Equal("12 秒 · 第 2 次呼叫", strings.FormatProgress(2, TimeSpan.FromSeconds(12.7), 0));
        Assert.Equal("2 分 32 秒 · 第 6 次呼叫 · ↓ 5,573 tokens", strings.FormatProgress(6, TimeSpan.FromSeconds(152), 5573));
    }
}
