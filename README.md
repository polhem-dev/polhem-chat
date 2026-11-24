# Polhem.Chat

**English** | [繁體中文](README.zh-TW.md)

[![Build CI](https://github.com/polhem-dev/polhem-chat/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-chat/actions/workflows/build-ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=alert_status)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=bugs)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=vulnerabilities)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=code_smells)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=coverage)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)

An AI chat control for [Avalonia](https://avaloniaui.net/), on top of any
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) `IChatClient`: streamed replies
rendered as Markdown, a live line that shows what the model is doing (reasoning, tool calls, time and tokens), per-turn usage, stop and copy. It does not depend on any model provider;
bring the `IChatClient` you already use.

| Package | For |
|---------|-----|
| `Polhem.Chat` | The conversation model: streamed updates turned into conversation entries, and a runner for each turn. No UI |
| `Polhem.Chat.Avalonia` | The chat control for Avalonia, rendering Markdown with [Polhem.Markdown.Avalonia](https://github.com/polhem-dev/polhem-markdown) |

The packages are published on [nuget.org](https://www.nuget.org/packages?q=Polhem.Chat); the changes of each release
are in the [CHANGELOG](CHANGELOG.md). They are at 0.x: a minor version may still change the API, and the CHANGELOG says
when it does.

## Getting started

The control needs Avalonia 12 or later. Start from an Avalonia application, for example one made with
`dotnet new avalonia.app`, and add the control and a client for your model. Any `IChatClient` works; this example uses
OpenAI:

```bash
dotnet add package Polhem.Chat.Avalonia
dotnet add package Microsoft.Extensions.AI.OpenAI
```

Add `ChatTheme` to the application's styles, after the base theme (Fluent and Semi are both supported, light and dark):

```xml
<Application xmlns:chat="using:Polhem.Chat.Avalonia" ...>
  <Application.Styles>
    <FluentTheme />
    <chat:ChatTheme />
  </Application.Styles>
</Application>
```

Put a `ChatView` in a window:

```xml
<Window xmlns:chat="using:Polhem.Chat.Avalonia" ...>
  <chat:ChatView x:Name="Chat" />
</Window>
```

and give it a runner for your client in the window's constructor:

```csharp
using Microsoft.Extensions.AI;
using OpenAI;
using Polhem.Chat;

InitializeComponent();
IChatClient client = new OpenAIClient(System.Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
    .GetChatClient("gpt-4.1-mini")
    .AsIChatClient();
Chat.Runner = new ChatRunner(client);
```

Set `OPENAI_API_KEY` and run the application. Enter sends, Shift+Enter starts a new line, and Esc or the stop button
stops the reply. For tool calls, wrap the client with `UseFunctionInvocation()` and pass the tools in the runner's
`Options`.

An application that runs its own loop sets `Conversation` instead and handles `PromptSubmitted` and `StopRequested`.
The [package README](src/Polhem.Chat.Avalonia/README.md) covers templates, texts and colors.

Replies show no images: a reply is the model's text, and an image address in it could carry data from the conversation
to the image's server. Set `MarkdownOptions` to allow them. For Chinese or Japanese replies, set `CjkFontFamily` so
that bold CJK text draws bold.

## Gallery

`samples/ChatGallery` shows the view both ways: with a runner, and with a conversation the application drives itself,
including an entry type of the application's own. Switches on top change the base theme (Fluent or Semi), the variant
(light or dark) and the language of the view's texts. It answers with scripted replies, so it needs no key: they stream
reasoning, a tool call and its result, Markdown and the token counts. Ask for an error or a long reply to see those
cases. Under **Model**, an OpenAI-compatible endpoint can be used instead; the key is typed in, or read from the
`OPENAI_API_KEY` environment variable, and is never stored.

```bash
dotnet run --project samples/ChatGallery.Desktop
```

The iOS head is not in the solution, because it needs the iOS workload. On a Mac, build and start it on a simulator:

```bash
dotnet build samples/ChatGallery.iOS -c Release -p:RuntimeIdentifier=iossimulator-arm64
dotnet build samples/ChatGallery.iOS -c Release -t:Run -p:RuntimeIdentifier=iossimulator-arm64 -p:_DeviceName=":v2:udid=<simulator UDID>"
```

If the build says the iOS workload needs another Xcode version, point `DEVELOPER_DIR` at that version for the build, and
delete the head's `bin` and `obj` folders before building again.

## Building

```bash
dotnet build Polhem.Chat.slnx --configuration Release
dotnet test Polhem.Chat.slnx --configuration Release --no-build
```

## License

[MIT](LICENSE)
