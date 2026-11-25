# Polhem.Chat — guidance for coding agents

## Language

- Everything maintained together is written in **English**: source code, XML documentation, comments, test method
  names and their `DisplayName` text, commit messages, and the files under `.claude/`.
- This overrides any personal or user-level setting that asks for another language for prose. Replies in a
  conversation may still follow the user's language.
- User documents are bilingual (`name.md` and `name.zh-TW.md`); change both files together.

## Project overview

An AI chat control for Avalonia on top of any `Microsoft.Extensions.AI` `IChatClient`. What each release shipped is in
`CHANGELOG.md`; planned work is tracked in the repository's GitHub issues.

- `Polhem.Chat` is the conversation model: observable entries, the conversion of streamed `ChatResponseUpdate`
  contents into entries, and the runner that sends a turn, streams it, stops it and measures it. It has no UI.
- `Polhem.Chat.Avalonia` is the control that shows that model.

Rules that hold across the repository:

- **Version**: `src/Directory.Build.props`, the only place it is declared. The two packages always ship together
  under that version.
- **Dependencies**: `Polhem.Chat` takes only `Microsoft.Extensions.AI.Abstractions`; `Polhem.Chat.Avalonia` takes
  `Polhem.Chat`, Avalonia and `Polhem.Markdown.Avalonia`. Neither may depend on the Polhem framework: the audience is any
  Avalonia developer. `src/Directory.Build.targets` (CHAT9001) and `DependencyGateTests` enforce it. Do not add a
  package reference without asking.
- **Lowest supported versions**: each package reference names the lowest version the package supports, not the newest,
  because that version becomes the minimum for every consumer.
- **Type names**: no public type may share its simple name with a type of `Microsoft.Extensions.AI`, Avalonia or the
  common .NET UI frameworks (`ChatMessage`, `Control`, `MenuItem`, `Message`…), because applications import those
  namespaces next to ours. `PublicApiGateTests` enforces it.
- **No provider client and no storage implementation** ship in these packages: they only know `IChatClient`, and history
  is loaded and saved as `Microsoft.Extensions.AI` messages the application stores however it likes.
- **Tests never call a real model API.** The runner is tested with a scripted `IChatClient` that replays a fixed list of
  updates.
- **Downstream**: the Polhem framework (`polhem-dev/polhem`) plans to use the control in DefineEditor's assistants.
  Changing what the model or the control exposes changes what it sees; say so in the pull request.

## Build and test

```bash
dotnet build Polhem.Chat.slnx --configuration Release
dotnet test Polhem.Chat.slnx --configuration Release --no-build
./check-md-links.sh
```

## Workflow

- How changes reach `main`, and who merges them, is the polhem-dev organization's contributing guide,
  <https://github.com/polhem-dev/.github/blob/main/CONTRIBUTING.md>: contributors work from a fork and open a pull
  request, and only the maintainer merges. `main` is protected, so nothing is pushed to it directly.
- Every change reaches `main` through a pull request. Agents name their branches `claude/<topic>`.
- Build and test locally before pushing when the environment allows it.
- **Never push a `v*` tag without the user's explicit consent.** Once publishing is set up it publishes the packages
  to nuget.org, and a published package cannot be withdrawn.
- Never change a test or the source code just to make a check pass.

## Local working documents

- `local/` at the repository root is ignored by git. Keep plans, drafts and personal notes there.
- Never commit anything under `local/`, never add it with `git add -f`, and never link to it or name a file in it from a
  committed file.
- Work other maintainers need to see belongs in a GitHub issue or pull request.
