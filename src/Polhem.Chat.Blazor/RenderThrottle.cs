namespace Polhem.Chat.Blazor;

/// <summary>
/// Turns change notifications into at most one render per <see cref="Interval"/>. A streaming reply changes with every
/// token; rendering each of them would send a diff to the browser per token on Blazor Server.
/// </summary>
/// <param name="invokeAsync">Runs work on the component's dispatcher.</param>
/// <param name="render">Renders the component; called on the dispatcher.</param>
internal sealed class RenderThrottle(Func<Func<Task>, Task> invokeAsync, Action render)
{
    /// <summary>The time a change waits for the changes after it before the component renders.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    private int _scheduled;
    private volatile bool _stopped;

    /// <summary>Asks for a render, unless one is already on its way.</summary>
    public void Request()
    {
        if (_stopped || Interlocked.Exchange(ref _scheduled, 1) == 1)
        {
            return;
        }
        _ = RenderLaterAsync();
    }

    /// <summary>Stops rendering, for a component that is being disposed.</summary>
    public void Stop() => _stopped = true;

    private async Task RenderLaterAsync()
    {
        await Task.Delay(Interval);
        if (_stopped)
        {
            return;
        }
        try
        {
            await invokeAsync(() =>
            {
                Volatile.Write(ref _scheduled, 0);
                if (!_stopped)
                {
                    render();
                }
                return Task.CompletedTask;
            });
        }
        // NOTE: The renderer may be disposed between the check above and the call, when the circuit or the page goes
        // away. There is nothing left to render then.
        catch (ObjectDisposedException)
        {
            _stopped = true;
        }
    }
}
