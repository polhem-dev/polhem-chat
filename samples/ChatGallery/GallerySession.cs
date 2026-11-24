using System.Globalization;
using Polhem.Chat;

namespace ChatGallery;

/// <summary>
/// What the gallery keeps while its view is rebuilt: the conversations, the model and the choices on the bar.
/// </summary>
/// <remarks>
/// NOTE: Switching the base theme rebuilds the view instead of restyling it. A control that is already shown keeps parts
/// of the old theme's template, and a tab control whose template changes while it shows its content throws. The
/// conversations live here, so a reply in progress keeps streaming into the new view.
/// </remarks>
public sealed class GallerySession
{
    public GallerySession()
    {
        Runner = new ChatRunner(DemoTools.WithTools(new DemoChatClient())) { Options = DemoTools.Options };
        Runner.Conversation.DescribeToolCall = ToolTitles.Describe;
        Culture = ToolTitles.IsTraditionalChinese(CultureInfo.CurrentUICulture) ? ToolTitles.Chinese : ToolTitles.English;
    }

    /// <summary>Gets the runner of the Runner tab.</summary>
    public ChatRunner Runner { get; }

    /// <summary>Gets or sets what the Runner tab shows about the model in use.</summary>
    public string ModelDescription { get; set; } = "Scripted replies.";

    /// <summary>Gets the conversation of the Host-driven tab and the loop that drives it.</summary>
    public HostSession Host { get; } = new();

    public bool UsesSemi { get; set; }

    /// <summary>Gets or sets the culture of the chat view's texts.</summary>
    public CultureInfo Culture { get; set; }

    public int SelectedTab { get; set; }
}
