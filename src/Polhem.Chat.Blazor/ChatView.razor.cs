using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using Polhem.Markdown;

namespace Polhem.Chat.Blazor;

/// <summary>
/// Shows a <see cref="ChatConversation"/>: replies stream in as Markdown, reasoning and tool calls show as collapsible
/// cards, tool calls that wait for the user's approval show their arguments in full with Approve and Reject buttons, and
/// each turn ends with its time and token counts.
/// </summary>
/// <remarks>
/// <para>
/// The view works in one of two ways. Give it a <see cref="Runner"/>, and it shows the runner's conversation and follows
/// whether a reply is in progress. Or give it only a <see cref="Conversation"/> that the application drives itself, and
/// set <see cref="IsGenerating"/> while a reply is in progress.
/// </para>
/// <para>
/// IMPORTANT: The conversation must change on the dispatcher of the component that shows it, which it does when that
/// component's UI event starts the runner. A conversation changed from another thread, or shown by views of two circuits
/// at once (a singleton, or one user's two tabs), can change while a view reads it.
/// </para>
/// <para>
/// The replies are drawn by <c>Polhem.Markdown.Blazor</c>, whose stylesheet the host page links:
/// <c>_content/Polhem.Markdown.Blazor/polhem-markdown.css</c>.
/// </para>
/// </remarks>
public sealed partial class ChatView : IAsyncDisposable
{
    private const string ModulePath = "./_content/Polhem.Chat.Blazor/polhem-chat.js";

    private readonly ChatComposer _composer = new();
    private readonly MarkdownOptions _noImages = CreateNoImages();
    private readonly List<ChatEntry> _watched = [];
    private RenderThrottle _throttle = null!;
    private RenderFragment<ChatEntry> _entryTemplate = null!;
    private RenderFragment _entries = null!;
    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<PromptBridge>? _promptBridge;
    private ChatConversation? _shown;
    private ChatStrings? _strings;
    private MarkdownOptions? _markdownOptions;
    private bool _showsActivityParameter;
    private int _revision;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the runner that sends the prompts and streams the replies. When it is set, the view shows the runner's
    /// conversation and <see cref="Conversation"/> is not used.
    /// </summary>
    [Parameter]
    public ChatRunner? Runner { get; set; }

    /// <summary>Gets or sets the conversation to show when the application drives it without a <see cref="Runner"/>.</summary>
    [Parameter]
    public ChatConversation? Conversation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a reply is in progress, for a conversation the application drives without a
    /// <see cref="Runner"/>. With a runner, the view follows <see cref="ChatRunner.IsRunning"/> instead.
    /// </summary>
    [Parameter]
    public bool IsGenerating { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the model's reasoning and its tool calls show as entries. By default they
    /// do not, except a tool call that waits for the user's approval: hiding it would leave the turn waiting for a decision
    /// nobody can make. The user turns it on and off with the toggle on the activity line, which raises
    /// <see cref="ShowsActivityChanged"/>, so it binds two-way with <c>@bind-ShowsActivity</c>.
    /// </summary>
    [Parameter]
    public bool ShowsActivity { get; set; }

    /// <summary>Gets or sets the callback raised when the user turns <see cref="ShowsActivity"/> on or off.</summary>
    [Parameter]
    public EventCallback<bool> ShowsActivityChanged { get; set; }

    /// <summary>
    /// Gets or sets the texts the view shows. The default is <see cref="ChatStrings.ForCulture"/> for the UI culture of the
    /// thread that created the view. Give it a new instance to change a text: changing a property of the instance the view
    /// already shows does not redraw it.
    /// </summary>
    [Parameter]
    public ChatStrings? Strings { get; set; }

    /// <summary>
    /// Gets or sets the options the replies are rendered with. <see langword="null"/>, the default, loads no images.
    /// </summary>
    /// <remarks>
    /// A reply is the model's text, and an image in it loads from wherever the model points it: a prompt injection can
    /// make the model write an image whose address carries what the conversation holds, and loading it sends that to the
    /// address's server. So by default an image shows as its alternative text. Assigning an instance hands that choice to
    /// the application, and a new <see cref="Polhem.Markdown.MarkdownOptions"/> loads images over <c>https</c>. Give the view
    /// a new instance to change the options: changing the sets of the one it already shows does not redraw the replies.
    /// </remarks>
    [Parameter]
    public MarkdownOptions? MarkdownOptions { get; set; }

    /// <summary>Gets or sets classes added to the view's root element.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>Gets or sets attributes added to the view's root element, such as <c>id</c> or <c>style</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private ChatStrings S => _composer.Strings;

    private MarkdownOptions ReplyOptions => MarkdownOptions ?? _noImages;

    private string RootClass => string.IsNullOrWhiteSpace(Class) ? "pch" : "pch " + Class;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _throttle.Stop();
        StopProgressTimer();
        Show(null);
        _composer.PropertyChanged -= OnComposerPropertyChanged;
        DisconnectComposer();
        _composer.Disconnect();
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("unwatchPrompt", _root);
                await _module.InvokeVoidAsync("unwatch", _root);
                await _module.InvokeVoidAsync("unfollow", _scroller);
                await _module.DisposeAsync();
            }
            // NOTE: On Blazor Server the circuit may already be gone, and the browser's side of the module with it.
            catch (JSDisconnectedException)
            {
                _module = null;
            }
        }
        // After the script stopped following and watching the keys: a scroll or a key in between would otherwise call a
        // disposed reference.
        _scrollBridge?.Dispose();
        _promptBridge?.Dispose();
    }

    /// <inheritdoc/>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // A parent passes the parameters it sets with every render, so this tells whether it binds the text now.
        _promptBound = parameters.TryGetValue<string?>(nameof(Prompt), out _)
            && parameters.TryGetValue<EventCallback<string?>>(nameof(PromptChanged), out var changed) && changed.HasDelegate;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        _throttle = new RenderThrottle(InvokeAsync, StateHasChanged);
        _entryTemplate = RenderEntry;
        _entries = BuildEntries;
        _composer.PropertyChanged += OnComposerPropertyChanged;
        ConnectComposer();
        _composer.Connect();
    }

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        _composer.Runner = Runner;
        _composer.Conversation = Conversation;
        if (Runner is null)
        {
            _composer.IsGenerating = IsGenerating;
        }
        _composer.Strings = Strings;
        ApplyComposingParameters();
        // NOTE: The user's toggle changes the state without the parameter, so the state follows the parameter only when the
        // parameter itself changes; a parent that renders again without binding it does not undo the user's choice.
        var activityChanged = ShowsActivity != _showsActivityParameter;
        if (activityChanged)
        {
            _showsActivityParameter = ShowsActivity;
            _showsActivity = ShowsActivity;
        }
        if (!ReferenceEquals(Strings, _strings) || !ReferenceEquals(MarkdownOptions, _markdownOptions))
        {
            _strings = Strings;
            _markdownOptions = MarkdownOptions;
            _revision++;
        }
        var shownBefore = _shown;
        Show(_composer.ShownConversation);
        if (!ReferenceEquals(shownBefore, _shown))
        {
            _ = FollowLatestAsync();
        }
        UpdateProgressTimer();
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            // NOTE: The view may be disposed while the module loads; it would then never dispose a module it held.
            if (_disposed)
            {
                await module.DisposeAsync();
                return;
            }
            _module = module;
            await _module.InvokeVoidAsync("watchOverflow", _root);
            // NOTE: Disposing may also happen while the script starts watching; disposing has then cleaned up already.
            if (_disposed)
            {
                return;
            }
            _scrollBridge = DotNetObjectReference.Create(new ScrollBridge(OnFollowsLatestChanged));
            await _module.InvokeVoidAsync("follow", _scroller, _scrollBridge);
            if (_disposed)
            {
                return;
            }
            _promptBridge = DotNetObjectReference.Create(new PromptBridge(SubmitFromBox, OnEscape));
            await _module.InvokeVoidAsync("watchPrompt", _root, _promptBox, _promptBridge);
        }
        if (_focusConversation && _module is not null && !_disposed)
        {
            _focusConversation = false;
            await _module.InvokeVoidAsync("scrollToLatest", _scroller, true);
        }
    }

    private static MarkdownOptions CreateNoImages()
    {
        var options = new MarkdownOptions();
        options.AllowedImageSchemes.Clear();
        return options;
    }

    // NOTE: Razor markup only finds public components, and EntryHost is not part of the package's API, so the list is
    // built here. All entries are in one region, so their keys keep each entry with its host as the list changes.
    private void BuildEntries(RenderTreeBuilder builder)
    {
        if (_shown is null)
        {
            return;
        }
        foreach (var entry in _shown.Entries)
        {
            if (!IsShown(entry))
            {
                continue;
            }
            builder.OpenComponent<EntryHost>(0);
            builder.SetKey(entry);
            builder.AddComponentParameter(1, nameof(EntryHost.Entry), entry);
            builder.AddComponentParameter(2, nameof(EntryHost.Template), _entryTemplate);
            builder.AddComponentParameter(3, nameof(EntryHost.Revision), _revision);
            builder.AddComponentParameter(4, nameof(EntryHost.CanRewind), CanRewind(entry));
            builder.CloseComponent();
        }
    }

    // The last prompt shows a button to change it, and the last reply one to send its prompt again, while the composer
    // allows it; their hosts render again when that changes.
    private bool CanRewind(ChatEntry entry) => entry switch
    {
        UserPromptEntry prompt => _composer.CanEditLastPrompt(prompt),
        AssistantReplyEntry reply => _composer.CanRegenerate(reply),
        _ => false,
    };

    // NOTE: Hidden entries stay in the conversation and its history; only the view leaves them out.
    private bool IsShown(ChatEntry entry) =>
        _showsActivity || entry is not (ReasoningEntry or ToolCallEntry { Status: not ToolCallStatus.AwaitingApproval });

    private async Task CopyAsync(string text)
    {
        if (_module is not null)
        {
            await _module.InvokeAsync<bool>("copyText", text);
        }
    }

    private void Show(ChatConversation? conversation)
    {
        if (ReferenceEquals(conversation, _shown))
        {
            return;
        }
        if (_shown is not null)
        {
            _shown.Entries.CollectionChanged -= OnEntriesChanged;
            _shown.PropertyChanged -= OnConversationPropertyChanged;
        }
        UnwatchAll();
        _shown = conversation;
        if (_shown is not null)
        {
            _shown.Entries.CollectionChanged += OnEntriesChanged;
            _shown.PropertyChanged += OnConversationPropertyChanged;
            Watch(_shown.Entries);
        }
        WatchTurn(_shown?.CurrentTurn);
    }

    // NOTE: A tool call that stops waiting for approval leaves the list while the activity is hidden, so the view follows
    // the state of each entry, not only the list.
    private void Watch(IEnumerable<ChatEntry> entries)
    {
        foreach (var entry in entries)
        {
            entry.PropertyChanged += OnEntryPropertyChanged;
            _watched.Add(entry);
        }
    }

    private void UnwatchAll()
    {
        foreach (var entry in _watched)
        {
            entry.PropertyChanged -= OnEntryPropertyChanged;
        }
        _watched.Clear();
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            UnwatchAll();
            Watch(_shown!.Entries);
            // A cleared conversation starts over at its end, without a jump button left over from before.
            _ = InvokeAsync(FollowLatestAsync);
        }
        else
        {
            foreach (var entry in e.OldItems?.OfType<ChatEntry>() ?? [])
            {
                entry.PropertyChanged -= OnEntryPropertyChanged;
                _watched.Remove(entry);
            }
            Watch(e.NewItems?.OfType<ChatEntry>() ?? []);
        }
        _throttle.Request();
    }

    // The list follows a tool call's state (whether it shows). The activity line follows a running tool's title and a
    // reply's first text as well, so a streaming reply renders the view only when what the line says changes, not with
    // every token.
    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var changesView = e.PropertyName is nameof(ToolCallEntry.Status) or nameof(AssistantReplyEntry.IsStreaming)
            || (e.PropertyName is nameof(ToolCallEntry.Title) or nameof(AssistantReplyEntry.Text)
                && !string.Equals(_composer.DescribeActivity(_showsActivity), _shownActivity, StringComparison.Ordinal));
        if (changesView)
        {
            _throttle.Request();
        }
    }

    private void OnComposerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ChatComposer.IsGenerating) or nameof(ChatComposer.ShownConversation):
                _ = InvokeAsync(UpdateProgressTimer);
                _throttle.Request();
                break;
            case nameof(ChatComposer.Prompt):
                NotifyPrompt();
                _throttle.Request();
                break;
            case nameof(ChatComposer.CanRewind) or nameof(ChatComposer.IsEditingPrompt) or nameof(ChatComposer.AttachmentError):
                _throttle.Request();
                break;
        }
    }
}
