using Microsoft.Extensions.AI;

namespace Polhem.Chat.UnitTests;

/// <summary>Entries an application adds to a turn in progress.</summary>
public class TurnAddEntryTests
{
    private sealed class NoteEntry(string text) : ChatEntry
    {
        public string Text { get; } = text;
    }

    [Fact(DisplayName = "An entry added during a turn goes after the reply so far, and later text starts a new reply")]
    public void AddEntry_DuringTurn_EndsTheOpenReply()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "Which one?"));
        var note = new NoteEntry("pick");

        turn.AddEntry(note);
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "Thanks."));

        Assert.Same(note, conversation.Entries[2]);
        var replies = conversation.Entries.OfType<AssistantReplyEntry>().ToList();
        Assert.Equal(["Which one?", "Thanks."], replies.Select(reply => reply.Text));
        Assert.False(replies[0].IsStreaming);
        Assert.Equal(3, conversation.Entries.IndexOf(replies[1]));
    }

    [Fact(DisplayName = "An entry added during a turn is not sent to the model and not saved with the history")]
    public void AddEntry_IsNotInTheHistory()
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Hi");
        var turn = conversation.BeginTurn();
        turn.AddEntry(new NoteEntry("pick"));
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, "Done."));
        turn.Complete();

        var history = conversation.SaveHistory();
        Assert.Equal(2, history.Count);
        Assert.DoesNotContain(history.SelectMany(message => message.Contents), content => content is TextContent { Text: "pick" });
        Assert.Contains(conversation.Entries, entry => entry is NoteEntry);
    }

    [Fact(DisplayName = "Adding an entry to a turn that has ended throws")]
    public void AddEntry_AfterEnd_Throws()
    {
        var conversation = new ChatConversation();
        var turn = conversation.BeginTurn();
        turn.Complete();

        Assert.Throws<InvalidOperationException>(() => turn.AddEntry(new NoteEntry("late")));
        Assert.Throws<ArgumentNullException>(() => conversation.BeginTurn().AddEntry(null!));
    }
}
