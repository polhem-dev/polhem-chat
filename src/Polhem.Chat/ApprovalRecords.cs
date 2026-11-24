using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>What the history must hold so that a later request never runs a call the user did not approve for it.</summary>
internal static class ApprovalRecords
{
    /// <summary>The additional property that marks a result this package wrote for a call that did not finish.</summary>
    public const string NotFinishedKey = "polhem.chat.notFinished";

    public const string NotFinishedResult =
        "The turn ended before the result of this call arrived. The call may not have run; do not assume it did.";

    /// <summary>
    /// Returns the messages that close every call the user approved that has no result in the messages: a result that
    /// says it did not finish, and the call itself before it when the history does not hold it yet. The list is empty
    /// when there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IMPORTANT: A client that runs tools runs an approved call whose result it does not find in the history. Without a
    /// result, the approval the user gave in one turn would run the call with whatever request comes next.
    /// </para>
    /// <para>
    /// A provider accepts a tool result only right after the call it answers, and rejects a call id twice. The client
    /// sends the approved calls again before it runs them, so a call it already sent gets only its result, which comes
    /// first, right after the history; the calls it never sent come next, each with its result.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ChatMessage> CloseApprovedCalls(IEnumerable<ChatMessage> messages)
    {
        var contents = messages.SelectMany(message => message.Contents).ToList();
        var results = contents.OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet(StringComparer.Ordinal);
        var sent = contents.OfType<FunctionCallContent>().Select(call => call.CallId).ToHashSet(StringComparer.Ordinal);
        var open = contents.OfType<ToolApprovalResponseContent>()
            .Where(response => response.Approved && !results.Contains(response.ToolCall.CallId))
            .Select(response => response.ToolCall)
            .OfType<FunctionCallContent>()
            .DistinctBy(call => call.CallId, StringComparer.Ordinal)
            .ToList();
        var closing = new List<ChatMessage>();
        var alreadySent = open.Where(call => sent.Contains(call.CallId)).ToList();
        if (alreadySent.Count > 0)
        {
            closing.Add(new ChatMessage(ChatRole.Tool, [.. alreadySent.Select(call => (AIContent)NotFinished(call.CallId))]));
        }
        var neverSent = open.Where(call => !sent.Contains(call.CallId)).ToList();
        if (neverSent.Count > 0)
        {
            closing.Add(new ChatMessage(ChatRole.Assistant, [.. neverSent.Select(call => (AIContent)new FunctionCallContent(call.CallId, call.Name, call.Arguments))]));
            closing.Add(new ChatMessage(ChatRole.Tool, [.. neverSent.Select(call => (AIContent)NotFinished(call.CallId))]));
        }
        return closing;
    }

    /// <summary>Gets a value indicating whether a result is one this package wrote for a call that did not finish.</summary>
    public static bool IsNotFinished(FunctionResultContent result) =>
        result.AdditionalProperties?.ContainsKey(NotFinishedKey) == true;

    private static FunctionResultContent NotFinished(string callId) =>
        new(callId, NotFinishedResult)
        {
            AdditionalProperties = new AdditionalPropertiesDictionary { [NotFinishedKey] = true },
        };
}
