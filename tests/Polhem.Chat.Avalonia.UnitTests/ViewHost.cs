using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Polhem.Chat.Avalonia.UnitTests;

internal static class ViewHost
{
    /// <summary>Shows a view in a window, so it has a top level, and lays it out.</summary>
    public static Window Show(ChatView view, double height = 600)
    {
        var window = new Window { Width = 500, Height = height, Content = view };
        window.Show();
        Settle(window);
        return window;
    }

    /// <summary>Runs the pending dispatcher jobs and layout passes, twice, so a scroll that follows a layout lands.</summary>
    public static void Settle(TopLevel window)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Replaces the base theme of the application, which the session starts with Fluent.</summary>
    public static void UseTheme(IStyle baseTheme)
    {
        var styles = Application.Current!.Styles;
        styles.Clear();
        styles.Add(baseTheme);
        styles.Add(new ChatTheme());
    }

    /// <summary>Returns a named part of the view's own template, not of a control inside it.</summary>
    public static T Part<T>(ChatView view, string name) where T : Control =>
        view.GetVisualDescendants().OfType<T>().Single(control => control.Name == name && control.TemplatedParent == view);

    /// <summary>Returns the control the view built for the entry at an index.</summary>
    public static Control EntryView(ChatView view, int index)
    {
        var presenter = Part<ItemsControl>(view, "PART_Entries").ContainerFromIndex(index);
        return Assert.IsType<Control>(Assert.IsType<global::Avalonia.Controls.Presenters.ContentPresenter>(presenter, exactMatch: false).Child, exactMatch: false);
    }

    public static IEnumerable<T> Descendants<T>(Visual root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    /// <summary>Gives the prompt box focus, as a click would.</summary>
    public static ChatPromptBox FocusPrompt(ChatView view)
    {
        var box = Part<ChatPromptBox>(view, "PART_PromptBox");
        box.Focus();
        return box;
    }

    public static void Press(TopLevel window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Settle(window);
    }

    /// <summary>Whether a control and every ancestor up to the view is visible.</summary>
    public static bool IsShown(Visual control) =>
        control.IsVisible && control.GetVisualAncestors().TakeWhile(ancestor => ancestor is not Window).All(ancestor => ancestor.IsVisible);
}
