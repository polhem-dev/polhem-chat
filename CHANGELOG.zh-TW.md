# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.Chat 各套件的重要變更，每個版本只列幾項重點。每個版本都連到 [`docs/zh-TW/changelogs/`](docs/zh-TW/changelogs/)
下的發佈說明，那裡完整描述每一項變更，並說明每項行為變更影響誰、要怎麼處理。格式依循
[Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循[語意化版本](https://semver.org/lang/zh-TW/)。

## [Unreleased]

詳細內容與遷移步驟：[docs/zh-TW/changelogs/0.2.0.md](docs/zh-TW/changelogs/0.2.0.md)

- **安全性：** 以 `ApprovalRequiredAIFunction` 包裝的工具，要使用者在卡片上核准後才會執行；卡片顯示完整參數，隱藏字元一律跳脫顯示。停止或結束該輪則視為拒絕。
- **行為變更：** `ToolCallStatus` 新增 `AwaitingApproval` 與 `Rejected` 兩個值。
- `ChatTurn.AddEntry` 在回合進行中加入應用程式自己的項目，排在該輪摘要之前。

## [0.1.2] - 2026-11-25

詳細內容與遷移步驟：[docs/zh-TW/changelogs/0.1.2.md](docs/zh-TW/changelogs/0.1.2.md)

- **行為變更：** 改用 `Polhem.Markdown.Avalonia` 0.2.1，回覆可呈現程式碼上色、數學式、Mermaid 圖表、註腳、提示區塊與允許的原始 HTML；新增依賴 `Svg.Skia` 與 `Avalonia.AvaloniaEdit`。
- **安全性：** 回覆不載入圖片（Markdown 與 HTML 寫法皆同），提示注入無法透過圖片網址把資料送出；以 `ChatView.MarkdownOptions` 開啟。
- `ChatView.CjkFontFamily` 讓回覆中粗體的中日文字以粗體顯示。

## [0.1.1] - 2026-11-24

詳細內容與遷移步驟：[docs/zh-TW/changelogs/0.1.1.md](docs/zh-TW/changelogs/0.1.1.md)

- **行為變更：** 推理過程與工具呼叫不再以項目顯示，改由一行即時的進度行說明模型正在做什麼；設定 `ChatView.ShowsActivity` 可再顯示這些項目。
- **行為變更：** 回覆的複製鈕改成小圖示，只在游標移到該則回覆上時出現。
- 進度行顯示動態圖示、模型正在做什麼、經過時間、第幾次要求，以及到目前為止的輸出 token。
- 每一輪的摘要在發出多次要求時列出次數，並列出輸入 token 中的快取部分。
- 自己跑迴圈的應用程式用 `ChatTurn.BeginCall` 計算要求次數；`ChatRunner` 會自己計算。
- 上方的控制項把自己捲入可見範圍、或 `ChatView` 在收到新項目時變高，畫面都會繼續跟著最新項目捲動。

## [0.1.0] - 2026-11-24

詳細內容：[docs/zh-TW/changelogs/0.1.0.md](docs/zh-TW/changelogs/0.1.0.md)

- `Polhem.Chat` 與 `Polhem.Chat.Avalonia` 首次發佈。
- `ChatConversation`、`ChatTurn` 與 `ChatRunner` 把任何 `IChatClient` 的串流更新轉成 UI 顯示的項目。
- `ChatView` 顯示由 `ChatRunner` 執行的對話，或由應用程式自己跑迴圈的對話。
- 回覆以 Markdown 呈現，應用程式可以為自訂的項目型別加上樣板。
- 輸入框按 Enter 送出，輸入法確認組字的 Enter 不會送出。
- 內建英文與繁體中文字串，主題支援 Fluent 與 Semi 的淺色、深色。

[Unreleased]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.2...HEAD
[0.1.2]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/polhem-dev/polhem-chat/releases/tag/v0.1.0
