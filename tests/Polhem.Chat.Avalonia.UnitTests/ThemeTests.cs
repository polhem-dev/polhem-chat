using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
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

    [Fact(DisplayName = "The view draws no frame by default, and draws the border and corners it is given around its content")]
    public Task Frame_SetOnView_DrawsAroundContent() => session.RunAsync(() =>
    {
        UseTheme(new FluentTheme());
        var plain = new ChatView();
        Show(plain);
        var plainFrame = Assert.IsType<Border>(plain.GetVisualChildren().Single());
        Assert.Equal(default, plainFrame.BorderThickness);

        var brush = new SolidColorBrush(Colors.Red);
        var view = new ChatView { BorderBrush = brush, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(8) };
        Show(view);
        var frame = Assert.IsType<Border>(view.GetVisualChildren().Single());
        Assert.Same(brush, frame.BorderBrush);
        Assert.Equal(new Thickness(2), frame.BorderThickness);
        Assert.Equal(new CornerRadius(8), frame.CornerRadius);
        Assert.True(frame.ClipToBounds);
        Assert.Contains(frame, Part<ScrollViewer>(view, "PART_ScrollViewer").GetVisualAncestors());
    });
}
