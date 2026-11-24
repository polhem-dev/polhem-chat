using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// The look of <see cref="ChatView"/>: its template and the colors of its entries. Add it to the application's styles
/// after the base theme (Fluent, Semi or another), whose buttons, text boxes and colors it builds on.
/// </summary>
/// <example>
/// <code language="xml"><![CDATA[
/// <Application.Styles>
///   <FluentTheme />
///   <chat:ChatTheme />
/// </Application.Styles>
/// ]]></code>
/// </example>
/// <remarks>
/// The brushes have light and dark variants under the keys <c>PolhemChatPromptBackground</c>,
/// <c>PolhemChatCardBackground</c>, <c>PolhemChatCardBorderBrush</c>, <c>PolhemChatErrorBackground</c>,
/// <c>PolhemChatErrorBorderBrush</c> and <c>PolhemChatErrorForeground</c>. Declare the same key in the application's
/// resources to change one.
/// </remarks>
public class ChatTheme : Styles
{
    /// <summary>Initializes a new instance of the <see cref="ChatTheme"/> class.</summary>
    public ChatTheme() => AvaloniaXamlLoader.Load(this);
}
