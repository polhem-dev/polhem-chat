# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.Chat 各套件值得注意的變更。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循
[語意化版本](https://semver.org/lang/zh-TW/)。

## [Unreleased]

### 新增

- `Polhem.Chat`：`ChatConversation` 保存送給模型的歷史，以及 UI 顯示的項目（`UserPromptEntry`、`AssistantReplyEntry`、
  `ReasoningEntry`、`ToolCallEntry`、`TurnSummaryEntry`、`ErrorEntry`，或應用程式自訂的項目型別）。`ChatTurn` 把串流的
  `ChatResponseUpdate` 隨到隨轉成這些項目，給自己跑迴圈的應用程式使用。`ChatRunner` 以任何 `IChatClient` 執行對話：
  送出、串流、停止、記錄每輪的時間與 token，並觸發 `MessageSending`（可改寫或取消提問）與 `ResponseReceived`。
  `LoadHistory` 與 `SaveHistory` 還原與保存對話，含每輪的統計。
