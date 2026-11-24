using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.AI;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>The card of a tool call that waits for the user's approval.</summary>
[Collection(HeadlessCollection.Name)]
public class ToolApprovalCardTests(HeadlessSession session)
{
    private static ToolApprovalRequestContent Request(IDictionary<string, object?> arguments) =>
        new("r1", new FunctionCallContent("c1", "delete_file", arguments));

    private static (ChatConversation Conversation, ChatTurn Turn, ChatView View, Window Window) Start(IDictionary<string, object?> arguments)
    {
        var conversation = new ChatConversation();
        conversation.AddUserPrompt("Delete my notes");
        var turn = conversation.BeginTurn();
        var view = new ChatView { Conversation = conversation, Strings = new ChatViewStrings(), IsGenerating = true };
        var window = Show(view);
        turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [Request(arguments)]));
        Settle(window);
        return (conversation, turn, view, window);
    }

    private static Button ButtonOf(Control card, string className) =>
        Descendants<Button>(card).Single(button => button.Classes.Contains(className));

    [Fact(DisplayName = "A call waiting for approval shows its card although the activity is hidden, and the line asks for approval")]
    public Task Pending_ShowsTheCard() => session.RunAsync(() =>
    {
        var (_, turn, view, window) = Start(new Dictionary<string, object?> { ["path"] = "notes.txt" });

        var entries = Part<ItemsControl>(view, "PART_Entries");
        Assert.False(view.ShowsActivity);
        Assert.True(entries.ContainerFromIndex(1)!.IsVisible);
        Assert.Equal("Waiting for your approval…", Part<TextBlock>(view, "PART_Waiting").Text);
        var card = EntryView(view, 1);
        Assert.True(IsShown(ButtonOf(card, "polhemChatApprove")));

        turn.Approve(turn.PendingApprovals[0]);
        Settle(window);
        Assert.False(entries.ContainerFromIndex(1)!.IsVisible);
    });

    [Fact(DisplayName = "The approve button approves the call and the reject button rejects it; neither works once decided")]
    public Task Buttons_DecideTheCall() => session.RunAsync(() =>
    {
        var (_, turn, view, window) = Start(new Dictionary<string, object?> { ["path"] = "notes.txt" });
        var entry = turn.PendingApprovals[0];
        var approve = ButtonOf(EntryView(view, 1), "polhemChatApprove");
        var reject = ButtonOf(EntryView(view, 1), "polhemChatReject");
        Assert.True(approve.IsEffectivelyEnabled);

        reject.Command!.Execute(reject.CommandParameter);
        Settle(window);

        Assert.Equal(ToolCallStatus.Rejected, entry.Status);
        Assert.False(view.ApproveToolCallCommand.CanExecute(entry));
        view.ApproveToolCallCommand.Execute(entry);
        Assert.Equal(ToolCallStatus.Rejected, entry.Status);
    });

    [Fact(DisplayName = "Long arguments show in full in a scrolling area, with a note that there is more to read")]
    public Task LongArguments_ShowInFull() => session.RunAsync(() =>
    {
        var body = string.Join("\n", Enumerable.Range(1, 300).Select(line => "line " + line));
        var (_, _, view, _) = Start(new Dictionary<string, object?> { ["content"] = body, ["path"] = "notes.txt" });
        var card = EntryView(view, 1);

        var text = Descendants<SelectableTextBlock>(card).Single(block => block.FontFamily.ToString().Contains("Menlo")).Text!;
        Assert.Contains("line 300", text);
        Assert.Contains("\"path\": \"notes.txt\"", text);
        var scroller = Descendants<ScrollViewer>(card).Single(viewer => viewer.Classes.Contains("polhemChatApprovalArguments"));
        Assert.True(scroller.Bounds.Height <= 240);
        var note = Descendants<TextBlock>(card).Single(block => block.Classes.Contains("polhemChatScrollForMore"));
        Assert.True(IsShown(note));
        Assert.Equal(new ChatViewStrings().ScrollForMore, note.Text);
    });

    [Fact(DisplayName = "Short arguments carry no note")]
    public Task ShortArguments_HaveNoNote() => session.RunAsync(() =>
    {
        var (_, _, view, _) = Start(new Dictionary<string, object?> { ["path"] = "notes.txt" });

        var note = Descendants<TextBlock>(EntryView(view, 1)).Single(block => block.Classes.Contains("polhemChatScrollForMore"));
        Assert.False(IsShown(note));
    });

    [Fact(DisplayName = "Characters that hide or reorder text show as escapes in the arguments and the title; other scripts show as they are")]
    public Task HiddenCharacters_ShowAsEscapes() => session.RunAsync(() =>
    {
        var (_, turn, view, _) = Start(new Dictionary<string, object?> { ["path"] = "notes\u202Etxt.exe", ["note"] = "刪除\u200B筆記" });
        var card = EntryView(view, 1);

        var text = Descendants<SelectableTextBlock>(card).Single(block => block.FontFamily.ToString().Contains("Menlo")).Text!;
        Assert.Contains("notes\\u{202E}txt.exe", text);
        Assert.Contains("刪除\\u{200B}筆記", text);
        Assert.DoesNotContain('\u202E', text);
        Assert.Contains("\n", text);

        turn.PendingApprovals[0].Title = "delete\u2066_file";
        Assert.Contains(Descendants<TextBlock>(card), block => block.Text == "delete\\u{2066}_file");
    });

    [Fact(DisplayName = "Esc while a call waits for approval stops the turn the application runs, which rejects the call")]
    public Task Escape_StopsAndRejects() => session.RunAsync(() =>
    {
        var (_, turn, view, window) = Start(new Dictionary<string, object?> { ["path"] = "notes.txt" });
        var entry = turn.PendingApprovals[0];
        view.StopRequested += (_, _) => turn.Stop();
        FocusPrompt(view);

        Press(window, Key.Escape);

        Assert.Equal(ToolCallStatus.Canceled, entry.Status);
        Assert.True(turn.IsEnded);
    });
}
