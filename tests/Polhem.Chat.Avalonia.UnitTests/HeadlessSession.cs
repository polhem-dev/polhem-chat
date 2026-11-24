using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace Polhem.Chat.Avalonia.UnitTests;

/// <summary>
/// Starts the Avalonia headless platform. Every test of <see cref="HeadlessCollection"/> runs its body on the session's
/// UI thread through <see cref="RunAsync(Func{Task})"/>, with a new application for each test.
/// </summary>
public sealed class HeadlessSession : IDisposable
{
    private readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.StartNew(typeof(HeadlessSession), AvaloniaTestIsolationLevel.PerTest);

    /// <summary>
    /// The application the session starts: the Fluent theme and the chat theme on the headless platform, laying out text
    /// with Skia. A test that needs another base theme swaps it with <see cref="ViewHost.UseTheme"/>.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public Task RunAsync(Action action) => _session.Dispatch(action, CancellationToken.None);

    public Task RunAsync(Func<Task> action)
        => _session.Dispatch(async () =>
        {
            await action();
            return true;
        }, CancellationToken.None);

    public void Dispose() => _session.Dispose();

    private sealed class TestApplication : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            Styles.Add(new ChatTheme());
        }
    }
}
