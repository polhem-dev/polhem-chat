using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// The line under the entries while a reply is in progress: the request the turn is on and its seconds, kept current
/// once a second.
/// </summary>
public partial class ChatView
{
    private const string ProgressPart = "PART_Progress";

    private TextBlock? _progressText;
    private ChatTurn? _watchedTurn;
    private DispatcherTimer? _progressTimer;

    /// <summary>Gets the text of the progress line, or an empty string when the view shows none.</summary>
    internal string ProgressText => _progressText?.IsVisible == true ? _progressText.Text ?? string.Empty : string.Empty;

    private void OnConversationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatConversation.CurrentTurn))
        {
            WatchTurn(_shown?.CurrentTurn);
        }
    }

    private void OnTurnPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateProgress();

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
    }

    // NOTE: The seconds change without the turn saying so, so a timer redraws the line while it shows. It runs only while
    // the view is in a visual tree and a turn is in progress, so a closed view does not keep it alive.
    internal void UpdateProgress()
    {
        var turn = _isAttached && IsGenerating ? _watchedTurn : null;
        if (_progressText is not null)
        {
            _progressText.IsVisible = turn is not null;
            _progressText.Text = turn is null ? string.Empty : Strings.FormatProgress(turn.Calls, turn.Elapsed);
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
}
