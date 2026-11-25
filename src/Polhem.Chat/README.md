# Polhem.Chat

An observable conversation model for AI chat user interfaces, built on
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai). It turns the streamed updates
of any `IChatClient` into conversation entries (text, reasoning, tool calls and their results, usage, errors) and runs
each turn with stop, timing and token counts. It has no user interface of its own; applications use it through a
front-end package:

- `Polhem.Chat.Avalonia`: a chat control for Avalonia.

What the user does with a view apart from drawing it (the prompt box and its files, submitting, stopping, regenerating,
editing the last prompt, approving tool calls) is `ChatComposer`, and the texts the views show, in English and
Traditional Chinese, are `ChatStrings`. Both are shared by every front end.

The packages are at 0.x: a minor version may still change the API. See the [repository](https://github.com/polhem-dev/polhem-chat).
