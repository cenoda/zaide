# Phase 10 M3: Manual Linux Evidence — Diagnostics + Problems

**Date:** 2026-07-13  
**Host:** Linux  
**Server:** csharp-ls 0.25.0 (`/home/cenoda/.dotnet/tools/csharp-ls`)  
**Client:** production `CsharpLsSession` + `LanguageDiagnosticsService` via  
`tools/Phase10M3DiagnosticsSmoke/`

## Command

```bash
export PATH="$PATH:$HOME/.dotnet/tools"

# Fixture with deliberate CS1002 (missing semicolon)
mkdir -p /tmp/zaide-phase10-m3-smoke
# Smoke.csproj + Broken.cs (return 1  // missing semicolon)

dotnet build tools/Phase10M3DiagnosticsSmoke/Phase10M3DiagnosticsSmoke.csproj
dotnet run --project tools/Phase10M3DiagnosticsSmoke/Phase10M3DiagnosticsSmoke.csproj --no-build -- /tmp/zaide-phase10-m3-smoke
```

## Observed result (exact)

```text
Fixture: /tmp/zaide-phase10-m3-smoke
csharp-ls: /home/cenoda/.dotnet/tools/csharp-ls
Session state=Ready gen=3 failure=
PASS publish: count=1
  [Error] CS1002: ; expected @ Broken.cs:6:17
PASS clear: remaining diagnostics=0 (errors on Broken.cs=0)
PASS Phase 10 M3 diagnostics smoke
```

## What this proves

1. Production session starts with real csharp-ls (StreamJsonRpc **parameter-object**
   `initialize` / document notifications — LSP object-shaped `params`).
2. `textDocument/publishDiagnostics` is received, validated (generation / open URI /
   version / utf-16 range), and stored as structured diagnostics.
3. Deliberate syntax error (CS1002) appears for `Broken.cs`.
4. After content is corrected via `Document.Content` (same edit path as the editor),
   the error clears (replace with empty / corrected diagnostics).

## UI surface

Problems projection is hosted in the bottom panel mode strip (`Terminal` |
`Problems`). Unit tests cover `ProblemsViewModel` ready/loading/unavailable/failed
projection and live/stale navigation through `EditorTabViewModel.OpenFileCommand`.

## Limitation

This smoke exercises the production service pipeline end-to-end on Linux with a
real server. Full desktop click-through of the Problems list is not automated here;
service + projection tests cover navigation/state contracts.
