using Avalonia.Controls;
using Avalonia.Input;
using Polhem.Chat.UnitTests;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>Regenerating the last reply and editing the last prompt in the view.</summary>
[Collection(HeadlessCollection.Name)]
public class RewindViewTests(HeadlessSession session)
{
    private static async Task<(ChatView View, ChatRunner Runner, Window Window)> TwoTurns()
    {
        var runner = new ChatRunner(new ScriptedChatClient(Text("Answer")));
        var view = new ChatView { Runner = runner };
        var window = Show(view);
        await view.SubmitAsync("First");
        await view.SubmitAsync("Second");
        Settle(window);
        return (view, runner, window);
    }

    private static Button ButtonIn(Control entry, string className) =>
        Descendants<Button>(entry).Single(button => button.Classes.Contains(className));

    [Fact(DisplayName = "With a runner, only the last reply offers to regenerate and only the last prompt offers to edit")]
    public Task Buttons_OnlyOnTheLast() => session.RunAsync(async () =>
    {
        var (view, runner, _) = await TwoTurns();
        var entries = runner.Conversation.Entries;
        int IndexOf<T>(int nth) where T : ChatEntry => entries.IndexOf(entries.OfType<T>().ElementAt(nth));

        Assert.False(ButtonIn(EntryView(view, IndexOf<AssistantReplyEntry>(0)), "polhemChatRegenerate").IsVisible);
        Assert.True(ButtonIn(EntryView(view, IndexOf<AssistantReplyEntry>(1)), "polhemChatRegenerate").IsVisible);
        Assert.False(ButtonIn(EntryView(view, IndexOf<UserPromptEntry>(0)), "polhemChatEdit").IsVisible);
        Assert.True(ButtonIn(EntryView(view, IndexOf<UserPromptEntry>(1)), "polhemChatEdit").IsVisible);
    });

    [Fact(DisplayName = "Regenerating replaces the last turn with a new one for the same prompt")]
    public Task Regenerate_ReplacesTheLastTurn() => session.RunAsync(async () =>
    {
        var (view, runner, window) = await TwoTurns();

        view.RegenerateCommand.Execute(null);
        for (var pass = 0; pass < 20 && runner.IsRunning; pass++)
        {
            await Task.Delay(10);
            Settle(window);
        }

        Assert.Equal(["First", "Second"], runner.Conversation.Entries.OfType<UserPromptEntry>().Select(p => p.Text));
        Assert.Equal(2, runner.Conversation.Entries.OfType<TurnSummaryEntry>().Count());
    });

    [Fact(DisplayName = "Editing puts the last prompt in the box with a note; canceling gives the box back what it held")]
    public Task Edit_Cancel_RestoresTheBox() => session.RunAsync(async () =>
    {
        var (view, runner, window) = await TwoTurns();
        view.Prompt = "half typed";
        var last = runner.Conversation.Entries.OfType<UserPromptEntry>().Last();

        view.EditLastPromptCommand.Execute(last);
        Settle(window);
        Assert.True(view.IsEditingPrompt);
        Assert.Equal("Second", view.Prompt);
        Assert.True(IsShown(Part<Border>(view, "PART_EditBanner")));

        Press(window, Key.Escape);
        Assert.False(view.IsEditingPrompt);
        Assert.Equal("half typed", view.Prompt);
        Assert.False(IsShown(Part<Border>(view, "PART_EditBanner")));
    });

    [Fact(DisplayName = "Submitting an edited prompt replaces the last one and ends the editing")]
    public Task Edit_Submit_ReplacesTheLastPrompt() => session.RunAsync(async () =>
    {
        var (view, runner, window) = await TwoTurns();
        view.EditLastPromptCommand.Execute(runner.Conversation.Entries.OfType<UserPromptEntry>().Last());
        view.Prompt = "Second, changed";

        await view.SubmitAsync(null);
        Settle(window);

        Assert.False(view.IsEditingPrompt);
        Assert.Equal(["First", "Second, changed"], runner.Conversation.Entries.OfType<UserPromptEntry>().Select(p => p.Text));
    });

    [Fact(DisplayName = "Sending a suggestion while editing ends the edit, and a later submit sends a new prompt")]
    public Task Edit_Suggestion_EndsTheEdit() => session.RunAsync(async () =>
    {
        var (view, runner, window) = await TwoTurns();
        view.Prompt = "draft";
        view.EditLastPromptCommand.Execute(runner.Conversation.Entries.OfType<UserPromptEntry>().Last());

        await view.SubmitAsync("Third");
        Settle(window);

        Assert.False(view.IsEditingPrompt);
        Assert.Equal("draft", view.Prompt);
        await view.SubmitAsync(null);
        Assert.Equal(["First", "Second", "Third", "draft"], runner.Conversation.Entries.OfType<UserPromptEntry>().Select(p => p.Text));
    });

    [Fact(DisplayName = "Showing another conversation ends the edit")]
    public Task Edit_OtherConversation_EndsTheEdit() => session.RunAsync(async () =>
    {
        var (view, runner, window) = await TwoTurns();
        view.EditLastPromptCommand.Execute(runner.Conversation.Entries.OfType<UserPromptEntry>().Last());

        var other = new ChatRunner(new ScriptedChatClient(Text("Other")));
        await other.SendAsync("Elsewhere");
        view.Runner = other;
        Settle(window);

        Assert.False(view.IsEditingPrompt);
    });

    [Fact(DisplayName = "An edit the runner's sending handler cancels goes back in the box, still editing")]
    public Task Edit_Canceled_StaysEditing() => session.RunAsync(async () =>
    {
        var (view, runner, window) = await TwoTurns();
        runner.MessageSending += (_, e) => e.Cancel = e.ReplacesLastPrompt;
        view.EditLastPromptCommand.Execute(runner.Conversation.Entries.OfType<UserPromptEntry>().Last());
        view.Prompt = "Second, changed";

        await view.SubmitAsync(null);
        Settle(window);

        Assert.True(view.IsEditingPrompt);
        Assert.Equal("Second, changed", view.Prompt);
        Assert.Equal(["First", "Second"], runner.Conversation.Entries.OfType<UserPromptEntry>().Select(p => p.Text));
    });

    [Fact(DisplayName = "A prompt entry the application added is not offered for editing, and the real last prompt still is")]
    public Task Edit_ApplicationEntry_IsNotOffered() => session.RunAsync(async () =>
    {
        var (view, runner, window) = await TwoTurns();
        var second = runner.Conversation.Entries.OfType<UserPromptEntry>().Last();
        var added = new UserPromptEntry("app note");
        runner.Conversation.Entries.Add(added);
        Settle(window);

        Assert.False(view.EditLastPromptCommand.CanExecute(added));
        Assert.True(view.EditLastPromptCommand.CanExecute(second));
        Assert.Same(second, runner.Conversation.LastPrompt);
    });

    [Fact(DisplayName = "When the application removed the last prompt's entry, no other prompt is offered for editing")]
    public Task Edit_RemovedEntry_OffersNothing() => session.RunAsync(async () =>
    {
        var (view, runner, window) = await TwoTurns();
        var prompts = runner.Conversation.Entries.OfType<UserPromptEntry>().ToList();
        runner.Conversation.Entries.Remove(prompts[1]);
        Settle(window);

        Assert.Null(runner.Conversation.LastPrompt);
        Assert.False(view.EditLastPromptCommand.CanExecute(prompts[0]));
        Assert.False(view.RegenerateCommand.CanExecute(null));
    });

    [Fact(DisplayName = "After a canceled edit goes back in the box, canceling the edit still gives back the draft from before")]
    public Task Edit_CanceledThenEsc_RestoresTheDraft() => session.RunAsync(async () =>
    {
        var (view, runner, window) = await TwoTurns();
        runner.MessageSending += (_, e) => e.Cancel = e.ReplacesLastPrompt;
        view.Prompt = "my draft";
        view.EditLastPromptCommand.Execute(runner.Conversation.Entries.OfType<UserPromptEntry>().Last());
        await view.SubmitAsync(null);
        Settle(window);
        Assert.True(view.IsEditingPrompt);

        FocusPrompt(view);
        Press(window, Key.Escape);

        Assert.False(view.IsEditingPrompt);
        Assert.Equal("my draft", view.Prompt);
    });

    [Fact(DisplayName = "Without a runner nothing is offered until the application allows it, and then the view asks the application")]
    public Task HostDriven_AsksTheApplication() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        conversation.BeginTurn().Complete();
        var view = new ChatView { Conversation = conversation };
        var window = Show(view);
        Assert.False(view.RegenerateCommand.CanExecute(null));
        Assert.False(view.EditLastPromptCommand.CanExecute(conversation.Entries[0]));

        var regenerated = 0;
        PromptSubmittedEventArgs? submitted = null;
        view.RegenerateRequested += (_, _) => regenerated++;
        view.PromptSubmitted += (_, e) => submitted = e;
        view.AllowsRewind = true;
        view.RegenerateCommand.Execute(null);
        view.EditLastPromptCommand.Execute(conversation.Entries[0]);
        Settle(window);
        _ = view.SubmitAsync(null);

        Assert.Equal(1, regenerated);
        Assert.True(submitted!.IsEdit);
        Assert.Equal("Hi", submitted.Text);
        Assert.False(view.IsEditingPrompt);
    });

    [Fact(DisplayName = "While a reply is in progress nothing can be regenerated or edited")]
    public Task Generating_OffersNothing() => session.RunAsync(async () =>
    {
        var (view, runner, _) = await TwoTurns();
        view.IsGenerating = true;

        Assert.False(view.RegenerateCommand.CanExecute(null));
        Assert.False(view.EditLastPromptCommand.CanExecute(runner.Conversation.Entries.OfType<UserPromptEntry>().Last()));
    });
}
