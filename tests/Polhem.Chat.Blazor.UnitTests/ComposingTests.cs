using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.AI;
using Microsoft.JSInterop;
using Polhem.Chat.UnitTests;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.Blazor.UnitTests;

/// <summary>The prompt box: typing, submitting, stopping, suggestions, changing the last prompt and attaching files.</summary>
public class ComposingTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Chat.Blazor/polhem-chat.js";

    private static readonly byte[] s_png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly BunitJSModuleInterop _module;

    public ComposingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(ModulePath);
    }

    // The bridge the view handed to the script, to play the script's part.
    private PromptBridge Bridge() =>
        ((DotNetObjectReference<PromptBridge>)_module.Invocations["watchPrompt"].Single().Arguments[2]!).Value;

    private static void Exchange(ChatConversation conversation, string prompt, string reply)
    {
        conversation.AddUserPrompt(prompt);
        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, reply));
        turn.Complete();
    }

    private IRenderedComponent<ChatView> WithRunner(ChatRunner runner, Action<ComponentParameterCollectionBuilder<ChatView>>? more = null) =>
        Render<ChatView>(parameters =>
        {
            parameters.Add(v => v.Runner, runner).Add(v => v.Strings, new ChatStrings());
            more?.Invoke(parameters);
        });

    [Fact(DisplayName = "Typing tells the parent, and a render of the parent that does not bind the text keeps it")]
    public void Typing_TellsTheParent()
    {
        string? told = null;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.PromptChanged, value => told = value));

        view.Find(".pch-prompt-box").Input("Hello");

        Assert.Equal("Hello", told);
        view.Render(parameters => parameters.Add(v => v.IsGenerating, false));
        Assert.Equal("Hello", view.Find(".pch-prompt-box").GetAttribute("value"));

        view.Render(parameters => parameters.Add(v => v.Prompt, "From the parent"));
        Assert.Equal("From the parent", view.Find(".pch-prompt-box").GetAttribute("value"));
    }

    [Fact(DisplayName = "The send button sends the prompt through the runner and empties the box, which tells the parent")]
    public async Task Send_ThroughTheRunner()
    {
        var runner = new ChatRunner(new ScriptedChatClient(Text("Hi there")));
        string? told = "unset";
        var view = WithRunner(runner, parameters => parameters
            .Add(v => v.Prompt, "Hello")
            .Add(v => v.PromptChanged, value => told = value));

        await view.Find(".pch-send").ClickAsync(new());

        view.WaitForAssertion(() => Assert.Equal("Hello", view.Find(".pch-prompt-text").TextContent));
        Assert.Equal(string.Empty, told);
        Assert.Equal(string.Empty, view.Find(".pch-prompt-box").GetAttribute("value"));
        _module.VerifyInvoke("scrollToLatest");
    }

    [Fact(DisplayName = "Enter in the box sends the text the box holds, ahead of the last input the view received")]
    public void Enter_SendsWhatTheBoxHolds()
    {
        PromptSubmittedEventArgs? submitted = null;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.PromptSubmitted, e => submitted = e));
        view.Find(".pch-prompt-box").Input("Hel");

        Bridge().Submit("Hello");

        view.WaitForAssertion(() => Assert.Equal("Hello", submitted?.Text));
        Assert.False(submitted!.IsEdit);
    }

    [Fact(DisplayName = "A submission the application cancels keeps the prompt in the box and does not scroll")]
    public void Submitted_Canceled_KeepsThePrompt()
    {
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.Prompt, "Keep me")
            .Add(v => v.PromptSubmitted, e => e.Cancel = true));

        view.Find(".pch-send").Click();

        Assert.Equal("Keep me", view.Find(".pch-prompt-box").GetAttribute("value"));
        Assert.Empty(_module.Invocations["scrollToLatest"]);
    }

    [Fact(DisplayName = "An application's submit handler that fails later is reported to the renderer")]
    public async Task Submitted_FailingHandler_IsReported()
    {
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.Prompt, "Hi")
            .Add(v => v.PromptSubmitted, async _ =>
            {
                await Task.Yield();
                throw new InvalidOperationException("The application failed.");
            }));

        view.Find(".pch-send").Click();

        var failure = await Renderer.UnhandledException.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("The application failed.", failure.Message);
    }

    [Fact(DisplayName = "While a reply is in progress the stop button replaces send, and it and Esc ask to stop")]
    public void Stop_ButtonAndEscape()
    {
        var stops = 0;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.IsGenerating, true)
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.StopRequested, () => stops++));
        Assert.Empty(view.FindAll(".pch-send"));

        view.Find(".pch-stop").Click();
        Bridge().Escape();

        view.WaitForAssertion(() => Assert.Equal(2, stops));
        view.Render(parameters => parameters.Add(v => v.IsGenerating, false));
        Assert.Single(view.FindAll(".pch-send"));
        Assert.Empty(view.FindAll(".pch-stop"));
    }

    [Fact(DisplayName = "An empty conversation offers the suggestions, which send themselves, and they go once it has entries")]
    public void Suggestions_SendThemselves()
    {
        var conversation = new ChatConversation();
        PromptSubmittedEventArgs? submitted = null;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, conversation)
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.Prompt, "Typed")
            .Add(v => v.Suggestions, ["Tell a joke", "Explain"])
            .Add(v => v.PromptSubmitted, e => submitted = e));
        Assert.Equal(new ChatStrings().EmptyTitle, view.Find(".pch-empty-title").TextContent);

        view.FindAll(".pch-suggestion")[0].Click();

        Assert.Equal("Tell a joke", submitted?.Text);
        // A suggestion goes alone: the box keeps what the user typed.
        Assert.Equal("Typed", view.Find(".pch-prompt-box").GetAttribute("value"));
        conversation.AddUserPrompt("Tell a joke");
        view.WaitForAssertion(() => Assert.Empty(view.FindAll(".pch-empty")));
    }

    [Fact(DisplayName = "The last prompt can be changed in the box, which says so, and Esc gives the box back what it held")]
    public async Task Edit_LastPrompt()
    {
        var runner = new ChatRunner(new ScriptedChatClient(Text("One")));
        var view = WithRunner(runner, parameters => parameters.Add(v => v.Prompt, "Draft"));
        await view.InvokeAsync(() => runner.SendAsync("First"));
        view.WaitForAssertion(() => Assert.Single(view.FindAll(".pch-edit")));

        await view.Find(".pch-edit").ClickAsync(new());

        Assert.True(view.Instance.IsEditingPrompt);
        Assert.Equal("First", view.Find(".pch-prompt-box").GetAttribute("value"));
        Assert.Equal(new ChatStrings().EditingPrompt, view.Find(".pch-edit-banner span").TextContent);
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier.EndsWith("focus", StringComparison.Ordinal));

        Bridge().Escape();

        view.WaitForAssertion(() => Assert.Empty(view.FindAll(".pch-edit-banner")));
        Assert.Equal("Draft", view.Find(".pch-prompt-box").GetAttribute("value"));
    }

    [Fact(DisplayName = "The cancel button of the banner ends the edit too")]
    public async Task Edit_CancelButton()
    {
        var runner = new ChatRunner(new ScriptedChatClient(Text("One")));
        var view = WithRunner(runner);
        await view.InvokeAsync(() => runner.SendAsync("First"));
        view.WaitForAssertion(() => Assert.Single(view.FindAll(".pch-edit")));
        await view.Find(".pch-edit").ClickAsync(new());

        view.Find(".pch-cancel-edit").Click();

        Assert.False(view.Instance.IsEditingPrompt);
        Assert.Equal(string.Empty, view.Find(".pch-prompt-box").GetAttribute("value"));
    }

    [Fact(DisplayName = "Only the last reply offers to send its prompt again, and only while the application allows it")]
    public void Regenerate_LastReplyOnly()
    {
        var conversation = new ChatConversation();
        Exchange(conversation, "One", "First");
        Exchange(conversation, "Two", "Second");
        var regenerations = 0;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, conversation)
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.RegenerateRequested, () => regenerations++));
        Assert.Empty(view.FindAll(".pch-regenerate"));

        view.Render(parameters => parameters.Add(v => v.AllowsRewind, true));

        var button = Assert.Single(view.FindAll(".pch-regenerate"));
        Assert.Equal(new ChatStrings().Regenerate, button.GetAttribute("aria-label"));
        Assert.Single(view.FindAll(".pch-edit"));
        button.Click();
        Assert.Equal(1, regenerations);
    }

    [Fact(DisplayName = "A picked picture waits under the box with its size, and its button removes it")]
    public async Task Attach_PickedPicture()
    {
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.AllowsAttachments, true));
        Assert.Contains("image/png", view.Find(".pch-file-input").GetAttribute("accept"), StringComparison.Ordinal);

        view.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(s_png, "dot.png", contentType: "image/png"));

        view.WaitForAssertion(() => Assert.Single(view.Instance.PendingAttachments));
        view.WaitForAssertion(() => Assert.Equal("dot.png", view.Find(".pch-pending .pch-file-name").TextContent));
        Assert.Equal(new ChatStrings().FormatFileSize(s_png.Length), view.Find(".pch-pending-size").TextContent);
        Assert.Single(view.FindAll(".pch-pending-thumbnail"));

        await view.Find(".pch-remove-attachment").ClickAsync(new());

        view.WaitForAssertion(() => Assert.Empty(view.Instance.PendingAttachments));
        view.WaitForAssertion(() => Assert.Empty(view.FindAll(".pch-pending")));
    }

    [Fact(DisplayName = "A file that is not allowed is not attached, says why, and tells the application")]
    public void Attach_Rejected()
    {
        ChatAttachmentRejectedEventArgs? rejected = null;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.AllowsAttachments, true)
            .Add(v => v.AttachmentRejected, e => rejected = e));

        view.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("echo", "run.sh"));

        view.WaitForAssertion(() => Assert.Equal(ChatAttachmentProblem.TypeNotAllowed, rejected?.Problem));
        Assert.Contains("run.sh", view.Find(".pch-attachment-error").TextContent, StringComparison.Ordinal);
        Assert.Empty(view.Instance.PendingAttachments);
    }

    [Fact(DisplayName = "Files past the limits' count are refused one by one, and the first ones are attached")]
    public void Attach_PastTheCount()
    {
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.AllowsAttachments, true)
            .Add(v => v.AttachmentLimits, new ChatAttachmentLimits { MaxCount = 1 }));

        view.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary(s_png, "a.png", contentType: "image/png"),
            InputFileContent.CreateFromBinary(s_png, "b.png", contentType: "image/png"));

        view.WaitForAssertion(() => Assert.Contains("b.png", view.Find(".pch-attachment-error").TextContent, StringComparison.Ordinal));
        Assert.Equal("a.png", Assert.Single(view.Instance.PendingAttachments).Name);
    }

    [Fact(DisplayName = "A file larger than the limit is refused before it is read in full")]
    public void Attach_TooLarge()
    {
        ChatAttachmentRejectedEventArgs? rejected = null;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.AllowsAttachments, true)
            .Add(v => v.AttachmentLimits, new ChatAttachmentLimits { MaxFileSize = 10 })
            .Add(v => v.AttachmentRejected, e => rejected = e));

        view.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(s_png, "big.png", contentType: "image/png"));

        view.WaitForAssertion(() => Assert.Equal(ChatAttachmentProblem.TooLarge, rejected?.Problem));
    }

    [Fact(DisplayName = "Content of the application's own is attached and sent with the prompt from the box")]
    public async Task Attach_ApplicationContent_SentWithThePrompt()
    {
        PromptSubmittedEventArgs? submitted = null;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.PromptSubmitted, e => submitted = e));
        Assert.Empty(view.FindAll(".pch-attach"));

        using var content = new MemoryStream(s_png);
        Assert.Null(await view.InvokeAsync(() => view.Instance.AddAttachmentAsync("pasted.png", content)));
        view.WaitForAssertion(() => Assert.Single(view.FindAll(".pch-pending-file")));

        await view.Find(".pch-send").ClickAsync(new());

        view.WaitForAssertion(() => Assert.Equal("pasted.png", Assert.Single(submitted!.Attachments).Name));
        view.WaitForAssertion(() => Assert.Empty(view.FindAll(".pch-pending")));
    }

    [Fact(DisplayName = "A disposed view stops watching the keys of its box")]
    public async Task Dispose_StopsWatchingTheBox()
    {
        var view = Render<ChatView>(parameters => parameters.Add(v => v.Conversation, new ChatConversation()));
        // The view calls its script once the script has loaded; the last call it makes then is to watch the prompt box.
        view.WaitForAssertion(() => _module.VerifyInvoke("watchPrompt"));

        await DisposeComponentsAsync();

        _module.VerifyInvoke("unwatchPrompt");
    }

    [Fact(DisplayName = "Esc while idle and not editing changes nothing")]
    public void Escape_Idle_DoesNothing()
    {
        var stops = 0;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.Prompt, "Draft")
            .Add(v => v.StopRequested, () => stops++));

        Bridge().Escape();

        view.WaitForAssertion(() => Assert.Equal("Draft", view.Find(".pch-prompt-box").GetAttribute("value")));
        Assert.Equal(0, stops);
    }

    [Fact(DisplayName = "A parent that binds the text and changes what the user typed holds the text in the box")]
    public void Typing_BoundParentChangesTheText()
    {
        var view = Render<BoundHost>();

        view.Find(".pch-prompt-box").Input("abcdef");
        Assert.Equal("abcd", view.Find(".pch-prompt-box").GetAttribute("value"));
        view.Find(".pch-prompt-box").Input("abcde");

        Assert.Equal("abcd", view.Find(".pch-prompt-box").GetAttribute("value"));
        Assert.Equal("abcd", view.Instance.Text);
    }

    [Fact(DisplayName = "A file the browser can no longer give is refused as unreadable instead of ending the circuit")]
    public async Task Attach_BrowserLostTheFile()
    {
        ChatAttachmentRejectedEventArgs? rejected = null;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.AttachmentRejected, e => rejected = e));

        var problem = await view.InvokeAsync(() => view.Instance.AddAttachmentAsync(new LostFile()));

        Assert.Equal(ChatAttachmentProblem.Unreadable, problem);
        Assert.Equal("lost.png", rejected?.FileName);
    }

    [Fact(DisplayName = "The view marks while a reply is in progress or the last prompt is changed, for the script's Esc")]
    public async Task Escape_Marks()
    {
        var runner = new ChatRunner(new ScriptedChatClient(Text("One")));
        var view = WithRunner(runner);
        Assert.False(view.Find(".pch").HasAttribute("data-generating"));
        await view.InvokeAsync(() => runner.SendAsync("First"));
        view.WaitForAssertion(() => Assert.Single(view.FindAll(".pch-edit")));

        await view.Find(".pch-edit").ClickAsync(new());

        Assert.True(view.Find(".pch").HasAttribute("data-editing"));
        view.Render(parameters => parameters.Add(v => v.IsGenerating, true).Add(v => v.Runner, null));
        view.WaitForAssertion(() => Assert.True(view.Find(".pch").HasAttribute("data-generating")));
    }

    [Fact(DisplayName = "The button sends, and stops while a reply is in progress")]
    public void SendOrStop_OneButton()
    {
        var stops = 0;
        var view = Render<ChatView>(parameters => parameters
            .Add(v => v.Conversation, new ChatConversation())
            .Add(v => v.Strings, new ChatStrings())
            .Add(v => v.StopRequested, () => stops++));
        Assert.Single(view.FindAll("button.pch-send"));

        view.Render(parameters => parameters.Add(v => v.IsGenerating, true));

        Assert.Empty(view.FindAll("button.pch-send"));
        Assert.Equal(new ChatStrings().Stop, view.Find("button.pch-stop").TextContent);
        view.Find("button.pch-stop").Click();
        Assert.Equal(1, stops);
    }

    private sealed class LostFile : IBrowserFile
    {
        public string Name => "lost.png";

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public long Size => 10;

        public string ContentType => "image/png";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new LostStream();
    }

    // Fails as the browser's stream does once the input has been given other files.
    private sealed class LostStream() : MemoryStream([1, 2, 3])
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new JSException("There is no file with ID 0. The file list may have changed.");
    }
}
