# Polhem.Chat.Avalonia 指南

[English](../en/avalonia.md) | **繁體中文**

Avalonia 聊天控制項的完整說明。簡短版本在[套件 README](../../src/Polhem.Chat.Avalonia/README.md)（英文），實際執行的樣子請看
[ChatGallery](../../samples/ChatGallery) 範例。

## 開始使用

控制項需要 Avalonia 12 以上。在基礎主題（Fluent、Semi 或其他）之後加入本套件的主題：

```xml
<Application.Styles>
  <FluentTheme />
  <chat:ChatTheme />
</Application.Styles>
```

其中 `xmlns:chat="using:Polhem.Chat.Avalonia"`。接著在視窗中放一個 `ChatView`，並給它一個使用你的 client 的 runner：

```xml
<chat:ChatView x:Name="Chat" />
```

```csharp
Chat.Runner = new ChatRunner(chatClient);
```

Enter 送出，Shift+Enter 換行，Esc 或停止鈕停止回覆。

## 自行執行對話迴圈

有自己工具迴圈的應用程式，改設定 `Conversation` 而不是 `Runner`，處理 `PromptSubmitted` 與 `StopRequested`，並在以
`ChatConversation.BeginTurn` 串流寫入對話時設定 `IsGenerating`。在每次呼叫模型前呼叫 `ChatTurn.BeginCall`，進度列與該輪摘要
就能顯示這一輪呼叫了模型幾次。

## 活動列

回覆進行中時，對話下方會有一行顯示動畫圖示、模型正在做什麼、已經過的時間、目前是第幾次呼叫，以及到目前為止的輸出 token。
推理與工具呼叫不會顯示成對話項目，除非設定了 `ShowsActivity`，或使用者點了該行最後的 › 把它們展開。`ActivityIcon`
可以指定另一張動畫 GIF 的 `avares://` URI。

## 回覆中的圖片與中日韓文字

回覆是模型產生的文字，不可信任。提示注入可以讓模型寫出一張圖片，其網址夾帶對話內容；載入這張圖片，就會把這些資料送到圖片
所在的伺服器。因此回覆不顯示圖片，只顯示它的替代文字，不論是以 Markdown 還是 HTML `img` 寫的。

這只適用於套件內建的回覆樣板。應用程式為 `AssistantReplyEntry` 自訂的樣板使用自己的 `MarkdownViewer`，除非另外給它選項，
否則它會載入 `https` 圖片。選項要以值設定，不要繫結到 view：viewer 一收到文字就會繪製，早於這類繫結生效的時間。

要顯示圖片，請把 `MarkdownOptions` 設為自己的選項。新建的 `MarkdownOptions` 允許 `https` 圖片；請在指定之前修改
`AllowedImageSchemes`，之後要變更時指定一個新的執行個體：

```csharp
var options = new MarkdownOptions();          // Polhem.Markdown
Chat.MarkdownOptions = options;               // 回覆會載入 https 圖片
```

同一組選項也決定哪些連結可以點擊，以及相對連結如何解析。

中文或日文的粗體字可能因為使用後備字型而顯示成一般粗細。請把 `CjkFontFamily` 設為使用者所讀語言的字型，例如繁體中文用
`PingFang TC, Microsoft JhengHei UI, Noto Sans CJK TC`。

## 樣板、文字與顏色

- 每種對話項目都有預設樣板。在 view 的 `DataTemplates`（或任何上層元素的）加入 `DataTemplate`，就能顯示應用程式自己的
  項目類型，或取代預設樣板。
- 介面文字內建英文與繁體中文。把 `ChatStrings.ForCulture(culture)`（可自行修改）指定給 `Strings`。
- 顏色是主題資源中的筆刷（見 `ChatTheme`）；在你的資源中宣告相同的 key 即可更改。
- view 預設不畫外框。在它上面設定 `BorderBrush`、`BorderThickness` 與 `CornerRadius` 就會畫出外框，內容會裁切在圓角之內。
