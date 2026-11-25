using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.UnitTests;

/// <summary>Files attached to a prompt: the checks, the history and the runner.</summary>
public class AttachmentTests
{
    internal static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static readonly byte[] s_jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F'];
    private static readonly byte[] s_gif = Encoding.ASCII.GetBytes("GIF89a\x01\x00\x01\x00");
    private static readonly byte[] s_webp = Encoding.ASCII.GetBytes("RIFF\x10\x00\x00\x00WEBPVP8 ");
    private static readonly byte[] s_pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%...");
    private static readonly byte[] s_exe = Encoding.ASCII.GetBytes("MZ\x90\x00\x03\x00\x00\x00");

    public static TheoryData<string, byte[], string> Pictures => new()
    {
        { "photo.png", Png, ChatAttachmentLimits.Png },
        { "photo.JPG", s_jpeg, ChatAttachmentLimits.Jpeg },
        { "photo.jpeg", s_jpeg, ChatAttachmentLimits.Jpeg },
        { "anim.gif", s_gif, ChatAttachmentLimits.Gif },
        { "photo.webp", s_webp, ChatAttachmentLimits.WebP },
        { "paper.pdf", s_pdf, ChatAttachmentLimits.Pdf },
        { "no-extension", Png, ChatAttachmentLimits.Png },
        { "Scan 2026.11.25", Png, ChatAttachmentLimits.Png },
        { "photo.jfif", s_jpeg, ChatAttachmentLimits.Jpeg },
        { "photo.jpe", s_jpeg, ChatAttachmentLimits.Jpeg },
    };

    [Theory(DisplayName = "Pictures and PDF are allowed by default, with the media type read from their content")]
    [MemberData(nameof(Pictures))]
    public void Check_AllowedTypes_ReadTheType(string fileName, byte[] content, string mediaType)
    {
        var check = new ChatAttachmentLimits().Check(fileName, content, []);

        Assert.True(check.IsAllowed);
        Assert.Null(check.Problem);
        Assert.Equal(mediaType, check.Attachment!.MediaType);
        Assert.Equal(fileName, check.Attachment.Name);
        Assert.Equal(content, check.Attachment.Data.ToArray());
    }

    [Theory(DisplayName = "Only the name of a file goes to the model, never the folders of its path")]
    [InlineData("/Users/someone/Pictures/photo.png")]
    [InlineData(@"C:\Users\someone\Pictures\photo.png")]
    [InlineData("../../photo.png")]
    public void Check_Path_KeepsTheNameOnly(string path)
    {
        var check = new ChatAttachmentLimits().Check(path, Png, []);

        Assert.Equal("photo.png", check.FileName);
        Assert.Equal("photo.png", check.Attachment!.Name);
    }

    [Theory(DisplayName = "A name that says another type than the content is rejected, and so is content of no known type")]
    [InlineData("photo.jpg", "png", ChatAttachmentProblem.ContentMismatch)]
    [InlineData("photo.png", "exe", ChatAttachmentProblem.ContentMismatch)]
    [InlineData("paper.pdf", "exe", ChatAttachmentProblem.ContentMismatch)]
    [InlineData("tool.exe", "exe", ChatAttachmentProblem.TypeNotAllowed)]
    [InlineData("notes.txt", "text", ChatAttachmentProblem.TypeNotAllowed)]
    [InlineData("data.bin", "text", ChatAttachmentProblem.TypeNotAllowed)]
    public void Check_WrongOrUnknownType_IsRejected(string fileName, string kind, ChatAttachmentProblem problem)
    {
        var content = kind switch
        {
            "png" => Png,
            "exe" => s_exe,
            _ => Encoding.UTF8.GetBytes("plain text"),
        };

        var check = new ChatAttachmentLimits().Check(fileName, content, []);

        Assert.False(check.IsAllowed);
        Assert.Null(check.Attachment);
        Assert.Equal(problem, check.Problem);
    }

    [Theory(DisplayName = "A name with an extension the package does not know is rejected, whatever the content")]
    [InlineData("photo.png.exe")]
    [InlineData("photo.png.exe.")]
    [InlineData("photo.png.exe . ")]
    [InlineData("photo.exe.1")]
    [InlineData("photo.png.\U0001D41E\U0001D431\U0001D41E")]
    [InlineData("photo.png.\U0001F480")]
    [InlineData("photo.png.\u0661")]
    [InlineData("photo.exe.\u3000.")]
    [InlineData("photo.heic")]
    [InlineData("invoice\u2024exe")]
    [InlineData("invoice\uFF0Eexe")]
    [InlineData("setup\u3002exe")]
    [InlineData("invoice\u2024exe.1")]
    public void Check_UnknownExtension_IsRejected(string fileName) =>
        Assert.Equal(ChatAttachmentProblem.TypeNotAllowed, new ChatAttachmentLimits().Check(fileName, Png, []).Problem);

    [Fact(DisplayName = "Characters that draw nothing are left out of the name the model reads, and a name of nothing is rejected")]
    public void Check_Name_IsCleaned()
    {
        var limits = new ChatAttachmentLimits();

        Assert.Equal("invoicegnp.png", limits.Check("invoice\u202Egnp.png", Png, []).Attachment!.Name);
        Assert.Equal("a.png", limits.Check("a\u0000.png", Png, []).Attachment!.Name);
        Assert.Equal(ChatAttachmentProblem.Unreadable, limits.Check("folder/", Png, []).Problem);
        Assert.Equal(ChatAttachmentProblem.Unreadable, limits.Check("\u200B", Png, []).Problem);
    }

    [Theory(DisplayName = "A cleaned name stays the same when cleaned again")]
    [InlineData("photo.exe.\u3000.")]
    [InlineData("photo.png\u2003.")]
    [InlineData(" a/b\\c.png . . ")]
    [InlineData("...")]
    public void FileNameOf_IsStable(string path)
    {
        var name = ChatAttachmentLimits.FileNameOf(path);

        Assert.Equal(name, ChatAttachmentLimits.FileNameOf(name));
        Assert.False(name.EndsWith('.') || (name.Length > 0 && char.IsWhiteSpace(name[^1])));
    }

    [Fact(DisplayName = "A picture whose name ends in white space before a dot is still a picture")]
    public void Check_TrailingSpaceBeforeDot_IsAllowed() =>
        Assert.Equal("photo.png", new ChatAttachmentLimits().Check("photo.png\u2003.", Png, []).Attachment!.Name);

    [Fact(DisplayName = "A very long name of dots and digits is checked quickly")]
    public void Check_LongName_IsQuick()
    {
        var name = string.Concat(Enumerable.Repeat("1.", 400_000)) + "png";
        var watch = System.Diagnostics.Stopwatch.StartNew();

        Assert.True(new ChatAttachmentLimits().Check(name, Png, []).IsAllowed);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), watch.Elapsed.ToString());
    }

    [Fact(DisplayName = "By default a file may be 5 MB, the picture limit of common providers, and no larger")]
    public void Defaults_FileSize()
    {
        var limits = new ChatAttachmentLimits();

        Assert.Null(limits.CheckSize(5L * 1024 * 1024, []));
        Assert.Equal(ChatAttachmentProblem.TooLarge, limits.CheckSize((5L * 1024 * 1024) + 1, []));
    }

    [Fact(DisplayName = "Limits as large as a long holds do not overflow")]
    public void Check_HugeLimits()
    {
        var limits = new ChatAttachmentLimits { MaxFileSize = long.MaxValue, MaxTotalSize = long.MaxValue };
        var first = limits.Check("a.png", Png, []).Attachment!;

        Assert.Null(limits.CheckSize(long.MaxValue - 1000, [first]));
        Assert.Equal(ChatAttachmentProblem.TotalTooLarge, limits.CheckSize(long.MaxValue, [first]));
        Assert.True(limits.Check("b.png", Png, [first]).IsAllowed);
    }

    [Fact(DisplayName = "Text types are known but allowed only when added, and must be UTF-8 without NUL")]
    public void Check_TextTypes()
    {
        var limits = new ChatAttachmentLimits();
        limits.AllowedMediaTypes.Add(ChatAttachmentLimits.PlainText);
        limits.AllowedMediaTypes.Add(ChatAttachmentLimits.Csv);

        Assert.Equal(ChatAttachmentLimits.PlainText, limits.Check("notes.txt", Encoding.UTF8.GetBytes("筆記"), []).Attachment!.MediaType);
        Assert.Equal(ChatAttachmentLimits.Csv, limits.Check("data.csv", "a,b\n1,2"u8, []).Attachment!.MediaType);
        Assert.Equal(ChatAttachmentProblem.Unreadable, limits.Check("notes.txt", [0x66, 0xC3, 0x28], []).Problem);
        Assert.Equal(ChatAttachmentProblem.Unreadable, limits.Check("notes.txt", "a\0b"u8, []).Problem);
        Assert.Equal(ChatAttachmentProblem.TypeNotAllowed, limits.Check("readme.md", "# Title"u8, []).Problem);
    }

    [Fact(DisplayName = "An empty file is rejected as unreadable")]
    public void Check_Empty_IsUnreadable() =>
        Assert.Equal(ChatAttachmentProblem.Unreadable, new ChatAttachmentLimits().Check("photo.png", [], []).Problem);

    [Fact(DisplayName = "The size, the count and the total are checked, before reading and after")]
    public void Check_Limits()
    {
        var limits = new ChatAttachmentLimits { MaxFileSize = 100, MaxCount = 2, MaxTotalSize = 150 };
        var first = limits.Check("a.png", Png, []).Attachment!;
        var second = limits.Check("b.png", Png, [first]).Attachment!;

        Assert.Equal(ChatAttachmentProblem.TooLarge, limits.CheckSize(101, []));
        Assert.Null(limits.CheckSize(100, []));
        Assert.Equal(ChatAttachmentProblem.TooMany, limits.CheckSize(1, [first, second]));
        Assert.Equal(ChatAttachmentProblem.TotalTooLarge, limits.CheckSize(150 - Png.Length + 1, [first]));
        Assert.Equal(ChatAttachmentProblem.TooMany, limits.Check("c.png", Png, [first, second]).Problem);
        Assert.Equal(ChatAttachmentProblem.TooLarge, limits.Check("big.png", [.. Png, .. new byte[100]], []).Problem);
    }

    [Fact(DisplayName = "A prompt with attachments goes into the history as one message, text first, and its entry shows them")]
    public void AddUserPrompt_WithAttachments()
    {
        var conversation = new ChatConversation();
        var picture = new ChatAttachmentLimits().Check("photo.png", Png, []).Attachment!;

        var entry = conversation.AddUserPrompt("What is this?", [picture]);

        Assert.Equal([picture], entry.Attachments);
        var message = Assert.Single(conversation.History);
        Assert.IsType<TextContent>(message.Contents[0]);
        Assert.Same(picture, message.Contents[1]);
        Assert.Empty(conversation.AddUserPrompt("Hi").Attachments);
    }

    [Fact(DisplayName = "A prompt may carry attachments without text, but not nothing at all")]
    public void AddUserPrompt_AttachmentsOnly()
    {
        var conversation = new ChatConversation();
        var picture = new ChatAttachmentLimits().Check("photo.png", Png, []).Attachment!;

        var entry = conversation.AddUserPrompt("", [picture]);

        Assert.Equal("", entry.Text);
        Assert.Single(conversation.History[0].Contents);
        Assert.Throws<ArgumentException>(() => conversation.AddUserPrompt("", []));

        // White space alone is no text: a provider may reject a text block of it.
        var spaced = conversation.AddUserPrompt("   ", [picture]);
        Assert.Equal("", spaced.Text);
        Assert.IsType<DataContent>(Assert.Single(conversation.History[^1].Contents));
    }

    [Fact(DisplayName = "Attachments come back from a saved history, with their names and types")]
    public void LoadHistory_ShowsAttachments()
    {
        var conversation = new ChatConversation();
        var picture = new ChatAttachmentLimits().Check("photo.png", Png, []).Attachment!;
        conversation.AddUserPrompt("What is this?", [picture]);
        conversation.AddUserPrompt("", [picture]);
        var contract = (JsonTypeInfo<List<ChatMessage>>)AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(List<ChatMessage>));
        var json = JsonSerializer.Serialize(conversation.SaveHistory().ToList(), contract);

        var loaded = new ChatConversation();
        loaded.LoadHistory(JsonSerializer.Deserialize(json, contract)!);

        var prompts = loaded.Entries.OfType<UserPromptEntry>().ToList();
        Assert.Equal(2, prompts.Count);
        Assert.Equal("photo.png", prompts[0].Attachments.Single().Name);
        Assert.Equal(ChatAttachmentLimits.Png, prompts[1].Attachments.Single().MediaType);
        Assert.Equal(Png, prompts[1].Attachments.Single().Data.ToArray());
    }

    [Fact(DisplayName = "The runner sends the attachments with the prompt, and a sending handler can remove or replace them")]
    public async Task SendAsync_WithAttachments()
    {
        var client = new ScriptedChatClient(Text("A pixel."));
        var runner = new ChatRunner(client);
        var picture = new ChatAttachmentLimits().Check("photo.png", Png, []).Attachment!;

        await runner.SendAsync("What is this?", [picture], CancellationToken.None);
        Assert.Same(picture, client.Requests[0][0].Contents[1]);

        runner.MessageSending += (_, e) =>
        {
            e.Attachments.Clear();
            e.Attachments.Add(new DataContent("hello"u8.ToArray(), ChatAttachmentLimits.PlainText) { Name = "note.txt" });
        };
        await runner.SendAsync("", [picture], CancellationToken.None);
        var sent = client.Requests[1].Last(message => message.Role == ChatRole.User);
        Assert.Equal("note.txt", Assert.IsType<DataContent>(Assert.Single(sent.Contents)).Name);
    }

    [Fact(DisplayName = "The runner refuses a prompt with neither text nor attachments")]
    public async Task SendAsync_Nothing_Throws()
    {
        var runner = new ChatRunner(new ScriptedChatClient(Text("Hi")));

        await Assert.ThrowsAsync<ArgumentException>(() => runner.SendAsync(" ", [], CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => runner.SendAsync(" "));
    }
}
