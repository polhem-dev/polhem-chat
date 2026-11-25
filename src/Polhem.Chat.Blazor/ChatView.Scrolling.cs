using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Polhem.Chat.Blazor;

/// <summary>
/// Following the newest entry: the view scrolls along until the user scrolls up, and comes back when they scroll to the
/// end, use the jump-to-latest button or <see cref="ScrollToLatestAsync"/>, or the view shows another conversation.
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
    /// user scrolls up and back on when they scroll to the end or the view scrolls to the latest entry.
    /// </summary>
    public bool FollowsLatest => _followsLatest;

    /// <summary>Scrolls to the newest entry and follows new entries again.</summary>
    /// <returns>A task that completes once the browser has scrolled.</returns>
    public async Task ScrollToLatestAsync()
    {
        _followsLatest = true;
        StateHasChanged();
        await FollowLatestAsync();
    }

    private async Task FollowLatestAsync()
    {
        _followsLatest = true;
        if (_module is not null && !_disposed)
        {
            try
            {
                await _module.InvokeVoidAsync("scrollToLatest", _scroller);
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
