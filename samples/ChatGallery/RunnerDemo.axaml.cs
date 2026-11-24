using System.ClientModel;
using System.Globalization;
using Avalonia.Controls;
using Microsoft.Extensions.AI;
using OpenAI;
using Polhem.Chat;

namespace ChatGallery;

/// <summary>
/// The chat view with a <see cref="ChatRunner"/>: the view sends each prompt through the runner, streams the reply and
/// stops it. The runner answers with <see cref="DemoChatClient"/> until another model is chosen.
/// </summary>
public sealed partial class RunnerDemo : UserControl
{
    private const string KeyVariable = "OPENAI_API_KEY";

    private readonly GallerySession _session;

    public RunnerDemo()
        : this(new GallerySession())
    {
    }

    public RunnerDemo(GallerySession session)
    {
        _session = session;
        InitializeComponent();
        ModelStatus.Text = session.ModelDescription;

        // The short way: one line gives the view a runner, which sends, streams and stops.
        Chat.Runner = session.Runner;
        Chat.Suggestions = ["Plan three days in Kyoto", "Give me a long reply", "Show me an error", "規劃京都三日遊"];

        var providerBox = ProviderBox;
        var endpointFields = EndpointFields;
        providerBox.SelectionChanged += (_, _) => endpointFields.IsVisible = providerBox.SelectedIndex == 1;
        UseButton.Click += (_, _) => UseModel(providerBox.SelectedIndex == 1);
    }


    private void UseModel(bool endpoint)
    {
        if (!endpoint)
        {
            Replace(new DemoChatClient(), options: null, "Scripted replies.");
            return;
        }

        var key = KeyBox.Text;
        if (string.IsNullOrWhiteSpace(key))
        {
            key = Environment.GetEnvironmentVariable(KeyVariable);
        }
        if (string.IsNullOrWhiteSpace(key)
            || !Uri.TryCreate(EndpointBox.Text, UriKind.Absolute, out var uri)
            || ModelBox.Text is not { Length: > 0 } model)
        {
            ModelStatus.Text = "Enter an endpoint, a model and a key, or set " + KeyVariable + ".";
            return;
        }

        // The key stays in memory for this session only; the gallery never stores it.
        var openAi = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = uri });
        var client = openAi.GetChatClient(model).AsIChatClient().AsBuilder().UseFunctionInvocation().Build();
        var tool = AIFunctionFactory.Create(
            () => DateTimeOffset.Now.ToString("F", CultureInfo.InvariantCulture),
            ToolTitles.CurrentTimeTool,
            "Returns the current local date and time.");
        Replace(client, new ChatOptions { Tools = [tool] }, model + " at " + uri.Host + ".");
    }

    private void Replace(IChatClient client, ChatOptions? options, string description)
    {
        // The runner reads the client when a turn starts, so a turn in progress finishes on the previous one.
        _session.Runner.Client = client;
        _session.Runner.Options = options;
        _session.ModelDescription = description;
        ModelStatus.Text = description;
    }
}
