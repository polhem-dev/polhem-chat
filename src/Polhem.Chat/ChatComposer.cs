using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Polhem.Chat;

/// <summary>
/// What the user does with a chat view, whatever UI framework draws it: the prompt box and the files attached to it,
/// submitting, stopping a reply, sending the last prompt again or changing it, and deciding on tool calls.
/// </summary>
/// <remarks>
/// <para>
/// A view gives the composer a <see cref="Runner"/>, and the composer sends each prompt through it and stops it on
/// request. Or the view gives it only a <see cref="Conversation"/> that the application drives itself: the composer then
/// raises <see cref="PromptSubmitted"/>, <see cref="StopRequested"/> and <see cref="RegenerateRequested"/>, and the
/// application sets <see cref="IsGenerating"/> while a reply is in progress.
/// </para>
/// <para>
/// The composer follows the runner and the conversation only between <see cref="Connect"/> and
/// <see cref="Disconnect"/>, which removes its handlers from them. A view connects it while it is shown and disconnects it
/// when it closes, so that a runner which outlives the view holds no handler that leads back to the view.
/// </para>
/// </remarks>
public sealed partial class ChatComposer : INotifyPropertyChanged
{
    private readonly ChatStrings _defaultStrings = ChatStrings.ForCulture(CultureInfo.CurrentUICulture);
    private readonly ChatAttachmentLimits _defaultLimits = new();
    private ChatRunner? _runner;
    private ChatConversation? _conversation;
    private ChatConversation? _shown;
    private bool _isConnected;
    private bool _isGenerating;
    private string? _prompt = string.Empty;
    private ChatStrings? _strings;
    private ChatAttachmentLimits? _limits;
    private bool? _allowsRewind;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when the user submits a prompt, before anything is sent. Set <see cref="CancelEventArgs.Cancel"/> to keep the
    /// prompt in the box and send nothing. Without a <see cref="Runner"/>, this is where the application sends the prompt.
    /// </summary>
    public event EventHandler<PromptSubmittedEventArgs>? PromptSubmitted;

    /// <summary>
    /// Occurs when the user asks to stop the reply in progress. Without a <see cref="Runner"/>, this is where the
    /// application stops its own turn.
    /// </summary>
    public event EventHandler? StopRequested;

    /// <summary>
    /// Gets or sets the runner that sends the prompts and streams the replies. When it is set, the composer works on the
    /// runner's conversation and <see cref="Conversation"/> is not used.
    /// </summary>
    public ChatRunner? Runner
    {
        get => _runner;
        set
        {
            if (ReferenceEquals(value, _runner))
            {
                return;
            }
            Unsubscribe();
            _runner = value;
            _shown = value?.Conversation ?? _conversation;
            Subscribe();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShownConversation));
            if (value is not null)
            {
                IsGenerating = value.IsRunning;
            }
            UpdateRewind();
        }
    }

    /// <summary>Gets or sets the conversation the application drives itself, without a <see cref="Runner"/>.</summary>
    public ChatConversation? Conversation
    {
        get => _conversation;
        set
        {
            if (ReferenceEquals(value, _conversation))
            {
                return;
            }
            _conversation = value;
            OnPropertyChanged();
            var shown = _runner?.Conversation ?? value;
            if (ReferenceEquals(shown, _shown))
            {
                return;
            }
            Unsubscribe();
            _shown = shown;
            Subscribe();
            OnPropertyChanged(nameof(ShownConversation));
            UpdateRewind();
        }
    }

    /// <summary>Gets the conversation the composer works on: the <see cref="Runner"/>'s, or else <see cref="Conversation"/>.</summary>
    public ChatConversation? ShownConversation => _shown;

    /// <summary>
    /// Gets or sets a value indicating whether a reply is in progress, during which no prompt can be sent. It follows
    /// <see cref="ChatRunner.IsRunning"/> when there is a <see cref="Runner"/>; without one, the application sets it.
    /// </summary>
    public bool IsGenerating
    {
        get => _isGenerating;
        set
        {
            if (value == _isGenerating)
            {
                return;
            }
            _isGenerating = value;
            OnPropertyChanged();
            UpdateRewind();
        }
    }

    /// <summary>Gets or sets the text in the prompt box.</summary>
    public string? Prompt
    {
        get => _prompt;
        set
        {
            SetField(ref _prompt, value);
            UpdateCommands();
        }
    }

    /// <summary>
    /// Gets or sets the texts that messages from the composer use. The default is <see cref="ChatStrings.ForCulture"/> for
    /// the UI culture of the thread that created the composer; setting <see langword="null"/> restores it.
    /// </summary>
    [AllowNull]
    public ChatStrings Strings
    {
        get => _strings ?? _defaultStrings;
        set => SetField(ref _strings, value);
    }

    /// <summary>
    /// Gets or sets how many files a prompt may carry, how large and of which types. Setting <see langword="null"/>
    /// restores the defaults of <see cref="ChatAttachmentLimits"/>.
    /// </summary>
    [AllowNull]
    public ChatAttachmentLimits AttachmentLimits
    {
        get => _limits ?? _defaultLimits;
        set => SetField(ref _limits, value);
    }

    /// <summary>
    /// Connects the composer to its runner and conversation: from now on it follows whether the runner is running, and
    /// ends an edit whose prompt is no longer the last one.
    /// </summary>
    public void Connect()
    {
        if (_isConnected)
        {
            return;
        }
        _isConnected = true;
        Subscribe();
        if (_runner is not null)
        {
            IsGenerating = _runner.IsRunning;
        }
        UpdateRewind();
        UpdateCommands();
    }

    /// <summary>Stops following the runner and the conversation, so that they no longer refer to the composer.</summary>
    public void Disconnect()
    {
        Unsubscribe();
        _isConnected = false;
    }

    private void Subscribe()
    {
        if (!_isConnected)
        {
            return;
        }
        if (_runner is not null)
        {
            _runner.PropertyChanged += OnRunnerPropertyChanged;
        }
        if (_shown is not null)
        {
            _shown.PropertyChanged += OnConversationPropertyChanged;
            _shown.Entries.CollectionChanged += OnEntriesChanged;
        }
        WatchCommands();
    }

    private void Unsubscribe()
    {
        if (_runner is not null)
        {
            _runner.PropertyChanged -= OnRunnerPropertyChanged;
        }
        if (_shown is not null)
        {
            _shown.PropertyChanged -= OnConversationPropertyChanged;
            _shown.Entries.CollectionChanged -= OnEntriesChanged;
        }
        UnwatchCommands();
    }

    private void OnRunnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatRunner.IsRunning) && sender is ChatRunner runner)
        {
            IsGenerating = runner.IsRunning;
        }
    }

    private void OnConversationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatConversation.LastPrompt))
        {
            UpdateRewind();
        }
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateRewind();

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
