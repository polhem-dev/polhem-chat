using System.Collections.ObjectModel;
using System.Globalization;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.UnitTests;

/// <summary>The host's commands the user reaches with a slash at the start of the prompt box.</summary>
public class PromptCommandTests
{
    private static readonly PromptCommand s_shorter = new("shorter", "Make this shorter: ") { Description = "Cut it down" };
    private static readonly PromptCommand s_translate = new("translate", "Translate to English.") { SendsImmediately = true };
    private static readonly PromptCommand s_grammar = new("fix-grammar", "Fix the grammar.");

    private static ChatComposer Composer(params PromptCommand[] commands) =>
        new() { Conversation = new ChatConversation(), Commands = commands };

    [Theory(DisplayName = "A command name must be typeable after the slash, and its text must not be blank")]
    [InlineData("", "Text")]
    [InlineData("two words", "Text")]
    [InlineData("/shorter", "Text")]
    [InlineData("shorter", " ")]
    public void Constructor_RejectsUntypeableNameOrBlankText(string name, string text) =>
        Assert.ThrowsAny<ArgumentException>(() => new PromptCommand(name, text));

    [Fact(DisplayName = "Without commands the box behaves as before: a slash opens nothing and is sent as typed")]
    public async Task NoCommands_SlashIsPlainText()
    {
        var composer = new ChatComposer { Conversation = new ChatConversation(), Prompt = "/shorter" };
        PromptSubmittedEventArgs? submitted = null;
        composer.PromptSubmitted += (_, e) => submitted = e;

        await composer.SubmitAsync();

        Assert.False(composer.IsCommandListOpen);
        Assert.Null(composer.SelectedCommand);
        Assert.Equal("/shorter", submitted!.Text);
    }

    [Fact(DisplayName = "A slash at the start lists every command, and the first is selected")]
    public void Slash_ListsAllAndSelectsFirst()
    {
        var composer = Composer(s_shorter, s_translate, s_grammar);

        composer.Prompt = "/";

        Assert.True(composer.IsCommandListOpen);
        Assert.Equal([s_shorter, s_translate, s_grammar], composer.CommandMatches);
        Assert.Same(s_shorter, composer.SelectedCommand);
    }

    [Fact(DisplayName = "Typing filters the names, ignoring case: those that start with the text first, then those that contain it")]
    public void Typing_FiltersStartingThenContaining()
    {
        var composer = Composer(s_grammar, s_shorter, s_translate);

        composer.Prompt = "/R";
        Assert.Equal([s_grammar, s_shorter, s_translate], composer.CommandMatches);

        composer.Prompt = "/t";
        Assert.Equal([s_translate, s_shorter], composer.CommandMatches);
    }

    [Theory(DisplayName = "The list stays closed when the slash is not first, after white space, or when nothing matches")]
    [InlineData("a /shorter")]
    [InlineData("/shorter please")]
    [InlineData("/\n")]
    [InlineData("/zzz")]
    [InlineData("")]
    public void List_ClosedOutsideAName(string prompt)
    {
        var composer = Composer(s_shorter, s_translate);

        composer.Prompt = prompt;

        Assert.False(composer.IsCommandListOpen);
        Assert.Empty(composer.CommandMatches);
        Assert.Null(composer.SelectedCommand);
    }

    [Fact(DisplayName = "The arrow keys move the selection and wrap around at either end")]
    public void MoveSelection_Wraps()
    {
        var composer = Composer(s_shorter, s_translate, s_grammar);
        composer.Prompt = "/";

        composer.MoveCommandSelection(-1);
        Assert.Same(s_grammar, composer.SelectedCommand);
        composer.MoveCommandSelection(1);
        Assert.Same(s_shorter, composer.SelectedCommand);
        composer.MoveCommandSelection(1);
        Assert.Same(s_translate, composer.SelectedCommand);
    }

    [Fact(DisplayName = "Moving the selection while the list is closed does nothing")]
    public void MoveSelection_Closed_DoesNothing()
    {
        var composer = Composer(s_shorter);

        composer.MoveCommandSelection(1);

        Assert.Null(composer.SelectedCommand);
    }

    [Fact(DisplayName = "The selection stays on its command while typing keeps it in the list, and goes to the first when it drops out")]
    public void Typing_KeepsSelectionWhileItMatches()
    {
        var composer = Composer(s_shorter, s_translate, s_grammar);
        composer.Prompt = "/";
        composer.MoveCommandSelection(1);

        composer.Prompt = "/t";
        Assert.Same(s_translate, composer.SelectedCommand);

        composer.Prompt = "/sh";
        Assert.Same(s_shorter, composer.SelectedCommand);
    }

    [Fact(DisplayName = "Choosing a command puts its text in the box, closes the list and sends nothing")]
    public async Task Choose_FillsBox()
    {
        var composer = Composer(s_shorter, s_translate);
        var submitted = 0;
        composer.PromptSubmitted += (_, _) => submitted++;
        composer.Prompt = "/sh";

        await composer.ChooseCommandAsync();

        Assert.Equal("Make this shorter: ", composer.Prompt);
        Assert.False(composer.IsCommandListOpen);
        Assert.Equal(0, submitted);
    }

    [Fact(DisplayName = "A command that sends at once goes through the runner with the files waiting in the box, and the box empties")]
    public async Task Choose_SendsImmediately()
    {
        var composer = new ChatComposer
        {
            Runner = new ChatRunner(new ScriptedChatClient(Text("Done"))),
            Commands = [s_shorter, s_translate],
        };
        await composer.AddAttachmentAsync("photo.png", AttachmentTests.Png.Length,
            () => Task.FromResult<Stream>(new MemoryStream(AttachmentTests.Png)));
        composer.Prompt = "/tr";

        await composer.ChooseCommandAsync();

        Assert.Equal(string.Empty, composer.Prompt);
        var prompt = Assert.Single(composer.ShownConversation!.Entries.OfType<UserPromptEntry>());
        Assert.Equal("Translate to English.", prompt.Text);
        Assert.Single(prompt.Attachments);
    }

    [Fact(DisplayName = "A command that sends at once stays in the box while a reply is in progress")]
    public async Task Choose_SendsImmediately_WhileGenerating_FillsBox()
    {
        var composer = Composer(s_translate);
        var submitted = 0;
        composer.PromptSubmitted += (_, _) => submitted++;
        composer.IsGenerating = true;
        composer.Prompt = "/";

        await composer.ChooseCommandAsync();

        Assert.Equal("Translate to English.", composer.Prompt);
        Assert.Equal(0, submitted);
    }

    [Fact(DisplayName = "Choosing with the list closed and no command given does nothing")]
    public async Task Choose_Closed_DoesNothing()
    {
        var composer = Composer(s_shorter);
        composer.Prompt = "Hello";

        await composer.ChooseCommandAsync();

        Assert.Equal("Hello", composer.Prompt);
    }

    [Fact(DisplayName = "A closed list stays closed as the user types on, and opens again after the slash is gone and typed anew")]
    public void Close_StaysClosedUntilSlashTypedAgain()
    {
        var composer = Composer(s_shorter);
        composer.Prompt = "/";

        composer.CloseCommandList();
        Assert.False(composer.IsCommandListOpen);
        composer.Prompt = "/sh";
        Assert.False(composer.IsCommandListOpen);

        composer.Prompt = string.Empty;
        composer.Prompt = "/";
        Assert.True(composer.IsCommandListOpen);
    }

    [Fact(DisplayName = "The host can change an observable list of commands while the list is open")]
    public void ObservableCommands_UpdateOpenList()
    {
        var commands = new ObservableCollection<PromptCommand> { s_shorter };
        var composer = new ChatComposer { Conversation = new ChatConversation(), Commands = commands };
        composer.Connect();
        composer.Prompt = "/";

        commands.Add(s_translate);
        Assert.Equal([s_shorter, s_translate], composer.CommandMatches);

        commands.Remove(s_shorter);
        Assert.Same(s_translate, composer.SelectedCommand);
    }

    [Fact(DisplayName = "A disconnected composer no longer listens to the host's collection")]
    public void ObservableCommands_Disconnected_NotFollowed()
    {
        var commands = new ObservableCollection<PromptCommand> { s_shorter };
        var composer = new ChatComposer { Conversation = new ChatConversation(), Commands = commands };
        composer.Connect();
        composer.Disconnect();
        composer.Prompt = "/";

        commands.Add(s_translate);

        Assert.Equal([s_shorter], composer.CommandMatches);
    }

    [Fact(DisplayName = "Replacing the commands updates the open list")]
    public void ReplaceCommands_UpdatesOpenList()
    {
        var composer = Composer(s_shorter);
        composer.Prompt = "/";

        composer.Commands = [s_grammar];

        Assert.Same(s_grammar, composer.SelectedCommand);
        composer.Commands = null;
        Assert.False(composer.IsCommandListOpen);
    }

    [Fact(DisplayName = "The selected command is described with its name, description and place in the list")]
    public void Describe_SelectedCommand()
    {
        var composer = Composer(s_translate, s_shorter);
        Assert.Null(composer.DescribeSelectedCommand());
        composer.Prompt = "/";
        composer.MoveCommandSelection(1);

        Assert.Equal("/shorter Cut it down (2 of 2)", composer.DescribeSelectedCommand());
        composer.Strings = ChatStrings.ForCulture(CultureInfo.GetCultureInfo("zh-TW"));
        Assert.Equal("/shorter Cut it down（第 2 項，共 2 項）", composer.DescribeSelectedCommand());
    }
}
