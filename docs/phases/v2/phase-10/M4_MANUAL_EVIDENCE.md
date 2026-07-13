# Phase 10 M4: Manual Linux Evidence — Completion + Hover

**Date:** 2026-07-13  
**Host:** Linux  
**Server:** csharp-ls 0.25.0 (`/home/cenoda/.dotnet/tools/csharp-ls`)  
**Client:** production `CsharpLsSession` + `LanguageCompletionService` +
`LanguageHoverService` via `tools/Phase10M4CompletionHoverSmoke/`

## Command

```bash
export PATH="$PATH:$HOME/.dotnet/tools"

dotnet build tools/Phase10M4CompletionHoverSmoke/Phase10M4CompletionHoverSmoke.csproj
dotnet run --project tools/Phase10M4CompletionHoverSmoke/Phase10M4CompletionHoverSmoke.csproj --no-build -- \
  tools/Phase10M0LanguageIntelligenceProof/fixture
```

## Observed result (exact)

```text
Fixture: /home/cenoda/zaide/tools/Phase10M0LanguageIntelligenceProof/fixture
csharp-ls: /home/cenoda/.dotnet/tools/csharp-ls
Session state=Ready gen=4 failure=
PASS completion: items=523 first=AbandonedMutexException
PASS hover: ```csharp
string Sample.Greet(string name)
```

A simple greet helper for hover/definition/completion targets.
PASS stale: dismissed completion did not mutate inactive/other editor text
PASS Phase 10 M4 completion/hover smoke
```

## What this proves

1. Production session negotiates completion/hover capabilities and serves
   `textDocument/completion` / `textDocument/hover` with parameter-object JSON-RPC.
2. Explicit completion on `Sample.cs` returns a non-empty item list (≈523 items at the
   `Greet` call site).
3. Hover at the same position returns Markdown/signature content for `Sample.Greet`.
4. After switching the active document and dismissing completion, stale completion
   state does not mutate text on the newly active file.

## Trigger policy (locked M4)

- **Explicit:** `editor.triggerSuggest` (default `Ctrl+Space`) — immediate request.
- **Automatic:** server-advertised trigger characters only (csharp-ls: `.`, `'`), debounced
  200 ms, cancellable.
- **Hover:** 450 ms dwell after caret movement; cancelled/replaced on caret/document/tab/session
  changes.

## Limitation

This smoke exercises the production service pipeline end-to-end on Linux with a real server.
Desktop popup/tooltip click-through is not automated; unit and input-routing tests cover
editor presentation contracts on the shared `EditorView`.
