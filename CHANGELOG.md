# Changelog

**English** | [繁體中文](CHANGELOG.zh-TW.md)

Notable changes to the Polhem.Chat packages. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.1] - 2026-11-24

### Added

- While a turn runs, one line under the conversation shows that the model is at work: an animated icon, what it is
  doing (*Thinking…*, or the title of the tool it runs), the running time ("2m 32s"), the request it is on and the
  output tokens reported so far. `ChatView.ActivityIcon` takes another animated GIF; the package decodes GIFs itself,
  with no further dependency. `ChatViewStrings` has the new `CallFormat`, `SecondsFormat`, `MinutesFormat` and
  `OutputTokensFormat` texts.
- Each turn's summary names the requests the turn sent when there were several, and the cached part of the input
  tokens when the provider reported one (`UsageDetails.CachedInputTokenCount`), with the `CallsFormat` and
  `CachedTokensFormat` texts.
- `ChatRunner` counts the requests of a turn itself. An application that runs its own loop calls the new
  `ChatTurn.BeginCall` before each one. Also new: `ChatTurn.Calls`, `ChatTurn.Elapsed`, `TurnSummaryEntry.Calls`
  (kept in the saved history), and change notification of `ChatConversation.CurrentTurn` and `ChatTurn.Calls`.

### Changed

- A reply's copy button is a small icon that shows while the pointer is over the reply, instead of a labeled button
  under every reply.
- The model's reasoning and its tool calls no longer show as entries: the conversation shows what is written for the
  user, and the activity line tells what the model is doing. The entries stay in the conversation and its history.
  `ChatView.ShowsActivity`, or the › at the end of the activity line, shows them again.

### Fixed

- `ChatView` no longer stops following the newest entry when a control far up brings itself into view, as when a
  button of a card that just closed hands its keyboard focus on. Only the user's own scrolling (wheel, scroll bar,
  keyboard, touch) leaves the end now.
- `ChatView` no longer stops following the newest entry when entries arrive while it grows taller, such as when a line
  under it goes away at the end of a turn. It took the offset the layout moved for the user scrolling up, and showed
  Jump to latest.

## [0.1.0] - 2026-11-24

### Added

- `Polhem.Chat`: `ChatConversation` keeps the history sent to the model and the entries a user interface shows
  (`UserPromptEntry`, `AssistantReplyEntry`, `ReasoningEntry`, `ToolCallEntry`, `TurnSummaryEntry`, `ErrorEntry`, or an
  entry type of the application's own). `ChatTurn` turns streamed `ChatResponseUpdate`s into those entries as they
  arrive, for applications that run their own loop. `ChatRunner` runs the conversation against any `IChatClient`: it
  sends, streams, stops, measures the time and tokens of each turn, and raises `MessageSending` (which can rewrite or
  cancel the prompt) and `ResponseReceived`. `LoadHistory` and `SaveHistory` restore and keep a conversation, turn
  summaries included.
- `Polhem.Chat.Avalonia`: `ChatView` shows a conversation and takes prompts, either through a `ChatRunner` or for an
  application that runs its own loop (`PromptSubmitted`, `StopRequested`, `IsGenerating`). Each entry type has a default
  template, replies render as Markdown, and applications register their own `DataTemplate`s for new or built-in entry
  types. `ChatPromptBox` sends on Enter, starts a new line on Shift+Enter, and ignores the Enter that confirms an input
  method's composition. Esc or the stop button stops a reply and keeps what arrived; finished replies copy their
  Markdown; the view follows new text until the user scrolls up and then offers Jump to latest. `ChatViewStrings` ships
  English and Traditional Chinese texts that applications can replace, and `ChatTheme` works on Fluent and Semi, light
  and dark.

[Unreleased]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/polhem-dev/polhem-chat/releases/tag/v0.1.0
