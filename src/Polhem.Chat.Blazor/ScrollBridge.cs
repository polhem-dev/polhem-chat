using Microsoft.JSInterop;

namespace Polhem.Chat.Blazor;

/// <summary>
/// Receives from the browser whether the view still follows the newest entry. It is a type of its own so that the method
/// the script calls is not part of <see cref="ChatView"/>'s API.
/// </summary>
/// <param name="changed">Called with the new value, on the thread the call arrives on.</param>
internal sealed class ScrollBridge(Action<bool> changed)
{
    /// <summary>Called by <c>polhem-chat.js</c> when the user scrolls away from the end or back to it.</summary>
    /// <param name="follows">Whether the view follows the newest entry now.</param>
    [JSInvokable]
    public void OnFollowsLatestChanged(bool follows) => changed(follows);
}
