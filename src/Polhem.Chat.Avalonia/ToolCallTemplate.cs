using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// A template for the calls of one tool: the view shows each <see cref="ToolCallEntry"/> whose
/// <see cref="ToolCallEntry.Name"/> is <see cref="ToolName"/> with <see cref="Template"/> instead of the default tool
/// call card, for example a weather card for a weather tool.
/// </summary>
/// <remarks>
/// <para>
/// Register it like any other template, in the view's <see cref="Control.DataTemplates"/> or an ancestor's up to the
/// application. The first template that matches wins, as for every data template.
/// </para>
/// <para>
/// A call shown with such a template shows whatever <see cref="ChatView.ShowsActivity"/> is set to: a tool that draws
/// its own card is there to be seen. While the call waits for approval the template shows it too, so it can offer its
/// own way to decide with <see cref="ChatView.ApproveToolCallCommand"/> and <see cref="ChatView.RejectToolCallCommand"/>;
/// it is then the template's task to show the user everything the tool will receive.
/// </para>
/// </remarks>
public class ToolCallTemplate : IDataTemplate
{
    /// <summary>Gets or sets the name of the tool whose calls this template shows, compared ordinally.</summary>
    public string? ToolName { get; set; }

    /// <summary>Gets or sets the template that builds the view of a call. Its data is the <see cref="ToolCallEntry"/>.</summary>
    [Content]
    public IDataTemplate? Template { get; set; }

    /// <inheritdoc/>
    public bool Match(object? data) =>
        Template is not null && data is ToolCallEntry call && string.Equals(call.Name, ToolName, StringComparison.Ordinal);

    /// <inheritdoc/>
    public Control? Build(object? param) => Template?.Build(param);
}
