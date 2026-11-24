using System.Globalization;
using System.Resources;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// The texts a <see cref="ChatView"/> shows: button labels, status words and the formats of the turn summary. The
/// package ships English and Traditional Chinese; an application changes any of them by assigning its own instance to
/// <see cref="ChatView.Strings"/>.
/// </summary>
/// <remarks>
/// A view reads the strings when its templates are built, so changing a property of an instance the view already shows
/// does not update it. Assign a new instance instead.
/// </remarks>
public class ChatViewStrings
{
    private static readonly ResourceManager s_resources =
        new("Polhem.Chat.Avalonia.Resources.ChatStrings", typeof(ChatViewStrings).Assembly);

    /// <summary>Initializes a new instance of the <see cref="ChatViewStrings"/> class with the English texts.</summary>
    public ChatViewStrings()
        : this(CultureInfo.InvariantCulture)
    {
    }

    private ChatViewStrings(CultureInfo culture)
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
    /// The culture is always passed explicitly. Avalonia resets the culture of the UI thread after dispatcher
    /// operations, so reading <see cref="CultureInfo.CurrentUICulture"/> later could return a different language than the
    /// application chose.
    /// </remarks>
    public static ChatViewStrings ForCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return new ChatViewStrings(culture);
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
