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

    public ToolCallEntry? FindToolCall(string callId) => _toolCalls.GetValueOrDefault(callId);

    /// <summary>Forgets an entry that left the conversation, so a call id it had may be used again.</summary>
    public void Forget(ChatEntry entry)
    {
        if (entry is ToolCallEntry call && _toolCalls.TryGetValue(call.CallId, out var known) && ReferenceEquals(known, call))
        {
            _toolCalls.Remove(call.CallId);
        }
    }

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
            case ToolApprovalRequestContent request:
                AddApprovalRequest(request);
                return true;
            case ToolApprovalResponseContent response:
                ApplyApprovalResponse(response);
                return true;
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

    private void AddApprovalRequest(ToolApprovalRequestContent request)
    {
        var entry = CreateRequestEntry(request);
        entry.Status = ToolCallStatus.AwaitingApproval;
        _toolCalls[request.ToolCall.CallId] = entry;
        AddEntry(entry);
    }

    /// <summary>
    /// Shows a request for approval that is rejected as it arrives, without taking the place of the call that first had
    /// its id.
    /// </summary>
    public void AddRejectedRequest(ToolApprovalRequestContent request, string reason)
    {
        var entry = CreateRequestEntry(request);
        entry.RejectionReason = reason;
        entry.Status = ToolCallStatus.Rejected;
        AddEntry(entry);
    }

    // WARNING: Every request gets an entry of its own, with the arguments of the call it asks about, so the card shows
    // what the user approves.
    private ToolCallEntry CreateRequestEntry(ToolApprovalRequestContent request)
    {
        var call = request.ToolCall;
        var (name, arguments) = call switch
        {
            FunctionCallContent function => (function.Name, function.Arguments),
            McpServerToolCallContent mcp => (mcp.ServerName + "/" + mcp.Name, mcp.Arguments),
            _ => (call.GetType().Name, null),
        };
        var entry = new ToolCallEntry(call.CallId, name)
        {
            Arguments = arguments,
            ApprovalRequest = request,
        };
        if (call is FunctionCallContent described && conversation.DescribeToolCall?.Invoke(described) is { } title)
        {
            entry.Title = title;
        }
        return entry;
    }

    /// <summary>Shows the user's decision on a call that needed approval.</summary>
    public void ApplyApprovalResponse(ToolApprovalResponseContent response)
    {
        if (!_toolCalls.TryGetValue(response.ToolCall.CallId, out var entry)
            || entry.Status != ToolCallStatus.AwaitingApproval)
        {
            return;
        }
        if (response.Approved)
        {
            entry.Status = ToolCallStatus.Running;
            return;
        }
        entry.RejectionReason = response.Reason;
        entry.Status = ToolCallStatus.Rejected;
    }

    private bool CompleteToolCall(FunctionResultContent result)
    {
        // A result whose call this conversation never saw has nothing to attach to; the result alone carries no name.
        if (!_toolCalls.TryGetValue(result.CallId, out var entry))
        {
            return false;
        }
        entry.Result = result.Result;
        // NOTE: A client that runs tools answers a rejected call with a result of its own, which says the call was
        // rejected and carries no exception. The entry already knows it was rejected, and keeps saying so.
        if (entry.Status is ToolCallStatus.Rejected or ToolCallStatus.Canceled)
        {
            Break();
            return true;
        }
        if (ApprovalRecords.IsNotFinished(result))
        {
            entry.Status = ToolCallStatus.Canceled;
            Break();
            return true;
        }
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
