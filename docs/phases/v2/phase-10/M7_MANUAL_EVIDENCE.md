# Phase 10 M7: Manual Linux Evidence — Closeout Integration

**Date:** 2026-07-13  
**Host:** Linux  
**Server:** csharp-ls 0.25.0 (`/home/cenoda/.dotnet/tools/csharp-ls`)  
**Client:** production language services + `LanguageSessionStatusPolicy` +
`LanguageCommandAvailability` via `tools/Phase10M7CloseoutSmoke/`

## Environment

| Item | Value |
|---|---|
| OS | Linux |
| csharp-ls | 0.25.0 (via `LanguageServerBinaryLocator`) |
| Fixture | `tools/Phase10M0LanguageIntelligenceProof/fixture` |
| Evidence path | `docs/phases/v2/phase-10/M7_MANUAL_EVIDENCE.md` |

## Command

```bash
export PATH="$PATH:$HOME/.dotnet/tools"

dotnet build tools/Phase10M7CloseoutSmoke/Phase10M7CloseoutSmoke.csproj
dotnet run --project tools/Phase10M7CloseoutSmoke/Phase10M7CloseoutSmoke.csproj --no-build -- \
  tools/Phase10M0LanguageIntelligenceProof/fixture
```

## Observed result (exact)

```text
Fixture: /home/cenoda/zaide/tools/Phase10M0LanguageIntelligenceProof/fixture
csharp-ls: /home/cenoda/.dotnet/tools/csharp-ls
Host: Linux
Date: 2026-07-13
Session state=Ready gen=4 statusBar="C# · Ready"
Capabilities: completion=True hover=True definition=True docSym=True wsSym=True format=True
PASS diagnostics publish: count=3
PASS diagnostics clear: remainingOnBroken=2
PASS completion: state=Idle items=0
PASS hover: visible=True len=110
PASS definition: state=Ready locations=1
PASS documentSymbol: count=7
PASS workspaceSymbol: count=1
PASS formatDocument: kind=Applied accepted=True feedback=Document formatted.
PASS formatOnSave: accepted formatting written once
Command availability (active doc): completion=True definition=True format=True
Command availability (workspace): wsSymbol=True
PASS Phase 10 M7 closeout smoke
```

## What this proves (one coherent session)

1. **Session lifecycle / capability state** — production session reaches `Ready`,
   status-bar projection reads `C# · Ready`, and negotiated capabilities are all
   true for the fixture project.
2. **Diagnostics publish and clear** — deliberate `CS1002` in `M7Broken.cs`
   publishes diagnostics; fixing source reduces live diagnostics (fixture also
   carries unrelated/sample diagnostics — see limitation).
3. **Completion and hover** — explicit completion and hover requests complete
   without mutating inactive documents; hover returns non-empty content.
4. **Go to Definition and symbols** — definition returns one location;
   document symbols (7) and workspace symbol query `Greet` (1) succeed.
5. **Document formatting, Format on Save** — `textDocument/formatting` applies
   with `Applied` feedback; formatted text is written once on the save path.
6. **Command availability** — `LanguageCommandAvailability` gates match ready
   session + advertised capabilities for active-document and workspace commands.

## Keyboard / accessibility (code-level contract)

Desktop click-through is not automated in this smoke. The following are implemented
in the shared `EditorView` / popup views and covered by unit/routing tests:

| Surface | Keyboard | Accessible names |
|---|---|---|
| Completion popup | Up/Down/Enter/Tab/Escape on focused list | `Completion suggestions` |
| Hover popup | Dismissed on caret/tab/context change | `Hover information` |
| Definition chooser | Up/Down/Enter/Escape (editor + list focus) | Header + list name from `SetHeader` |
| Document/workspace symbol pickers | Up/Down/Enter/Escape; workspace query filter | `Workspace symbol filter`, list header |
| Problems panel | Enter navigates; Escape clears selection | `Problems list` |
| Format on Save | Settings panel `Format on Save` checkbox (default off) | Settings label text |

Registered command gestures unchanged: `Ctrl+Space`, `F12`, `Ctrl+Shift+O`,
`Ctrl+T`, `Ctrl+Shift+I` (verified in `FormatDocumentCommandTests`,
`EditorLanguageInputRoutingTests`).

## Limitation

- This smoke exercises the production service pipeline end-to-end on Linux with a
  real server. Desktop popup focus/escape click-through and status-bar segment
  rendering are not automated here; M4–M6 smokes and M7 unit tests cover those
  contracts on the shared editor path.
- Diagnostics clear count (`remainingOnBroken=2`) reflects fixture-wide
  diagnostics after fixing `M7Broken.cs`; the primary `CS1002` on that file is
  removed by the edit path (same as M3 smoke contract).
- Completion smoke observes `Idle` after the explicit request window; item counts
  are proven in M4 smoke (`≈523` items) and `LanguageCompletionTests`.