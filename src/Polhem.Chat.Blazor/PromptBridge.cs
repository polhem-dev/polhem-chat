using Microsoft.JSInterop;

namespace Polhem.Chat.Blazor;

/// <summary>What the view's script tells it about the keys pressed in the view.</summary>
internal sealed class PromptBridge(Action<string> submit, Action escape)
{
    /// <summary>Called when the user presses Enter in the prompt box, outside an input method's composition.</summary>
    /// <param name="text">The text in the box.</param>
    [JSInvokable]
    public void Submit(string text) => submit(text);

    /// <summary>Called when the user presses Esc in the view.</summary>
    [JSInvokable]
    public void Escape() => escape();
}
