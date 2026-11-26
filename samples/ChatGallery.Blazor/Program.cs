using System.ClientModel;
using ChatGallery.Blazor.Client;
using ChatGallery.Blazor.Components;
using Microsoft.Extensions.AI;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// A real model for the server page, when the server is started with a key. The key is read from the configuration
// (the OPENAI_API_KEY environment variable, or user secrets) and stays on the server; it is never stored by the gallery.
if (builder.Configuration["OPENAI_API_KEY"] is { Length: > 0 } key)
{
    var model = builder.Configuration["OPENAI_MODEL"] is { Length: > 0 } name ? name : "gpt-4o-mini";
    var endpoint = new Uri(builder.Configuration["OPENAI_ENDPOINT"] is { Length: > 0 } url ? url : "https://api.openai.com/v1");
    IChatClient client = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = endpoint })
        .GetChatClient(model)
        .AsIChatClient();
    builder.Services.AddSingleton(new ServerModel(client, model + " at " + endpoint.Host));
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ChatGallery.Blazor.Client.Gallery).Assembly);

await app.RunAsync();
