using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>The places an application fills with its own content: the header, and above and below the prompt box.</summary>
[Collection(HeadlessCollection.Name)]
public class SlotTests(HeadlessSession session)
{
    [Fact(DisplayName = "Each slot takes no room while it is empty, and shows its content once it has some")]
    public Task Slots_ShowOnlyWithContent() => session.RunAsync(() =>
    {
        var view = new ChatView { Conversation = new ChatConversation() };
        var window = Show(view);
        string[] names = ["PART_Header", "PART_PromptHeader", "PART_PromptFooter"];
        Assert.All(names, name => Assert.False(Part<ContentPresenter>(view, name).IsVisible));

        view.HeaderContent = new TextBlock { Text = "Header" };
        view.PromptHeaderContent = new TextBlock { Text = "Above" };
        view.PromptFooterContent = new TextBlock { Text = "Below" };
        Settle(window);

        Assert.All(names, name => Assert.True(IsShown(Part<ContentPresenter>(view, name))));
        var header = Part<ContentPresenter>(view, "PART_Header");
        var box = Part<ChatPromptBox>(view, "PART_PromptBox");
        var above = Part<ContentPresenter>(view, "PART_PromptHeader");
        var below = Part<ContentPresenter>(view, "PART_PromptFooter");
        var scroller = Part<ScrollViewer>(view, "PART_ScrollViewer");
        double Top(Control control) => control.TranslatePoint(default, view)!.Value.Y;
        Assert.True(Top(header) < Top(scroller));
        Assert.True(Top(above) < Top(box));
        Assert.True(Top(below) > Top(box));

        view.PromptFooterContent = null;
        Settle(window);
        Assert.False(below.IsVisible);
    });

    [Fact(DisplayName = "A slot shows plain data through its template")]
    public Task Slot_UsesItsTemplate() => session.RunAsync(() =>
    {
        var view = new ChatView
        {
            Conversation = new ChatConversation(),
            PromptFooterContent = "Answers can be wrong.",
            PromptFooterContentTemplate = new FuncDataTemplate<string>((text, _) => new TextBlock { Text = "Note: " + text }),
        };
        Show(view);

        var block = Part<ContentPresenter>(view, "PART_PromptFooter").GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.Equal("Note: Answers can be wrong.", block.Text);
    });

    [Fact(DisplayName = "The header stays in place while the conversation scrolls")]
    public Task Header_DoesNotScroll() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        for (var index = 0; index < 40; index++)
        {
            conversation.Entries.Add(new AssistantReplyEntry("Reply " + index));
        }
        var view = new ChatView { Conversation = conversation, HeaderContent = new TextBlock { Text = "Header" } };
        var window = Show(view, height: 400);
        var header = Part<ContentPresenter>(view, "PART_Header");
        var top = header.TranslatePoint(default, view)!.Value.Y;

        Part<ScrollViewer>(view, "PART_ScrollViewer").Offset = default;
        Settle(window);

        Assert.Equal(top, header.TranslatePoint(default, view)!.Value.Y);
        Assert.True(IsShown(header));
        Assert.True(Part<ScrollViewer>(view, "PART_ScrollViewer").Extent.Height > 400);
    });
}
