using Avalonia.Controls;
using Polhem.Chat.Avalonia;

namespace ChatGallery;

/// <summary>
/// The chat view showing a conversation that the host drives itself (<see cref="HostSession"/>): the view only shows the
/// entries and reports the prompts and stop requests, and the host sets <see cref="ChatView.IsGenerating"/>.
/// </summary>
public sealed partial class HostDrivenDemo : UserControl
{
    public HostDrivenDemo()
        : this(new HostSession())
    {
    }

    public HostDrivenDemo(HostSession session)
    {
        Session = session;
        DataContext = session;
        InitializeComponent();
        var chat = Chat;
        chat.Suggestions = ["Plan three days in Kyoto", "Delete my old notes", "規劃京都三日遊"];
        chat.PromptSubmitted += (_, e) => _ = session.SendAsync(e.Text);
        chat.StopRequested += (_, _) => session.Stop();
        ReloadButton.Click += (_, _) => session.ReloadHistory();
        ClearButton.Click += (_, _) => session.Clear();
    }

    public HostSession Session { get; }

}
