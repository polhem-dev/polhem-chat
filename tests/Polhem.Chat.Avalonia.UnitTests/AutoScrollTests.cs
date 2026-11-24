using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class AutoScrollTests(HeadlessSession session)
{
    [Fact(DisplayName = "New entries and a growing reply keep the newest text in sight")]
    public Task Following_ContentGrows_StaysAtEnd() => session.RunAsync(() =>
    {
        var (window, view, conversation, scrollViewer) = ShowLongConversation();
        Assert.True(IsAtEnd(scrollViewer));

        var reply = new AssistantReplyEntry(isStreaming: true);
        conversation.Entries.Add(reply);
        for (var line = 0; line < 20; line++)
        {
            reply.AppendText($"Line {line}\n\n");
            Settle(window);
        }

        Assert.True(scrollViewer.Extent.Height > scrollViewer.Viewport.Height * 2);
        Assert.True(IsAtEnd(scrollViewer));
        Assert.True(view.FollowsLatest);
        Assert.False(IsShown(Part<Button>(view, "PART_ScrollToLatest")));
    });

    [Fact(DisplayName = "After the user scrolls up, a growing reply no longer moves the view, and Jump to latest appears")]
    public Task ScrolledUp_ContentGrows_KeepsPosition() => session.RunAsync(() =>
    {
        var (window, view, conversation, scrollViewer) = ShowLongConversation();
        var reply = new AssistantReplyEntry(isStreaming: true);
        conversation.Entries.Add(reply);
        Settle(window);

        window.MouseWheel(new Point(250, 100), new Vector(0, 10));
        Settle(window);
        var offset = scrollViewer.Offset.Y;
        Assert.False(IsAtEnd(scrollViewer));
        Assert.False(view.FollowsLatest);
        Assert.True(IsShown(Part<Button>(view, "PART_ScrollToLatest")));

        for (var line = 0; line < 10; line++)
        {
            reply.AppendText($"Line {line}\n\n");
            Settle(window);
        }

        Assert.Equal(offset, scrollViewer.Offset.Y);
        Assert.False(IsAtEnd(scrollViewer));
    });

    [Fact(DisplayName = "Scrolling up in the same layout pass as new text still leaves the end")]
    public Task ScrollUpWhileGrowing_SamePass_StopsFollowing() => session.RunAsync(() =>
    {
        var (window, view, conversation, scrollViewer) = ShowLongConversation();
        var reply = new AssistantReplyEntry(isStreaming: true);
        conversation.Entries.Add(reply);
        Settle(window);

        reply.AppendText("Line\n\nLine\n\nLine");
        window.MouseWheel(new Point(250, 100), new Vector(0, 10));
        Settle(window);

        Assert.False(view.FollowsLatest);
        Assert.False(IsAtEnd(scrollViewer));
    });

    [Fact(DisplayName = "Jump to latest scrolls to the end and follows new text again")]
    public Task JumpToLatest_FollowsAgain() => session.RunAsync(() =>
    {
        var (window, view, conversation, scrollViewer) = ShowLongConversation();
        scrollViewer.Offset = new Vector(0, 0);
        Settle(window);
        Assert.False(view.FollowsLatest);

        Part<Button>(view, "PART_ScrollToLatest").Command!.Execute(null);
        Settle(window);
        Assert.True(IsAtEnd(scrollViewer));
        Assert.True(view.FollowsLatest);
        Assert.False(IsShown(Part<Button>(view, "PART_ScrollToLatest")));

        conversation.Entries.Add(new AssistantReplyEntry("More\n\nand more\n\nand more"));
        Settle(window);
        Assert.True(IsAtEnd(scrollViewer));
    });

    [Fact(DisplayName = "Scrolling back to the end by hand follows new text again")]
    public Task ScrolledBackToEnd_FollowsAgain() => session.RunAsync(() =>
    {
        var (window, view, conversation, scrollViewer) = ShowLongConversation();
        scrollViewer.Offset = new Vector(0, 0);
        Settle(window);

        scrollViewer.ScrollToEnd();
        Settle(window);
        conversation.Entries.Add(new UserPromptEntry("Another"));
        Settle(window);

        Assert.True(view.FollowsLatest);
        Assert.True(IsAtEnd(scrollViewer));
    });

    [Fact(DisplayName = "Submitting a prompt while scrolled up returns to the end")]
    public Task ScrolledUp_Submit_ReturnsToEnd() => session.RunAsync(() =>
    {
        var (window, view, conversation, scrollViewer) = ShowLongConversation();
        view.PromptSubmitted += (_, e) => conversation.AddUserPrompt(e.Text);
        scrollViewer.Offset = new Vector(0, 0);
        Settle(window);

        view.SubmitCommand.Execute("Next question");
        Settle(window);

        Assert.True(view.FollowsLatest);
        Assert.True(IsAtEnd(scrollViewer));
    });

    private static (Window Window, ChatView View, ChatConversation Conversation, ScrollViewer ScrollViewer) ShowLongConversation()
    {
        var conversation = new ChatConversation();
        for (var index = 0; index < 30; index++)
        {
            conversation.Entries.Add(new UserPromptEntry($"Prompt {index}"));
        }
        var view = new ChatView { Conversation = conversation };
        var window = Show(view, height: 300);
        return (window, view, conversation, Part<ScrollViewer>(view, "PART_ScrollViewer"));
    }

    private static bool IsAtEnd(ScrollViewer scrollViewer) =>
        scrollViewer.Offset.Y + scrollViewer.Viewport.Height >= scrollViewer.Extent.Height - 1;
}
