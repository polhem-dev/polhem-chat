# Polhem.Chat.Blazor guide

**English** | [繁體中文](../zh-TW/blazor.md)

The full guide to the Blazor chat component. The [package README](../../src/Polhem.Chat.Blazor/README.md)
has the short version, and the [ChatGallery.Blazor](../../samples/ChatGallery.Blazor) sample shows it running on the
server and in the browser.

## Getting started

The component needs .NET 10. Link the Markdown viewer's stylesheet in the page's `<head>`:

```html
<link rel="stylesheet" href="_content/Polhem.Markdown.Blazor/polhem-markdown.css" />
```

Register your `IChatClient` with the services, for example with `builder.Services.AddChatClient(...)` from
`Microsoft.Extensions.AI`. Then give a `ChatView` a runner for it, in a component with an interactive render mode. The
view fills the height of its container, so give the container one:

```razor
@using Microsoft.Extensions.AI
@using Polhem.Chat
@using Polhem.Chat.Blazor

<div style="height: 600px">
    <ChatView Runner="runner" />
</div>

@code {
    private ChatRunner runner = null!;

    [Inject]
    private IChatClient ChatClient { get; set; } = null!;

    protected override void OnInitialized() => runner = new ChatRunner(ChatClient);
}
```

Enter sends, Shift+Enter starts a new line, and Esc or the stop button stops the reply.

## Server or WebAssembly

On WebAssembly the component runs in the browser, and so does a `ChatRunner` you create there. **Never put a model API
key in a WebAssembly app**: anyone can read it from the downloaded files. Keep the model client on the server: render
the view with Interactive Server, or have the browser's `IChatClient` call an endpoint of your own server that calls the
model.

The view works either way; the repository's `ChatGallery.Blazor` sample shows both modes with the same component.

## Running your own loop

An application with its own tool loop sets `Conversation` instead of `Runner`, handles `PromptSubmitted` and
`StopRequested`, and sets `IsGenerating` while it streams into the conversation with `ChatConversation.BeginTurn`.

## Images in replies

A reply is the model's text, so it is not trusted. A prompt injection can have the model write an image whose address
carries data from the conversation, and loading the image would send that data to the image's server. Replies
therefore show no images, only their alternative text. Set `MarkdownOptions` to options of your own to allow them; a
new `MarkdownOptions` allows images over `https`.

## Attachments on Blazor Server

With `AllowsAttachments`, the user attaches files with the attach button, by dropping them on the view or by pasting a
picture in the prompt box. On Blazor Server the browser streams each file to the server in pieces, so the hub's
`MaximumReceiveMessageSize` (32 KB by default) does not need raising; `ChatAttachmentLimits` decides how large a file may
be (5 MB by default). The files wait in the server's memory, per circuit, until the prompt is sent.

## Theme

The view inherits the page's font and text color. Its other colors are drawn from the text color, or from the page's
`Canvas` color, so it reads on a light or a dark page and follows the page when it switches. To change them, set these
CSS variables on the view (its `Class` parameter) or on any ancestor. The font is the page's; set `font-family` and
`font-size` on the view to change it.

The view draws no frame of its own. Draw one on its container, as the `ChatGallery.Blazor` sample does: a border in
`var(--pch-card-border, color-mix(in srgb, currentColor 14%, transparent))`, a radius and `overflow: hidden`.

| Variable | What it sets |
|---|---|
| `--pch-small-font-size` | Summaries, file names and other secondary text. |
| `--pch-code-font` | Tool arguments. |
| `--pch-padding`, `--pch-gap` | The space around the conversation and between entries. |
| `--pch-radius`, `--pch-card-radius` | The corners of the prompt bubbles, and of cards and boxes. |
| `--pch-prompt-background` | The user's prompts and the edit banner. |
| `--pch-card-background`, `--pch-card-border` | Reasoning and tool call cards and attached files; the border also of buttons and the composer card. |
| `--pch-composer-background`, `--pch-composer-radius` | The card that holds the prompt box, its attached files and its buttons. |
| `--pch-send-background`, `--pch-send-text` | The round send button. |
| `--pch-muted` | Secondary text. |
| `--pch-accent`, `--pch-accent-text` | The approve button and the focus ring. |
| `--pch-error`, `--pch-error-background` | Errors. |
| `--pch-jump-background`, `--pch-jump-dot`, `--pch-shadow` | The jump-to-latest button and the activity spinner. |

## Slots and templates

`HeaderContent`, `EmptyContent`, `PromptHeaderContent` and `PromptFooterContent` take the application's own content
around the conversation and the prompt box. `PromptTemplate`, `ReplyTemplate`, `ReasoningTemplate`, `ToolCallTemplate`,
`SummaryTemplate` and `ErrorTemplate` replace how a kind of entry is drawn, and `ToolCallTemplates` the calls of one
tool, by its name:

```razor
<ChatView Runner="runner" ToolCallTemplates="toolTemplates">
    <EmptyContent><h2>Ask about your orders</h2></EmptyContent>
</ChatView>

@code {
    private readonly Dictionary<string, RenderFragment<ToolCallEntry>> toolTemplates = new()
    {
        ["weather"] = call => @<WeatherCard Call="call" />,
    };
}
```

A template acts through the view's public methods, such as `ApproveToolCall`, `Regenerate` or `CopyAsync`; take the view
with `@ref`. Some things stay with the view:

- While a call waits for approval, the view adds its own approval under the template: the arguments in full, hidden
  characters escaped, and the Approve and Reject buttons.
- A reply template that renders Markdown should pass the view's `ReplyMarkdownOptions`, which load no images by
  default: an image in a reply loads from wherever the model points it, and a prompt injection can make it point
  somewhere that receives the conversation.
- The view draws an entry again when the entry changes, not with every render of the page. A template that reads other
  state calls the view's `RedrawEntries` when it changes. A template that captures a loop or local variable is new with
  every render and draws all entries again; keep it in a field.
- The view's styles do not reach a template's markup. Its theme values are inherited as `--_pch-*` variables, such as
  `var(--_pch-accent)`.
- An exception in a template fails the render, as in any component; wrap the template in an `ErrorBoundary` to contain it.

## Languages

The texts come from `ChatStrings`, in the UI culture the view is created in (`CultureInfo.CurrentUICulture`), with
English and Traditional Chinese included. Give the view a `ChatStrings` of your own to choose the language or change a
text. On WebAssembly, the app loads the satellite assembly of the culture it starts with, so set the culture before the
app starts, as the ASP.NET Core globalization guide describes.
