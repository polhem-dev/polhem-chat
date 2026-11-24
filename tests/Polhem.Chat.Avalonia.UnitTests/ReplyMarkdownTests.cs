using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Polhem.Markdown;
using Polhem.Markdown.Avalonia;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class ReplyMarkdownTests(HeadlessSession session)
{
    // NOTE: The loader replaces the viewer's own before any option allows an image, so no test reaches the network.
    private const string Reply = """
        Look: ![markdown pen](https://example.com/pen.png)

        <img src="https://example.com/ink.png" alt="html ink">
        """;

    [Fact(DisplayName = "By default a reply loads no image, written in Markdown or in HTML, and shows the alternative text")]
    public Task Images_Default_ShowAlternativeTextOnly() => session.RunAsync(() =>
    {
        var (_, view, viewer, requested) = ShowReply();

        Assert.Null(view.MarkdownOptions);
        Assert.Empty(Assert.IsType<MarkdownOptions>(viewer.Options).AllowedImageSchemes);
        Assert.Empty(requested);
        var text = ShownText(viewer);
        Assert.Contains("markdown pen", text, StringComparison.Ordinal);
        Assert.Contains("html ink", text, StringComparison.Ordinal);
    });

    [Fact(DisplayName = "Options the application sets load the images they allow, from Markdown and from HTML")]
    public Task Images_OptionsAllowHttps_LoadImages() => session.RunAsync(async () =>
    {
        var (window, view, viewer, requested) = ShowReply();

        view.MarkdownOptions = new MarkdownOptions();
        Settle(window);
        await WaitForAsync(() => requested.Count >= 2);

        Assert.Same(view.MarkdownOptions, viewer.Options);
        Assert.Equal(
            ["https://example.com/ink.png", "https://example.com/pen.png"],
            requested.Select(uri => uri.AbsoluteUri).Order(StringComparer.Ordinal));
    });

    [Fact(DisplayName = "Setting the options back to null stops loading images again")]
    public Task Images_OptionsCleared_LoadNoImages() => session.RunAsync(() =>
    {
        var (window, view, viewer, _) = ShowReply();
        view.MarkdownOptions = new MarkdownOptions();
        Settle(window);

        view.MarkdownOptions = null;
        Settle(window);

        Assert.Empty(Assert.IsType<MarkdownOptions>(viewer.Options).AllowedImageSchemes);
    });

    [Fact(DisplayName = "The view's CJK font family reaches the reply's viewer")]
    public Task CjkFontFamily_Set_ReachesViewer() => session.RunAsync(() =>
    {
        var (window, view, viewer, _) = ShowReply();
        Assert.Null(viewer.CjkFontFamily);

        var family = new FontFamily("PingFang TC, Noto Sans CJK TC");
        view.CjkFontFamily = family;
        Settle(window);

        Assert.Equal(family, viewer.CjkFontFamily);
    });

    /// <summary>
    /// Shows a reply whose viewer records the images it is asked to load instead of fetching them.
    /// </summary>
    private static (Window Window, ChatView View, MarkdownViewer Viewer, ConcurrentQueue<Uri> Requested) ShowReply()
    {
        var conversation = new ChatConversation();
        conversation.Entries.Add(new AssistantReplyEntry(Reply));
        var view = new ChatView { Conversation = conversation };
        var window = Show(view);
        var viewer = Assert.Single(Descendants<MarkdownViewer>(EntryView(view, 0)));
        var requested = new ConcurrentQueue<Uri>();
        viewer.ImageLoader = (uri, _) =>
        {
            requested.Enqueue(uri);
            return Task.FromResult<Stream?>(null);
        };
        Settle(window);
        return (window, view, viewer, requested);
    }

    /// <summary>The text the viewer shows: that of its text blocks and of their inlines.</summary>
    private static string ShownText(MarkdownViewer viewer) => string.Join(
        "\n",
        Descendants<TextBlock>(viewer).Select(block => block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
            : block.Text));

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }
}
