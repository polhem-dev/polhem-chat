# Polhem.Chat.Blazor

An AI chat component for Blazor on top of any
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) `IChatClient`. It shows the
conversation model of `Polhem.Chat` and uses the same `ChatComposer` and `ChatStrings` as `Polhem.Chat.Avalonia`.

The component is under development and not published yet. See the [repository](https://github.com/polhem-dev/polhem-chat).

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

| Variable | What it sets |
|---|---|
| `--pch-small-font-size` | Summaries, file names and other secondary text. |
| `--pch-code-font` | Tool arguments. |
| `--pch-padding`, `--pch-gap` | The space around the conversation and between entries. |
| `--pch-radius`, `--pch-card-radius` | The corners of the prompt bubbles, and of cards and boxes. |
| `--pch-prompt-background` | The user's prompts and the edit banner. |
| `--pch-card-background`, `--pch-card-border` | Reasoning and tool call cards and attached files; the border also of buttons and the prompt box. |
| `--pch-muted` | Secondary text. |
| `--pch-accent`, `--pch-accent-text` | The send and approve buttons, and the focus ring. |
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
