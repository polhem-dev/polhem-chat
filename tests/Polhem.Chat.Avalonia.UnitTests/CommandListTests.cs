using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>The host's commands the user opens with a slash at the start of the prompt box.</summary>
[Collection(HeadlessCollection.Name)]
public class CommandListTests(HeadlessSession session)
{
    private static readonly PromptCommand s_shorter = new("shorter", "Make this shorter: ") { Description = "Cut it down" };
    private static readonly PromptCommand s_translate = new("translate", "Translate to English.") { SendsImmediately = true };

    [Fact(DisplayName = "A slash opens the list of commands, and the arrows move its selection")]
    public Task Slash_OpensList_ArrowsMove() => session.RunAsync(() =>
    {
        var (window, view, _) = ShowHosted();
        FocusPrompt(view);
        var list = Part<ListBox>(view, "PART_CommandList");
        Assert.False(IsShown(list));

        window.KeyTextInput("/");
        Settle(window);
        Assert.True(view.IsCommandListOpen);
        Assert.True(IsShown(list));
        Assert.Equal([s_shorter, s_translate], list.Items.Cast<PromptCommand>());
        Assert.Same(s_shorter, list.SelectedItem);

        Press(window, Key.Down);
        Assert.Same(s_translate, list.SelectedItem);
        Assert.Equal("/translate  (2 of 2)", Part<TextBlock>(view, "PART_CommandAnnouncement").Text);
        Press(window, Key.Up);
        Assert.Same(s_shorter, list.SelectedItem);
        Assert.Equal("/", view.Prompt);
    });

    [Fact(DisplayName = "Enter puts the selected command in the box with the caret after it, and submits nothing")]
    public Task Enter_FillsBox() => session.RunAsync(() =>
    {
        var (window, view, submitted) = ShowHosted();
        var box = FocusPrompt(view);
        window.KeyTextInput("/sh");

        Press(window, Key.Enter);

        Assert.Empty(submitted);
        Assert.Equal("Make this shorter: ", view.Prompt);
        Assert.Equal(box.Text!.Length, box.CaretIndex);
        Assert.False(IsShown(Part<ListBox>(view, "PART_CommandList")));

        window.KeyTextInput("this text");
        Press(window, Key.Enter);
        Assert.Equal(["Make this shorter: this text"], submitted);
    });

    [Fact(DisplayName = "Enter on a command that sends at once submits its text")]
    public Task Enter_SendsImmediately() => session.RunAsync(() =>
    {
        var (window, view, submitted) = ShowHosted();
        FocusPrompt(view);
        window.KeyTextInput("/tr");

        Press(window, Key.Enter);

        Assert.Equal(["Translate to English."], submitted);
        Assert.Equal(string.Empty, view.Prompt);
    });

    [Fact(DisplayName = "Esc closes the list, before it would stop a reply, and Enter then sends what was typed")]
    public Task Esc_ClosesList() => session.RunAsync(() =>
    {
        var (window, view, submitted) = ShowHosted();
        var stops = 0;
        view.StopRequested += (_, _) => stops++;
        FocusPrompt(view);
        window.KeyTextInput("/sh");

        Press(window, Key.Escape);

        Assert.False(view.IsCommandListOpen);
        Assert.Equal(0, stops);
        Press(window, Key.Enter);
        Assert.Equal(["/sh"], submitted);
    });

    [Fact(DisplayName = "Clicking a command chooses it and leaves the focus in the box")]
    public Task Click_ChoosesCommand() => session.RunAsync(() =>
    {
        var (window, view, _) = ShowHosted();
        var box = FocusPrompt(view);
        window.KeyTextInput("/");
        Settle(window);
        var item = Assert.IsType<ListBoxItem>(Part<ListBox>(view, "PART_CommandList").ContainerFromIndex(0));
        var center = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Settle(window);

        Assert.Equal("Make this shorter: ", view.Prompt);
        Assert.True(box.IsFocused);
    });

    [Fact(DisplayName = "While an input method composes, the arrows and Enter go to it and leave the list alone")]
    public Task Composing_LeavesList() => session.RunAsync(() =>
    {
        var (window, view, submitted) = ShowHosted();
        var box = FocusPrompt(view);
        window.KeyTextInput("/");
        var inputMethod = InputMethodClient(box);
        inputMethod.SetPreeditText("ㄋㄧˇ");
        Settle(window);

        Press(window, Key.Down);
        Press(window, Key.Enter);
        Press(window, Key.Escape);

        Assert.True(view.IsCommandListOpen);
        Assert.Same(s_shorter, Part<ListBox>(view, "PART_CommandList").SelectedItem);
        Assert.Equal("/", view.Prompt);
        Assert.Empty(submitted);
    });

    [Fact(DisplayName = "Without commands, a slash is plain text that Enter sends")]
    public Task NoCommands_SlashIsText() => session.RunAsync(() =>
    {
        var view = new ChatView { Conversation = new ChatConversation() };
        var submitted = new List<string>();
        view.PromptSubmitted += (_, e) => submitted.Add(e.Text);
        var window = Show(view);
        FocusPrompt(view);
        window.KeyTextInput("/shorter");

        Press(window, Key.Down);
        Press(window, Key.Enter);

        Assert.False(IsShown(Part<ListBox>(view, "PART_CommandList")));
        Assert.Equal(["/shorter"], submitted);
    });

    private static (Window Window, ChatView View, List<string> Submitted) ShowHosted()
    {
        var view = new ChatView { Conversation = new ChatConversation(), Commands = [s_shorter, s_translate] };
        var submitted = new List<string>();
        view.PromptSubmitted += (_, e) => submitted.Add(e.Text);
        return (Show(view), view, submitted);
    }

    private static TextInputMethodClient InputMethodClient(ChatPromptBox box)
    {
        var query = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        box.RaiseEvent(query);
        return query.Client!;
    }
}
