using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace ChatGallery;

/// <summary>
/// An <see cref="IChatClient"/> that plays a scripted reply instead of calling a model, so the gallery shows every kind
/// of entry without a key: reasoning, a tool call and its result, a Markdown reply streamed in small pieces, and the
/// token counts. A prompt that mentions an error plays a failing tool and a lost connection; one that asks for a long
/// reply streams slowly, to try stopping and scrolling. A prompt in Chinese gets a reply in Chinese.
/// </summary>
public sealed class DemoChatClient : IChatClient
{
    private const int ChunkLength = 6;
    private static readonly TimeSpan s_chunkDelay = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan s_slowChunkDelay = TimeSpan.FromMilliseconds(120);
    private int _callCount;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? "";
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
        yield return new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent(callId, DemoReplies.WeatherTool, arguments)]);

        // NOTE: The tool "runs" for a moment, so the card shows its running state before the result arrives.
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        var result = failing
            ? new FunctionResultContent(callId, null) { Exception = new TimeoutException(replies.ToolError) }
            : new FunctionResultContent(callId, replies.ToolResult);
        yield return new ChatResponseUpdate(ChatRole.Tool, [result]);

        var shown = 0;
        foreach (var chunk in Chunks(replies.Answer))
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

    private static IEnumerable<string> Chunks(string text)
    {
        for (var start = 0; start < text.Length; start += ChunkLength)
        {
            yield return text.Substring(start, Math.Min(ChunkLength, text.Length - start));
        }
    }
}
