using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Semi.Avalonia;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>The view works on the base themes it promises, Fluent and Semi, in light and dark.</summary>
[Collection(HeadlessCollection.Name)]
public class ThemeTests(HeadlessSession session)
{
    public static TheoryData<string, string> Themes => new()
    {
        { "Fluent", "Light" },
        { "Fluent", "Dark" },
        { "Semi", "Light" },
        { "Semi", "Dark" },
    };

    [Theory(DisplayName = "On each base theme and variant, the view is templated, its colors follow the variant, and it renders")]
    [MemberData(nameof(Themes))]
    public Task Theme_EachBaseAndVariant_Renders(string baseTheme, string variant) => session.RunAsync(() =>
    {
        UseTheme(baseTheme == "Semi" ? new SemiTheme() : new FluentTheme());
        var conversation = new ChatConversation();
        conversation.Entries.Add(new UserPromptEntry("Hi"));
        conversation.Entries.Add(new ReasoningEntry("Thinking it over"));
        conversation.Entries.Add(new ToolCallEntry("call-1", "lookup"));
        conversation.Entries.Add(new AssistantReplyEntry("Hello **there**"));
        conversation.Entries.Add(new ErrorEntry("Something failed"));
        var view = new ChatView { Conversation = conversation };
        var window = new Window
        {
            Width = 500,
            Height = 600,
            Content = view,
            RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        Settle(window);

        // The prompt box and the buttons take their templates from the base theme.
        Assert.NotEmpty(Descendants<TextPresenter>(Part<ChatPromptBox>(view, "PART_PromptBox")));
        Assert.NotEmpty(Descendants<ContentPresenter>(Part<Button>(view, "PART_SendButton")));

        // The chat colors follow the variant.
        var prompt = Assert.IsType<Border>(EntryView(view, 0));
        var expected = variant == "Dark" ? Color.Parse("#4D3B82F6") : Color.Parse("#263B82F6");
        Assert.Equal(expected, Assert.IsType<ISolidColorBrush>(prompt.Background, exactMatch: false).Color);

        Assert.NotNull(window.CaptureRenderedFrame());
    });
}
