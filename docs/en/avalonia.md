# Polhem.Chat.Avalonia guide

**English** | [繁體中文](../zh-TW/avalonia.md)

The full guide to the Avalonia chat control. The [package README](../../src/Polhem.Chat.Avalonia/README.md)
has the short version, and the [ChatGallery](../../samples/ChatGallery) sample shows it running.

## Getting started

The control needs Avalonia 12 or later. Add the theme after your base theme (Fluent, Semi or another):

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
Calling `ChatTurn.BeginCall` before each request to the model lets the progress line and the turn summary show how
many requests the turn took.

## The activity line

While a reply is in progress, one line under the conversation shows an animated icon, what the model is doing, the
running time, the request it is on and the output tokens so far. The reasoning and the tool calls do not show as
entries unless `ShowsActivity` is set, or the user opens them with the › at the end of the line. `ActivityIcon` takes
an `avares://` URI of another animated GIF.

## Images and CJK text in replies

A reply is the model's text, so it is not trusted. A prompt injection can have the model write an image whose address
carries data from the conversation, and loading the image would send that data to the image's server. Replies
therefore show no images: each shows its alternative text, written in Markdown or as an HTML `img`.

This holds for the package's reply template. A template of your own for `AssistantReplyEntry` uses its own
`MarkdownViewer`, which loads `https` images unless you give it options. Set them as a value, not through a binding to
the view, because the viewer renders as soon as its text arrives, before such a binding resolves.

To show images, set `MarkdownOptions` to options of your own. A new `MarkdownOptions` allows images over `https`;
change `AllowedImageSchemes` before assigning it, and assign a new instance to change it later:

```csharp
var options = new MarkdownOptions();          // Polhem.Markdown
Chat.MarkdownOptions = options;               // replies now load https images
```

The same options decide which links can be clicked and how relative links resolve.

Bold Chinese or Japanese text can draw regular through the fallback fonts. Set `CjkFontFamily` to the family for the
language your users read, for example `PingFang TC, Microsoft JhengHei UI, Noto Sans CJK TC` for Traditional Chinese.

## Templates, texts and colors

- Each entry type has a default template. Add a `DataTemplate` to the view's `DataTemplates`, or to any ancestor's, to
  show an entry type of your own or to replace a default.
- The texts ship in English and Traditional Chinese. Assign `ChatStrings.ForCulture(culture)`, changed as you like,
  to `Strings`.
- The colors are brushes in the theme's resources (see `ChatTheme`); declare the same key in your resources to change
  one.
- The view draws no frame by default. Set `BorderBrush`, `BorderThickness` and `CornerRadius` on it to draw one; the
  content is clipped to the rounded corners.
