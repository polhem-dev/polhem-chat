# Polhem.Chat.Blazor 指南

[English](../en/blazor.md) | **繁體中文**

Blazor 聊天元件的完整說明。簡短版本在[套件 README](../../src/Polhem.Chat.Blazor/README.md)（英文），
[ChatGallery.Blazor](../../samples/ChatGallery.Blazor) 範例則示範它在伺服器與瀏覽器中執行的樣子。

## 開始使用

元件需要 .NET 10。在頁面的 `<head>` 連結 Markdown viewer 的樣式表：

```html
<link rel="stylesheet" href="_content/Polhem.Markdown.Blazor/polhem-markdown.css" />
```

把你的 `IChatClient` 註冊到服務中，例如用 `Microsoft.Extensions.AI` 的 `builder.Services.AddChatClient(...)`。接著在具有互動
轉譯模式的元件中，給 `ChatView` 一個使用它的 runner。view 會撐滿容器的高度，所以容器要有高度：

```razor
@using Microsoft.Extensions.AI
@using Polhem.Chat
@using Polhem.Chat.Blazor

<div style="height: 600px">
    <ChatView Runner="runner" />
</div>

@code {
    private ChatRunner runner = null!;

    [Inject]
    private IChatClient ChatClient { get; set; } = null!;

    protected override void OnInitialized() => runner = new ChatRunner(ChatClient);
}
```

Enter 送出，Shift+Enter 換行，Esc 或停止鈕停止回覆。

## Server 或 WebAssembly

在 WebAssembly 中，元件在瀏覽器裡執行，你在那裡建立的 `ChatRunner` 也一樣。**絕不要把模型的 API 金鑰放進 WebAssembly
應用程式**：任何人都能從下載的檔案讀到它。請把模型 client 留在伺服器：以 Interactive Server 轉譯 view，或讓瀏覽器端的
`IChatClient` 呼叫你自己伺服器上的端點，再由伺服器呼叫模型。

兩種方式 view 都能運作；repo 中的 `ChatGallery.Blazor` 範例以同一個元件示範兩種模式。

## 自行執行對話迴圈

有自己工具迴圈的應用程式，改設定 `Conversation` 而不是 `Runner`，處理 `PromptSubmitted` 與 `StopRequested`，並在以
`ChatConversation.BeginTurn` 串流寫入對話時設定 `IsGenerating`。

## 回覆中的圖片

回覆是模型產生的文字，不可信任。提示注入可以讓模型寫出一張圖片，其網址夾帶對話內容；載入這張圖片，就會把這些資料送到圖片
所在的伺服器。因此回覆不顯示圖片，只顯示它的替代文字。要允許圖片，請把 `MarkdownOptions` 設為自己的選項；新建的
`MarkdownOptions` 允許 `https` 圖片。

## Blazor Server 上的附件

設定 `AllowsAttachments` 後，使用者可以用附加鈕、把檔案拖到 view 上，或在輸入框貼上圖片來附加檔案。在 Blazor Server 上，
瀏覽器會把每個檔案分段串流到伺服器，所以不需要調高 hub 的 `MaximumReceiveMessageSize`（預設 32 KB）；檔案大小上限由
`ChatAttachmentLimits` 決定（預設 5 MB）。送出提示之前，檔案以 circuit 為單位暫存在伺服器記憶體中。

## 主題

view 沿用頁面的字型與文字顏色。其他顏色由文字顏色或頁面的 `Canvas` 顏色推算，所以在淺色或深色頁面上都清楚，頁面切換時也會
跟著變。要更改顏色，請在 view（它的 `Class` 參數）或任何上層元素設定下列 CSS 變數。字型沿用頁面的；要更改，請在 view 上設定
`font-family` 與 `font-size`。

view 本身不畫外框。請在它的容器上畫，做法和 `ChatGallery.Blazor` 範例相同：框線顏色用
`var(--pch-card-border, color-mix(in srgb, currentColor 14%, transparent))`，加上圓角與 `overflow: hidden`。

| 變數 | 設定的對象 |
|---|---|
| `--pch-small-font-size` | 摘要、檔名與其他次要文字。 |
| `--pch-code-font` | 工具參數。 |
| `--pch-padding`、`--pch-gap` | 對話四周的留白，以及項目之間的間距。 |
| `--pch-radius`、`--pch-card-radius` | 提示泡泡的圓角，以及卡片與方塊的圓角。 |
| `--pch-prompt-background` | 使用者的提示與編輯橫幅。 |
| `--pch-card-background`、`--pch-card-border` | 推理與工具呼叫卡片、附加的檔案；框線也用於按鈕與輸入卡片。 |
| `--pch-composer-background`、`--pch-composer-radius` | 容納輸入框、附加檔案與按鈕的卡片。 |
| `--pch-send-background`、`--pch-send-text` | 圓形送出鈕。 |
| `--pch-muted` | 次要文字。 |
| `--pch-accent`、`--pch-accent-text` | 核准鈕與焦點框。 |
| `--pch-error`、`--pch-error-background` | 錯誤。 |
| `--pch-jump-background`、`--pch-jump-dot`、`--pch-shadow` | 跳到最新的按鈕與活動指示圖示。 |

## Slot 與樣板

`HeaderContent`、`EmptyContent`、`PromptHeaderContent` 與 `PromptFooterContent` 放置應用程式自己的內容，位於對話與輸入框
周圍。`PromptTemplate`、`ReplyTemplate`、`ReasoningTemplate`、`ToolCallTemplate`、`SummaryTemplate` 與 `ErrorTemplate`
取代某一種項目的繪製方式，`ToolCallTemplates` 則依工具名稱取代某個工具的呼叫：

```razor
<ChatView Runner="runner" ToolCallTemplates="toolTemplates">
    <EmptyContent><h2>Ask about your orders</h2></EmptyContent>
</ChatView>

@code {
    private readonly Dictionary<string, RenderFragment<ToolCallEntry>> toolTemplates = new()
    {
        ["weather"] = call => @<WeatherCard Call="call" />,
    };
}
```

樣板透過 view 的公開方法操作，例如 `ApproveToolCall`、`Regenerate` 或 `CopyAsync`；用 `@ref` 取得 view。有些事仍由 view
負責：

- 呼叫等待核准時，view 會在樣板下方加上自己的核准區：完整的參數（隱藏字元已跳脫），以及核准與拒絕按鈕。
- 繪製 Markdown 的回覆樣板應傳入 view 的 `ReplyMarkdownOptions`，它預設不載入圖片：回覆中的圖片會從模型指定的任何位置載入，
  而提示注入可以讓它指向接收對話內容的地方。
- 它也應該把項目的 `IsStreaming` 傳給 viewer 的 `IsStreaming`，和 view 內建的樣板一樣：回覆串流時，最後一個區塊尚未結束的
  語法會先照完成後的樣子顯示。
- view 在項目變更時才重繪該項目，而不是頁面每次轉譯都重繪。讀取其他狀態的樣板，在該狀態變更時要呼叫 view 的
  `RedrawEntries`。擷取迴圈變數或區域變數的樣板，每次轉譯都是新的，會重繪所有項目；請把它存在欄位中。
- view 的樣式不會套用到樣板的標記。它的主題值以 `--_pch-*` 變數繼承下來，例如 `var(--_pch-accent)`。
- 樣板中的例外會讓轉譯失敗，和任何元件一樣；用 `ErrorBoundary` 包住樣板來限制影響範圍。

## 語言

介面文字來自 `ChatStrings`，採用建立 view 時的 UI 文化特性（`CultureInfo.CurrentUICulture`），內建英文與繁體中文。給 view
一個自己的 `ChatStrings`，即可選擇語言或修改文字。在 WebAssembly 中，應用程式會載入啟動時文化特性的附屬組件，所以請依 ASP.NET
Core 全球化指南的說明，在應用程式啟動前設定文化特性。
