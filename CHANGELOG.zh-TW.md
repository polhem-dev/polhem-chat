# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.Chat 各套件值得注意的變更。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循
[語意化版本](https://semver.org/lang/zh-TW/)。

## [Unreleased]

### 修正

- `ChatView` 在沒有任何項目顯示進度時，會顯示回覆正在進行中：送出提示後、第一段推理或文字到達之前，以及工具結果之後、
  下一次回應到達之前。它跟著 `IsGenerating`，所以 `ChatRunner` 和自己跑迴圈的應用程式都適用。主題在 `:waiting`
  偽類下以 `PART_Waiting` 顯示。

## [0.1.0] - 2026-11-24

### 新增

- `Polhem.Chat`：`ChatConversation` 保存送給模型的歷史，以及 UI 顯示的項目（`UserPromptEntry`、`AssistantReplyEntry`、
  `ReasoningEntry`、`ToolCallEntry`、`TurnSummaryEntry`、`ErrorEntry`，或應用程式自訂的項目型別）。`ChatTurn` 把串流的
  `ChatResponseUpdate` 隨到隨轉成這些項目，給自己跑迴圈的應用程式使用。`ChatRunner` 以任何 `IChatClient` 執行對話：
  送出、串流、停止、記錄每輪的時間與 token，並觸發 `MessageSending`（可改寫或取消提問）與 `ResponseReceived`。
  `LoadHistory` 與 `SaveHistory` 還原與保存對話，含每輪的統計。
- `Polhem.Chat.Avalonia`：`ChatView` 顯示對話並接收提問，可透過 `ChatRunner` 執行，也可給自己跑迴圈的應用程式使用
  （`PromptSubmitted`、`StopRequested`、`IsGenerating`）。每種項目都有預設樣板，回覆以 Markdown 呈現，應用程式可為
  新的或內建的項目型別註冊自己的 `DataTemplate`。`ChatPromptBox` 按 Enter 送出、Shift+Enter 換行，輸入法確認組字的
  Enter 不會送出。Esc 或停止鈕可停止回覆並保留已收到的內容；完成的回覆可複製 Markdown 原文；畫面會跟著新文字捲動，
  使用者往上捲之後改顯示「回到最新」。`ChatViewStrings` 內建英文與繁體中文，應用程式可替換；`ChatTheme` 在 Fluent 與
  Semi 的淺色、深色下都能使用。

[Unreleased]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/polhem-dev/polhem-chat/releases/tag/v0.1.0
