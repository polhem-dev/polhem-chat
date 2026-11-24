# Polhem.Chat.Avalonia

An AI chat control for Avalonia on top of any [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
`IChatClient`: streamed replies rendered as Markdown with `Polhem.Markdown.Avalonia`, reasoning, tool calls, per-turn
usage, stop and copy. The conversation model is in `Polhem.Chat`.

## Getting started

Add the theme after your base theme (Fluent, Semi or another):

```xml
<Application.Styles>
  <FluentTheme />
  <chat:ChatTheme />
</Application.Styles>
```

with `xmlns:chat="using:Polhem.Chat.Avalonia"`. Then put a `ChatView` in a window and give it a runner for your client:

```xml
<chat:ChatView x:Name="Chat" />
```

```csharp
Chat.Runner = new ChatRunner(chatClient);
```

Enter sends, Shift+Enter starts a new line, and Esc or the stop button stops the reply.

## Running your own loop

An application with its own tool loop sets `Conversation` instead of `Runner`, handles `PromptSubmitted` and
`StopRequested`, and sets `IsGenerating` while it streams into the conversation with `ChatConversation.BeginTurn`.

## Templates, texts and colors

- Each entry type has a default template. Add a `DataTemplate` to the view's `DataTemplates`, or to any ancestor's, to
  show an entry type of your own or to replace a default.
- The texts ship in English and Traditional Chinese. Assign `ChatViewStrings.ForCulture(culture)`, changed as you like,
  to `Strings`.
- The colors are brushes in the theme's resources (see `ChatTheme`); declare the same key in your resources to change
  one.

The packages are in preview; their API is not settled yet. See the [repository](https://github.com/polhem-dev/polhem-chat).
