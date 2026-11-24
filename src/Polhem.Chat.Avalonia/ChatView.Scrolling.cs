using Avalonia.Controls;

namespace Polhem.Chat.Avalonia;

/// <summary>Following the newest entry: the view scrolls along until the user scrolls up, and comes back on request.</summary>
public partial class ChatView
{
    // How close to the end, in device-independent pixels, still counts as being at the end. Without some slack a
    // fractional layout offset would make the view stop following a reply it never left.
    private const double EndTolerance = 4;

    private bool _followsLatest = true;

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollViewer is not { } scrollViewer)
        {
            return;
        }
        // NOTE: One layout pass can both grow the content and move the offset, so the deltas cannot tell who moved it
        // from their size alone. The direction can: the view only ever scrolls down, so an offset that went up while the
        // content did not shrink was the user leaving the end.
        if (e.OffsetDelta.Y < 0 && e.ExtentDelta.Y >= 0)
        {
            SetFollowsLatest(IsAtEnd(scrollViewer));
        }
        else if (_followsLatest)
        {
            if (!IsAtEnd(scrollViewer))
            {
                scrollViewer.ScrollToEnd();
            }
        }
        else if (e.OffsetDelta.Y > 0 && IsAtEnd(scrollViewer))
        {
            SetFollowsLatest(true);
        }
    }

    private static bool IsAtEnd(ScrollViewer scrollViewer) =>
        scrollViewer.Offset.Y + scrollViewer.Viewport.Height >= scrollViewer.Extent.Height - EndTolerance;

    private void ScrollToLatest()
    {
        SetFollowsLatest(true);
        _scrollViewer?.ScrollToEnd();
    }

    private void SetFollowsLatest(bool value)
    {
        _followsLatest = value;
        PseudoClasses.Set(ScrolledUpPseudoClass, !value);
    }
}
