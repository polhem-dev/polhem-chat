using System.ComponentModel;

namespace Polhem.Chat.Blazor;

/// <summary>
/// The line under the entries while a reply is in progress: a spinner, what the model is doing, the request the turn is
/// on, its running time and the output tokens reported so far, kept current once a second; and the toggle that shows the
/// model's reasoning and tool calls.
/// </summary>
public sealed partial class ChatView
{
    private static readonly TimeSpan s_progressInterval = TimeSpan.FromSeconds(1);

    private bool _showsActivity;
    private string? _shownActivity;
    private bool _focusConversation;
    private ChatTurn? _turn;
    private Timer? _progressTimer;

    private string? Progress => _composer.IsGenerating && _shown?.CurrentTurn is { } turn
        ? S.FormatProgress(turn.Calls, turn.Elapsed, turn.Usage?.OutputTokenCount ?? 0)
        : null;

    // What the line says, read while rendering, so a change can be told from a token that changes nothing.
    private string? DescribeActivity() => _shownActivity = _composer.DescribeActivity(_showsActivity);

    private async Task ToggleActivityAsync()
    {
        _showsActivity = !_showsActivity;
        // The line, and the toggle with it, goes away once the activity is hidden and no reply is in progress; the
        // keyboard focus then moves to the conversation instead of the start of the page.
        _focusConversation = !_showsActivity && !_composer.IsGenerating;
        await ShowsActivityChanged.InvokeAsync(_showsActivity);
    }

    private void OnConversationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatConversation.CurrentTurn))
        {
            WatchTurn(_shown?.CurrentTurn);
            _throttle.Request();
        }
    }

    // The turn's requests, its usage and whether it waits for approval change what the line says.
    private void WatchTurn(ChatTurn? turn)
    {
        if (_turn is not null)
        {
            _turn.PropertyChanged -= OnTurnPropertyChanged;
        }
        _turn = turn;
        if (_turn is not null)
        {
            _turn.PropertyChanged += OnTurnPropertyChanged;
        }
    }

    private void OnTurnPropertyChanged(object? sender, PropertyChangedEventArgs e) => _throttle.Request();

    // NOTE: The seconds change without the turn saying so, so a timer renders the line while it shows. It runs only while
    // a reply is in progress.
    private void UpdateProgressTimer()
    {
        if (_disposed || !_composer.IsGenerating)
        {
            StopProgressTimer();
            return;
        }
        _progressTimer ??= new Timer(_ => _throttle.Request(), null, s_progressInterval, s_progressInterval);
    }

    private void StopProgressTimer()
    {
        _progressTimer?.Dispose();
        _progressTimer = null;
    }
}
