using System.Text.Json;
using Microsoft.Extensions.AI;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.UnitTests;

/// <summary>Context sent with a prompt: what the model receives, what the entry shows, the history and the rewinds.</summary>
public class PromptContextTests
{
    private static readonly PromptContext s_selection = new("Selection", "The quick brown fox.");

    [Fact(DisplayName = "Context goes to the model before the prompt, in one text block, and the entry shows the typed text")]
    public async Task Send_WithContext_ModelReceivesItAndEntryShowsTheText()
    {
        var client = new ScriptedChatClient(Text("Answer"));
        var runner = new ChatRunner(client);
        runner.MessageSending += (_, e) => e.Context.Add(s_selection);

        await runner.SendAsync("Shorten it");

        var entry = runner.Conversation.Entries.OfType<UserPromptEntry>().Single();
        Assert.Equal("Shorten it", entry.Text);
        Assert.Equal("Selection", Assert.Single(entry.Context).Label);
        var sent = Assert.Single(client.Requests.Single());
        var block = Assert.IsType<TextContent>(Assert.Single(sent.Contents));
        Assert.Equal("<context label=\"Selection\">\nThe quick brown fox.\n</context>\n\nShorten it", block.Text);
    }

    [Fact(DisplayName = "Without context, the prompt is sent as it was before")]
    public async Task Send_WithoutContext_SendsThePromptUnchanged()
    {
        var client = new ScriptedChatClient(Text("Answer"));
        var runner = new ChatRunner(client);

        await runner.SendAsync("Hello");

        var sent = Assert.Single(client.Requests.Single());
        Assert.Same(runner.Conversation.History[0], sent);
        Assert.Empty(runner.Conversation.Entries.OfType<UserPromptEntry>().Single().Context);
    }

    [Fact(DisplayName = "Context with files goes first in the text block, and the files follow it")]
    public async Task Send_ContextAndFiles_TextBlockThenFiles()
    {
        var client = new ScriptedChatClient(Text("Answer"));
        var runner = new ChatRunner(client);
        var picture = new ChatAttachmentLimits().Check("photo.png", AttachmentTests.Png, []).Attachment!;
        runner.MessageSending += (_, e) =>
        {
            e.Context.Add(s_selection);
            e.Context.Add(new PromptContext("notes.md", "# Notes\n"));
        };

        await runner.SendAsync(string.Empty, [picture], CancellationToken.None);

        var sent = Assert.Single(client.Requests.Single());
        Assert.Collection(
            sent.Contents,
            content => Assert.Equal(
                "<context label=\"Selection\">\nThe quick brown fox.\n</context>\n\n<context label=\"notes.md\">\n# Notes\n</context>",
                Assert.IsType<TextContent>(content).Text),
            content => Assert.Same(picture, content));
        var entry = runner.Conversation.Entries.OfType<UserPromptEntry>().Single();
        Assert.Equal(string.Empty, entry.Text);
        Assert.Equal(["Selection", "notes.md"], entry.Context.Select(context => context.Label));
    }

    [Fact(DisplayName = "A label cannot close the attribute it is written in")]
    public async Task Send_LabelWithQuote_IsEscaped()
    {
        var client = new ScriptedChatClient(Text("Answer"));
        var runner = new ChatRunner(client);
        runner.MessageSending += (_, e) => e.Context.Add(new PromptContext("a\"b<c>&\nd", "x"));

        await runner.SendAsync("Q");

        Assert.StartsWith("<context label=\"a&quot;b&lt;c>&amp; d\">\n", client.Requests[0][0].Text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "The history keeps the context's text as given, with its label, apart from the prompt's text")]
    public void AddUserPrompt_WithContext_KeepsItInTheMessage()
    {
        var conversation = new ChatConversation();

        conversation.AddUserPrompt("Q", [], [s_selection]);

        var message = Assert.Single(conversation.History);
        Assert.Collection(
            message.Contents,
            content =>
            {
                Assert.Equal(s_selection.Text, Assert.IsType<TextContent>(content).Text);
                Assert.Equal("Selection", content.AdditionalProperties![PromptContextRecord.LabelKey]);
            },
            content => Assert.Equal("Q", Assert.IsType<TextContent>(content).Text));
    }

    [Fact(DisplayName = "A history saved as JSON keeps each prompt's context and shows it again when loaded")]
    public async Task SaveHistory_RoundTripThroughJson_RestoresContext()
    {
        var client = new ScriptedChatClient(Text("Answer"));
        var runner = new ChatRunner(client);
        runner.MessageSending += (_, e) => e.Context.Add(s_selection);
        await runner.SendAsync("Shorten it");

        var json = JsonSerializer.Serialize(runner.Conversation.SaveHistory(), AIJsonUtilities.DefaultOptions);
        var loaded = new ChatConversation();
        loaded.LoadHistory(JsonSerializer.Deserialize<List<ChatMessage>>(json, AIJsonUtilities.DefaultOptions)!);

        var entry = loaded.Entries.OfType<UserPromptEntry>().Single();
        Assert.Equal("Shorten it", entry.Text);
        var context = Assert.Single(entry.Context);
        Assert.Equal("Selection", context.Label);
        Assert.Equal(s_selection.Text, context.Text);
        Assert.Equal(
            "<context label=\"Selection\">\nThe quick brown fox.\n</context>\n\nShorten it",
            loaded.CreateRequestMessages()[0].Text);
    }

    [Fact(DisplayName = "A history saved before context existed loads as before")]
    public void LoadHistory_SavedWithoutContext_LoadsAsBefore()
    {
        // A user message as SaveHistory wrote it before context existed, with a text block and no additional properties.
        const string Saved = """
            [
              {"role":"user","contents":[{"$type":"text","text":"Hello"}]},
              {"role":"assistant","contents":[{"$type":"text","text":"Hi"}]}
            ]
            """;
        var conversation = new ChatConversation();

        conversation.LoadHistory(JsonSerializer.Deserialize<List<ChatMessage>>(Saved, AIJsonUtilities.DefaultOptions)!);

        var entry = conversation.Entries.OfType<UserPromptEntry>().Single();
        Assert.Equal("Hello", entry.Text);
        Assert.Empty(entry.Context);
        Assert.Same(conversation.History[0], conversation.CreateRequestMessages()[0]);
    }

    [Fact(DisplayName = "Regenerating sends the context again, and a handler sees it already there")]
    public async Task Regenerate_SendsTheContextAgain()
    {
        var client = new ScriptedChatClient(Text("Answer"));
        var runner = new ChatRunner(client);
        var seen = new List<int>();
        runner.MessageSending += (_, e) =>
        {
            seen.Add(e.Context.Count);
            if (!e.ReplacesLastPrompt)
            {
                e.Context.Add(s_selection);
            }
        };
        await runner.SendAsync("Shorten it");

        await runner.RegenerateAsync(CancellationToken.None);

        Assert.Equal([0, 1], seen);
        Assert.Equal(client.Requests[0][0].Text, client.Requests[1][0].Text);
        var entry = runner.Conversation.Entries.OfType<UserPromptEntry>().Single();
        Assert.Equal("Shorten it", entry.Text);
        Assert.Equal("Selection", Assert.Single(entry.Context).Label);
    }

    [Fact(DisplayName = "Editing the last prompt keeps its context, which a handler may remove")]
    public async Task EditLastPrompt_KeepsTheContextUnlessRemoved()
    {
        var client = new ScriptedChatClient(Text("Answer"));
        var runner = new ChatRunner(client);
        var remove = false;
        runner.MessageSending += (_, e) =>
        {
            if (!e.ReplacesLastPrompt)
            {
                e.Context.Add(s_selection);
            }
            else if (remove)
            {
                e.Context.Clear();
            }
        };
        await runner.SendAsync("Shorten it");

        await runner.EditLastPromptAsync("Translate it", [], CancellationToken.None);
        Assert.Equal(
            "<context label=\"Selection\">\nThe quick brown fox.\n</context>\n\nTranslate it",
            client.Requests[^1][0].Text);

        remove = true;
        await runner.EditLastPromptAsync("Explain it", [], CancellationToken.None);
        Assert.Equal("Explain it", client.Requests[^1][0].Text);
        Assert.Empty(runner.Conversation.Entries.OfType<UserPromptEntry>().Single().Context);
    }

    [Fact(DisplayName = "A context needs a label")]
    public void Constructor_BlankLabel_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PromptContext(" ", "text"));
        Assert.Throws<ArgumentNullException>(() => new PromptContext("Selection", null!));
    }
}
