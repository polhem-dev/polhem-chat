using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// Builds the view of an entry. A template the application registered wins: the view's own
/// <see cref="Control.DataTemplates"/>, then its ancestors' up to the application. Without one, the package's default
/// template for the entry type is used.
/// </summary>
/// <remarks>
/// The entries panel uses this as its item template because an item template comes before every
/// <see cref="Control.DataTemplates"/> lookup. Placing the defaults anywhere in the tree would let them shadow an
/// application's templates registered further up, or be shadowed by an unrelated template registered nearer.
/// </remarks>
internal sealed class ChatEntryTemplate(ChatView view) : IDataTemplate
{
    private static DefaultEntryTemplates? s_defaults;

    public bool Match(object? data) => data is ChatEntry;

    public Control? Build(object? param)
    {
        var template = view.FindDataTemplate(param)
            ?? Defaults().Values.OfType<IDataTemplate>().FirstOrDefault(candidate => candidate.Match(param));
        return template?.Build(param);
    }

    // The collection expression calls the constructor, which loads the compiled XAML.
    private static DefaultEntryTemplates Defaults() => s_defaults ??= [];
}
