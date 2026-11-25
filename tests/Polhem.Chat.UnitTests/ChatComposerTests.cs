using Microsoft.Extensions.AI;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.UnitTests;

/// <summary>What the user does with a chat view, without any view: submitting, editing, attaching and stopping.</summary>
public class ChatComposerTests
{
    private static Func<Task<Stream>> Open(byte[] content) => () => Task.FromResult<Stream>(new MemoryStream(content));

    private static async Task<ChatComposer> AfterTwoTurns()
    {
        var composer = new ChatComposer { Runner = new ChatRunner(new ScriptedChatClient(Text("Answer"))) };
        composer.Connect();
        await composer.SubmitAsync("First");
        await composer.SubmitAsync("Second");
        return composer;
    }

    [Fact(DisplayName = "A prompt from the box goes through the runner with its files, and the box empties")]
    public async Task Submit_FromBox_SendsWithFilesAndEmptiesBox()
    {
        var client = new ScriptedChatClient(Text("Hi"));
        var composer = new ChatComposer { Runner = new ChatRunner(client), Prompt = "Look" };
        await composer.AddAttachmentAsync("photo.png", AttachmentTests.Png.Length, Open(AttachmentTests.Png));

        await composer.SubmitAsync();

        Assert.Equal(string.Empty, composer.Prompt);
        Assert.Empty(composer.PendingAttachments);
        var prompt = Assert.Single(composer.ShownConversation!.Entries.OfType<UserPromptEntry>());
        Assert.Equal("Look", prompt.Text);
        Assert.Single(prompt.Attachments);
    }

    [Fact(DisplayName = "A prompt passed in, such as a suggestion, leaves the box and its files alone")]
    public async Task Submit_Text_LeavesBoxAlone()
    {
        var composer = new ChatComposer { Runner = new ChatRunner(new ScriptedChatClient(Text("Hi"))), Prompt = "Draft" };
        await composer.AddAttachmentAsync("photo.png", AttachmentTests.Png.Length, Open(AttachmentTests.Png));

        await composer.SubmitAsync("Plan a trip");

        Assert.Equal("Draft", composer.Prompt);
        Assert.Single(composer.PendingAttachments);
        Assert.Empty(composer.ShownConversation!.Entries.OfType<UserPromptEntry>().Single().Attachments);
    }

    [Fact(DisplayName = "Without a runner the application gets the prompt, and canceling it keeps the prompt in the box")]
    public async Task Submit_WithoutRunner_RaisesAndCancels()
    {
        var composer = new ChatComposer { Conversation = new ChatConversation(), Prompt = "Hello" };
        PromptSubmittedEventArgs? submitted = null;
        composer.PromptSubmitted += (_, e) =>
        {
            submitted = e;
            e.Cancel = true;
        };

        await composer.SubmitAsync();

        Assert.Equal("Hello", submitted!.Text);
        Assert.False(submitted.IsEdit);
        Assert.Equal("Hello", composer.Prompt);
    }

    [Fact(DisplayName = "Nothing is submitted while a reply is in progress or when the prompt is blank")]
    public async Task Submit_BlankOrGenerating_DoesNothing()
    {
        var composer = new ChatComposer { Conversation = new ChatConversation(), Prompt = "  " };
        var raised = 0;
        composer.PromptSubmitted += (_, _) => raised++;

        await composer.SubmitAsync();
        composer.Prompt = "Hello";
        composer.IsGenerating = true;
        await composer.SubmitAsync();

        Assert.Equal(0, raised);
        Assert.Equal("Hello", composer.Prompt);
    }

    [Fact(DisplayName = "Editing takes the last prompt into the box, and canceling gives the box back what it held")]
    public async Task Edit_TakesLastPrompt_CancelRestoresBox()
    {
        var composer = await AfterTwoTurns();
        composer.Prompt = "Unsent draft";
        var last = composer.ShownConversation!.LastPrompt!;

        Assert.False(composer.CanEditLastPrompt(composer.ShownConversation.Entries.OfType<UserPromptEntry>().First()));
        composer.EditLastPrompt(last);
        Assert.True(composer.IsEditingPrompt);
        Assert.Equal("Second", composer.Prompt);

        composer.CancelEdit();
        Assert.False(composer.IsEditingPrompt);
        Assert.Equal("Unsent draft", composer.Prompt);
    }

    [Fact(DisplayName = "Submitting an edit replaces the last prompt and its turn")]
    public async Task Edit_Submit_ReplacesLastPrompt()
    {
        var composer = await AfterTwoTurns();
        composer.EditLastPrompt(composer.ShownConversation!.LastPrompt!);
        composer.Prompt = "Second, changed";

        await composer.SubmitAsync();

        Assert.False(composer.IsEditingPrompt);
        Assert.Equal(["First", "Second, changed"], composer.ShownConversation.Entries.OfType<UserPromptEntry>().Select(entry => entry.Text));
    }

    [Fact(DisplayName = "An edit ends when its prompt is no longer the last one, but only while the composer is connected")]
    public async Task Edit_EndsWhenPromptGoes_OnlyWhileConnected()
    {
        var composer = await AfterTwoTurns();
        var conversation = composer.ShownConversation!;
        composer.EditLastPrompt(conversation.LastPrompt!);

        composer.Disconnect();
        conversation.RewindLastPrompt();
        Assert.True(composer.IsEditingPrompt);

        composer.Connect();
        Assert.False(composer.IsEditingPrompt);
    }

    [Fact(DisplayName = "By default the last prompt can be sent again with a runner and not without one")]
    public async Task Regenerate_DefaultsFollowTheRunner()
    {
        var composer = await AfterTwoTurns();
        Assert.True(composer.CanRegenerate());
        Assert.True(composer.CanRegenerate(composer.ShownConversation!.Entries.OfType<AssistantReplyEntry>().Last()));
        Assert.False(composer.CanRegenerate(composer.ShownConversation.Entries.OfType<AssistantReplyEntry>().First()));

        var hostDriven = new ChatComposer { Conversation = composer.ShownConversation };
        var requested = 0;
        hostDriven.RegenerateRequested += (_, _) => requested++;
        Assert.False(hostDriven.CanRegenerate());

        hostDriven.Regenerate();
        Assert.Equal(0, requested);

        hostDriven.AllowsRewind = true;
        Assert.True(hostDriven.CanRegenerate());
        hostDriven.Regenerate();
        Assert.Equal(1, requested);
    }

    [Fact(DisplayName = "A refused file sets the message and raises the event with the cleaned name; an attached one clears the message")]
    public async Task Attach_Refused_ThenAccepted()
    {
        var composer = new ChatComposer { Strings = new ChatStrings() };
        ChatAttachmentRejectedEventArgs? rejected = null;
        composer.AttachmentRejected += (_, e) => rejected = e;

        var problem = await composer.AddAttachmentAsync("/tmp/huge\u202Egnp.exe", 50L * 1024 * 1024, () => throw new InvalidOperationException("Never opened."));

        Assert.Equal(ChatAttachmentProblem.TooLarge, problem);
        Assert.Equal("hugegnp.exe", rejected!.FileName);
        Assert.Contains("\\u{202E}", composer.AttachmentError, StringComparison.Ordinal);

        Assert.Null(await composer.AddAttachmentAsync("photo.png", AttachmentTests.Png.Length, Open(AttachmentTests.Png)));
        Assert.Null(composer.AttachmentError);
    }

    [Fact(DisplayName = "The same refusal twice in a row says so again, so a view that hid the message shows it")]
    public async Task Attach_SameRefusalTwice_NotifiesTwice()
    {
        var composer = new ChatComposer();
        var notified = 0;
        composer.PropertyChanged += (_, e) => notified += e.PropertyName == nameof(ChatComposer.AttachmentError) ? 1 : 0;

        await composer.AddAttachmentAsync("huge.png", 50L * 1024 * 1024, Open(AttachmentTests.Png));
        await composer.AddAttachmentAsync("huge.png", 50L * 1024 * 1024, Open(AttachmentTests.Png));

        Assert.Equal(2, notified);
    }

    [Fact(DisplayName = "A file whose size cannot be read is refused as unreadable without being opened")]
    public async Task Attach_SizeUnreadable_NotOpened()
    {
        var composer = new ChatComposer();
        var opened = false;

        var problem = await composer.AddAttachmentAsync(
            "gone.png",
            () => throw new IOException("The file went away."),
            () =>
            {
                opened = true;
                return Task.FromResult<Stream>(new MemoryStream(AttachmentTests.Png));
            });

        Assert.Equal(ChatAttachmentProblem.Unreadable, problem);
        Assert.False(opened);
        Assert.Empty(composer.PendingAttachments);
    }

    [Fact(DisplayName = "Stopping stops the runner and tells the application")]
    public async Task Stop_StopsRunnerAndRaises()
    {
        var pause = new Pause();
        var composer = new ChatComposer { Runner = new ChatRunner(new ScriptedChatClient(Text("Partial"), pause)) };
        composer.Connect();
        var stopped = 0;
        composer.StopRequested += (_, _) => stopped++;
        var sending = composer.SubmitAsync("Go");
        await pause.Reached.Task;
        Assert.True(composer.IsGenerating);

        composer.Stop();
        await sending;

        Assert.Equal(1, stopped);
        Assert.False(composer.IsGenerating);
        Assert.Equal(TurnOutcome.Stopped, composer.ShownConversation!.Entries.OfType<TurnSummaryEntry>().Single().Outcome);
    }

    [Fact(DisplayName = "A second decision on a call that no longer waits does nothing, as for a double click")]
    public void Decide_Twice_SecondDoesNothing()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Clean up");
        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("r1", new FunctionCallContent("c1", "delete"))]));
        var composer = new ChatComposer { Conversation = conversation };
        var entry = conversation.Entries.OfType<ToolCallEntry>().Single();

        composer.Approve(entry);
        composer.Approve(entry);
        composer.Reject(entry);

        Assert.Equal(ToolCallStatus.Running, entry.Status);
        Assert.False(composer.CanDecide(entry));
    }

    [Fact(DisplayName = "The activity line says the model is thinking until a reply is being written, and nothing when idle")]
    public void DescribeActivity_FollowsTheTurn()
    {
        var conversation = new ChatConversation();
        var composer = new ChatComposer { Conversation = conversation, Strings = new ChatStrings() };
        Assert.Null(composer.DescribeActivity(showsActivity: false));

        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        composer.IsGenerating = true;
        Assert.Equal("Thinking…", composer.DescribeActivity(showsActivity: false));

        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "Hello"));
        Assert.Null(composer.DescribeActivity(showsActivity: false));
    }
}
