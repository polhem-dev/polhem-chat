using Microsoft.Extensions.AI;

namespace ChatGallery.Blazor.Client;

/// <summary>
/// A real model the server offers to the gallery's server page. The server registers it only when it is started with a
/// key, and the client and its key stay in the server's memory; the WebAssembly page never sees it.
/// </summary>
public sealed class ServerModel(IChatClient client, string description)
{
    /// <summary>Gets the client of the model, without tools.</summary>
    public IChatClient Client { get; } = client;

    /// <summary>Gets the model's name and host, as the page shows them.</summary>
    public string Description { get; } = description;
}
