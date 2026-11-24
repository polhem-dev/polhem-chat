# Polhem.Chat

[English](README.md) | **繁體中文**

[![Build CI](https://github.com/polhem-dev/polhem-chat/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-chat/actions/workflows/build-ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=alert_status)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=bugs)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=vulnerabilities)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=code_smells)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=coverage)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)

[Avalonia](https://avaloniaui.net/) 的 AI 聊天控制項，可接任何
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) 的 `IChatClient`：
以 Markdown 呈現的串流回覆、即時顯示模型正在做什麼的一行（推理、工具呼叫、時間與 token）、每輪用量、停止與複製。它不依賴任何模型供應商，沿用你已經在用的
`IChatClient` 即可。

| 套件 | 用途 |
|------|------|
| `Polhem.Chat` | 對話模型：把串流更新轉成對話項目，並負責執行每一輪。不含 UI |
| `Polhem.Chat.Avalonia` | Avalonia 的聊天控制項，以 [Polhem.Markdown.Avalonia](https://github.com/polhem-dev/polhem-markdown) 呈現 Markdown |

套件發佈在 [nuget.org](https://www.nuget.org/packages?q=Polhem.Chat)，各版變更見 [CHANGELOG](CHANGELOG.zh-TW.md)。
目前是 0.x：minor 版仍可能變更 API，CHANGELOG 會寫明。

## 開始使用

控制項需要 Avalonia 12 以上。從一個 Avalonia 應用程式開始（例如用 `dotnet new avalonia.app` 建立），加入控制項和你的
模型用戶端。任何 `IChatClient` 都可以，這個範例用 OpenAI：

```bash
dotnet add package Polhem.Chat.Avalonia
dotnet add package Microsoft.Extensions.AI.OpenAI
```

把 `ChatTheme` 加進應用程式的樣式，放在基礎主題之後（支援 Fluent 與 Semi，淺色與深色皆可）：

```xml
<Application xmlns:chat="using:Polhem.Chat.Avalonia" ...>
  <Application.Styles>
    <FluentTheme />
    <chat:ChatTheme />
  </Application.Styles>
</Application>
```

在視窗裡放一個 `ChatView`：

```xml
<Window xmlns:chat="using:Polhem.Chat.Avalonia" ...>
  <chat:ChatView x:Name="Chat" />
</Window>
```

再在視窗的建構式裡給它一個包住你 client 的 runner：

```csharp
using Microsoft.Extensions.AI;
using OpenAI;
using Polhem.Chat;

InitializeComponent();
IChatClient client = new OpenAIClient(System.Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
    .GetChatClient("gpt-4.1-mini")
    .AsIChatClient();
Chat.Runner = new ChatRunner(client);
```

設定 `OPENAI_API_KEY` 後執行應用程式即可。Enter 送出、Shift+Enter 換行，Esc 或停止鈕停止回覆。要讓模型呼叫工具，
以 `UseFunctionInvocation()` 包住 client，並把工具放進 runner 的 `Options`。

自己跑迴圈的應用程式改設 `Conversation`，並處理 `PromptSubmitted` 與 `StopRequested`。樣板、文字與顏色的自訂方式見
[套件 README](src/Polhem.Chat.Avalonia/README.md)（英文）。

## 範例程式

`samples/ChatGallery` 示範控件的兩種用法：交給執行器，以及由應用程式自己操作對話，並示範一個應用程式自訂的項目型別。
上方的切換可改變基礎主題（Fluent 或 Semi）、淺色或深色，以及控件文字的語言。它用腳本化的回覆回答，不需要金鑰：
串流顯示推理、工具呼叫與結果、Markdown 和 token 數。要求「錯誤」或「長一點的回覆」可看到這兩種情況。
在 **Model** 底下可以改接 OpenAI 相容的端點；金鑰由使用者輸入，或讀取環境變數 `OPENAI_API_KEY`，不會儲存。

```bash
dotnet run --project samples/ChatGallery.Desktop
```

iOS head 需要 iOS workload，所以不在方案裡。在 Mac 上建置並在模擬器啟動：

```bash
dotnet build samples/ChatGallery.iOS -c Release -p:RuntimeIdentifier=iossimulator-arm64
dotnet build samples/ChatGallery.iOS -c Release -t:Run -p:RuntimeIdentifier=iossimulator-arm64 -p:_DeviceName=":v2:udid=<模擬器 UDID>"
```

如果建置訊息說 iOS workload 需要另一版 Xcode，建置時把 `DEVELOPER_DIR` 指向那一版，並先刪除該 head 的 `bin` 與 `obj`
資料夾再重新建置。

## 建置

```bash
dotnet build Polhem.Chat.slnx --configuration Release
dotnet test Polhem.Chat.slnx --configuration Release --no-build
```

## 授權

[MIT](LICENSE)
