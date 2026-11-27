using Bunit;
using Microsoft.JSInterop;

namespace Polhem.Chat.Blazor.UnitTests;

/// <summary>The host's commands the user opens with a slash at the start of the prompt box.</summary>
public class CommandListTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Chat.Blazor/polhem-chat.js";

    private static readonly PromptCommand s_shorter = new("shorter", "Make this shorter: ") { Description = "Cut it down" };
    private static readonly PromptCommand s_translate = new("translate", "Translate to English.") { SendsImmediately = true };

    private readonly BunitJSModuleInterop _module;
    private readonly List<string> _submitted = [];

    public CommandListTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(ModulePath);
    }

    private PromptBridge Bridge() =>
        ((DotNetObjectReference<PromptBridge>)_module.Invocations["watchPrompt"].Single().Arguments[2]!).Value;

    private IRenderedComponent<ChatView> Hosted(IEnumerable<PromptCommand>? commands) =>
        Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.Commands, commands)
            .Add(v => v.PromptSubmitted, e => _submitted.Add(e.Text)));

    [Fact(DisplayName = "A slash opens a list box of the commands, which the prompt box points to and marks as the root")]
    public void Slash_OpensListBox()
    {
        var view = Hosted([s_shorter, s_translate]);
        var box = view.Find(".pch-prompt-box");
        Assert.Equal("list", box.GetAttribute("aria-autocomplete"));
        Assert.Empty(view.FindAll(".pch-commands"));

        box.Input("/");

        var list = view.Find(".pch-commands");
        Assert.Equal("listbox", list.GetAttribute("role"));
        Assert.Equal("Commands", list.GetAttribute("aria-label"));
        var options = view.FindAll(".pch-command");
        Assert.Equal(["/shorter", "/translate"], options.Select(o => o.QuerySelector(".pch-command-name")!.TextContent));
        Assert.Equal("Cut it down", options[0].QuerySelector(".pch-command-description")!.TextContent);
        Assert.Equal(["true", "false"], options.Select(o => o.GetAttribute("aria-selected")));
        box = view.Find(".pch-prompt-box");
        Assert.Equal(list.Id, box.GetAttribute("aria-controls"));
        Assert.Equal(options[0].Id, box.GetAttribute("aria-activedescendant"));
        Assert.NotNull(view.Find(".pch").GetAttribute("data-commands"));
        Assert.Equal("/shorter Cut it down (1 of 2)", view.Find(".pch-visually-hidden").TextContent);
    }

    [Fact(DisplayName = "The script's arrows move the selection, and its Esc closes the list")]
    public void Bridge_MovesAndCloses()
    {
        var view = Hosted([s_shorter, s_translate]);
        view.Find(".pch-prompt-box").Input("/");

        Bridge().MoveCommand(1);
        view.WaitForAssertion(() =>
            Assert.Equal("true", view.FindAll(".pch-command")[1].GetAttribute("aria-selected")));
        Assert.Equal(view.FindAll(".pch-command")[1].Id, view.Find(".pch-prompt-box").GetAttribute("aria-activedescendant"));

        Bridge().CloseCommands();
        view.WaitForAssertion(() => Assert.Empty(view.FindAll(".pch-commands")));
        Assert.Null(view.Find(".pch").GetAttribute("data-commands"));
        Assert.Null(view.Find(".pch-prompt-box").GetAttribute("aria-activedescendant"));
    }

    [Fact(DisplayName = "Enter on the list puts the command in the box, even before the list was drawn, and submits nothing")]
    public void Enter_FillsBox()
    {
        var view = Hosted([s_shorter, s_translate]);

        Bridge().Submit("/sh");

        view.WaitForAssertion(() =>
            Assert.Equal("Make this shorter: ", view.Find(".pch-prompt-box").GetAttribute("value")));
        Assert.Empty(_submitted);
        Assert.Empty(view.FindAll(".pch-commands"));
    }

    [Fact(DisplayName = "Enter on a command that sends at once submits its text")]
    public void Enter_SendsImmediately()
    {
        var view = Hosted([s_shorter, s_translate]);

        Bridge().Submit("/tr");

        view.WaitForAssertion(() => Assert.Equal(["Translate to English."], _submitted));
    }

    [Fact(DisplayName = "Clicking a command chooses it")]
    public void Click_ChoosesCommand()
    {
        var view = Hosted([s_shorter, s_translate]);
        view.Find(".pch-prompt-box").Input("/");

        view.FindAll(".pch-command")[1].Click();

        view.WaitForAssertion(() => Assert.Equal(["Translate to English."], _submitted));
    }

    [Fact(DisplayName = "Without commands, a slash opens nothing and Enter sends it as typed")]
    public void NoCommands_SlashIsText()
    {
        var view = Hosted(null);
        var box = view.Find(".pch-prompt-box");
        Assert.Null(box.GetAttribute("aria-autocomplete"));

        box.Input("/shorter");
        Assert.Empty(view.FindAll(".pch-commands"));
        Bridge().Submit("/shorter");

        view.WaitForAssertion(() => Assert.Equal(["/shorter"], _submitted));
    }

    [Fact(DisplayName = "Names and descriptions show their hidden characters escaped")]
    public void Names_Escaped()
    {
        var view = Hosted([new PromptCommand("evil\u202Etxt", "Text") { Description = "a\u202Eb" }]);

        view.Find(".pch-prompt-box").Input("/");

        Assert.DoesNotContain('\u202E', view.Find(".pch-command").TextContent);
    }
}
