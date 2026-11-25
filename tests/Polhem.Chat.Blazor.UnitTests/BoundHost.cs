using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Polhem.Chat.Blazor.UnitTests;

/// <summary>A parent that binds the view's text and keeps it to four characters, as an application may.</summary>
internal sealed class BoundHost : ComponentBase
{
    public string? Text { get; private set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ChatView>(0);
        builder.AddComponentParameter(1, nameof(ChatView.Conversation), new ChatConversation());
        builder.AddComponentParameter(2, nameof(ChatView.Strings), new ChatStrings());
        builder.AddComponentParameter(3, nameof(ChatView.Prompt), Text);
        builder.AddComponentParameter(4, nameof(ChatView.PromptChanged), EventCallback.Factory.Create<string?>(
            this, value => Text = value is { Length: > 4 } ? value[..4] : value));
        builder.CloseComponent();
    }
}
