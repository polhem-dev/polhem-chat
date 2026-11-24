using Avalonia;
using Avalonia.Media;
using Polhem.Markdown;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// How the default template renders an <see cref="AssistantReplyEntry"/>: the Markdown options its viewer applies, and
/// the font its CJK text is drawn in.
/// </summary>
public partial class ChatView
{
    /// <summary>Defines the <see cref="MarkdownOptions"/> property.</summary>
    public static readonly StyledProperty<MarkdownOptions?> MarkdownOptionsProperty =
        AvaloniaProperty.Register<ChatView, MarkdownOptions?>(nameof(MarkdownOptions));

    /// <summary>Defines the <see cref="CjkFontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily?> CjkFontFamilyProperty =
        AvaloniaProperty.Register<ChatView, FontFamily?>(nameof(CjkFontFamily));

    /// <summary>
    /// Gets or sets the options the replies are rendered with. <see langword="null"/>, the default, loads no images.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A reply comes from the model, so its text is not trusted: a prompt injection can have the model write an image
    /// whose address carries data from the conversation, and loading the image sends that data to the image's server.
    /// By default the reply template therefore shows a reply's images as their alternative text, whether written in
    /// Markdown or as an HTML <c>img</c>. Links and the other options keep the defaults of
    /// <see cref="Polhem.Markdown.MarkdownOptions"/>.
    /// </para>
    /// <para>
    /// Assigning an instance hands that choice to the application, and a new
    /// <see cref="Polhem.Markdown.MarkdownOptions"/> loads images over <c>https</c>. Assign a new instance to change the
    /// options; changing the sets of the assigned one does not rebuild the replies already shown.
    /// </para>
    /// <para>
    /// Only the package's own reply template applies this property. A template the application registers for
    /// <see cref="AssistantReplyEntry"/> shows replies with its own viewer, whose options are the application's to set:
    /// a <see cref="Polhem.Markdown.Avalonia.MarkdownViewer"/> without options loads images over <c>https</c>.
    /// </para>
    /// </remarks>
    public MarkdownOptions? MarkdownOptions
    {
        get => GetValue(MarkdownOptionsProperty);
        set => SetValue(MarkdownOptionsProperty, value);
    }

    /// <summary>
    /// Gets or sets the font family the replies draw CJK text in, such as
    /// <c>PingFang TC, Microsoft JhengHei UI, Noto Sans CJK TC</c>. <see langword="null"/>, the default, leaves it to
    /// the fallback fonts, which can draw bold CJK text regular.
    /// </summary>
    /// <remarks>
    /// The view does not choose a family itself, because Traditional Chinese, Simplified Chinese and Japanese draw the
    /// same characters differently.
    /// </remarks>
    public FontFamily? CjkFontFamily
    {
        get => GetValue(CjkFontFamilyProperty);
        set => SetValue(CjkFontFamilyProperty, value);
    }
}
