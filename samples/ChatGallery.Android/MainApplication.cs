using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace ChatGallery.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<GalleryApp>
{
    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();
}
