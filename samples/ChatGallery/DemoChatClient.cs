using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace ChatGallery;

/// <summary>
/// An <see cref="IChatClient"/> that plays a scripted reply instead of calling a model, so the gallery shows every kind
/// of entry without a key: reasoning, a tool call and its result, a Markdown reply streamed in small pieces, and the
/// token counts. A prompt that mentions an error plays a failing tool and a lost connection; one that asks for a long
/// reply streams slowly, to try stopping and scrolling; one that asks to delete something calls
/// <see cref="DemoTools.DeleteNotesTool"/>, which needs the user's approval. A prompt in Chinese gets a reply in Chinese.
/// </summary>
public sealed class DemoChatClient : IChatClient
{
    private const int ChunkLength = 6;
    private static readonly TimeSpan s_chunkDelay = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan s_slowChunkDelay = TimeSpan.FromMilliseconds(120);
    private int _callCount;
    private int _replyCount;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        var prompt = list.LastOrDefault(message => message.Role == ChatRole.User && message.Text.Length > 0)?.Text ?? "";
        var chinese = DemoReplies.IsChinese(prompt);

        if (ShortReply(list, chinese) is { } shortReply)
        {
            foreach (var chunk in Chunks(shortReply))
            {
                await Task.Delay(s_chunkDelay, cancellationToken);
                yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
            }
            yield break;
        }
        if (DemoTools.AsksToDelete(prompt))
        {
            await Task.Delay(s_chunkDelay * 10, cancellationToken);
            var deleteId = "call-" + Interlocked.Increment(ref _callCount).ToString(CultureInfo.InvariantCulture);
            yield return new ChatResponseUpdate(ChatRole.Assistant,
                [new FunctionCallContent(deleteId, DemoTools.DeleteNotesTool, DemoTools.Arguments(chinese))]);
            yield break;
        }

        var replies = DemoReplies.For(prompt);
        var callId = "call-" + Interlocked.Increment(ref _callCount).ToString(CultureInfo.InvariantCulture);
        var failing = DemoReplies.AsksForError(prompt);
        var delay = DemoReplies.AsksForLongReply(prompt) ? s_slowChunkDelay : s_chunkDelay;

        foreach (var chunk in Chunks(replies.Reasoning))
        {
            await Task.Delay(delay, cancellationToken);
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent(chunk)]);
        }

        await Task.Delay(delay * 10, cancellationToken);
        var arguments = new Dictionary<string, object?> { ["city"] = replies.City, ["days"] = 3 };
        // The scripted model plays the weather call and its result itself, so a client that runs tools leaves it alone.
        yield return new ChatResponseUpdate(ChatRole.Assistant,
            [new FunctionCallContent(callId, DemoReplies.WeatherTool, arguments) { InformationalOnly = true }]);

        // NOTE: The tool "runs" for a moment, so the card shows its running state before the result arrives.
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        var result = failing
            ? new FunctionResultContent(callId, null) { Exception = new TimeoutException(replies.ToolError) }
            : new FunctionResultContent(callId, replies.ToolResult);
        yield return new ChatResponseUpdate(ChatRole.Tool, [result]);

        // Each reply opens with another line, so a regenerated reply shows it is new.
        var opening = Openings(chinese)[Interlocked.Increment(ref _replyCount) % 3];
        var shown = 0;
        foreach (var chunk in Chunks(opening + "\n\n" + replies.Answer))
        {
            await Task.Delay(delay, cancellationToken);
            shown += chunk.Length;
            if (failing && shown > replies.Answer.Length / 3)
            {
                throw new HttpRequestException(replies.ConnectionLost);
            }
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
        }

        var usage = new UsageDetails
        {
            InputTokenCount = 120 + (prompt.Length * 2),
            OutputTokenCount = (replies.Reasoning.Length + replies.Answer.Length) / 4,
        };
        usage.TotalTokenCount = usage.InputTokenCount + usage.OutputTokenCount;
        yield return new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(usage)]);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    /// <summary>
    /// The reply to the result of the delete call, which the client that runs tools sends back, or to a prompt with
    /// files, of which a scripted model can only say what arrived; <see langword="null"/> for any other request.
    /// </summary>
    private static string? ShortReply(List<ChatMessage> messages, bool chinese)
    {
        if (messages.LastOrDefault() is { } last && last.Role == ChatRole.Tool
            && last.Contents.OfType<FunctionResultContent>().FirstOrDefault() is { } deleteResult)
        {
            return DemoTools.After(deleteResult, chinese);
        }
        if (messages.LastOrDefault(message => message.Role == ChatRole.User) is not { } asked
            || asked.Contents.OfType<DataContent>().ToList() is not { Count: > 0 } files)
        {
            return null;
        }
        var lines = files.Select(file => $"- **{file.Name}**: {file.MediaType}, {file.Data.Length:N0} bytes");
        return (chinese ? "收到 " + files.Count + " 個檔案：\n\n" : $"I received {files.Count} file(s):\n\n")
            + string.Join("\n", lines);
    }

    private static string[] Openings(bool chinese) => chinese
        ? ["好的，來看看。", "沒問題，這是我的建議。", "我重新整理了一次。"]
        : ["Sure, let's see.", "Here is what I suggest.", "Let me put it another way."];

    private static IEnumerable<string> Chunks(string text)
    {
        for (var start = 0; start < text.Length; start += ChunkLength)
        {
            yield return text.Substring(start, Math.Min(ChunkLength, text.Length - start));
        }
    }
}
