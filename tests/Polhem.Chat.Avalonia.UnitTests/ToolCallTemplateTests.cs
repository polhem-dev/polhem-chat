using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Microsoft.Extensions.AI;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>Templates an application gives the calls of one tool.</summary>
[Collection(HeadlessCollection.Name)]
public class ToolCallTemplateTests(HeadlessSession session)
{
    private static ToolCallTemplate WeatherTemplate() => new()
    {
        ToolName = "get_weather",
        Template = new FuncDataTemplate<ToolCallEntry>((call, _) => new TextBlock { Classes = { "weatherCard" }, Text = "Weather: " + call.Title }),
    };

    private static ChatConversation Conversation(params ToolCallEntry[] calls)
    {
        var conversation = new ChatConversation();
        foreach (var call in calls)
        {
            conversation.Entries.Add(call);
        }
        return conversation;
    }

    [Fact(DisplayName = "A call of the named tool shows with the tool's template, and other calls with the default card")]
    public Task Template_MatchesByName() => session.RunAsync(() =>
    {
        var view = new ChatView
        {
            Conversation = Conversation(new ToolCallEntry("c1", "get_weather"), new ToolCallEntry("c2", "lookup")),
            ShowsActivity = true,
        };
        view.DataTemplates.Add(WeatherTemplate());
        Show(view);

        Assert.Equal("Weather: get_weather", Assert.IsType<TextBlock>(EntryView(view, 0)).Text);
        Assert.IsNotType<TextBlock>(EntryView(view, 1));
    });

    [Fact(DisplayName = "The tool name is compared ordinally, so a different case does not match")]
    public Task Template_IsCaseSensitive() => session.RunAsync(() =>
    {
        var template = WeatherTemplate();

        Assert.True(template.Match(new ToolCallEntry("c1", "get_weather")));
        Assert.False(template.Match(new ToolCallEntry("c1", "Get_Weather")));
        Assert.False(template.Match(new UserPromptEntry("get_weather")));
        Assert.False(new ToolCallTemplate { ToolName = "get_weather" }.Match(new ToolCallEntry("c1", "get_weather")));
    });

    [Fact(DisplayName = "A call with a tool template shows while the activity is hidden; other calls stay hidden")]
    public Task Template_ShowsWithActivityHidden() => session.RunAsync(() =>
    {
        var view = new ChatView { Conversation = Conversation(new ToolCallEntry("c1", "get_weather"), new ToolCallEntry("c2", "lookup")) };
        view.DataTemplates.Add(WeatherTemplate());
        Show(view);

        var entries = Part<ItemsControl>(view, "PART_Entries");
        Assert.False(view.ShowsActivity);
        Assert.True(entries.ContainerFromIndex(0)!.IsVisible);
        Assert.False(entries.ContainerFromIndex(1)!.IsVisible);
    });

    [Fact(DisplayName = "A tool template registered on the application applies to every view, and so does a call waiting for approval")]
    public Task Template_FromAncestor_AppliesToPendingCall() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();
        var view = new ChatView { Conversation = conversation, IsGenerating = true };
        var host = new Border { Child = view };
        host.DataTemplates.Add(WeatherTemplate());
        var window = new Window { Width = 500, Height = 600, Content = host };
        window.Show();

        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("r1", new FunctionCallContent("c1", "get_weather"))]));
        Settle(window);

        Assert.Equal(ToolCallStatus.AwaitingApproval, turn.PendingApprovals[0].Status);
        Assert.IsType<TextBlock>(EntryView(view, 0));
    });
}
