using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Polhem.Chat.Blazor;

/// <summary>
/// What the application puts in the view: its own content in the slots around the conversation and the prompt box, and
/// its own templates for a kind of entry or for the calls of one tool, in place of the defaults.
/// </summary>
/// <remarks>
/// <para>
/// The view draws an entry again when that entry changes, when the buttons that send the last prompt again or change it
/// come or go on it, and when the view is given other texts, other Markdown options or another template; not with every
/// render of the parent, so that a long conversation does not draw all its replies again as the user types. A template
/// that reads other state, the application's own or the view's (<see cref="IsEditingPrompt"/>, <see cref="ShowsActivity"/>),
/// is drawn again only then; call <see cref="RedrawEntries"/> when that state changes.
/// </para>
/// <para>
/// Templates are compared with delegate equality: the same method on the same target. A template written in Razor markup
/// or as a method of the parent, which captures only the parent component, equals the one of the previous render. One
/// that captures anything else, such as a loop variable, a local variable or the context of an enclosing component,
/// is new with every render of the parent and draws every entry again each time; keep such a template in a field.
/// </para>
/// <para>
/// A template's markup belongs to the application, so the view's styles do not reach it. The view's resolved theme
/// values are inherited as <c>--_pch-</c> variables (<c>var(--_pch-accent)</c>), which the template may read.
/// </para>
/// </remarks>
public sealed partial class ChatView
{
    // The default comparer of string keys is ordinal, as tool names are compared.
    private readonly Dictionary<string, RenderFragment<ToolCallEntry>> _toolTemplates = [];
    private RenderFragment<UserPromptEntry>? _promptTemplate;
    private RenderFragment<AssistantReplyEntry>? _replyTemplate;
    private RenderFragment<ReasoningEntry>? _reasoningTemplate;
    private RenderFragment<ToolCallEntry>? _toolCallTemplate;
    private RenderFragment<TurnSummaryEntry>? _summaryTemplate;
    private RenderFragment<ErrorEntry>? _errorTemplate;

    /// <summary>Gets or sets the application's content above the conversation. It does not scroll.</summary>
    [Parameter]
    public RenderFragment? HeaderContent { get; set; }

    /// <summary>
    /// Gets or sets what the view shows above the <see cref="Suggestions"/> while the conversation is empty, in place of
    /// <see cref="ChatStrings.EmptyTitle"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? EmptyContent { get; set; }

    /// <summary>Gets or sets the application's content above the prompt box.</summary>
    [Parameter]
    public RenderFragment? PromptHeaderContent { get; set; }

    /// <summary>Gets or sets the application's content below the prompt box.</summary>
    [Parameter]
    public RenderFragment? PromptFooterContent { get; set; }

    /// <summary>Gets or sets the template of the user's prompts, in place of the default bubble.</summary>
    [Parameter]
    public RenderFragment<UserPromptEntry>? PromptTemplate { get; set; }

    /// <summary>Gets or sets the template of the model's replies, in place of the default Markdown.</summary>
    /// <remarks>
    /// WARNING: A reply is the model's text, and a prompt injection can make the model write an image whose address
    /// carries what the conversation holds. A template that renders the reply as Markdown should pass
    /// <see cref="ReplyMarkdownOptions"/>, which load no images unless the application chose otherwise, rather than a
    /// viewer's defaults, which load them.
    /// </remarks>
    [Parameter]
    public RenderFragment<AssistantReplyEntry>? ReplyTemplate { get; set; }

    /// <summary>Gets or sets the template of the model's reasoning, shown while <see cref="ShowsActivity"/> is on.</summary>
    [Parameter]
    public RenderFragment<ReasoningEntry>? ReasoningTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template of the tool calls that have no template of their own in <see cref="ToolCallTemplates"/>,
    /// in place of the default card.
    /// </summary>
    /// <remarks>
    /// A call that waits for the user's approval shows with this template too, while <see cref="ShowsActivity"/> is off
    /// as well, and the view adds its own approval under it: the arguments in full, hidden characters escaped, and the
    /// Approve and Reject buttons. What the user approves is what the tool receives, so a template cannot take that away.
    /// </remarks>
    [Parameter]
    public RenderFragment<ToolCallEntry>? ToolCallTemplate { get; set; }

    /// <summary>
    /// Gets or sets templates for the calls of particular tools, by <see cref="ToolCallEntry.Name"/> compared ordinally,
    /// for example a weather card for a weather tool.
    /// </summary>
    /// <remarks>
    /// A call shown with such a template shows whatever <see cref="ShowsActivity"/> is set to: a tool that draws its own
    /// card is there to be seen. While the call waits for approval the view adds its own approval under the template, as
    /// for <see cref="ToolCallTemplate"/>.
    /// </remarks>
    [Parameter]
    public IReadOnlyDictionary<string, RenderFragment<ToolCallEntry>>? ToolCallTemplates { get; set; }

    /// <summary>Gets or sets the template of the line that ends each turn, with its time and tokens.</summary>
    [Parameter]
    public RenderFragment<TurnSummaryEntry>? SummaryTemplate { get; set; }

    /// <summary>Gets or sets the template of the errors that ended a turn.</summary>
    [Parameter]
    public RenderFragment<ErrorEntry>? ErrorTemplate { get; set; }

    /// <summary>
    /// Gets the Markdown options the default reply template renders with: <see cref="MarkdownOptions"/>, or else options
    /// that load no images. A reply template that renders Markdown passes them on unchanged: changing them changes how the
    /// view's own replies render too.
    /// </summary>
    public Polhem.Markdown.MarkdownOptions ReplyMarkdownOptions => ReplyOptions;

    /// <summary>Draws every entry again, for templates that read state other than their entry's.</summary>
    /// <remarks>Call it on the component's dispatcher, as any member of a component: <c>InvokeAsync</c> from elsewhere.</remarks>
    public void RedrawEntries()
    {
        _revision++;
        StateHasChanged();
    }

    /// <summary>Returns whether a tool call waits for the user's approval in the turn in progress.</summary>
    /// <param name="entry">The tool call.</param>
    /// <returns><see langword="true"/> when <see cref="ApproveToolCall"/> and <see cref="RejectToolCall"/> decide it now.</returns>
    public bool CanDecideToolCall(ToolCallEntry entry) => _composer.CanDecide(entry);

    /// <summary>
    /// Approves a tool call that waits for the user's approval, so the client runs it. Nothing happens when
    /// <see cref="CanDecideToolCall"/> is <see langword="false"/>, as for a second click.
    /// </summary>
    /// <param name="entry">The tool call.</param>
    public void ApproveToolCall(ToolCallEntry entry) => _composer.Approve(entry);

    /// <summary>
    /// Rejects a tool call that waits for the user's approval, so the client does not run it. Nothing happens when
    /// <see cref="CanDecideToolCall"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="entry">The tool call.</param>
    public void RejectToolCall(ToolCallEntry entry) => _composer.Reject(entry);

    /// <summary>Returns whether a reply's prompt can be sent again in place of its turn.</summary>
    /// <param name="reply">The reply, which must be the last one and answer the last prompt.</param>
    /// <returns><see langword="true"/> when <see cref="Regenerate"/> sends it.</returns>
    public bool CanRegenerate(AssistantReplyEntry reply) => _composer.CanRegenerate(reply);

    /// <summary>
    /// Sends the last prompt again in place of its turn, through the <see cref="Runner"/>; without one, raises
    /// <see cref="RegenerateRequested"/>. Nothing happens when the user may not rewind now.
    /// </summary>
    public void Regenerate() => _composer.Regenerate();

    /// <summary>Returns whether a prompt can be taken into the box to be changed.</summary>
    /// <param name="entry">The prompt, which must be the conversation's last.</param>
    /// <returns><see langword="true"/> when <see cref="EditLastPromptAsync"/> takes it.</returns>
    public bool CanEditLastPrompt(UserPromptEntry entry) => _composer.CanEditLastPrompt(entry);

    /// <summary>
    /// Puts the last prompt and its files in the box for the user to change, and moves the keyboard focus to the box;
    /// sending then replaces it and everything after it. Nothing happens when <see cref="CanEditLastPrompt"/> is
    /// <see langword="false"/>.
    /// </summary>
    /// <param name="entry">The last prompt.</param>
    /// <returns>A task that completes once the box has the focus, or at once before the view is interactive.</returns>
    public async Task EditLastPromptAsync(UserPromptEntry entry)
    {
        _composer.EditLastPrompt(entry);
        // NOTE: Before the first interactive render, or once disposed, there is no box in the browser to focus.
        if (_composer.IsEditingPrompt && _module is not null && !_disposed)
        {
            await _promptBox.FocusAsync();
        }
    }

    /// <summary>Stops changing the last prompt and gives the box back what it held before.</summary>
    public void CancelEdit() => _composer.CancelEdit();

    /// <summary>Copies text to the clipboard.</summary>
    /// <param name="text">The text.</param>
    /// <returns>
    /// <see langword="true"/> when the clipboard took it; <see langword="false"/> when the browser refused it, which it
    /// does outside a secure context (HTTPS or localhost), before the view's script has loaded, once the view is disposed
    /// or its circuit is gone.
    /// </returns>
    public async Task<bool> CopyAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_module is null || _disposed)
        {
            return false;
        }
        try
        {
            return await _module.InvokeAsync<bool>("copyText", text);
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
    }

    // The templates change what every entry draws only when they are really other templates; see the remarks above.
    private bool ApplyTemplates()
    {
        var changed = false;
        Take(ref _promptTemplate, PromptTemplate, ref changed);
        Take(ref _replyTemplate, ReplyTemplate, ref changed);
        Take(ref _reasoningTemplate, ReasoningTemplate, ref changed);
        Take(ref _toolCallTemplate, ToolCallTemplate, ref changed);
        Take(ref _summaryTemplate, SummaryTemplate, ref changed);
        Take(ref _errorTemplate, ErrorTemplate, ref changed);
        var tools = ToolCallTemplates ?? ReadOnlyDictionary<string, RenderFragment<ToolCallEntry>>.Empty;
        if (tools.Count != _toolTemplates.Count
            || tools.Any(pair => !_toolTemplates.TryGetValue(pair.Key, out var known) || !Equals(known, pair.Value)))
        {
            _toolTemplates.Clear();
            foreach (var (name, template) in tools)
            {
                _toolTemplates[name] = template;
            }
            changed = true;
        }
        return changed;
    }

    private static void Take<T>(ref T? current, T? given, ref bool changed)
        where T : Delegate
    {
        // NOTE: Delegate equality compares the method and its target, so a template Razor creates again with each render
        // of the parent equals the one before.
        if (!Equals(current, given))
        {
            current = given;
            changed = true;
        }
    }

    private RenderFragment RenderEntry(ChatEntry entry) => entry switch
    {
        UserPromptEntry prompt => _promptTemplate?.Invoke(prompt) ?? RenderPrompt(prompt),
        AssistantReplyEntry reply => _replyTemplate?.Invoke(reply) ?? RenderReply(reply),
        ReasoningEntry reasoning => _reasoningTemplate?.Invoke(reasoning) ?? RenderReasoning(reasoning),
        ToolCallEntry call => (_toolTemplates.GetValueOrDefault(call.Name) ?? _toolCallTemplate) is { } template
            ? RenderToolTemplate(template, call)
            : RenderToolCall(call),
        TurnSummaryEntry summary => _summaryTemplate?.Invoke(summary) ?? RenderSummary(summary),
        ErrorEntry error => _errorTemplate?.Invoke(error) ?? RenderError(error),
        _ => static _ => { },
    };

    private bool HasToolTemplate(ToolCallEntry call) => _toolTemplates.ContainsKey(call.Name);
}
