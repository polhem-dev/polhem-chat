using Avalonia;
using Avalonia.VisualTree;
using Polhem.Markdown;
using Polhem.Markdown.Avalonia;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// The viewer of the default reply template. It applies <see cref="ChatView.MarkdownOptions"/> of the view it is in,
/// and options that load no images when that is <see langword="null"/>.
/// </summary>
/// <remarks>
/// <para>
/// IMPORTANT: The options are set in the constructor, not bound. A viewer renders as soon as its text is set, and the
/// text binding resolves from the data context before a binding to an ancestor such as the view does, so a bound
/// <see cref="MarkdownViewer.Options"/> is still <see langword="null"/> on the first render and the viewer falls back to
/// <see cref="MarkdownOptions"/> defaults, which load images over <c>https</c>. <c>ReplyMarkdownTests</c> checks that a
/// reply loaded from history makes no connection.
/// </para>
/// <para>
/// Out of the visual tree, as when the view is on a hidden tab, the viewer loads no images either, whatever the view
/// it left allowed. It follows the view again once it is back.
/// </para>
/// <para>
/// Each viewer holds its own options that load no images, so code that reaches one through
/// <see cref="MarkdownViewer.Options"/> cannot open images for every view.
/// </para>
/// </remarks>
internal sealed class ReplyMarkdownViewer : MarkdownViewer
{
    private readonly MarkdownOptions _noImages = CreateNoImages();
    private ChatView? _view;

    public ReplyMarkdownViewer()
    {
        Options = _noImages;
    }

    // Styles written for the Markdown viewer apply to this one too.
    protected override Type StyleKeyOverride => typeof(MarkdownViewer);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _view = this.FindAncestorOfType<ChatView>();
        if (_view is not null)
        {
            _view.PropertyChanged += OnViewPropertyChanged;
            ApplyOptions(_view.MarkdownOptions);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_view is not null)
        {
            _view.PropertyChanged -= OnViewPropertyChanged;
            _view = null;
        }

        // IMPORTANT: The text keeps arriving while the reply is out of the tree, and the viewer renders it there. Without
        // a view to follow, the viewer must not keep options that allow images.
        Options = _noImages;
        base.OnDetachedFromVisualTree(e);
    }

    private static MarkdownOptions CreateNoImages()
    {
        var options = new MarkdownOptions();
        options.AllowedImageSchemes.Clear();
        return options;
    }

    private void OnViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ChatView.MarkdownOptionsProperty)
        {
            ApplyOptions(e.GetNewValue<MarkdownOptions?>());
        }
    }

    // NOTE: Assigning the same instance again does not rebuild, so attaching costs nothing while the view keeps its default.
    private void ApplyOptions(MarkdownOptions? options) => Options = options ?? _noImages;
}
