using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Data.Converters;

namespace Polhem.Chat.Avalonia;

/// <summary>The converters the default entry templates use to turn entry values into text.</summary>
internal static class EntryConverters
{
    /// <summary>Converts a <see cref="ToolCallStatus"/> and the view's <see cref="ChatViewStrings"/> to the status word.</summary>
    public static IMultiValueConverter ToolStatus { get; } = new FuncMultiValueConverter<object?, string>(values =>
    {
        var list = values.ToList();
        if (list is not [ToolCallStatus status, ChatViewStrings strings])
        {
            return string.Empty;
        }
        return status switch
        {
            ToolCallStatus.Running => strings.ToolRunning,
            ToolCallStatus.Completed => strings.ToolCompleted,
            ToolCallStatus.Failed => strings.ToolFailed,
            ToolCallStatus.AwaitingApproval => strings.ToolAwaitingApproval,
            ToolCallStatus.Rejected => strings.ToolRejected,
            _ => strings.ToolCanceled,
        };
    });

    /// <summary>Converts the state of a tool call to whether it waits for the user's approval.</summary>
    public static IValueConverter IsAwaitingApproval { get; } =
        new FuncValueConverter<ToolCallStatus, bool>(status => status == ToolCallStatus.AwaitingApproval);

    /// <summary>Converts text from the model to text whose hidden characters show as escapes.</summary>
    public static IValueConverter Visible { get; } = new FuncValueConverter<string?, string>(VisibleText.Escape);

    /// <summary>Converts the arguments of a tool call to indented JSON in full, for the user to approve.</summary>
    public static IValueConverter ApprovalArguments { get; } =
        new FuncValueConverter<IDictionary<string, object?>?, string>(VisibleText.FormatArguments);

    /// <summary>Converts the extent and the viewport of a scroll viewer to whether its content is taller than its area.</summary>
    public static IMultiValueConverter IsOverflowing { get; } = new FuncMultiValueConverter<object?, bool>(values =>
        values.ToList() is [Size extent, Size viewport] && extent.Height > viewport.Height + 1);

    /// <summary>Converts the arguments of a tool call to whether there are any.</summary>
    public static IValueConverter HasArguments { get; } =
        new FuncValueConverter<IDictionary<string, object?>?, bool>(arguments => arguments is { Count: > 0 });

    /// <summary>Converts a <see cref="TurnSummaryEntry"/> and the view's <see cref="ChatViewStrings"/> to one line.</summary>
    public static IMultiValueConverter TurnSummary { get; } = new FuncMultiValueConverter<object?, string>(values =>
    {
        var list = values.ToList();
        return list is [TurnSummaryEntry summary, ChatViewStrings strings] ? DescribeTurn(summary, strings) : string.Empty;
    });

    /// <summary>Converts the arguments of a tool call to one <c>name: value</c> line each.</summary>
    public static IValueConverter ToolArguments { get; } =
        new FuncValueConverter<IDictionary<string, object?>?, string>(arguments =>
        {
            if (arguments is null || arguments.Count == 0)
            {
                return string.Empty;
            }
            var text = new StringBuilder();
            foreach (var (name, value) in arguments)
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }
                text.Append(VisibleText.Escape(name)).Append(": ").Append(FormatValue(value));
            }
            return text.ToString();
        });

    /// <summary>Converts the result of a tool call to text.</summary>
    public static IValueConverter ToolResult { get; } = new FuncValueConverter<object?, string>(FormatValue);

    internal static string DescribeTurn(TurnSummaryEntry summary, ChatViewStrings strings)
    {
        var parts = new List<string> { strings.FormatElapsed(summary.Elapsed) };
        // One request is the usual case and says nothing; the count is worth a place only when a turn took several.
        if (summary.Calls > 1)
        {
            parts.Add(strings.FormatCalls(summary.Calls));
        }
        if (summary.Usage is { } usage && (usage.InputTokenCount is not null || usage.OutputTokenCount is not null))
        {
            parts.Add(usage.CachedInputTokenCount is > 0
                ? strings.FormatCachedTokens(usage.InputTokenCount ?? 0, usage.CachedInputTokenCount.Value, usage.OutputTokenCount ?? 0)
                : strings.FormatTokens(usage.InputTokenCount ?? 0, usage.OutputTokenCount ?? 0));
        }
        if (summary.Outcome == TurnOutcome.Stopped)
        {
            parts.Add(strings.Stopped);
        }
        else if (summary.Outcome == TurnOutcome.Failed)
        {
            parts.Add(strings.TurnFailed);
        }
        return string.Join(" · ", parts);
    }

    // A JSON element, which is what most providers return for arguments and results, formats as its JSON text.
    private static string FormatValue(object? value) => VisibleText.Escape(Convert.ToString(value, CultureInfo.InvariantCulture));
}
