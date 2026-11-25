using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// The line under the entries while a reply is in progress: an animated icon, what the model is doing, the request the
/// turn is on, its running time and the output tokens reported so far, kept current once a second.
/// </summary>
public partial class ChatView
{
    /// <summary>Defines the <see cref="ActivityIcon"/> property.</summary>
    public static readonly StyledProperty<Uri?> ActivityIconProperty =
        AvaloniaProperty.Register<ChatView, Uri?>(nameof(ActivityIcon));

    /// <summary>Defines the <see cref="ShowsActivity"/> property.</summary>
    public static readonly StyledProperty<bool> ShowsActivityProperty =
        AvaloniaProperty.Register<ChatView, bool>(nameof(ShowsActivity));

    private const string ProgressPart = "PART_Progress";
    private const string SpinnerPart = "PART_Spinner";

    private static readonly Uri s_defaultActivityIcon = new("avares://Polhem.Chat.Avalonia/Assets/activity.gif");
    private static GifAnimation? s_defaultAnimation;

    private readonly GifPlayer _activityIcon = new();

    private TextBlock? _progressText;
    private ChatTurn? _watchedTurn;
    private DispatcherTimer? _progressTimer;

    /// <summary>
    /// Gets or sets the animated GIF that plays at the start of the activity line while a reply is in progress, as an
    /// <c>avares://</c> URI. <see langword="null"/>, the default, plays the package's own.
    /// </summary>
    public Uri? ActivityIcon
    {
        get => GetValue(ActivityIconProperty);
        set => SetValue(ActivityIconProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the model's reasoning and its tool calls show as entries. By default they
    /// do not: the conversation shows what is written for the user, and while a turn runs one line under it says what
    /// the model is doing, which request it is on, its seconds and its output tokens.
    /// </summary>
    public bool ShowsActivity
    {
        get => GetValue(ShowsActivityProperty);
        set => SetValue(ShowsActivityProperty, value);
    }

    /// <summary>Gets the text of the progress line, or an empty string when the view shows none.</summary>
    internal string ProgressText => _progressText?.IsVisible == true ? _progressText.Text ?? string.Empty : string.Empty;

    private void OnConversationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatConversation.CurrentTurn))
        {
            WatchTurn(_shown?.CurrentTurn);
        }
        else if (e.PropertyName == nameof(ChatConversation.LastPrompt))
        {
            UpdateRewind();
        }
    }

    private void OnTurnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateProgress();
        if (e.PropertyName == nameof(ChatTurn.IsAwaitingApproval))
        {
            UpdateWaiting();
        }
    }

    private void WatchTurn(ChatTurn? turn)
    {
        if (_watchedTurn is not null)
        {
            _watchedTurn.PropertyChanged -= OnTurnPropertyChanged;
        }
        _watchedTurn = turn;
        if (_watchedTurn is not null)
        {
            _watchedTurn.PropertyChanged += OnTurnPropertyChanged;
        }
        UpdateProgress();
        _approveCommand.RaiseCanExecuteChanged();
        _rejectCommand.RaiseCanExecuteChanged();
    }

    // NOTE: The seconds change without the turn saying so, so a timer redraws the line while it shows. It runs only while
    // the view is in a visual tree and a turn is in progress, so a closed view does not keep it alive.
    /// <summary>Gets the player of the activity icon.</summary>
    internal GifPlayer ActivityIconPlayer => _activityIcon;

    internal void UpdateProgress()
    {
        _activityIcon.IsPlaying = _isAttached && IsGenerating;
        var turn = _isAttached && IsGenerating ? _watchedTurn : null;
        if (_progressText is not null)
        {
            _progressText.IsVisible = turn is not null;
            _progressText.Text = turn is null
                ? string.Empty
                : Strings.FormatProgress(turn.Calls, turn.Elapsed, turn.Usage?.OutputTokenCount ?? 0);
        }
        if (turn is null)
        {
            _progressTimer?.Stop();
            return;
        }
        if (_progressTimer is null)
        {
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _progressTimer.Tick += (_, _) => UpdateProgress();
        }
        _progressTimer.Start();
    }

    private static GifAnimation LoadActivityIcon(Uri? source)
    {
        if (source is null)
        {
            return s_defaultAnimation ??= Read(s_defaultActivityIcon);
        }
        return Read(source);

        static GifAnimation Read(Uri uri)
        {
            using var stream = AssetLoader.Open(uri);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return GifDecoder.Decode(memory.ToArray());
        }
    }
}
