using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.Avalonia;

/// <summary>The converters the default entry templates use to turn entry values into text.</summary>
internal static class EntryConverters
{
    /// <summary>Converts a <see cref="ToolCallStatus"/> and the view's <see cref="ChatStrings"/> to the status word.</summary>
    public static IMultiValueConverter ToolStatus { get; } = new FuncMultiValueConverter<object?, string>(values =>
        values.ToList() is [ToolCallStatus status, ChatStrings strings] ? strings.DescribeToolStatus(status) : string.Empty);

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

    /// <summary>Converts a count to whether it is above zero.</summary>
    public static IValueConverter IsPositive { get; } = new FuncValueConverter<int, bool>(count => count > 0);

    /// <summary>Converts an attachment to a small picture of it, or <see langword="null"/> when it is not a picture.</summary>
    public static IValueConverter Thumbnail { get; } = new FuncValueConverter<DataContent?, Bitmap?>(AttachmentPreview.Thumbnail);

    /// <summary>Converts an attachment to whether it is not a picture, which shows a document icon instead.</summary>
    public static IValueConverter IsDocument { get; } =
        new FuncValueConverter<DataContent?, bool>(attachment => attachment is not null && !attachment.HasTopLevelMediaType("image"));

    /// <summary>Converts an attachment and the view's <see cref="ChatStrings"/> to its size.</summary>
    public static IMultiValueConverter FileSize { get; } = new FuncMultiValueConverter<object?, string>(values =>
        values.ToList() is [DataContent attachment, ChatStrings strings] ? strings.FormatFileSize(attachment.Data.Length) : string.Empty);

    /// <summary>Converts the arguments of a tool call to whether there are any.</summary>
    public static IValueConverter HasArguments { get; } =
        new FuncValueConverter<IDictionary<string, object?>?, bool>(arguments => arguments is { Count: > 0 });

    /// <summary>Converts a <see cref="TurnSummaryEntry"/> and the view's <see cref="ChatStrings"/> to one line.</summary>
    public static IMultiValueConverter TurnSummary { get; } = new FuncMultiValueConverter<object?, string>(values =>
        values.ToList() is [TurnSummaryEntry summary, ChatStrings strings] ? strings.DescribeTurn(summary) : string.Empty);

    /// <summary>Converts the arguments of a tool call to one <c>name: value</c> line each.</summary>
    public static IValueConverter ToolArguments { get; } =
        new FuncValueConverter<IDictionary<string, object?>?, string>(VisibleText.ListArguments);

    /// <summary>Converts the result of a tool call to text.</summary>
    public static IValueConverter ToolResult { get; } = new FuncValueConverter<object?, string>(VisibleText.FormatValue);
}
