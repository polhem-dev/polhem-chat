using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Polhem.Chat.Blazor;

/// <summary>
/// Following the newest entry: the view scrolls along until it leaves the end by scrolling up, and comes back when it is
/// scrolled to the end, the user uses the jump-to-latest button, the application calls <see cref="ScrollToLatestAsync"/>,
/// or the view shows another conversation or a cleared one.
/// </summary>
/// <remarks>
/// The browser does the following itself (<c>polhem-chat.js</c>), so a streaming reply costs no round trip per token; it
/// tells the view only when the user leaves the end or comes back to it.
/// </remarks>
public sealed partial class ChatView
{
    private ElementReference _scroller;
    private DotNetObjectReference<ScrollBridge>? _scrollBridge;
    private bool _followsLatest = true;

    /// <summary>
    /// Gets a value indicating whether the view keeps the newest entry in sight as entries arrive. It turns off when the
    /// view leaves the end by scrolling up, however the user moved it (the wheel, the keyboard, the focus, finding text),
    /// and back on when it is at the end again.
    /// </summary>
    public bool FollowsLatest => _followsLatest;

    /// <summary>Scrolls to the newest entry and follows new entries again.</summary>
    /// <returns>
    /// A task that completes once the browser has scrolled; or at once, before the view's script has loaded (during
    /// prerendering, or before the first interactive render) or after its circuit went away.
    /// </returns>
    /// <remarks>Call it on the component's dispatcher, as any member of a component: <c>InvokeAsync</c> from elsewhere.</remarks>
    public async Task ScrollToLatestAsync()
    {
        _followsLatest = true;
        StateHasChanged();
        await FollowLatestAsync();
    }

    // The button goes away once it has scrolled, so the keyboard focus moves to the conversation.
    private async Task JumpToLatestAsync()
    {
        _followsLatest = true;
        await FollowLatestAsync(focus: true);
    }

    private async Task FollowLatestAsync() => await FollowLatestAsync(focus: false);

    private async Task FollowLatestAsync(bool focus)
    {
        _followsLatest = true;
        if (_module is not null && !_disposed)
        {
            try
            {
                await _module.InvokeVoidAsync("scrollToLatest", _scroller, focus);
            }
            // NOTE: On Blazor Server the circuit may go away while the view asks; the module went with it.
            catch (JSDisconnectedException)
            {
                _module = null;
            }
        }
    }

    private void OnFollowsLatestChanged(bool follows) => _ = InvokeAsync(() =>
    {
        if (_disposed || follows == _followsLatest)
        {
            return;
        }
        _followsLatest = follows;
        StateHasChanged();
    });
}
