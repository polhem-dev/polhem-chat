# Changelog

**English** | [繁體中文](CHANGELOG.zh-TW.md)

Notable changes to the Polhem.Chat packages, a few lines per version. Each version links to its release notes under
[`docs/en/changelogs/`](docs/en/changelogs/), which describe every change in full and, for each change in behaviour,
who it affects and what to do. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

- The view's jump-to-latest button is a round icon: an arrow down, or three pulsing dots while a reply is in progress. Its text stays as the tooltip and the accessible name, and the dots take the new `PolhemChatScrollButtonDotBrush` resource.

## [0.2.0] - 2026-11-25

Details and migration steps: [docs/en/changelogs/0.2.0.md](docs/en/changelogs/0.2.0.md)

- **Security:** A tool wrapped in `ApprovalRequiredAIFunction` runs only after the user approves it on its card, which shows the arguments in full with hidden characters escaped; stopping or ending the turn rejects it.
- **Security:** Files attached to a prompt (`ChatView.AllowsAttachments`, off by default) are checked against `ChatAttachmentLimits`: an allowlist of types read from the content, not the name, and limits on size and count checked before a file is read.
- **Changed:** `ToolCallStatus` has two new values, `AwaitingApproval` and `Rejected`.
- **Changed:** A loaded history shows the files of its user messages, and a user message with files but no text shows as a prompt.
- The last reply can be regenerated and the last prompt edited: `ChatRunner.RegenerateAsync`, `ChatRunner.EditLastPromptAsync`, `ChatConversation.RewindLastPrompt`, and buttons in the view, on by default with a runner (`ChatView.AllowsRewind`).
- `ToolCallTemplate` draws the calls of one tool with a template of the application's own, shown even while the activity is hidden; `ToolCallEntry.TryGetArgument` and `TryGetResult` read their values without reflection.
- `ChatView.HeaderContent`, `PromptHeaderContent` and `PromptFooterContent`, each with a template, show the application's own content above the conversation and above and below the prompt box.
- `ChatTurn.AddEntry` adds an entry of the application's own while a turn is in progress, before the turn's summary.

## [0.1.2] - 2026-11-25

Details and migration steps: [docs/en/changelogs/0.1.2.md](docs/en/changelogs/0.1.2.md)

- **Changed:** `Polhem.Markdown.Avalonia` 0.2.1: replies render highlighted code, math, Mermaid diagrams, footnotes, alerts and allowed raw HTML. It brings `Svg.Skia` and `Avalonia.AvaloniaEdit` as new dependencies.
- **Security:** Replies load no images, written in Markdown or in HTML, so a prompt injection cannot send data out through an image address. `ChatView.MarkdownOptions` allows them.
- `ChatView.CjkFontFamily` draws bold CJK text in replies bold.

## [0.1.1] - 2026-11-24

Details and migration steps: [docs/en/changelogs/0.1.1.md](docs/en/changelogs/0.1.1.md)

- **Changed:** Reasoning and tool calls no longer show as entries; one live activity line tells what the model is doing, and `ChatView.ShowsActivity` brings the entries back.
- **Changed:** A reply's copy button is a small icon that shows while the pointer is over the reply.
- The activity line shows an animated icon, what the model is doing, the running time, the request it is on and the output tokens so far.
- Each turn's summary names its requests when there were several, and the cached part of the input tokens.
- `ChatTurn.BeginCall` lets an application that runs its own loop count its requests; `ChatRunner` counts them itself.
- `ChatView` keeps following the newest entry when a control higher up scrolls itself into view, and when the view grows taller as entries arrive.

## [0.1.0] - 2026-11-24

Details: [docs/en/changelogs/0.1.0.md](docs/en/changelogs/0.1.0.md)

- First release of `Polhem.Chat` and `Polhem.Chat.Avalonia`.
- `ChatConversation`, `ChatTurn` and `ChatRunner` turn the streamed updates of any `IChatClient` into entries a user interface shows.
- `ChatView` shows a conversation run by a `ChatRunner`, or by an application that runs its own loop.
- Replies render as Markdown, and applications add templates for entry types of their own.
- The prompt box sends on Enter and ignores the Enter that confirms an input method's composition.
- English and Traditional Chinese texts, and a theme for Fluent and Semi, light and dark.

[Unreleased]: https://github.com/polhem-dev/polhem-chat/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.2...v0.2.0
[0.1.2]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/polhem-dev/polhem-chat/releases/tag/v0.1.0
