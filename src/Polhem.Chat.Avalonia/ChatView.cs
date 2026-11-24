using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// Shows a <see cref="ChatConversation"/> and takes the user's prompts: replies stream in as Markdown, reasoning and tool
/// calls show as collapsible cards, and each turn ends with its time and token counts.
/// </summary>
/// <remarks>
/// <para>
/// The view works in one of two ways. Give it a <see cref="Runner"/>, and it sends each prompt through the runner and
/// stops it on request. Or give it only a <see cref="Conversation"/> that the application drives itself: the view then
/// shows the entries, raises <see cref="PromptSubmitted"/> and <see cref="StopRequested"/>, and the application sets
/// <see cref="IsGenerating"/> while a reply is in progress.
/// </para>
/// <para>
/// The look comes from <see cref="ChatTheme"/>, which the application adds to its styles after its base theme. Each
/// entry type has a default template. An application shows its own entry types, or replaces a default, by adding a
/// <see cref="IDataTemplate"/> to the view's <see cref="Control.DataTemplates"/> or to any ancestor's.
/// </para>
/// </remarks>
[TemplatePart(ScrollViewerPart, typeof(ScrollViewer))]
[TemplatePart(EntriesPart, typeof(ItemsControl))]
[PseudoClasses(EmptyPseudoClass, GeneratingPseudoClass, ScrolledUpPseudoClass, WaitingPseudoClass)]
public partial class ChatView : TemplatedControl
{
    /// <summary>Defines the <see cref="Runner"/> property.</summary>
    public static readonly StyledProperty<ChatRunner?> RunnerProperty =
        AvaloniaProperty.Register<ChatView, ChatRunner?>(nameof(Runner));

    /// <summary>Defines the <see cref="Conversation"/> property.</summary>
    public static readonly StyledProperty<ChatConversation?> ConversationProperty =
        AvaloniaProperty.Register<ChatView, ChatConversation?>(nameof(Conversation));

    /// <summary>Defines the <see cref="IsGenerating"/> property.</summary>
    public static readonly StyledProperty<bool> IsGeneratingProperty =
        AvaloniaProperty.Register<ChatView, bool>(nameof(IsGenerating));

    /// <summary>Defines the <see cref="Prompt"/> property.</summary>
    public static readonly StyledProperty<string?> PromptProperty =
        AvaloniaProperty.Register<ChatView, string?>(nameof(Prompt), string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="Strings"/> property.</summary>
    public static readonly StyledProperty<ChatViewStrings> StringsProperty =
        AvaloniaProperty.Register<ChatView, ChatViewStrings>(nameof(Strings), defaultValue: null!, coerce: CoerceStrings);

    /// <summary>Defines the <see cref="EmptyContent"/> property.</summary>
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<ChatView, object?>(nameof(EmptyContent));

    /// <summary>Defines the <see cref="EmptyContentTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> EmptyContentTemplateProperty =
        AvaloniaProperty.Register<ChatView, IDataTemplate?>(nameof(EmptyContentTemplate));

    /// <summary>Defines the <see cref="Suggestions"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<string>?> SuggestionsProperty =
        AvaloniaProperty.Register<ChatView, IEnumerable<string>?>(nameof(Suggestions));

    private const string ScrollViewerPart = "PART_ScrollViewer";
    private const string EntriesPart = "PART_Entries";
    private const string EmptyPseudoClass = ":empty";
    private const string GeneratingPseudoClass = ":generating";
    private const string ScrolledUpPseudoClass = ":scrolledup";

    // NOTE: Captured once. Avalonia re-runs coercion whenever the property re-evaluates, and resets the UI thread's
    // culture after dispatcher operations, so creating the default inside the coercion would let the language drift.
    private readonly ChatViewStrings _defaultStrings = ChatViewStrings.ForCulture(CultureInfo.CurrentUICulture);
    private readonly ViewCommand _submitCommand;
    private readonly ViewCommand _stopCommand;
    private readonly ViewCommand _copyCommand;
    private readonly ViewCommand _scrollToLatestCommand;
    private ScrollViewer? _scrollViewer;
    private ItemsControl? _entries;
    private ChatRunner? _activeRunner;
    private ChatConversation? _shown;
    private readonly List<ChatEntry> _watchedEntries = [];
    private bool _isAttached;

    /// <summary>Initializes a new instance of the <see cref="ChatView"/> class.</summary>
    public ChatView()
    {
        _submitCommand = new ViewCommand(parameter => _ = SubmitAsync(parameter as string), _ => !IsGenerating);
        _stopCommand = new ViewCommand(_ => Stop(), _ => IsGenerating);
        _copyCommand = new ViewCommand(parameter => _ = CopyAsync(parameter), parameter => CopyText(parameter) is not null);
        _scrollToLatestCommand = new ViewCommand(_ => ScrollToLatest(), _ => true);
        CoerceValue(StringsProperty);
        PseudoClasses.Set(EmptyPseudoClass, true);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(ChatPromptBox.SubmitRequestedEvent, OnSubmitRequested);
    }

    /// <summary>
    /// Occurs when the user submits a prompt, before anything is sent. Set <see cref="CancelEventArgs.Cancel"/> to keep the
    /// prompt in the box and send nothing. Without a <see cref="Runner"/>, this is where the application sends the prompt.
    /// </summary>
    public event EventHandler<PromptSubmittedEventArgs>? PromptSubmitted;

    /// <summary>
    /// Occurs when the user asks to stop the reply in progress, with the stop button or Esc. Without a
    /// <see cref="Runner"/>, this is where the application stops its own turn.
    /// </summary>
    public event EventHandler? StopRequested;

    /// <summary>
    /// Gets or sets the runner that sends the prompts and streams the replies. When it is set, the view shows the runner's
    /// conversation and <see cref="Conversation"/> is not used.
    /// </summary>
    public ChatRunner? Runner
    {
        get => GetValue(RunnerProperty);
        set => SetValue(RunnerProperty, value);
    }

    /// <summary>
    /// Gets or sets the conversation to show when the application drives it without a <see cref="Runner"/>.
    /// </summary>
    public ChatConversation? Conversation
    {
        get => GetValue(ConversationProperty);
        set => SetValue(ConversationProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a reply is in progress: the stop button replaces the send button and no
    /// prompt can be sent. It follows <see cref="ChatRunner.IsRunning"/> when there is a <see cref="Runner"/>; without
    /// one, the application sets it.
    /// </summary>
    public bool IsGenerating
    {
        get => GetValue(IsGeneratingProperty);
        set => SetValue(IsGeneratingProperty, value);
    }

    /// <summary>Gets or sets the text in the prompt box. It binds two-way by default.</summary>
    public string? Prompt
    {
        get => GetValue(PromptProperty);
        set => SetValue(PromptProperty, value);
    }

    /// <summary>
    /// Gets or sets the texts the view shows. The default is <see cref="ChatViewStrings.ForCulture"/> for the UI culture
    /// of the thread that created the view; assign an instance to choose the language or change a text. Setting
    /// <see langword="null"/> restores the default.
    /// </summary>
    public ChatViewStrings Strings
    {
        get => GetValue(StringsProperty);
        set => SetValue(StringsProperty, value);
    }

    /// <summary>
    /// Gets or sets what the view shows above the <see cref="Suggestions"/> while the conversation is empty. The default
    /// theme shows <see cref="ChatViewStrings.EmptyTitle"/> when it is <see langword="null"/>.
    /// </summary>
    public object? EmptyContent
    {
        get => GetValue(EmptyContentProperty);
        set => SetValue(EmptyContentProperty, value);
    }

    /// <summary>Gets or sets the template of <see cref="EmptyContent"/>.</summary>
    public IDataTemplate? EmptyContentTemplate
    {
        get => GetValue(EmptyContentTemplateProperty);
        set => SetValue(EmptyContentTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the prompts offered while the conversation is empty. Clicking one submits it, as if the user had
    /// typed it.
    /// </summary>
    public IEnumerable<string>? Suggestions
    {
        get => GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    /// <summary>
    /// Gets the command that submits a prompt: its parameter when that is a string, and the text in the prompt box
    /// otherwise.
    /// </summary>
    public ICommand SubmitCommand => _submitCommand;

    /// <summary>Gets the command that stops the reply in progress.</summary>
    public ICommand StopCommand => _stopCommand;

    /// <summary>
    /// Gets the command that copies text to the clipboard: the Markdown of an <see cref="AssistantReplyEntry"/>, the text
    /// of a <see cref="UserPromptEntry"/> or <see cref="ReasoningEntry"/>, or a string.
    /// </summary>
    public ICommand CopyCommand => _copyCommand;

    /// <summary>Gets the command that scrolls to the newest entry and follows new entries again.</summary>
    public ICommand ScrollToLatestCommand => _scrollToLatestCommand;

    /// <summary>
    /// Gets a value indicating whether the view keeps the newest entry in sight as entries arrive. It turns off when the
    /// user scrolls up and back on when they scroll to the end, submit a prompt or use
    /// <see cref="ScrollToLatestCommand"/>.
    /// </summary>
    public bool FollowsLatest => _followsLatest;

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        }
        if (_entries is not null)
        {
            _entries.ItemsSource = null;
        }

        _scrollViewer = e.NameScope.Find<ScrollViewer>(ScrollViewerPart);
        _entries = e.NameScope.Find<ItemsControl>(EntriesPart);
        _progressText = e.NameScope.Find<TextBlock>(ProgressPart);
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }
        if (_entries is not null)
        {
            _entries.ItemTemplate = new ChatEntryTemplate(this);
            _entries.ItemsSource = _shown?.Entries;
        }
        UpdateProgress();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        Subscribe();
        if (_activeRunner is not null)
        {
            SetCurrentValue(IsGeneratingProperty, _activeRunner.IsRunning);
        }
        UpdateEmpty();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Unsubscribe();
        _isAttached = false;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RunnerProperty)
        {
            AttachRunner(change.GetNewValue<ChatRunner?>());
        }
        else if (change.Property == ConversationProperty)
        {
            ShowConversation();
        }
        else if (change.Property == StringsProperty)
        {
            UpdateProgress();
        }
        else if (change.Property == IsGeneratingProperty)
        {
            PseudoClasses.Set(GeneratingPseudoClass, change.GetNewValue<bool>());
            UpdateWaiting();
            UpdateProgress();
            _submitCommand.RaiseCanExecuteChanged();
            _stopCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Submits a prompt: raises <see cref="PromptSubmitted"/>, then sends it through the <see cref="Runner"/> if there is
    /// one. Nothing happens while a reply is in progress or when the prompt is blank.
    /// </summary>
    /// <param name="text">The prompt, or <see langword="null"/> for the text in the prompt box.</param>
    internal async Task SubmitAsync(string? text)
    {
        var fromBox = text is null;
        text ??= Prompt;
        if (IsGenerating || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var submitted = new PromptSubmittedEventArgs(text);
        PromptSubmitted?.Invoke(this, submitted);
        if (submitted.Cancel)
        {
            return;
        }
        if (fromBox)
        {
            SetCurrentValue(PromptProperty, string.Empty);
        }
        ScrollToLatest();

        if (_activeRunner is { } runner)
        {
            var result = await runner.SendAsync(text);
            // A MessageSending handler of the runner canceled the prompt: give it back to the user instead of losing it.
            if (result is null && fromBox && string.IsNullOrEmpty(Prompt))
            {
                SetCurrentValue(PromptProperty, text);
            }
        }
    }

    /// <summary>Copies the text of an entry, or a string, to the clipboard.</summary>
    /// <param name="parameter">An entry or a string.</param>
    internal async Task CopyAsync(object? parameter)
    {
        if (CopyText(parameter) is { } text && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private static ChatViewStrings CoerceStrings(AvaloniaObject owner, ChatViewStrings? value) =>
        value ?? ((ChatView)owner)._defaultStrings;

    private static string? CopyText(object? parameter) => parameter switch
    {
        AssistantReplyEntry { IsStreaming: false } reply when reply.Text.Length > 0 => reply.Text,
        UserPromptEntry prompt => prompt.Text,
        ReasoningEntry { IsStreaming: false } reasoning => reasoning.Text,
        string text when text.Length > 0 => text,
        _ => null,
    };

    private void Stop()
    {
        _activeRunner?.Stop();
        StopRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AttachRunner(ChatRunner? runner)
    {
        Unsubscribe();
        _activeRunner = runner;
        _shown = runner?.Conversation ?? Conversation;
        Subscribe();
        Refresh();
    }

    private void ShowConversation()
    {
        var conversation = _activeRunner?.Conversation ?? Conversation;
        if (ReferenceEquals(conversation, _shown))
        {
            return;
        }
        Unsubscribe();
        _shown = conversation;
        Subscribe();
        Refresh();
    }

    private void Refresh()
    {
        if (_activeRunner is not null)
        {
            SetCurrentValue(IsGeneratingProperty, _activeRunner.IsRunning);
        }
        if (_entries is not null)
        {
            _entries.ItemsSource = _shown?.Entries;
        }
        UpdateEmpty();
        ScrollToLatest();
    }

    // NOTE: The runner and the conversation usually outlive the window that shows them, so the view listens to them only
    // while it is in a visual tree. Otherwise their events would keep every closed view alive.
    private void Subscribe()
    {
        if (!_isAttached)
        {
            return;
        }
        if (_activeRunner is not null)
        {
            _activeRunner.PropertyChanged += OnRunnerPropertyChanged;
        }
        if (_shown is not null)
        {
            _shown.Entries.CollectionChanged += OnEntriesChanged;
            _shown.PropertyChanged += OnConversationPropertyChanged;
            Watch(_shown.Entries);
        }
        WatchTurn(_shown?.CurrentTurn);
    }

    private void Unsubscribe()
    {
        if (_activeRunner is not null)
        {
            _activeRunner.PropertyChanged -= OnRunnerPropertyChanged;
        }
        if (_shown is not null)
        {
            _shown.Entries.CollectionChanged -= OnEntriesChanged;
            _shown.PropertyChanged -= OnConversationPropertyChanged;
        }
        UnwatchAll();
        WatchTurn(null);
    }

    private void OnRunnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatRunner.IsRunning) && sender is ChatRunner runner)
        {
            SetCurrentValue(IsGeneratingProperty, runner.IsRunning);
        }
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateEmpty();
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            UnwatchAll();
            Watch(_shown!.Entries);
            ScrollToLatest();
            return;
        }
        if (e.OldItems is not null)
        {
            foreach (var entry in e.OldItems.OfType<ChatEntry>())
            {
                entry.PropertyChanged -= OnEntryPropertyChanged;
                _watchedEntries.Remove(entry);
            }
        }
        if (e.NewItems is not null)
        {
            Watch(e.NewItems.OfType<ChatEntry>());
        }
    }

    private void UpdateEmpty()
    {
        PseudoClasses.Set(EmptyPseudoClass, _shown is null || _shown.Entries.Count == 0);
        UpdateWaiting();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsGenerating)
        {
            e.Handled = true;
            Stop();
        }
    }

    private void OnSubmitRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = SubmitAsync(null);
    }
}
