using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Polhem.Chat.Avalonia;

/// <summary>The default template of each entry type, keyed by a name; see <see cref="ChatEntryTemplate"/>.</summary>
internal sealed partial class DefaultEntryTemplates : ResourceDictionary
{
    public DefaultEntryTemplates() => AvaloniaXamlLoader.Load(this);
}
