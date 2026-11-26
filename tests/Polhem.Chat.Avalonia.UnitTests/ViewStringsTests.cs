using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>The texts a view shows: the default for its culture, and an application's own.</summary>
[Collection(HeadlessCollection.Name)]
public class ViewStringsTests(HeadlessSession session)
{
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

            Assert.Equal("送出", AutomationProperties.GetName(Part<Button>(view, "PART_SendButton")));
        }
        finally
        {
            CultureInfo.CurrentUICulture = saved;
        }
    });

    [Fact(DisplayName = "An application replaces a text by assigning its own strings, and null restores the defaults")]
    public Task View_HostStrings_ReplaceDefaults() => session.RunAsync(() =>
    {
        var view = new ChatView { Strings = new ChatStrings { Send = "Ask", PromptPlaceholder = "Your question" } };
        var window = Show(view);

        Assert.Equal("Ask", AutomationProperties.GetName(Part<Button>(view, "PART_SendButton")));
        Assert.Equal("Your question", Part<ChatPromptBox>(view, "PART_PromptBox").PlaceholderText);

        view.Strings = null!;
        Settle(window);
        Assert.Equal(ChatStrings.ForCulture(CultureInfo.CurrentUICulture).Send, AutomationProperties.GetName(Part<Button>(view, "PART_SendButton")));
        Assert.NotNull(view.Strings);
    });
}
