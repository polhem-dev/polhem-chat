using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Polhem.Chat.Avalonia;

/// <summary>Following the newest entry: the view scrolls along until the user scrolls up, and comes back on request.</summary>
public partial class ChatView
{
    // How close to the end, in device-independent pixels, still counts as being at the end. Without some slack a
    // fractional layout offset would make the view stop following a reply it never left.
    private const double EndTolerance = 4;

    // How long after the user's last scroll input a move of the offset still counts as theirs. A wheel or trackpad keeps
    // sending events while it moves, so this only has to bridge the gap between two of them.
    private static readonly TimeSpan s_userScrollWindow = TimeSpan.FromMilliseconds(400);

    private bool _followsLatest = true;
    private DateTime _lastUserScroll = DateTime.MinValue;

    private bool IsUserScrolling => DateTime.UtcNow - _lastUserScroll < s_userScrollWindow;

    // Tunnelling, and with handled events too, so the input is seen before the scroll viewer or a scroll bar takes it.
    private void AttachScrollViewer(ScrollViewer? scrollViewer)
    {
        if (_scrollViewer is { } previous)
        {
            previous.ScrollChanged -= OnScrollChanged;
            previous.RemoveHandler(PointerWheelChangedEvent, OnUserScrollInput);
            previous.RemoveHandler(PointerPressedEvent, OnScrollBarPressed);
            previous.RemoveHandler(PointerMovedEvent, OnPointerDragged);
            previous.RemoveHandler(KeyDownEvent, OnScrollKey);
        }
        _scrollViewer = scrollViewer;
        if (scrollViewer is not null)
        {
            scrollViewer.ScrollChanged += OnScrollChanged;
            scrollViewer.AddHandler(PointerWheelChangedEvent, OnUserScrollInput, RoutingStrategies.Tunnel, handledEventsToo: true);
            scrollViewer.AddHandler(PointerPressedEvent, OnScrollBarPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            scrollViewer.AddHandler(PointerMovedEvent, OnPointerDragged, RoutingStrategies.Tunnel, handledEventsToo: true);
            scrollViewer.AddHandler(KeyDownEvent, OnScrollKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void OnUserScrollInput(object? sender, RoutedEventArgs e) => _lastUserScroll = DateTime.UtcNow;

    private void OnScrollBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsInScrollBar(e.Source))
        {
            _lastUserScroll = DateTime.UtcNow;
        }
    }

    // A scroll bar's thumb being dragged, or a finger panning the content.
    private void OnPointerDragged(object? sender, PointerEventArgs e)
    {
        var properties = e.GetCurrentPoint(null).Properties;
        if ((properties.IsLeftButtonPressed && IsInScrollBar(e.Source)) || e.Pointer.Type == PointerType.Touch)
        {
            _lastUserScroll = DateTime.UtcNow;
        }
    }

    private void OnScrollKey(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.PageUp or Key.Home or Key.Down or Key.PageDown or Key.End)
        {
            _lastUserScroll = DateTime.UtcNow;
        }
    }

    private static bool IsInScrollBar(object? source) =>
        source is Visual visual && (visual is ScrollBar || visual.FindAncestorOfType<ScrollBar>() is not null);

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollViewer is not { } scrollViewer)
        {
            return;
        }
        // NOTE: The offset also moves without the user: the layout moves it when the view grows taller, and a control
        // that takes focus brings itself into view, which in a chat is often far above the end (a button of a card that
        // just closed hands its focus on). So only an offset that went up while the user was scrolling, with the wheel,
        // a scroll bar, the keyboard or a touch gesture, leaves the end; any other move is followed back to it.
        if (e.OffsetDelta.Y < 0 && e.ExtentDelta.Y >= 0 && e.ViewportDelta.Y <= 0 && IsUserScrolling)
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
