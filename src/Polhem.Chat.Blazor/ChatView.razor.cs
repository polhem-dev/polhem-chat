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
/// IMPORTANT: The conversation must change on the component's dispatcher, which it does when the runner is started from
/// a UI event. A conversation changed from another thread can change while the view reads it.
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
    private readonly List<ChatEntry> _watched = [];
    private RenderThrottle _throttle = null!;
    private RenderFragment<ChatEntry> _entryTemplate = null!;
    private RenderFragment _entries = null!;
    private ElementReference _root;
    private IJSObjectReference? _module;
    private ChatConversation? _shown;
    private ChatStrings? _strings;
    private MarkdownOptions? _markdownOptions;
    private bool _showsActivity;
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
    /// nobody can make.
    /// </summary>
    [Parameter]
    public bool ShowsActivity { get; set; }

    /// <summary>
    /// Gets or sets the texts the view shows. The default is <see cref="ChatStrings.ForCulture"/> for the UI culture of the
    /// thread that created the view.
    /// </summary>
    [Parameter]
    public ChatStrings? Strings { get; set; }

    /// <summary>Gets or sets the options of the Markdown viewer that draws the replies.</summary>
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

    private string RootClass => string.IsNullOrWhiteSpace(Class) ? "pch" : "pch " + Class;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _throttle.Stop();
        Show(null);
        _composer.PropertyChanged -= OnComposerPropertyChanged;
        _composer.Disconnect();
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("unwatch", _root);
                await _module.DisposeAsync();
            }
            // NOTE: On Blazor Server the circuit may already be gone, and the browser's side of the module with it.
            catch (JSDisconnectedException)
            {
                _module = null;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        _throttle = new RenderThrottle(InvokeAsync, StateHasChanged);
        _entryTemplate = RenderEntry;
        _entries = BuildEntries;
        _composer.PropertyChanged += OnComposerPropertyChanged;
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
        if (!ReferenceEquals(Strings, _strings) || !ReferenceEquals(MarkdownOptions, _markdownOptions) || ShowsActivity != _showsActivity)
        {
            _strings = Strings;
            _markdownOptions = MarkdownOptions;
            _showsActivity = ShowsActivity;
            _revision++;
        }
        Show(_composer.ShownConversation);
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            if (!_disposed)
            {
                await _module.InvokeVoidAsync("watchOverflow", _root);
            }
        }
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
            builder.CloseComponent();
        }
    }

    // NOTE: Hidden entries stay in the conversation and its history; only the view leaves them out.
    private bool IsShown(ChatEntry entry) =>
        ShowsActivity || entry is not (ReasoningEntry or ToolCallEntry { Status: not ToolCallStatus.AwaitingApproval });

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
        }
        UnwatchAll();
        _shown = conversation;
        if (_shown is not null)
        {
            _shown.Entries.CollectionChanged += OnEntriesChanged;
            Watch(_shown.Entries);
        }
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

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolCallEntry.Status))
        {
            _throttle.Request();
        }
    }

    private void OnComposerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatComposer.IsGenerating) or nameof(ChatComposer.ShownConversation))
        {
            _throttle.Request();
        }
    }
}
