using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Polhem.Chat.UnitTests;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class PromptBoxTests(HeadlessSession session)
{
    [Fact(DisplayName = "Enter submits the prompt and empties the box")]
    public Task Enter_SubmitsPrompt() => session.RunAsync(() =>
    {
        var (window, view, submitted) = ShowHosted();
        FocusPrompt(view);
        window.KeyTextInput("hello");

        Press(window, Key.Enter);

        Assert.Equal(["hello"], submitted);
        Assert.Equal(string.Empty, view.Prompt);
    });

    [Fact(DisplayName = "Shift+Enter starts a new line and submits nothing")]
    public Task ShiftEnter_InsertsLineBreak() => session.RunAsync(() =>
    {
        var (window, view, submitted) = ShowHosted();
        var box = FocusPrompt(view);
        window.KeyTextInput("first");

        Press(window, Key.Enter, RawInputModifiers.Shift);
        window.KeyTextInput("second");
        Settle(window);

        Assert.Empty(submitted);
        Assert.Equal(["first", "second"], box.Text!.Split(["\r\n", "\n"], StringSplitOptions.None));
        Assert.Equal(box.Text, view.Prompt);
    });

    [Fact(DisplayName = "An Enter that confirms an input method's composition submits nothing")]
    public Task Enter_WhileComposing_DoesNotSubmit() => session.RunAsync(() =>
    {
        var (window, view, submitted) = ShowHosted();
        var box = FocusPrompt(view);
        var inputMethod = InputMethodClient(box);
        window.KeyTextInput("我");

        // What an input method does while the user types phonetic symbols: the text is shown but not committed. The box
        // already holds committed text, so only the composition check stands between this Enter and a submission.
        inputMethod.SetPreeditText("ㄋㄧˇ");
        Settle(window);
        Assert.True(box.IsComposing);
        Press(window, Key.Enter);
        Assert.Empty(submitted);
        Assert.Equal("我", view.Prompt);

        // The input method commits the chosen character and clears its composition; the next Enter submits.
        inputMethod.SetPreeditText(null);
        window.KeyTextInput("你");
        Settle(window);
        Assert.False(box.IsComposing);
        Press(window, Key.Enter);
        Assert.Equal(["我你"], submitted);
    });

    [Fact(DisplayName = "A key the input method processes, which is how Windows reports its Enter, submits nothing")]
    public Task ImeProcessedKey_DoesNotSubmit() => session.RunAsync(() =>
    {
        var (window, view, submitted) = ShowHosted();
        FocusPrompt(view);
        window.KeyTextInput("ni");

        Press(window, Key.ImeProcessed);

        Assert.Empty(submitted);
    });

    [Fact(DisplayName = "A blank prompt is not submitted")]
    public Task Enter_BlankPrompt_DoesNotSubmit() => session.RunAsync(() =>
    {
        var (window, view, submitted) = ShowHosted();
        FocusPrompt(view);
        window.KeyTextInput("   ");

        Press(window, Key.Enter);

        Assert.Empty(submitted);
    });

    [Fact(DisplayName = "A handler that cancels the submission keeps the prompt in the box")]
    public Task Submit_Canceled_KeepsPrompt() => session.RunAsync(() =>
    {
        var view = new ChatView { Conversation = new ChatConversation() };
        view.PromptSubmitted += (_, e) => e.Cancel = true;
        var window = Show(view);
        FocusPrompt(view);
        window.KeyTextInput("keep me");

        Press(window, Key.Enter);

        Assert.Equal("keep me", view.Prompt);
    });

    [Fact(DisplayName = "While the application generates a reply, Enter submits nothing and Esc asks it to stop")]
    public Task Hosted_Generating_EscRequestsStop() => session.RunAsync(() =>
    {
        var (window, view, submitted) = ShowHosted();
        var stops = 0;
        view.StopRequested += (_, _) => stops++;
        view.IsGenerating = true;
        Settle(window);
        FocusPrompt(view);
        window.KeyTextInput("next");

        Press(window, Key.Enter);
        Press(window, Key.Escape);

        Assert.Empty(submitted);
        Assert.Equal(1, stops);
        Assert.True(IsShown(Part<Button>(view, "PART_StopButton")));
        Assert.False(view.SubmitCommand.CanExecute(null));
    });

    [Fact(DisplayName = "Esc stops the runner's reply, which keeps what arrived and is marked as stopped")]
    public Task Runner_Esc_StopsAndMarksReply() => session.RunAsync(async () =>
    {
        var pause = new ScriptedChatClient.Pause();
        var runner = new ChatRunner(new ScriptedChatClient(ScriptedChatClient.Text("Partial"), pause, ScriptedChatClient.Text(" never")));
        var view = new ChatView { Runner = runner };
        var window = Show(view);
        FocusPrompt(view);
        window.KeyTextInput("Go");
        Press(window, Key.Enter);
        await pause.Reached.Task;

        Press(window, Key.Escape);
        while (runner.IsRunning)
        {
            await Task.Yield();
        }
        Settle(window);

        var reply = Assert.IsType<AssistantReplyEntry>(runner.Conversation.Entries[1]);
        Assert.Equal("Partial", reply.Text);
        Assert.True(reply.IsStopped);
        var stopped = Descendants<TextBlock>(EntryView(view, 1)).Single(text => text.Classes.Contains("polhemChatStopped"));
        Assert.True(IsShown(stopped));
        Assert.Equal(view.Strings.Stopped, stopped.Text);
        Assert.False(view.IsGenerating);
        Assert.True(IsShown(Part<Button>(view, "PART_SendButton")));
    });

    [Fact(DisplayName = "The stop button stops the runner too")]
    public Task Runner_StopCommand_Stops() => session.RunAsync(async () =>
    {
        var pause = new ScriptedChatClient.Pause();
        var runner = new ChatRunner(new ScriptedChatClient(pause));
        var view = new ChatView { Runner = runner, Prompt = "Go" };
        Show(view);
        var sending = view.SubmitAsync(null);
        await pause.Reached.Task;

        var stop = Part<Button>(view, "PART_StopButton");
        Assert.True(stop.Command!.CanExecute(null));
        stop.Command.Execute(null);
        await sending;

        Assert.Equal(TurnOutcome.Stopped, Assert.IsType<TurnSummaryEntry>(runner.Conversation.Entries[^1]).Outcome);
    });

    [Fact(DisplayName = "When the runner's MessageSending handler cancels, the prompt goes back into the box")]
    public Task Runner_MessageSendingCanceled_RestoresPrompt() => session.RunAsync(async () =>
    {
        var runner = new ChatRunner(new ScriptedChatClient());
        runner.MessageSending += (_, e) => e.Cancel = true;
        var view = new ChatView { Runner = runner, Prompt = "Try again" };
        Show(view);

        await view.SubmitAsync(null);

        Assert.Equal("Try again", view.Prompt);
        Assert.Empty(runner.Conversation.Entries);
    });

    private static (Window Window, ChatView View, List<string> Submitted) ShowHosted()
    {
        var view = new ChatView { Conversation = new ChatConversation() };
        var submitted = new List<string>();
        view.PromptSubmitted += (_, e) => submitted.Add(e.Text);
        return (Show(view), view, submitted);
    }

    /// <summary>
    /// Returns the client an input method talks to, the way the platform gets it when the box gains focus. The headless
    /// platform has no input method of its own.
    /// </summary>
    private static TextInputMethodClient InputMethodClient(ChatPromptBox box)
    {
        var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        box.RaiseEvent(request);
        return Assert.IsAssignableFrom<TextInputMethodClient>(request.Client);
    }
}
