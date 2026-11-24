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
以 Markdown 呈現的串流回覆、推理過程、工具呼叫、每輪用量、停止與複製。它不依賴任何模型供應商，沿用你已經在用的
`IChatClient` 即可。

| 套件 | 用途 |
|------|------|
| `Polhem.Chat` | 對話模型：把串流更新轉成對話項目，並負責執行每一輪。不含 UI |
| `Polhem.Chat.Avalonia` | Avalonia 的聊天控制項，以 [Polhem.Markdown.Avalonia](https://github.com/polhem-dev/polhem-markdown) 呈現 Markdown |

這些套件仍在開發中，尚未發佈，API 也還沒定案。

## 開始使用

把 `ChatTheme` 加進應用程式的樣式，放在基礎主題之後（支援 Fluent 與 Semi，淺色與深色皆可），在視窗裡放一個
`ChatView`，再給它一個包住你 client 的 runner：

```xml
<Application.Styles>
  <FluentTheme />
  <chat:ChatTheme />
</Application.Styles>
```

```csharp
chatView.Runner = new ChatRunner(chatClient);
```

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
