using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Polhem.Chat;

namespace ChatGallery;

/// <summary>The two ways to use the chat view on tabs, under switches for the theme, the variant and the language.</summary>
public sealed partial class GalleryView : UserControl
{
    public GalleryView()
        : this(new GallerySession())
    {
    }

    public GalleryView(GallerySession session)
    {
        InitializeComponent();

        // NOTE: The root view of an iOS app keeps the background and text color of the base theme it started with, so
        // the gallery takes them from the theme in use. These are the keys each theme gives its windows.
        this[!BackgroundProperty] = this.GetResourceObservable(session.UsesSemi ? "WindowDefaultBackground" : "SystemControlBackgroundAltHighBrush").ToBinding();
        this[!ForegroundProperty] = this.GetResourceObservable(session.UsesSemi ? "WindowDefaultForeground" : "SystemControlForegroundBaseHighBrush").ToBinding();
        var runner = new RunnerDemo(session);
        var host = new HostDrivenDemo(session.Host);
        RunnerTab.Content = runner;
        HostTab.Content = host;
        var tabs = Demos;
        tabs.SelectedIndex = session.SelectedTab;
        tabs.SelectionChanged += (_, _) => session.SelectedTab = tabs.SelectedIndex;

        var themeBox = ThemeBox;
        themeBox.SelectedIndex = session.UsesSemi ? 1 : 0;
        themeBox.SelectionChanged += (_, _) => ((GalleryApp)Application.Current!).UseBaseTheme(semi: themeBox.SelectedIndex == 1);

        var darkSwitch = DarkSwitch;
        darkSwitch.IsChecked = Application.Current!.ActualThemeVariant == ThemeVariant.Dark;
        darkSwitch.IsCheckedChanged += (_, _) =>
            Application.Current.RequestedThemeVariant = darkSwitch.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;

        var languageBox = LanguageBox;
        UseLanguage();
        languageBox.SelectedIndex = Equals(session.Culture, ToolTitles.Chinese) ? 1 : 0;
        languageBox.SelectionChanged += (_, _) =>
        {
            session.Culture = languageBox.SelectedIndex == 1 ? ToolTitles.Chinese : ToolTitles.English;
            UseLanguage();
        };

        void UseLanguage()
        {
            // The texts are passed with their culture: Avalonia resets the culture of the UI thread, so the view does not
            // read it later.
            var strings = ChatStrings.ForCulture(session.Culture);
            runner.Chat.Strings = strings;
            host.Chat.Strings = strings;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this)?.InsetsManager is { } insets)
        {
            Padding = insets.SafeAreaPadding;
            insets.SafeAreaChanged += (_, args) => Padding = args.SafeAreaPadding;
        }
    }
}
