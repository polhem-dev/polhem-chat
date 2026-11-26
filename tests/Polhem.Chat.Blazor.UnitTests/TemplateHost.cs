using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Polhem.Chat.Blazor.UnitTests;

/// <summary>
/// A parent that gives the view a reply template as Razor does, a new delegate on the same method with each render, and
/// counts how often the template draws.
/// </summary>
internal sealed class TemplateHost : ComponentBase
{
    [Parameter]
    public ChatConversation? Conversation { get; set; }

    /// <summary>Gets or sets a value indicating whether the parent gives its other template.</summary>
    [Parameter]
    public bool Plain { get; set; }

    public int Drawn { get; private set; }

    // The same texts with every render: new texts draw every entry again, which is not what this host tests.
    private readonly ChatStrings _strings = new();

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ChatView>(0);
        builder.AddComponentParameter(1, nameof(ChatView.Conversation), Conversation);
        builder.AddComponentParameter(2, nameof(ChatView.Strings), _strings);
        builder.AddComponentParameter(3, nameof(ChatView.ReplyTemplate), Plain
            ? new RenderFragment<AssistantReplyEntry>(Exclaimed)
            : new RenderFragment<AssistantReplyEntry>(Counted));
        builder.CloseComponent();
    }

    private RenderFragment Counted(AssistantReplyEntry reply) => builder =>
    {
        Drawn++;
        builder.AddContent(0, reply.Text);
    };

    private RenderFragment Exclaimed(AssistantReplyEntry reply) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "host-reply");
        builder.AddContent(2, reply.Text + "!");
        builder.CloseElement();
    };
}
