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
    /// Returns the messages that close every call the user approved that has no result in the messages: the call, as the
    /// client sends it once approved, and a result that says it did not finish. The list is empty when there is none.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: A client that runs tools runs an approved call whose result it does not find in the history. Without a
    /// result, the approval the user gave in one turn would run the call with whatever request comes next. The call goes
    /// in too, because a provider accepts a tool result only after the call it answers.
    /// </remarks>
    public static IReadOnlyList<ChatMessage> CloseApprovedCalls(IEnumerable<ChatMessage> messages)
    {
        var contents = messages.SelectMany(message => message.Contents).ToList();
        var results = contents.OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet(StringComparer.Ordinal);
        var open = contents.OfType<ToolApprovalResponseContent>()
            .Where(response => response.Approved && !results.Contains(response.ToolCall.CallId))
            .Select(response => response.ToolCall)
            .OfType<FunctionCallContent>()
            .DistinctBy(call => call.CallId, StringComparer.Ordinal)
            .ToList();
        if (open.Count == 0)
        {
            return [];
        }
        return
        [
            new ChatMessage(ChatRole.Assistant, [.. open.Select(call => (AIContent)new FunctionCallContent(call.CallId, call.Name, call.Arguments))]),
            new ChatMessage(ChatRole.Tool, [.. open.Select(call => (AIContent)NotFinished(call.CallId))]),
        ];
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
