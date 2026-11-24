# Changelog

**English** | [繁體中文](CHANGELOG.zh-TW.md)

Notable changes to the Polhem.Chat packages. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- `Polhem.Chat`: `ChatConversation` keeps the history sent to the model and the entries a user interface shows
  (`UserPromptEntry`, `AssistantReplyEntry`, `ReasoningEntry`, `ToolCallEntry`, `TurnSummaryEntry`, `ErrorEntry`, or an
  entry type of the application's own). `ChatTurn` turns streamed `ChatResponseUpdate`s into those entries as they
  arrive, for applications that run their own loop. `ChatRunner` runs the conversation against any `IChatClient`: it
  sends, streams, stops, measures the time and tokens of each turn, and raises `MessageSending` (which can rewrite or
  cancel the prompt) and `ResponseReceived`. `LoadHistory` and `SaveHistory` restore and keep a conversation, turn
  summaries included.
