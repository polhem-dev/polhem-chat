using Android.Content.PM;
using Avalonia.Android;

namespace ChatGallery.Android;

[Activity(
    Label = "Chat Gallery",
    Theme = "@style/GalleryTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
