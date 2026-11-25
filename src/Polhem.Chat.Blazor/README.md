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
