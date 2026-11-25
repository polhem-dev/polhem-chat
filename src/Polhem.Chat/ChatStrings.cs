using System.Globalization;
using System.Resources;

namespace Polhem.Chat;

/// <summary>
/// The texts a chat view shows: button labels, status words and the formats of the turn summary. The package ships
/// English and Traditional Chinese; an application changes any of them by giving the view its own instance.
/// </summary>
/// <remarks>
/// A view reads the strings when its templates are built, so changing a property of an instance the view already shows
/// does not update it. Give it a new instance instead.
/// </remarks>
public class ChatStrings
{
    private static readonly ResourceManager s_resources =
        new("Polhem.Chat.Resources.ChatStrings", typeof(ChatStrings).Assembly);

    /// <summary>Initializes a new instance of the <see cref="ChatStrings"/> class with the English texts.</summary>
    public ChatStrings()
        : this(CultureInfo.InvariantCulture)
    {
    }

    private ChatStrings(CultureInfo culture)
    {
        Culture = culture;
        PromptPlaceholder = Load(nameof(PromptPlaceholder), culture);
        Send = Load(nameof(Send), culture);
        Stop = Load(nameof(Stop), culture);
        Stopped = Load(nameof(Stopped), culture);
        Thinking = Load(nameof(Thinking), culture);
        Reasoning = Load(nameof(Reasoning), culture);
        Copy = Load(nameof(Copy), culture);
        CopyCode = Load(nameof(CopyCode), culture);
        JumpToLatest = Load(nameof(JumpToLatest), culture);
        EmptyTitle = Load(nameof(EmptyTitle), culture);
        ToolRunning = Load(nameof(ToolRunning), culture);
        ToolCompleted = Load(nameof(ToolCompleted), culture);
        ToolFailed = Load(nameof(ToolFailed), culture);
        ToolCanceled = Load(nameof(ToolCanceled), culture);
        ToolArguments = Load(nameof(ToolArguments), culture);
        ToolResult = Load(nameof(ToolResult), culture);
        TurnFailed = Load(nameof(TurnFailed), culture);
        ElapsedFormat = Load(nameof(ElapsedFormat), culture);
        TokensFormat = Load(nameof(TokensFormat), culture);
        CachedTokensFormat = Load(nameof(CachedTokensFormat), culture);
        CallsFormat = Load(nameof(CallsFormat), culture);
        CallFormat = Load(nameof(CallFormat), culture);
        SecondsFormat = Load(nameof(SecondsFormat), culture);
        MinutesFormat = Load(nameof(MinutesFormat), culture);
        OutputTokensFormat = Load(nameof(OutputTokensFormat), culture);
        Regenerate = Load(nameof(Regenerate), culture);
        EditPrompt = Load(nameof(EditPrompt), culture);
        EditingPrompt = Load(nameof(EditingPrompt), culture);
        CancelEdit = Load(nameof(CancelEdit), culture);
        Attach = Load(nameof(Attach), culture);
        RemoveAttachment = Load(nameof(RemoveAttachment), culture);
        AttachmentTooLargeFormat = Load(nameof(AttachmentTooLargeFormat), culture);
        AttachmentTotalTooLargeFormat = Load(nameof(AttachmentTotalTooLargeFormat), culture);
        AttachmentTooManyFormat = Load(nameof(AttachmentTooManyFormat), culture);
        AttachmentTypeNotAllowedFormat = Load(nameof(AttachmentTypeNotAllowedFormat), culture);
        AttachmentContentMismatchFormat = Load(nameof(AttachmentContentMismatchFormat), culture);
        AttachmentUnreadableFormat = Load(nameof(AttachmentUnreadableFormat), culture);
        KilobytesFormat = Load(nameof(KilobytesFormat), culture);
        MegabytesFormat = Load(nameof(MegabytesFormat), culture);
        Approve = Load(nameof(Approve), culture);
        Reject = Load(nameof(Reject), culture);
        AwaitingApproval = Load(nameof(AwaitingApproval), culture);
        ToolAwaitingApproval = Load(nameof(ToolAwaitingApproval), culture);
        ToolRejected = Load(nameof(ToolRejected), culture);
        ScrollForMore = Load(nameof(ScrollForMore), culture);
    }

    /// <summary>Gets or sets the culture that formats numbers and times, and that the texts were loaded for.</summary>
    public CultureInfo Culture { get; set; }

    /// <summary>Gets or sets the placeholder of the empty prompt box.</summary>
    public string PromptPlaceholder { get; set; }

    /// <summary>Gets or sets the label of the button that sends the prompt.</summary>
    public string Send { get; set; }

    /// <summary>Gets or sets the label of the button that stops the reply in progress.</summary>
    public string Stop { get; set; }

    /// <summary>Gets or sets the mark of a reply or turn that was stopped.</summary>
    public string Stopped { get; set; }

    /// <summary>Gets or sets the text shown while the model has not written anything yet, or is still reasoning.</summary>
    public string Thinking { get; set; }

    /// <summary>Gets or sets the header of a finished reasoning block.</summary>
    public string Reasoning { get; set; }

    /// <summary>Gets or sets the label of the button that copies a reply.</summary>
    public string Copy { get; set; }

    /// <summary>Gets or sets the label of the button that copies a code block inside a reply.</summary>
    public string CopyCode { get; set; }

    /// <summary>Gets or sets the label of the button that scrolls back to the newest entry.</summary>
    public string JumpToLatest { get; set; }

    /// <summary>Gets or sets the text shown while the conversation is empty.</summary>
    public string EmptyTitle { get; set; }

    /// <summary>Gets or sets the state of a tool call that has no result yet.</summary>
    public string ToolRunning { get; set; }

    /// <summary>Gets or sets the state of a tool call that returned a result.</summary>
    public string ToolCompleted { get; set; }

    /// <summary>Gets or sets the state of a tool call that failed.</summary>
    public string ToolFailed { get; set; }

    /// <summary>Gets or sets the state of a tool call whose turn was stopped.</summary>
    public string ToolCanceled { get; set; }

    /// <summary>Gets or sets the state of a tool call that waits for the user's approval.</summary>
    public string ToolAwaitingApproval { get; set; }

    /// <summary>Gets or sets the state of a tool call that was rejected.</summary>
    public string ToolRejected { get; set; }

    /// <summary>Gets or sets the label of the button that approves a tool call.</summary>
    public string Approve { get; set; }

    /// <summary>Gets or sets the label of the button that rejects a tool call.</summary>
    public string Reject { get; set; }

    /// <summary>Gets or sets what the line under the entries says while a tool call waits for the user's approval.</summary>
    public string AwaitingApproval { get; set; }

    /// <summary>
    /// Gets or sets the note under the arguments of a tool call that waits for approval when they are longer than their
    /// area, which tells the user there is more to read before deciding.
    /// </summary>
    public string ScrollForMore { get; set; }

    /// <summary>Gets or sets the label of the button that attaches files, and of the file types the picker offers.</summary>
    public string Attach { get; set; }

    /// <summary>Gets or sets the label of the button that removes an attached file.</summary>
    public string RemoveAttachment { get; set; }

    /// <summary>Gets or sets the composite format of the message for a file larger than the limit. Argument 0 is the file name and argument 1 the limit, formatted with <see cref="FormatFileSize"/>.</summary>
    public string AttachmentTooLargeFormat { get; set; }

    /// <summary>Gets or sets the composite format of the message for a file that would bring the files of a prompt over their limit. Argument 0 is the file name and argument 1 the limit, formatted with <see cref="FormatFileSize"/>.</summary>
    public string AttachmentTotalTooLargeFormat { get; set; }

    /// <summary>Gets or sets the composite format of the message for a file past the count limit. Argument 0 is the file name and argument 1 the limit, as an <see cref="int"/>.</summary>
    public string AttachmentTooManyFormat { get; set; }

    /// <summary>Gets or sets the composite format of the message for a file of a type that is not allowed. Argument 0 is the file name.</summary>
    public string AttachmentTypeNotAllowedFormat { get; set; }

    /// <summary>Gets or sets the composite format of the message for a file whose content is not the type its name says. Argument 0 is the file name.</summary>
    public string AttachmentContentMismatchFormat { get; set; }

    /// <summary>Gets or sets the composite format of the message for a file that could not be read, is empty, or is text that is not UTF-8. Argument 0 is the file name.</summary>
    public string AttachmentUnreadableFormat { get; set; }

    /// <summary>Gets or sets the composite format of a size under a megabyte. Argument 0 is the number of kilobytes, as a <see cref="double"/>.</summary>
    public string KilobytesFormat { get; set; }

    /// <summary>Gets or sets the composite format of a size from a megabyte. Argument 0 is the number of megabytes, as a <see cref="double"/>.</summary>
    public string MegabytesFormat { get; set; }

    /// <summary>Gets or sets the label of the button that sends the last prompt again.</summary>
    public string Regenerate { get; set; }

    /// <summary>Gets or sets the label of the button that puts the last prompt in the box to change it.</summary>
    public string EditPrompt { get; set; }

    /// <summary>Gets or sets the note above the prompt box while it holds the last prompt to change.</summary>
    public string EditingPrompt { get; set; }

    /// <summary>Gets or sets the label of the button that stops changing the last prompt.</summary>
    public string CancelEdit { get; set; }

    /// <summary>Gets or sets the heading of the arguments of a tool call.</summary>
    public string ToolArguments { get; set; }

    /// <summary>Gets or sets the heading of the result of a tool call.</summary>
    public string ToolResult { get; set; }

    /// <summary>Gets or sets the mark of a turn that ended with an error.</summary>
    public string TurnFailed { get; set; }

    /// <summary>
    /// Gets or sets the composite format of the time a turn took. Argument 0 is the number of seconds, as a
    /// <see cref="double"/>.
    /// </summary>
    public string ElapsedFormat { get; set; }

    /// <summary>
    /// Gets or sets the composite format of the tokens a turn used. Argument 0 is the input token count and argument 1
    /// the output token count, as <see cref="long"/> values.
    /// </summary>
    public string TokensFormat { get; set; }

    /// <summary>
    /// Gets or sets the composite format of the tokens a turn used when the provider read part of the input from its
    /// cache. Argument 0 is the input token count, argument 1 the cached part of it and argument 2 the output token
    /// count, as <see cref="long"/> values.
    /// </summary>
    public string CachedTokensFormat { get; set; }

    /// <summary>
    /// Gets or sets the composite format of the requests a turn sent to the model, shown when there was more than one.
    /// Argument 0 is the count, as an <see cref="int"/>.
    /// </summary>
    public string CallsFormat { get; set; }

    /// <summary>
    /// Gets or sets the composite format of the request a turn in progress is on. Argument 0 is the request, as an
    /// <see cref="int"/>.
    /// </summary>
    public string CallFormat { get; set; }

    /// <summary>
    /// Gets or sets the composite format of a running time under a minute. Argument 0 is the whole seconds, as an
    /// <see cref="int"/>.
    /// </summary>
    public string SecondsFormat { get; set; }

    /// <summary>
    /// Gets or sets the composite format of a running time of a minute or more. Argument 0 is the whole minutes and
    /// argument 1 the seconds past them, as <see cref="int"/> values.
    /// </summary>
    public string MinutesFormat { get; set; }

    /// <summary>
    /// Gets or sets the composite format of the output tokens a turn in progress has reported so far, added to the
    /// progress line once there are some. Argument 0 is the count, as a <see cref="long"/>.
    /// </summary>
    public string OutputTokensFormat { get; set; }

    /// <summary>
    /// Returns the texts for a culture: Traditional Chinese for the <c>zh-Hant</c> cultures (<c>zh-TW</c>, <c>zh-HK</c>
    /// and <c>zh-MO</c>), and English otherwise.
    /// </summary>
    /// <param name="culture">The culture to load the texts for.</param>
    /// <returns>A new instance, which the caller may change before assigning it to a view.</returns>
    /// <remarks>
    /// The culture is always passed explicitly. Some UI frameworks reset the culture of the UI thread, Avalonia after its
    /// dispatcher operations for one, so reading <see cref="CultureInfo.CurrentUICulture"/> later could return a
    /// different language than the application chose.
    /// </remarks>
    public static ChatStrings ForCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return new ChatStrings(culture);
    }

    /// <summary>Formats the time a turn took with <see cref="ElapsedFormat"/>.</summary>
    /// <param name="elapsed">The time the turn took.</param>
    /// <returns>The formatted text.</returns>
    public virtual string FormatElapsed(TimeSpan elapsed) =>
        string.Format(Culture, ElapsedFormat, elapsed.TotalSeconds);

    /// <summary>Formats the tokens a turn used with <see cref="TokensFormat"/>.</summary>
    /// <param name="inputTokens">The input token count.</param>
    /// <param name="outputTokens">The output token count.</param>
    /// <returns>The formatted text.</returns>
    public virtual string FormatTokens(long inputTokens, long outputTokens) =>
        string.Format(Culture, TokensFormat, inputTokens, outputTokens);

    /// <summary>Formats the tokens a turn used, part of them cached, with <see cref="CachedTokensFormat"/>.</summary>
    /// <param name="inputTokens">The input token count, the cached ones included.</param>
    /// <param name="cachedInputTokens">The part of the input the provider read from its cache.</param>
    /// <param name="outputTokens">The output token count.</param>
    /// <returns>The formatted text.</returns>
    public virtual string FormatCachedTokens(long inputTokens, long cachedInputTokens, long outputTokens) =>
        string.Format(Culture, CachedTokensFormat, inputTokens, cachedInputTokens, outputTokens);

    /// <summary>
    /// Formats the message for a file that was not attached, with the format of its problem. The file name shows with
    /// the characters that draw nothing escaped.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="problem">Why it was not attached.</param>
    /// <param name="limits">The limits it was checked against.</param>
    /// <returns>The formatted text.</returns>
    public virtual string FormatAttachmentProblem(string fileName, ChatAttachmentProblem problem, ChatAttachmentLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        var name = VisibleText.Escape(fileName);
        return problem switch
        {
            ChatAttachmentProblem.TooLarge => string.Format(Culture, AttachmentTooLargeFormat, name, FormatFileSize(limits.MaxFileSize)),
            ChatAttachmentProblem.TotalTooLarge => string.Format(Culture, AttachmentTotalTooLargeFormat, name, FormatFileSize(limits.MaxTotalSize)),
            ChatAttachmentProblem.TooMany => string.Format(Culture, AttachmentTooManyFormat, name, limits.MaxCount),
            ChatAttachmentProblem.TypeNotAllowed => string.Format(Culture, AttachmentTypeNotAllowedFormat, name),
            ChatAttachmentProblem.ContentMismatch => string.Format(Culture, AttachmentContentMismatchFormat, name),
            _ => string.Format(Culture, AttachmentUnreadableFormat, name),
        };
    }

    /// <summary>Formats a file size with <see cref="KilobytesFormat"/> or <see cref="MegabytesFormat"/>.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The formatted text.</returns>
    public virtual string FormatFileSize(long bytes) => bytes < 1024 * 1024
        ? string.Format(Culture, KilobytesFormat, Math.Max(1, Math.Ceiling(bytes / 1024.0)))
        : string.Format(Culture, MegabytesFormat, bytes / (1024.0 * 1024));

    /// <summary>
    /// Describes a finished turn in one line: its time, the requests it sent when there were several, its tokens when the
    /// provider reported them, and whether it was stopped or failed.
    /// </summary>
    /// <param name="summary">The summary of the turn.</param>
    /// <returns>The formatted text.</returns>
    public virtual string DescribeTurn(TurnSummaryEntry summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var parts = new List<string> { FormatElapsed(summary.Elapsed) };
        // One request is the usual case and says nothing; the count is worth a place only when a turn took several.
        if (summary.Calls > 1)
        {
            parts.Add(FormatCalls(summary.Calls));
        }
        if (summary.Usage is { } usage && (usage.InputTokenCount is not null || usage.OutputTokenCount is not null))
        {
            parts.Add(usage.CachedInputTokenCount is > 0
                ? FormatCachedTokens(usage.InputTokenCount ?? 0, usage.CachedInputTokenCount.Value, usage.OutputTokenCount ?? 0)
                : FormatTokens(usage.InputTokenCount ?? 0, usage.OutputTokenCount ?? 0));
        }
        if (summary.Outcome == TurnOutcome.Stopped)
        {
            parts.Add(Stopped);
        }
        else if (summary.Outcome == TurnOutcome.Failed)
        {
            parts.Add(TurnFailed);
        }
        return string.Join(" · ", parts);
    }

    /// <summary>Returns the word for the state of a tool call, such as <see cref="ToolRunning"/>.</summary>
    /// <param name="status">The state of the call.</param>
    /// <returns>The word.</returns>
    public virtual string DescribeToolStatus(ToolCallStatus status) => status switch
    {
        ToolCallStatus.Running => ToolRunning,
        ToolCallStatus.Completed => ToolCompleted,
        ToolCallStatus.Failed => ToolFailed,
        ToolCallStatus.AwaitingApproval => ToolAwaitingApproval,
        ToolCallStatus.Rejected => ToolRejected,
        _ => ToolCanceled,
    };

    /// <summary>Formats the requests a turn sent with <see cref="CallsFormat"/>.</summary>
    /// <param name="calls">The count.</param>
    /// <returns>The formatted text.</returns>
    public virtual string FormatCalls(int calls) => string.Format(Culture, CallsFormat, calls);

    /// <summary>
    /// Formats a turn in progress: its running time (<see cref="SecondsFormat"/> or <see cref="MinutesFormat"/>), then
    /// the request it is on (<see cref="CallFormat"/>) when it counts them, then <see cref="OutputTokensFormat"/> once
    /// tokens were reported.
    /// </summary>
    /// <param name="calls">The request the turn is on; 0 when it does not count them.</param>
    /// <param name="elapsed">The time since the turn began.</param>
    /// <param name="outputTokens">The output tokens the turn has reported so far; 0 when none.</param>
    /// <returns>The formatted text.</returns>
    public virtual string FormatProgress(int calls, TimeSpan elapsed, long outputTokens)
    {
        var seconds = (int)Math.Max(0, Math.Floor(elapsed.TotalSeconds));
        var parts = new List<string>
        {
            seconds < 60
                ? string.Format(Culture, SecondsFormat, seconds)
                : string.Format(Culture, MinutesFormat, seconds / 60, seconds % 60),
        };
        if (calls > 0)
        {
            parts.Add(string.Format(Culture, CallFormat, calls));
        }
        if (outputTokens > 0)
        {
            parts.Add(string.Format(Culture, OutputTokensFormat, outputTokens));
        }
        return string.Join(" · ", parts);
    }

    private static string Load(string name, CultureInfo culture) =>
        s_resources.GetString(name, culture)
        ?? throw new MissingManifestResourceException($"The string '{name}' is missing from the resources.");
}
