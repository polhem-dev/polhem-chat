using Avalonia;
using ChatGallery;

AppBuilder.Configure<GalleryApp>()
    .UsePlatformDetect()
    .WithInterFont()
    .StartWithClassicDesktopLifetime(args);
