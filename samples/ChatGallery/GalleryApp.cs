using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using Polhem.Chat;
using Polhem.Chat.Avalonia;
using Semi.Avalonia;

namespace ChatGallery;

/// <summary>
/// The gallery application, on the desktop as a window and on iOS as the single view. Its styles are a base theme
/// followed by <see cref="ChatTheme"/>, which is what an application adds to use the chat view.
/// </summary>
public sealed class GalleryApp : Application
{
    private readonly GallerySession _session = new();

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new ChatTheme());

        // Every call of the weather tool shows as a card of the gallery's own, in both tabs.
        DataTemplates.Add(new ToolCallTemplate
        {
            ToolName = DemoReplies.WeatherTool,
            Template = new FuncDataTemplate<ToolCallEntry>((_, _) => new WeatherCard()),
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = CreateWindow();
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new GalleryView(_session);
                break;
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Replaces the base theme, which comes before the chat theme so that the chat theme builds on it. The view is taken
    /// down first and built again afterwards, from the same <see cref="GallerySession"/>.
    /// </summary>
    public void UseBaseTheme(bool semi)
    {
        if (_session.UsesSemi == semi)
        {
            return;
        }
        _session.UsesSemi = semi;

        // NOTE: Posted, because the request comes from a control of the view that is about to be taken down. The top
        // level keeps what the previous theme gave it (background, text color, font), so on the desktop the window is
        // replaced too. The root of an iOS app cannot be, and GalleryView paints its own background and text color.
        Dispatcher.UIThread.Post(() =>
        {
            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime { MainWindow: { } previous } desktop:
                    previous.Content = null;
                    Styles[0] = CreateBaseTheme(semi);
                    var window = CreateWindow();
                    window.Width = previous.Width;
                    window.Height = previous.Height;
                    window.Position = previous.Position;
                    desktop.MainWindow = window;
                    window.Show();
                    previous.Close();
                    break;
                case ISingleViewApplicationLifetime singleView:
                    singleView.MainView = null;
                    Styles[0] = CreateBaseTheme(semi);
                    singleView.MainView = new GalleryView(_session);
                    break;
            }
        });
    }

    private static IStyle CreateBaseTheme(bool semi) => semi ? new SemiTheme() : new FluentTheme();

    private Window CreateWindow() =>
        new() { Title = "Polhem.Chat Gallery", Width = 960, Height = 860, Content = new GalleryView(_session) };
}
