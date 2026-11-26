using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.AI;
using Polhem.Chat.UnitTests;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.Blazor.UnitTests;

/// <summary>The application's own content and templates, the view's public actions for them, and its language.</summary>
public class TemplatesTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Chat.Blazor/polhem-chat.js";

    private readonly BunitJSModuleInterop _module;

    public TemplatesTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(ModulePath);
    }

    private static ChatResponseUpdate Update(params AIContent[] contents) => new(ChatRole.Assistant, [.. contents]);

    private static RenderFragment Markup(string html) => builder => builder.AddMarkupContent(0, html);

    private static ChatConversation Answered(string prompt, string reply)
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt(prompt);
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new TextContent(reply)));
        turn.Complete();
        return conversation;
    }

    [Fact(DisplayName = "The application's content shows above the conversation, in the empty state and around the prompt box")]
    public void Slots_ShowTheApplicationsContent()
    {
        var conversation = new ChatConversation();
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, conversation)
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.Suggestions, ["Hi"])
            .Add(v => v.HeaderContent, Markup("<h2 id=\"header\">Assistant</h2>"))
            .Add(v => v.EmptyContent, Markup("<p id=\"empty\">Ask about your data</p>"))
            .Add(v => v.PromptHeaderContent, Markup("<span id=\"above\">Model: test</span>"))
            .Add(v => v.PromptFooterContent, Markup("<small id=\"below\">Answers may be wrong.</small>")));

        Assert.Equal("Assistant", view.Find(".pch-header #header").TextContent);
        Assert.Equal("Ask about your data", view.Find(".pch-empty #empty").TextContent);
        Assert.Empty(view.FindAll(".pch-empty-title"));
        Assert.Single(view.FindAll(".pch-suggestion"));
        Assert.Equal("Model: test", view.Find(".pch-composer #above").TextContent);
        Assert.Equal("Answers may be wrong.", view.Find(".pch-composer #below").TextContent);

        conversation.AddUserPrompt("Hi");
        view.WaitForAssertion(() => Assert.Empty(view.FindAll("#empty")));
        Assert.Single(view.FindAll("#header"));
    }

    [Fact(DisplayName = "A template for a kind of entry replaces its default, and the other kinds keep theirs")]
    public void Templates_ReplaceTheirKind()
    {
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, Answered("Hi", "Hello"))
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.ReplyTemplate, reply => Markup($"<div class=\"my-reply\">{reply.Text.ToUpperInvariant()}</div>"))
            .Add(v => v.SummaryTemplate, _ => Markup("<div class=\"my-summary\">done</div>")));

        Assert.Equal("HELLO", view.Find(".my-reply").TextContent);
        Assert.Empty(view.FindAll(".pch-reply"));
        Assert.Equal("done", view.Find(".my-summary").TextContent);
        Assert.Equal("Hi", view.Find(".pch-prompt-text").TextContent);
    }

    [Fact(DisplayName = "Every kind of entry takes a template of the application's own")]
    public void Templates_EveryKind()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new TextReasoningContent("Think.")));
        turn.Apply(Update(new FunctionCallContent("c1", "search")));
        turn.Fail(new InvalidOperationException("Broken."));
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, conversation)
            .Add(v => v.ShowsActivity, true)
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.PromptTemplate, prompt => Markup($"<i class=\"t-prompt\">{prompt.Text}</i>"))
            .Add(v => v.ReasoningTemplate, reasoning => Markup($"<i class=\"t-reasoning\">{reasoning.Text}</i>"))
            .Add(v => v.ToolCallTemplate, call => Markup($"<i class=\"t-call\">{call.Name}</i>"))
            .Add(v => v.ErrorTemplate, _ => Markup("<i class=\"t-error\">failed</i>")));

        Assert.Equal("Hi", view.Find(".t-prompt").TextContent);
        Assert.Equal("Think.", view.Find(".t-reasoning").TextContent);
        Assert.Equal("search", view.Find(".t-call").TextContent);
        Assert.Equal("failed", view.Find(".t-error").TextContent);
    }

    [Fact(DisplayName = "A tool's own template shows its calls while the activity is hidden, and the other calls stay hidden")]
    public void ToolTemplates_ByName()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Weather?");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new FunctionCallContent("c1", "weather")));
        turn.Apply(Update(new FunctionCallContent("c2", "search")));
        var templates = new Dictionary<string, RenderFragment<ToolCallEntry>>
        {
            ["weather"] = call => Markup($"<div class=\"weather-card\">{call.Name}</div>"),
        };
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, conversation)
            .Add(v => v.IsGenerating, true)
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.ToolCallTemplates, templates));

        Assert.Equal("weather", view.Find(".weather-card").TextContent);
        Assert.Empty(view.FindAll(".pch-tool-call"));

        // A dictionary given anew with the same templates changes nothing; one without them goes back to the defaults.
        view.Render(parameters => parameters.Add(v => v.ToolCallTemplates, new Dictionary<string, RenderFragment<ToolCallEntry>>(templates)));
        Assert.Single(view.FindAll(".weather-card"));
        view.Render(parameters => parameters.Add(v => v.ToolCallTemplates, null).Add(v => v.ShowsActivity, true));
        Assert.Empty(view.FindAll(".weather-card"));
        Assert.Equal(2, view.FindAll(".pch-tool-call").Count);
    }

    [Fact(DisplayName = "A template given anew by each render of the parent does not draw the entries again; another one does")]
    public void Templates_DrawAgainOnlyWhenOthers()
    {
        var host = Render<TemplateHost>(parameters => parameters.Add(h => h.Conversation, Answered("Hi", "Hello")));
        host.WaitForAssertion(() => Assert.Contains("Hello", host.Find(".pch-entries").TextContent, StringComparison.Ordinal));
        var drawn = host.Instance.Drawn;

        host.Render(parameters => parameters.Add(h => h.Plain, false));
        host.Render(parameters => parameters.Add(h => h.Plain, false));
        Assert.Equal(drawn, host.Instance.Drawn);

        host.Render(parameters => parameters.Add(h => h.Plain, true));
        Assert.Equal("Hello!", host.Find(".host-reply").TextContent);
    }

    [Fact(DisplayName = "A template can approve or reject a call through the view, only while it waits for the user")]
    public void Actions_DecideToolCalls()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Delete");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new ToolApprovalRequestContent("r1", new FunctionCallContent("c1", "delete"))));
        turn.Apply(Update(new ToolApprovalRequestContent("r2", new FunctionCallContent("c2", "delete"))));
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, conversation)
            .Add(v => v.IsGenerating, true)
            .Add(v => v.Strings, new ChatStrings()));
        var calls = conversation.Entries.OfType<ToolCallEntry>().ToList();
        Assert.All(calls, call => Assert.True(view.Instance.CanDecideToolCall(call)));

        view.InvokeAsync(() => view.Instance.ApproveToolCall(calls[0]));
        view.InvokeAsync(() => view.Instance.RejectToolCall(calls[1]));

        Assert.All(calls, call => Assert.False(view.Instance.CanDecideToolCall(call)));
        Assert.Equal(ToolCallStatus.Rejected, calls[1].Status);
        Assert.NotEqual(ToolCallStatus.Rejected, calls[0].Status);
    }

    [Fact(DisplayName = "A template can send the last prompt again, change it and stop changing it, through the view")]
    public async Task Actions_Rewind()
    {
        var client = new ScriptedChatClient(Text("One"));
        var runner = new ChatRunner(client);
        var view = Render<ChatView>(parameters => parameters.Add(v => v.Runner, runner).Add(v => v.Strings, new ChatStrings()));
        await view.InvokeAsync(() => runner.SendAsync("First"));
        var prompt = runner.Conversation.Entries.OfType<UserPromptEntry>().Single();
        var reply = runner.Conversation.Entries.OfType<AssistantReplyEntry>().Single();
        Assert.True(view.Instance.CanEditLastPrompt(prompt));
        Assert.True(view.Instance.CanRegenerate(reply));

        await view.InvokeAsync(() => view.Instance.EditLastPromptAsync(prompt));
        Assert.True(view.Instance.IsEditingPrompt);
        await view.InvokeAsync(view.Instance.CancelEdit);
        Assert.False(view.Instance.IsEditingPrompt);

        await view.InvokeAsync(view.Instance.Regenerate);
        view.WaitForAssertion(() => Assert.Equal(2, client.Requests.Count));
        view.WaitForAssertion(() => Assert.NotSame(reply, runner.Conversation.Entries.OfType<AssistantReplyEntry>().Single()));
    }

    [Fact(DisplayName = "Copying through the view reports whether the browser took the text, and false before its script loads")]
    public async Task Actions_Copy()
    {
        _module.Setup<bool>("copyText", "Hello").SetResult(true);
        var view = Render<ChatView>(parameters => parameters.Add(v => v.Conversation, new ChatConversation()));
        var instance = view.Instance;
        // The view calls its script once the script has loaded; the last call it makes then is to watch the prompt box.
        view.WaitForAssertion(() => _module.VerifyInvoke("watchPrompt"));

        Assert.True(await view.InvokeAsync(() => instance.CopyAsync("Hello")));

        await DisposeComponentsAsync();
        Assert.False(await instance.CopyAsync("Hello"));
    }

    [Fact(DisplayName = "Without texts of its own the view speaks the UI culture it was created in")]
    public void Strings_FollowTheUICulture()
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-TW");
            var view = Render<ChatView>(parameters => parameters.Add(v => v.Conversation, new ChatConversation()));

            Assert.Equal(ChatStrings.ForCulture(CultureInfo.GetCultureInfo("zh-TW")).Send, view.Find(".pch-send").TextContent);
            Assert.NotEqual(ChatStrings.ForCulture(CultureInfo.InvariantCulture).Send, view.Find(".pch-send").TextContent);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [Fact(DisplayName = "A call that waits for approval keeps the view's approval under the application's template")]
    public void Approval_StaysUnderATemplate()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Delete");
        var turn = conversation.BeginTurn();
        turn.Apply(Update(new ToolApprovalRequestContent("r1", new FunctionCallContent("c1", "delete", new Dictionary<string, object?> { ["path"] = "notes\u202Etxt.exe" }))));
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, conversation)
            .Add(v => v.IsGenerating, true)
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.ToolCallTemplates, new Dictionary<string, RenderFragment<ToolCallEntry>>
            {
                ["delete"] = _ => Markup("<div class=\"my-delete\">Delete a file</div>"),
            }));

        Assert.Single(view.FindAll(".pch-tool-template .my-delete"));
        var arguments = view.Find(".pch-tool-template .pch-approval-args").TextContent;
        Assert.Contains("\\u{202E}", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("\u202E", arguments, StringComparison.Ordinal);

        view.Find(".pch-approve").Click();

        var call = conversation.Entries.OfType<ToolCallEntry>().Single();
        view.WaitForAssertion(() => Assert.Empty(view.FindAll(".pch-approval")));
        Assert.NotEqual(ToolCallStatus.AwaitingApproval, call.Status);
        Assert.Single(view.FindAll(".my-delete"));
    }

    [Fact(DisplayName = "The options a reply template is given load no images unless the application chose otherwise")]
    public void ReplyMarkdownOptions_LoadNoImages()
    {
        var view = Render<ChatView>(parameters => parameters.Add(v => v.Conversation, new ChatConversation()));
        Assert.Empty(view.Instance.ReplyMarkdownOptions.AllowedImageSchemes);

        var chosen = new Polhem.Markdown.MarkdownOptions();
        view.Render(parameters => parameters.Add(v => v.MarkdownOptions, chosen));
        Assert.Same(chosen, view.Instance.ReplyMarkdownOptions);
    }

    [Fact(DisplayName = "Redrawing the entries draws a template that reads the application's state again")]
    public async Task RedrawEntries_DrawsAgain()
    {
        var label = "first";
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, Answered("Hi", "Hello"))
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.ReplyTemplate, _ => builder => builder.AddMarkupContent(0, $"<b class=\"label\">{label}</b>")));
        view.WaitForAssertion(() => Assert.Equal("first", view.Find(".label").TextContent));

        label = "second";
        await view.InvokeAsync(view.Instance.RedrawEntries);

        view.WaitForAssertion(() => Assert.Equal("second", view.Find(".label").TextContent));
    }
}
