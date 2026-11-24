using System.Net;
using System.Net.Sockets;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Polhem.Markdown;
using Polhem.Markdown.Avalonia;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>
/// How the default reply template renders a reply. The image tests count the connections a listener on the loopback
/// address receives, so they see a load however early it starts, without reaching beyond this machine.
/// </summary>
[Collection(HeadlessCollection.Name)]
public class ReplyMarkdownTests(HeadlessSession session)
{
    // NOTE: Long enough for the viewer's own loader to have connected, which it does at once on a background thread.
    private static readonly TimeSpan s_quietPeriod = TimeSpan.FromSeconds(1);

    [Fact(DisplayName = "A reply loaded from history loads no image, in Markdown or in HTML, and shows the alternative text")]
    public Task Images_ReplyFromHistory_MakeNoConnection() => session.RunAsync(async () =>
    {
        using var listener = new ConnectionCounter();
        var conversation = new ChatConversation();
        conversation.Entries.Add(new AssistantReplyEntry(Reply(listener)));
        var view = new ChatView { Conversation = conversation };
        Show(view);

        await Task.Delay(s_quietPeriod);

        Assert.Equal(0, listener.Count);
        var text = ShownText(Assert.Single(Descendants<MarkdownViewer>(EntryView(view, 0))));
        Assert.Contains("markdown pen", text, StringComparison.Ordinal);
        Assert.Contains("html ink", text, StringComparison.Ordinal);
    });

    [Fact(DisplayName = "A whole reply added while the view shows loads no image")]
    public Task Images_ReplyAddedWhileShown_MakeNoConnection() => session.RunAsync(async () =>
    {
        using var listener = new ConnectionCounter();
        var conversation = new ChatConversation();
        var window = Show(new ChatView { Conversation = conversation });

        conversation.Entries.Add(new AssistantReplyEntry(Reply(listener)));
        Settle(window);
        await Task.Delay(s_quietPeriod);

        Assert.Equal(0, listener.Count);
    });

    [Fact(DisplayName = "A streamed reply loads no image")]
    public Task Images_StreamedReply_MakeNoConnection() => session.RunAsync(async () =>
    {
        using var listener = new ConnectionCounter();
        var conversation = new ChatConversation();
        var window = Show(new ChatView { Conversation = conversation });
        var reply = new AssistantReplyEntry(string.Empty) { IsStreaming = true };
        conversation.Entries.Add(reply);
        Settle(window);

        reply.Text = Reply(listener);
        Settle(window);
        await Task.Delay(s_quietPeriod);

        Assert.Equal(0, listener.Count);
    });

    [Fact(DisplayName = "Options the application sets load the images they allow, and setting null stops it again")]
    public Task Images_OptionsAllowHttps_LoadImages() => session.RunAsync(async () =>
    {
        using var listener = new ConnectionCounter();
        var conversation = new ChatConversation();
        conversation.Entries.Add(new AssistantReplyEntry(Reply(listener)));
        var view = new ChatView { Conversation = conversation };
        var window = Show(view);
        var viewer = Assert.Single(Descendants<MarkdownViewer>(EntryView(view, 0)));

        view.MarkdownOptions = new MarkdownOptions();
        Settle(window);
        await listener.WaitForAsync(2);

        Assert.Same(view.MarkdownOptions, viewer.Options);
        Assert.Equal(2, listener.Count);

        view.MarkdownOptions = null;
        Settle(window);

        Assert.Empty(Assert.IsType<MarkdownOptions>(viewer.Options).AllowedImageSchemes);
    });

    [Fact(DisplayName = "A reply that keeps streaming while the view is out of the window loads no image")]
    public Task Images_ViewDetached_MakeNoConnection() => session.RunAsync(async () =>
    {
        using var listener = new ConnectionCounter();
        var conversation = new ChatConversation();
        var reply = new AssistantReplyEntry("hello") { IsStreaming = true };
        conversation.Entries.Add(reply);
        var view = new ChatView { Conversation = conversation, MarkdownOptions = new MarkdownOptions() };
        var window = Show(view);
        window.Content = null;
        Settle(window);

        view.MarkdownOptions = null;
        reply.Text = Reply(listener);
        await Task.Delay(s_quietPeriod);

        Assert.Equal(0, listener.Count);
    });

    [Fact(DisplayName = "Each reply has its own options that load no images, so changing one opens no other")]
    public Task Images_DefaultOptions_NotShared() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        conversation.Entries.Add(new AssistantReplyEntry("one"));
        conversation.Entries.Add(new AssistantReplyEntry("two"));
        var view = new ChatView { Conversation = conversation };
        Show(view);

        var first = Assert.Single(Descendants<MarkdownViewer>(EntryView(view, 0)));
        var second = Assert.Single(Descendants<MarkdownViewer>(EntryView(view, 1)));

        Assert.NotSame(first.Options, second.Options);
    });

    [Fact(DisplayName = "The view's CJK font family reaches the reply's viewer")]
    public Task CjkFontFamily_Set_ReachesViewer() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        conversation.Entries.Add(new AssistantReplyEntry("這是**京都**"));
        var view = new ChatView { Conversation = conversation };
        var window = Show(view);
        var viewer = Assert.Single(Descendants<MarkdownViewer>(EntryView(view, 0)));
        Assert.Null(viewer.CjkFontFamily);

        var family = new FontFamily("PingFang TC, Noto Sans CJK TC");
        view.CjkFontFamily = family;
        Settle(window);

        Assert.Equal(family, viewer.CjkFontFamily);
    });

    /// <summary>A reply with an image in Markdown and one in HTML, both from the listener.</summary>
    private static string Reply(ConnectionCounter listener) => $"""
        Look: ![markdown pen](https://127.0.0.1:{listener.Port}/pen.png)

        <img src="https://127.0.0.1:{listener.Port}/ink.png" alt="html ink">
        """;

    /// <summary>The text the viewer shows: that of its text blocks and of their inlines.</summary>
    private static string ShownText(MarkdownViewer viewer) => string.Join(
        "\n",
        Descendants<TextBlock>(viewer).Select(block => block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
            : block.Text));

    /// <summary>Accepts connections on a free loopback port and counts them.</summary>
    private sealed class ConnectionCounter : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private int _count;

        public ConnectionCounter()
        {
            _listener.Start();
            _ = AcceptAsync();
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public int Count => Volatile.Read(ref _count);

        public async Task WaitForAsync(int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (Count < count && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }

        public void Dispose() => _listener.Stop();

        private async Task AcceptAsync()
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    Interlocked.Increment(ref _count);
                }
            }
            catch (ObjectDisposedException)
            {
                // The listener stopped.
            }
            catch (SocketException)
            {
                // The listener stopped.
            }
        }
    }
}
