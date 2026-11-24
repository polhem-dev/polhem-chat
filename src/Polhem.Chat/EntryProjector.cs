using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// Turns message contents into entries of a <see cref="ChatConversation"/>. Consecutive text goes into one reply and
/// consecutive reasoning into one reasoning block; any other entry in between starts a new one.
/// </summary>
internal sealed class EntryProjector(ChatConversation conversation)
{
    private readonly Dictionary<string, ToolCallEntry> _toolCalls = [];
    private ChatEntry? _open;

    public IEnumerable<ToolCallEntry> ToolCalls => _toolCalls.Values;

    public void Reset() => _toolCalls.Clear();

    /// <summary>Ends the current reply or reasoning block, so the next text starts a new one.</summary>
    public void Break()
    {
        switch (_open)
        {
            case AssistantReplyEntry reply:
                reply.IsStreaming = false;
                break;
            case ReasoningEntry reasoning:
                reasoning.IsStreaming = false;
                break;
        }
        _open = null;
    }

    /// <summary>Adds the entries for one content item.</summary>
    /// <returns><see langword="false"/> when the content has no entry of its own (usage, unknown content).</returns>
    public bool Add(AIContent content, bool streaming)
    {
        switch (content)
        {
            case TextContent text:
                AppendReply(text.Text, streaming);
                return true;
            case TextReasoningContent reasoning:
                AppendReasoning(reasoning.Text, streaming);
                return true;
            case FunctionCallContent call:
                AddToolCall(call);
                return true;
            case FunctionResultContent result:
                return CompleteToolCall(result);
            case ErrorContent error:
                AddEntry(new ErrorEntry(error.Message, error.ErrorCode, error.Details));
                return true;
            default:
                return false;
        }
    }

    public void AddEntry(ChatEntry entry)
    {
        Break();
        conversation.Entries.Add(entry);
    }

    private void AppendReply(string? text, bool streaming)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        if (_open is AssistantReplyEntry reply)
        {
            reply.AppendText(text);
            return;
        }
        AddEntry(new AssistantReplyEntry(text, streaming));
        _open = conversation.Entries[^1];
    }

    private void AppendReasoning(string? text, bool streaming)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        if (_open is ReasoningEntry reasoning)
        {
            reasoning.AppendText(text);
            return;
        }
        AddEntry(new ReasoningEntry(text, streaming));
        _open = conversation.Entries[^1];
    }

    private void AddToolCall(FunctionCallContent call)
    {
        if (_toolCalls.TryGetValue(call.CallId, out var existing))
        {
            existing.Arguments = call.Arguments;
            return;
        }
        var entry = new ToolCallEntry(call.CallId, call.Name)
        {
            Arguments = call.Arguments,
        };
        if (conversation.DescribeToolCall?.Invoke(call) is { } title)
        {
            entry.Title = title;
        }
        _toolCalls[call.CallId] = entry;
        AddEntry(entry);
    }

    private bool CompleteToolCall(FunctionResultContent result)
    {
        // A result whose call this conversation never saw has nothing to attach to; the result alone carries no name.
        if (!_toolCalls.TryGetValue(result.CallId, out var entry))
        {
            return false;
        }
        entry.Result = result.Result;
        if (result.Exception is { } exception)
        {
            entry.ErrorMessage = exception.Message;
            entry.Status = ToolCallStatus.Failed;
        }
        else
        {
            entry.Status = ToolCallStatus.Completed;
        }
        Break();
        return true;
    }
}
