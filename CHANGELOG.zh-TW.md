# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.Chat 各套件值得注意的變更。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循
[語意化版本](https://semver.org/lang/zh-TW/)。

## [Unreleased]

## [0.1.1] - 2026-11-24

### 新增

- 回合進行中，對話下方有一行顯示模型正在工作：動態圖示、目前在做什麼（「思考中…」或正在執行的工具標題）、經過時間
  （「2 分 32 秒」）、第幾次要求，以及到目前為止回報的輸出 token。`ChatView.ActivityIcon` 可以換成其他動態 GIF；套件自己
  解碼 GIF，不多加任何依賴。`ChatViewStrings` 新增 `CallFormat`、`SecondsFormat`、`MinutesFormat`、
  `OutputTokensFormat` 字串。
- 每一輪的摘要在發出多次要求時列出要求次數，provider 回報快取時列出輸入 token 中的快取部分
  （`UsageDetails.CachedInputTokenCount`），對應 `CallsFormat`、`CachedTokensFormat` 字串。
- `ChatRunner` 自己計算每一輪的要求次數；自己跑迴圈的應用程式在每次要求前呼叫新的 `ChatTurn.BeginCall`。另新增
  `ChatTurn.Calls`、`ChatTurn.Elapsed`、`TurnSummaryEntry.Calls`（保存在歷史中），以及 `ChatConversation.CurrentTurn`
  與 `ChatTurn.Calls` 的變更通知。

### 變更

- 回覆的複製鈕改成小圖示，只在游標移到該則回覆上時出現，不再在每則回覆下方放一個有文字的按鈕。
- 模型的推理過程與工具呼叫不再以項目顯示：對話只顯示寫給使用者的內容，由進度行說明模型正在做什麼。這些項目仍留在對話與
  歷史中；設定 `ChatView.ShowsActivity`，或按進度行最後的 ›，可以再顯示它們。

### 修正

- 上方的控制項把自己捲入可見範圍時（例如剛關閉的卡片中的按鈕把鍵盤焦點交出去），`ChatView` 不會再停止跟著最新項目捲動。
  現在只有使用者自己捲動（滾輪、捲軸、鍵盤、觸控）才會離開最下方。
- `ChatView` 在變高的同時收到新項目時（例如一輪結束時它下方的一行消失），不會再停止跟著最新項目捲動。之前它把版面移動的
  捲動位置誤當成使用者往上捲，並顯示「回到最新」。

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

[Unreleased]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/polhem-dev/polhem-chat/releases/tag/v0.1.0
