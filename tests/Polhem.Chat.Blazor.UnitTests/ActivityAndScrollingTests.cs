using Bunit;
using Microsoft.Extensions.AI;
using Microsoft.JSInterop;

namespace Polhem.Chat.Blazor.UnitTests;

/// <summary>The activity line while a reply is in progress, and following the newest entry.</summary>
public class ActivityAndScrollingTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Chat.Blazor/polhem-chat.js";

    private readonly BunitJSModuleInterop _module;

    public ActivityAndScrollingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(ModulePath);
    }

    private static ChatResponseUpdate Update(params AIContent[] contents) => new(ChatRole.Assistant, [.. contents]);

    private IRenderedComponent<ChatView> HostDriven(ChatConversation conversation, bool isGenerating) =>
        Render<ChatView>(parameters => parameters
            .Add(view => view.Conversation, conversation)
            .Add(view => view.IsGenerating, isGenerating)
            .Add(view => view.Strings, new ChatStrings()));

    // The bridge the view handed to the script, to play the script's part.
    private ScrollBridge Bridge() =>
        ((DotNetObjectReference<ScrollBridge>)_module.Invocations["follow"].Single().Arguments[1]!).Value;

    [Fact(DisplayName = "While a reply is in progress the line says what the model does, the request it is on, and goes away after")]
    public void Activity_FollowsTheTurn()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Weather?");
        var turn = conversation.BeginTurn();
        turn.BeginCall();
        var view = HostDriven(conversation, isGenerating: true);

        Assert.Single(view.FindAll(".pch-spinner"));
        Assert.Equal("Thinking…", view.Find(".pch-waiting").TextContent);
        Assert.Contains(" · call 1", view.Find(".pch-progress").TextContent, StringComparison.Ordinal);

        turn.Apply(Update(new FunctionCallContent("c1", "weather")));
        view.WaitForAssertion(() => Assert.Equal("weather…", view.Find(".pch-waiting").TextContent));

        turn.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("c1", "Sunny")]));
        turn.Apply(Update(new TextContent("Sunny.")));
        view.WaitForAssertion(() => Assert.Empty(view.FindAll(".pch-waiting")));

        turn.Complete();
        view.Render(parameters => parameters.Add(v => v.IsGenerating, false));
        Assert.Empty(view.FindAll(".pch-activity"));
    }

    [Fact(DisplayName = "The seconds on the progress line count up while the turn runs")]
    public void Activity_SecondsCountUp()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        conversation.BeginTurn();
        var strings = new ChatStrings();
        var view = HostDriven(conversation, isGenerating: true);
        Assert.Equal(strings.FormatProgress(0, TimeSpan.Zero, 0), view.Find(".pch-progress").TextContent);

        view.WaitForAssertion(() => Assert.Equal(strings.FormatProgress(0, TimeSpan.FromSeconds(1), 0), view.Find(".pch-progress").TextContent),
            TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "The toggle shows the reasoning, tells the parent, and a render of the parent does not undo it")]
    public void Activity_Toggle()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new TextReasoningContent("Think.")));
        bool? told = null;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, conversation)
            .Add(v => v.IsGenerating, true)
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.ShowsActivityChanged, value => told = value));
        Assert.Empty(view.FindAll(".pch-reasoning"));
        Assert.Equal(new ChatStrings().ShowActivity, view.Find(".pch-activity-toggle").GetAttribute("aria-label"));

        view.Find(".pch-activity-toggle").Click();

        Assert.True(told);
        Assert.Single(view.FindAll(".pch-reasoning"));
        Assert.Equal("true", view.Find(".pch-activity-toggle").GetAttribute("aria-pressed"));
        view.Render(parameters => parameters.Add(v => v.IsGenerating, true));
        Assert.Single(view.FindAll(".pch-reasoning"));
    }

    [Fact(DisplayName = "The view follows the newest entry in the browser, and shows the jump button when the user scrolls up")]
    public async Task Scrolling_JumpButton()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var view = HostDriven(conversation, isGenerating: false);
        Assert.True(view.Instance.FollowsLatest);
        Assert.Empty(view.FindAll(".pch-jump"));

        Bridge().OnFollowsLatestChanged(false);
        view.WaitForAssertion(() => Assert.Single(view.FindAll(".pch-jump svg")));
        Assert.False(view.Instance.FollowsLatest);
        Assert.Equal(new ChatStrings().JumpToLatest, view.Find(".pch-jump").GetAttribute("aria-label"));

        view.Render(parameters => parameters.Add(v => v.IsGenerating, true));
        Assert.Single(view.FindAll(".pch-jump-dots"));

        await view.Find(".pch-jump").ClickAsync(new());

        Assert.True(view.Instance.FollowsLatest);
        Assert.Empty(view.FindAll(".pch-jump"));
        _module.VerifyInvoke("scrollToLatest");
    }

    [Fact(DisplayName = "Showing another conversation scrolls to its newest entry")]
    public void Scrolling_NewConversation()
    {
        var view = HostDriven(new ChatConversation(), isGenerating: false);
        Bridge().OnFollowsLatestChanged(false);
        view.WaitForAssertion(() => Assert.False(view.Instance.FollowsLatest));

        view.Render(parameters => parameters.Add(v => v.Conversation, new ChatConversation()));

        Assert.True(view.Instance.FollowsLatest);
        _module.VerifyInvoke("scrollToLatest");
    }

    [Fact(DisplayName = "A disposed view stops following in the browser and leaves no handler on the turn")]
    public async Task Dispose_StopsFollowing()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        HostDriven(conversation, isGenerating: true);

        await DisposeComponentsAsync();

        _module.VerifyInvoke("unfollow");
        turn.BeginCall();
        Assert.Null(typeof(ChatTurn).GetField("PropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(turn));
    }
}
