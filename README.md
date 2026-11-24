# Polhem.Chat

**English** | [繁體中文](README.zh-TW.md)

[![Build CI](https://github.com/polhem-dev/polhem-chat/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-chat/actions/workflows/build-ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=alert_status)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=bugs)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=vulnerabilities)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=code_smells)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-chat&metric=coverage)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-chat)

An AI chat control for [Avalonia](https://avaloniaui.net/), on top of any
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) `IChatClient`: streamed replies
rendered as Markdown, reasoning, tool calls, per-turn usage, stop and copy. It does not depend on any model provider;
bring the `IChatClient` you already use.

| Package | For |
|---------|-----|
| `Polhem.Chat` | The conversation model: streamed updates turned into conversation entries, and a runner for each turn. No UI |
| `Polhem.Chat.Avalonia` | The chat control for Avalonia, rendering Markdown with [Polhem.Markdown.Avalonia](https://github.com/polhem-dev/polhem-markdown) |

The packages are in development and not yet published; their API is not settled.

## Building

```bash
dotnet build Polhem.Chat.slnx --configuration Release
dotnet test Polhem.Chat.slnx --configuration Release --no-build
```

## License

[MIT](LICENSE)
