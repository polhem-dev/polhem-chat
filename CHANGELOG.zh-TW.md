# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.Chat 各套件的重要變更，每個版本只列幾項重點。每個版本都連到 [`docs/zh-TW/changelogs/`](docs/zh-TW/changelogs/)
下的發佈說明，那裡完整描述每一項變更，並說明每項行為變更影響誰、要怎麼處理。格式依循
[Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循[語意化版本](https://semver.org/lang/zh-TW/)。

## [Unreleased]

- 兩個 view 都新增 `Commands`，讓宿主在輸入框提供指令：在輸入框開頭打 `/` 會列出名稱相符的 `PromptCommand`，只用鍵盤就能以方向鍵和 Enter 選定，螢幕閱讀器會念出目前選取的指令。指令會把文字填進輸入框讓使用者接著寫，設定 `SendsImmediately` 則立刻送出。宿主可以隨時更換清單；沒有提供指令的宿主行為不變。
- `PromptContext` 讓提問帶著使用者沒有打的文字一起送出，例如文件中選取的文字：由 `MessageSending` 的處理常式加進 `MessageSendingEventArgs.Context`。模型會在同一個文字區塊、提問之前收到它；兩個 view 都只在提問上顯示它的標籤，就像檔案一樣。`SaveHistory` 與 `LoadHistory` 會保存它，重新生成或編輯上一則提問時會再送一次。沒有加 context 的應用程式行為不變。

## [0.3.0] - 2026-11-27

詳細內容與遷移步驟：[docs/zh-TW/changelogs/0.3.0.md](docs/zh-TW/changelogs/0.3.0.md)

- **新套件：** `Polhem.Chat.Blazor`，Blazor 的聊天元件（Interactive Server 與 WebAssembly）：串流 Markdown 的對話、活動列、工具核准、含附件的輸入框、重新生成與編輯、slot、項目樣板與主題變數。`ChatGallery.Blazor` 範例示範兩種模式。
- `Polhem.Chat` 新增 `ChatComposer`，承接使用者對 chat view 除了畫面以外的操作（輸入框、檔案、送出、停止、重新生成、編輯、核准），供各 UI 框架的 view 共用；`ChatView` 改用它，原有測試不需修改即全部通過。
- `Polhem.Chat` 新增 `ChatPicturePreview`，不解碼、只讀圖片標頭來判斷附加的圖片是否小到可以預覽；各 view 顯示圖片前都會先用它檢查。
- 新增 `ChatStrings.ShowActivity`，作為顯示推理與工具呼叫的切換鈕名稱；Avalonia view 的切換鈕現在以它作為提示與無障礙名稱。
- 回覆串流時，兩個 view 會把最後一個區塊尚未結束的語法，先照完成後的樣子顯示：強調與行內程式碼先收尾、連結在網址寫完前只顯示文字、表格的標題列先顯示成表格。
- **安全性：** 回覆與思考過程中的雙向嵌入、覆寫與隔離字元（不論直接寫出或以 `&#x202E;` 這類參照寫成）在兩個 view 都以跳脫序列（`\u{202E}`）顯示，畫面上讀到的就是回覆實際的內容；由右至左的文字照常顯示。應用程式自己的回覆樣板可用 `VisibleText.EscapeBidiControls` 做同樣的處理。
- **安全性：** frame 超出宣告尺寸的 GIF，以及超過 5 MB 的圖片，不再顯示預覽。
- **變更：** `PromptSubmittedEventArgs` 與 `ChatAttachmentRejectedEventArgs` 移到 `Polhem.Chat`。
- **變更：** `ChatViewStrings` 改名為 `ChatStrings` 並移到 `Polhem.Chat`，由各 UI 框架的 view 共用，新增 `DescribeTurn` 與 `DescribeToolStatus`；`VisibleText` 在那裡改為公開。
- **變更：** 輸入框、附加的檔案，以及附加、送出、停止鈕合成一張位於對話中的圓角卡片，上方不再有分隔線；送出與停止改為圖示，文字成為 tooltip 與無障礙名稱。
- **變更：** 兩個 view 改用 `Polhem.Markdown.Avalonia` 與 `Polhem.Markdown.Blazor` 0.4.1，連帶使用 `Polhem.Markdown` 0.4.1。自行參考這些套件的應用程式也要改用 0.4.1。
- **修正：** Android 上回覆進行中時，活動圖示會畫成黑色方塊；現在能正常播放。
- **修正：** Avalonia 的 `ChatView` 原本忽略 `BorderBrush`、`BorderThickness` 與 `CornerRadius`；現在它們會在 view 周圍畫出外框並讓四角變圓。預設仍然沒有外框。

## [0.2.1] - 2026-11-25

詳細內容：[docs/zh-TW/changelogs/0.2.1.md](docs/zh-TW/changelogs/0.2.1.md)

- 視圖的「回到最新」按鈕改為圓形圖示：平常是向下箭頭，回覆進行中改為三個依序閃動的圓點。原本的文字保留為提示與無障礙名稱；圓點顏色取自新的資源 `PolhemChatScrollButtonDotBrush`。

## [0.2.0] - 2026-11-25

詳細內容與遷移步驟：[docs/zh-TW/changelogs/0.2.0.md](docs/zh-TW/changelogs/0.2.0.md)

- **安全性：** 以 `ApprovalRequiredAIFunction` 包裝的工具，要使用者在卡片上核准後才會執行；卡片顯示完整參數，隱藏字元一律跳脫顯示。停止或結束該輪則視為拒絕。
- **安全性：** 附加在提問上的檔案（`ChatView.AllowsAttachments`，預設關閉）依 `ChatAttachmentLimits` 檢查：類型採白名單、由檔案內容判斷而非檔名，大小與數量在讀檔前就先檢查。
- **行為變更：** `ToolCallStatus` 新增 `AwaitingApproval` 與 `Rejected` 兩個值。
- **行為變更：** 載入的歷史會顯示 user 訊息中的檔案；有檔案但沒有文字的 user 訊息也會顯示成提問。
- 可以重新生成最後一則回覆、編輯上一則提問：`ChatRunner.RegenerateAsync`、`ChatRunner.EditLastPromptAsync`、`ChatConversation.RewindLastPrompt`，以及 view 上的按鈕（有 runner 時預設開啟，見 `ChatView.AllowsRewind`）。
- `ToolCallTemplate` 以應用程式自己的樣板顯示某個工具的呼叫，即使隱藏活動也會顯示；`ToolCallEntry.TryGetArgument` 與 `TryGetResult` 不靠反射讀取參數與結果。
- `ChatView.HeaderContent`、`PromptHeaderContent`、`PromptFooterContent`（各附樣板屬性）在對話上方、輸入框上方與下方顯示應用程式自己的內容。
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

[Unreleased]: https://github.com/polhem-dev/polhem-chat/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/polhem-dev/polhem-chat/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/polhem-dev/polhem-chat/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.2...v0.2.0
[0.1.2]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/polhem-dev/polhem-chat/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/polhem-dev/polhem-chat/releases/tag/v0.1.0
