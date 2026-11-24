namespace ChatGallery;

/// <summary>The texts <see cref="DemoChatClient"/> plays, in English and in Chinese.</summary>
public sealed record DemoReplies(
    string City,
    string Reasoning,
    string ToolResult,
    string ToolError,
    string ConnectionLost,
    string Answer)
{
    public const string WeatherTool = "get_weather";

    private const string EnglishAnswer = """
        Here is the plan for **three days in Kyoto**, with the forecast the tool returned.

        ## Forecast

        | Day | Weather | High / low |
        |-----|---------|-----------|
        | Fri | Sunny | 18 / 9 °C |
        | Sat | Cloudy | 16 / 10 °C |
        | Sun | Light rain | 14 / 9 °C |

        ## Plan

        1. **Friday**: Fushimi Inari early, before the crowds, then Kiyomizu-dera.
        2. **Saturday**: Arashiyama bamboo grove and the river walk.
        3. **Sunday**: museums, since it rains. The *National Museum* is a good choice.

        > Bring a light umbrella for Sunday.

        Budget: $3 \times 12{,}000 = 36{,}000$ yen for the hotel, or per night
        $$\frac{36{,}000}{3} = 12{,}000$$

        ![Map of the three days](https://example.com/kyoto-map.png)

        The same plan as data, if you want to keep it:

        ```json
        { "city": "Kyoto", "days": ["Fushimi Inari", "Arashiyama", "Museums"] }
        ```

        Read more on [the city's site](https://kyoto.travel/en/).
        """;

    private const string ChineseAnswer = """
        這是**京都三日遊**的安排，天氣來自工具查到的預報。

        ## 天氣預報

        | 日期 | 天氣 | 高溫／低溫 |
        |------|------|-----------|
        | 週五 | 晴 | 18／9 °C |
        | 週六 | 多雲 | 16／10 °C |
        | 週日 | 小雨 | 14／9 °C |

        ## 行程

        1. **週五**：一早去伏見稻荷大社，避開人潮，再到清水寺。
        2. **週六**：嵐山竹林與河岸步道。
        3. **週日**：下雨，改逛博物館，*京都國立博物館*是不錯的選擇。

        > 週日記得帶把輕便的傘。

        **住宿預算**：$3 \times 12{,}000 = 36{,}000$ 日圓，每晚
        $$\frac{36{,}000}{3} = 12{,}000$$

        ![三日行程地圖](https://example.com/kyoto-map.png)

        同一份行程的資料格式，方便保存：

        ```json
        { "city": "京都", "days": ["伏見稻荷", "嵐山", "博物館"] }
        ```

        更多資訊見[京都觀光網站](https://kyoto.travel/tw/)。
        """;

    private static readonly DemoReplies s_english = new(
        City: "Kyoto",
        Reasoning: "The user wants a short trip plan. I should check the weather first, then put the outdoor sights on the "
            + "dry days and the museums on the rainy one.",
        ToolResult: "Fri: sunny, 18/9 °C. Sat: cloudy, 16/10 °C. Sun: light rain, 14/9 °C.",
        ToolError: "The weather service did not answer in time.",
        ConnectionLost: "The connection to the model was lost.",
        Answer: EnglishAnswer);

    private static readonly DemoReplies s_chinese = new(
        City: "京都",
        Reasoning: "使用者想要一份簡短的行程。先查天氣，再把戶外景點排在晴天，博物館排在下雨那天。",
        ToolResult: "週五：晴，18/9 °C。週六：多雲，16/10 °C。週日：小雨，14/9 °C。",
        ToolError: "天氣服務沒有及時回應。",
        ConnectionLost: "與模型的連線中斷了。",
        Answer: ChineseAnswer);

    /// <summary>Returns the replies in the language of the prompt.</summary>
    public static DemoReplies For(string prompt)
    {
        var replies = IsChinese(prompt) ? s_chinese : s_english;
        return AsksForLongReply(prompt)
            ? replies with { Answer = string.Join("\n\n---\n\n", Enumerable.Repeat(replies.Answer, 4)) }
            : replies;
    }

    /// <summary>Returns the question the host asks after a reply, in the language of the prompt.</summary>
    public static ChoiceEntry Choices(string prompt) => prompt.Any(IsCjk)
        ? new ChoiceEntry("接下來要做什麼？", ["給我長一點的回覆", "示範錯誤"])
        : new ChoiceEntry("What next?", ["Give me a long reply", "Show me an error"]);

    public static bool AsksForError(string prompt) =>
        prompt.Contains("error", StringComparison.OrdinalIgnoreCase) || prompt.Contains("錯誤", StringComparison.Ordinal);

    public static bool AsksForLongReply(string prompt) =>
        prompt.Contains("long", StringComparison.OrdinalIgnoreCase) || prompt.Contains("長", StringComparison.Ordinal);

    public static bool IsChinese(string prompt) => prompt.Any(IsCjk);

    private static bool IsCjk(char character) => character is >= '\u3400' and <= '\u9FFF';
}
