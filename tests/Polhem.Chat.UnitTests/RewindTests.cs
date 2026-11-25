using Microsoft.Extensions.AI;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.UnitTests;

/// <summary>Sending the last prompt again, as it was or changed.</summary>
public class RewindTests
{
    private sealed class NoteEntry : ChatEntry;

    private static async Task<(ChatRunner Runner, ScriptedChatClient Client)> TwoTurns()
    {
        var client = new ScriptedChatClient(Text("Answer"));
        var runner = new ChatRunner(client);
        await runner.SendAsync("First");
        await runner.SendAsync("Second");
        return (runner, client);
    }

    [Fact(DisplayName = "Rewinding removes the last prompt and everything after it, entries of the application's own included")]
    public async Task Rewind_RemovesTheLastTurn()
    {
        var (runner, _) = await TwoTurns();
        var conversation = runner.Conversation;
        conversation.Entries.Add(new NoteEntry());
        var historyBefore = conversation.History.Count;

        var removed = conversation.RewindLastPrompt();

        Assert.Equal("Second", removed!.Text);
        Assert.Equal(historyBefore / 2, conversation.History.Count);
        Assert.Equal("First", conversation.Entries.OfType<UserPromptEntry>().Single().Text);
        Assert.IsType<TurnSummaryEntry>(conversation.Entries[^1]);
        Assert.DoesNotContain(conversation.Entries, entry => entry is NoteEntry);
    }

    [Fact(DisplayName = "Rewinding an empty conversation returns nothing, and rewinding during a turn throws")]
    public void Rewind_EmptyOrDuringTurn()
    {
        var conversation = new ChatConversation();
        Assert.Null(conversation.RewindLastPrompt());

        conversation.AddUserPrompt("Hi");
        conversation.BeginTurn();
        Assert.Throws<InvalidOperationException>(() => conversation.RewindLastPrompt());
    }

    [Fact(DisplayName = "A message that only answers approval requests is not the prompt that is rewound")]
    public async Task Rewind_SkipsApprovalAnswers()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Delete my notes");
        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("ficc_c1", new FunctionCallContent("c1", "delete"))]));
        var waiting = turn.WaitForApprovalsAsync();
        turn.Reject(turn.PendingApprovals[0]);
        await waiting;
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "OK."));
        turn.Complete();

        var removed = conversation.RewindLastPrompt();

        Assert.Equal("Delete my notes", removed!.Text);
        Assert.Empty(conversation.History);
        Assert.Empty(conversation.Entries);
    }

    [Fact(DisplayName = "After rewinding, a call id of the removed turn may be used again")]
    public void Rewind_ForgetsTheCallIds()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Read it");
        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("c1", "read")]));
        turn.Complete();
        conversation.RewindLastPrompt();

        conversation.AddUserPrompt("Delete it");
        var next = conversation.BeginTurn();
        next.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new ToolApprovalRequestContent("ficc_c1", new FunctionCallContent("c1", "delete"))]));

        Assert.Single(next.PendingApprovals);
    }

    [Fact(DisplayName = "A prompt entry the application added itself is not the prompt rewound, and the two lists stay in step")]
    public async Task Rewind_ApplicationPromptEntry_IsNotAPrompt()
    {
        var (runner, _) = await TwoTurns();
        var conversation = runner.Conversation;
        conversation.Entries.Add(new UserPromptEntry("added by the application"));

        var removed = conversation.RewindLastPrompt();

        Assert.Equal("Second", removed!.Text);
        Assert.Equal(["First"], conversation.Entries.OfType<UserPromptEntry>().Select(p => p.Text));
        Assert.Equal("Answer", conversation.History[^1].Text);
    }

    [Fact(DisplayName = "An empty prompt added with AddUserPrompt is still the prompt rewound")]
    public void Rewind_EmptyPrompt()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("A");
        conversation.BeginTurn().Complete();
        conversation.AddUserPrompt("");

        Assert.Equal("", conversation.RewindLastPrompt()!.Text);
        Assert.Equal(["A"], conversation.Entries.OfType<UserPromptEntry>().Select(p => p.Text));
        Assert.Equal(2, conversation.History.Count);
    }

    [Fact(DisplayName = "A sending handler can tell a prompt that replaces the last one, and does not add to its text twice")]
    public async Task Regenerate_MarksTheSending()
    {
        var client = new ScriptedChatClient(Text("Answer"));
        var runner = new ChatRunner(client);
        runner.MessageSending += (_, e) =>
        {
            if (!e.ReplacesLastPrompt)
            {
                e.Text = "[ctx] " + e.Text;
            }
        };
        await runner.SendAsync("Q");

        await runner.RegenerateAsync(CancellationToken.None);

        Assert.Equal("[ctx] Q", runner.Conversation.Entries.OfType<UserPromptEntry>().Single().Text);
        Assert.Equal("[ctx] Q", client.Requests[^1][^1].Text);
    }

    [Fact(DisplayName = "Regenerating sends the last prompt again, with its attachments, in place of its turn")]
    public async Task Regenerate_SendsTheSamePrompt()
    {
        var client = new ScriptedChatClient(Text("Answer"));
        var runner = new ChatRunner(client);
        var picture = new ChatAttachmentLimits().Check("photo.png", AttachmentTests.Png, []).Attachment!;
        await runner.SendAsync("First");
        await runner.SendAsync("What is this?", [picture], CancellationToken.None);

        var result = await runner.RegenerateAsync(CancellationToken.None);

        Assert.Equal(TurnOutcome.Completed, result!.Summary.Outcome);
        Assert.Equal(["First", "What is this?"], runner.Conversation.Entries.OfType<UserPromptEntry>().Select(p => p.Text));
        Assert.Equal(client.Requests[1].Select(m => m.Text), client.Requests[2].Select(m => m.Text));
        Assert.Same(picture, client.Requests[2][^1].Contents.OfType<DataContent>().Single());
        Assert.Equal(2, runner.Conversation.Entries.OfType<TurnSummaryEntry>().Count());
    }

    [Fact(DisplayName = "Editing sends the changed prompt in place of the last one")]
    public async Task Edit_ReplacesTheLastPrompt()
    {
        var (runner, client) = await TwoTurns();

        await runner.EditLastPromptAsync("Second, changed", [], CancellationToken.None);

        Assert.Equal(["First", "Second, changed"], runner.Conversation.Entries.OfType<UserPromptEntry>().Select(p => p.Text));
        Assert.Equal(["First", "Answer", "Second, changed"], client.Requests[^1].Select(m => m.Text));
    }

    [Fact(DisplayName = "A sending handler that cancels a regenerated or edited prompt leaves the conversation as it was")]
    public async Task Canceled_LeavesTheConversation()
    {
        var (runner, client) = await TwoTurns();
        var entries = runner.Conversation.Entries.ToList();
        var history = runner.Conversation.History.ToList();
        var seen = new List<string>();
        runner.MessageSending += (_, e) =>
        {
            seen.Add(e.Text);
            e.Cancel = true;
        };

        Assert.Null(await runner.RegenerateAsync(CancellationToken.None));
        Assert.Null(await runner.EditLastPromptAsync("Changed", [], CancellationToken.None));

        Assert.Equal(["Second", "Changed"], seen);
        Assert.Equal(entries, runner.Conversation.Entries);
        Assert.Equal(history, runner.Conversation.History);
        Assert.Equal(2, client.Requests.Count);
    }

    [Fact(DisplayName = "With no prompt, regenerating and editing send nothing")]
    public async Task NothingToReplace_SendsNothing()
    {
        var client = new ScriptedChatClient(Text("Answer"));
        var runner = new ChatRunner(client);

        Assert.Null(await runner.RegenerateAsync(CancellationToken.None));
        Assert.Null(await runner.EditLastPromptAsync("Hi", [], CancellationToken.None));
        Assert.Empty(client.Requests);
    }
}
