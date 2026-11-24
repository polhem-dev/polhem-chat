# Polhem.Chat

[English](README.md) | **繁體中文**

[![Build CI](https://github.com/polhem-dev/polhem-chat/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-chat/actions/workflows/build-ci.yml)

[Avalonia](https://avaloniaui.net/) 的 AI 聊天控制項，可接任何
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) 的 `IChatClient`：
以 Markdown 呈現的串流回覆、推理過程、工具呼叫、每輪用量、停止與複製。它不依賴任何模型供應商，沿用你已經在用的
`IChatClient` 即可。

| 套件 | 用途 |
|------|------|
| `Polhem.Chat` | 對話模型：把串流更新轉成對話項目，並負責執行每一輪。不含 UI |
| `Polhem.Chat.Avalonia` | Avalonia 的聊天控制項，以 [Polhem.Markdown.Avalonia](https://github.com/polhem-dev/polhem-markdown) 呈現 Markdown |

這些套件仍在開發中，尚未發佈，API 也還沒定案。

## 建置

```bash
dotnet build Polhem.Chat.slnx --configuration Release
dotnet test Polhem.Chat.slnx --configuration Release --no-build
```

## 授權

[MIT](LICENSE)
