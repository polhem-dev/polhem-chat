using Avalonia.Controls;
using Microsoft.Extensions.AI;
using Polhem.Chat.UnitTests;
using static Polhem.Chat.Avalonia.UnitTests.ViewHost;
using static Polhem.Chat.UnitTests.ScriptedChatClient;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>Attaching files to the prompt in the view.</summary>
[Collection(HeadlessCollection.Name)]
public class AttachmentViewTests(HeadlessSession session)
{
    private static readonly byte[] s_png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static Func<Task<Stream>> Open(byte[] content) => () => Task.FromResult<Stream>(new MemoryStream(content));

    [Fact(DisplayName = "The attach button shows only when attachments are allowed")]
    public Task AttachButton_FollowsAllowsAttachments() => session.RunAsync(() =>
    {
        var view = new ChatView { Conversation = new ChatConversation() };
        var window = Show(view);
        var button = Part<Button>(view, "PART_AttachButton");
        Assert.False(IsShown(button));
        Assert.False(view.AttachCommand.CanExecute(null));

        view.AllowsAttachments = true;
        Settle(window);

        Assert.True(IsShown(button));
        Assert.True(view.AttachCommand.CanExecute(null));
    });

    [Fact(DisplayName = "An allowed file waits in the box, shows its name and size, and can be removed")]
    public Task AddAttachment_ShowsAndRemoves() => session.RunAsync(async () =>
    {
        var view = new ChatView { Conversation = new ChatConversation(), AllowsAttachments = true, Strings = new ChatViewStrings() };
        var window = Show(view);

        Assert.Null(await view.AddAttachmentAsync("/tmp/photo.png", s_png.Length, Open(s_png)));
        Settle(window);

        var attachment = Assert.Single(view.PendingAttachments);
        Assert.Equal("photo.png", attachment.Name);
        var list = Part<ItemsControl>(view, "PART_Attachments");
        Assert.True(IsShown(list));
        Assert.Contains(Descendants<TextBlock>(list), block => block.Text == "photo.png");
        Assert.Contains(Descendants<TextBlock>(list), block => block.Text == "1 KB");
        Assert.Contains(Descendants<Image>(list), image => image.Source is not null);

        view.RemoveAttachmentCommand.Execute(attachment);
        Settle(window);
        Assert.Empty(view.PendingAttachments);
        Assert.False(IsShown(list));
    });

    [Fact(DisplayName = "A file whose reported size is over the limit is never opened, and the reason shows under the box")]
    public Task AddAttachment_TooLarge_IsNotRead() => session.RunAsync(async () =>
    {
        var view = new ChatView { Conversation = new ChatConversation(), AllowsAttachments = true, Strings = new ChatViewStrings() };
        var window = Show(view);
        var opened = false;
        ChatAttachmentRejectedEventArgs? rejected = null;
        view.AttachmentRejected += (_, e) => rejected = e;

        var problem = await view.AddAttachmentAsync("huge\u202Egnp.exe", 50L * 1024 * 1024, () =>
        {
            opened = true;
            return Task.FromResult<Stream>(new MemoryStream(s_png));
        });
        Settle(window);

        Assert.Equal(ChatAttachmentProblem.TooLarge, problem);
        Assert.False(opened);
        Assert.Equal(ChatAttachmentProblem.TooLarge, rejected!.Problem);
        Assert.Equal("hugegnp.exe", rejected.FileName);
        var error = Part<TextBlock>(view, "PART_AttachmentError");
        Assert.True(IsShown(error));
        Assert.Equal("huge\\u{202E}gnp.exe is larger than 5.0 MB.", error.Text);

        Assert.Null(await view.AddAttachmentAsync("photo.png", null, Open(s_png)));
        Settle(window);
        Assert.False(IsShown(error));
    });

    [Fact(DisplayName = "A file that reports no size is read only up to the limit")]
    public Task AddAttachment_UnknownSize_StopsAtTheLimit() => session.RunAsync(async () =>
    {
        var view = new ChatView { Conversation = new ChatConversation(), AllowsAttachments = true };
        view.AttachmentLimits = new ChatAttachmentLimits { MaxFileSize = 1000 };
        var stream = new CountingStream(new MemoryStream([.. s_png, .. new byte[1_000_000]]));
        Show(view);

        var problem = await view.AddAttachmentAsync("photo.png", null, () => Task.FromResult<Stream>(stream));

        Assert.Equal(ChatAttachmentProblem.TooLarge, problem);
        Assert.True(stream.BytesRead <= 1001, $"read {stream.BytesRead} bytes");
        Assert.Empty(view.PendingAttachments);
    });

    [Fact(DisplayName = "Content from the application's own source is checked like a file, and its stream stays open")]
    public Task AddAttachment_FromStream() => session.RunAsync(async () =>
    {
        var view = new ChatView { Conversation = new ChatConversation(), AllowsAttachments = true };
        Show(view);
        using var pasted = new MemoryStream(s_png);
        using var program = new MemoryStream("MZ\u0090\0"u8.ToArray());

        Assert.Null(await view.AddAttachmentAsync("pasted.png", pasted));
        Assert.Equal(ChatAttachmentProblem.ContentMismatch, await view.AddAttachmentAsync("pasted.png", program));

        Assert.True(pasted.CanRead);
        Assert.Equal("pasted.png", Assert.Single(view.PendingAttachments).Name);
    });

    [Fact(DisplayName = "Submitting sends the attachments with the prompt through the runner and empties the box")]
    public Task Submit_SendsAttachments() => session.RunAsync(async () =>
    {
        var client = new ScriptedChatClient(Text("A pixel."));
        var runner = new ChatRunner(client);
        var view = new ChatView { Runner = runner, AllowsAttachments = true };
        Show(view);
        await view.AddAttachmentAsync("photo.png", s_png.Length, Open(s_png));

        await view.SubmitAsync(null);

        var prompt = runner.Conversation.Entries.OfType<UserPromptEntry>().Single();
        Assert.Equal("", prompt.Text);
        Assert.Equal("photo.png", prompt.Attachments.Single().Name);
        Assert.Empty(view.PendingAttachments);
        Assert.IsType<DataContent>(client.Requests[0][0].Contents.Single());
    });

    [Fact(DisplayName = "A prompt the runner's sending handler cancels gives its text and attachments back to the box")]
    public Task Submit_Canceled_KeepsAttachments() => session.RunAsync(async () =>
    {
        var runner = new ChatRunner(new ScriptedChatClient(Text("Hi")));
        runner.MessageSending += (_, e) => e.Cancel = true;
        var view = new ChatView { Runner = runner, AllowsAttachments = true, Prompt = "What is this?" };
        Show(view);
        await view.AddAttachmentAsync("photo.png", s_png.Length, Open(s_png));

        await view.SubmitAsync(null);

        Assert.Equal("What is this?", view.Prompt);
        Assert.Single(view.PendingAttachments);
    });

    [Fact(DisplayName = "A suggestion is sent alone, leaving the attachments in the box")]
    public Task Suggestion_LeavesAttachments() => session.RunAsync(async () =>
    {
        var runner = new ChatRunner(new ScriptedChatClient(Text("Hi")));
        var view = new ChatView { Runner = runner, AllowsAttachments = true };
        Show(view);
        await view.AddAttachmentAsync("photo.png", s_png.Length, Open(s_png));

        await view.SubmitAsync("Plan a trip");

        Assert.Empty(runner.Conversation.Entries.OfType<UserPromptEntry>().Single().Attachments);
        Assert.Single(view.PendingAttachments);
    });

    [Fact(DisplayName = "A prompt with an attachment shows the picture in the conversation")]
    public Task Prompt_ShowsItsPicture() => session.RunAsync(() =>
    {
        var conversation = new ChatConversation();
        var picture = new ChatAttachmentLimits().Check("photo.png", s_png, []).Attachment!;
        conversation.AddUserPrompt("What is this?", [picture]);
        var view = new ChatView { Conversation = conversation };
        Show(view);

        var entry = EntryView(view, 0);
        Assert.Contains(Descendants<Image>(entry), image => image.Source is not null && IsShown(image));
        Assert.Contains(Descendants<SelectableTextBlock>(entry), block => block.Text == "What is this?");
    });

    [Fact(DisplayName = "A picture that declares more pixels than the limit gets no preview and is never decoded")]
    public Task Thumbnail_HugePicture_IsNotDecoded() => session.RunAsync(() =>
    {
        // A valid PNG whose header says 12000 × 12000: decoding it would take over half a gigabyte.
        var huge = (byte[])s_png.Clone();
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(huge.AsSpan(16), 12000);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(huge.AsSpan(20), 12000);

        Assert.Equal((12000, 12000), ImageHeader.ReadSize(huge));
        Assert.Null(AttachmentPreview.Thumbnail(new DataContent(huge, ChatAttachmentLimits.Png)));
    });

    [Theory(DisplayName = "A long strip gets no preview, and a tall picture is decoded to the preview's height")]
    [InlineData(16, 999_999, false)]
    [InlineData(10, 300, false)]
    [InlineData(100, 1000, true)]
    public Task Thumbnail_TallPictures(int width, int height, bool previewed) => session.RunAsync(() =>
    {
        var tall = (byte[])s_png.Clone();
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(tall.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(tall.AsSpan(20), height);

        // The body is a 1 × 1 picture, so a decode that is allowed fails or yields a small bitmap; neither may be large.
        var thumbnail = AttachmentPreview.Thumbnail(new DataContent(tall, ChatAttachmentLimits.Png));

        if (!previewed)
        {
            Assert.Null(thumbnail);
        }
        Assert.True(thumbnail is null || thumbnail.PixelSize.Height <= AttachmentPreview.ThumbnailWidth);
    });

    [Fact(DisplayName = "The same attachment is decoded once, and a type the package does not check gets no preview")]
    public Task Thumbnail_IsCached() => session.RunAsync(() =>
    {
        var picture = new DataContent(s_png, ChatAttachmentLimits.Png);

        Assert.Same(AttachmentPreview.Thumbnail(picture), AttachmentPreview.Thumbnail(picture));
        Assert.Null(AttachmentPreview.Thumbnail(new DataContent(s_png, "image/bmp")));
    });

    [Theory(DisplayName = "The header gives the size of each picture type")]
    [InlineData("gif", 3, 2)]
    [InlineData("jpeg", 640, 480)]
    [InlineData("webp-vp8x", 300, 200)]
    public void ImageHeader_ReadsEachType(string kind, int width, int height)
    {
        byte[] data = kind switch
        {
            "gif" => [.. "GIF89a"u8, 3, 0, 2, 0, 0, 0, 0],
            "jpeg" => [0xFF, 0xD8, 0xFF, 0xE0, 0, 4, 0, 0, 0xFF, 0xC0, 0, 11, 8, 0x01, 0xE0, 0x02, 0x80, 3, 0, 0],
            _ => [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. "VP8X"u8, 10, 0, 0, 0, 0, 0, 0, 0, 0x2B, 0x01, 0x00, 0xC7, 0x00, 0x00, 0, 0],
        };

        Assert.Equal((width, height), ImageHeader.ReadSize(data));
    }

    [Fact(DisplayName = "A file that fails to open is refused as unreadable, and the next file is still attached")]
    public Task AddAttachment_OpenFails_IsUnreadable() => session.RunAsync(async () =>
    {
        var view = new ChatView { Conversation = new ChatConversation(), AllowsAttachments = true };
        Show(view);

        var problem = await view.AddAttachmentAsync("gone.png", 100, () => throw new InvalidOperationException("The file went away."));

        Assert.Equal(ChatAttachmentProblem.Unreadable, problem);
        Assert.Null(await view.AddAttachmentAsync("photo.png", null, Open(s_png)));
    });

    [Fact(DisplayName = "A limit as large as a long holds still reads files")]
    public Task AddAttachment_HugeLimit_Reads() => session.RunAsync(async () =>
    {
        var view = new ChatView { Conversation = new ChatConversation(), AllowsAttachments = true };
        view.AttachmentLimits = new ChatAttachmentLimits { MaxFileSize = long.MaxValue, MaxTotalSize = long.MaxValue };
        Show(view);

        Assert.Null(await view.AddAttachmentAsync("photo.png", null, Open(s_png)));
    });

    [Fact(DisplayName = "A picture whose body is broken shows no preview, without throwing")]
    public Task Thumbnail_BrokenPicture_IsNull() => session.RunAsync(() =>
    {
        var broken = new DataContent((byte[])[.. s_png[..16], .. new byte[64]], ChatAttachmentLimits.Png);

        Assert.Null(AttachmentPreview.Thumbnail(broken));
        Assert.NotNull(AttachmentPreview.Thumbnail(new DataContent(s_png, ChatAttachmentLimits.Png)));
        Assert.Null(AttachmentPreview.Thumbnail(new DataContent("%PDF-1.7"u8.ToArray(), ChatAttachmentLimits.Pdf)));
    });

    private sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
