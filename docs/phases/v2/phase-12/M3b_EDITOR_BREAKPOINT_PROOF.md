# Phase 12 M3b: Editor Breakpoint Projection Proof

## Purpose and result

This records Linux validation for Phase 12 M3b: `debug.toggleBreakpoint` / F9
registration, editor breakpoint persistence/projection, and DAP replacement on
mutation while a debug session is active.

**Result: PASS.** Focused M3b unit tests pass, production proof shows a persisted
`Program.cs` breakpoint is sent on launch and hit after continue, and full
regression gates are green.

## Environment and adapter provenance

| Item | Observed value |
|---|---|
| Date | 2026-07-14 |
| Host | Linux x64 |
| SDK | .NET SDK 10.0.109 |
| Adapter | NetCoreDbg 3.2.0-1092 (`NET Core debugger 3.2.0-1`) |
| Adapter path | `/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` |
| Adapter argv | `netcoredbg --interpreter=vscode` |
| Fixture project | `tests/fixtures/workflow-console/WorkflowConsole.csproj` |
| Fixture source | `tests/fixtures/workflow-console/Program.cs` |
| Breakpoint line | 2 (one-based) |

## Automated proof gates

```bash
dotnet test Zaide.slnx --no-build \
  --filter "FullyQualifiedName~EditorBreakpoint|FullyQualifiedName~DebugToggleBreakpoint|FullyQualifiedName~ReplaceBreakpoints|FullyQualifiedName~M3bDebugBreakpointProofTests"
```

Observed on 2026-07-14:

- `DebugToggleBreakpointCommandTests` — PASS (F9 registry binding, availability, caret mapping)
- `EditorBreakpointViewModelTests` — PASS (toggle, projection, DAP replacement, tab switch)
- `EditorBreakpointProjectionTests` — PASS (pure path/line projection)
- `EditorBreakpointRegressionTests` — PASS (folding/tab/search unaffected)
- `DebugSessionServiceTests.ReplaceBreakpointsBySourceAsync_*` — PASS
- `M3bDebugBreakpointProofTests.ProductionProof_PersistedBreakpointSentAndHitAfterContinue` — PASS (~1 s)

The fixture keeps the entry stop on line 1 and places this proof's persisted
breakpoint on the later executable line 2. Reusing the entry location does not
exercise a post-continue breakpoint stop and can time out after a valid continue.

## Operator smoke checklist (workflow-console)

| Step | Gesture / action | Expected visual/behavior | Result |
|---|---|---|---|
| Open `Program.cs` | File tree / open file | Saved on-disk tab active | PASS (fixture path) |
| Toggle breakpoint at caret | `F9` | Enabled margin marker on current line | PASS (command + projection tests) |
| Toggle again | `F9` | Marker removed / disabled state truthful | PASS (toggle tests) |
| Start debugging | `F5` | Persisted breakpoint sent to adapter | PASS (production proof) |
| Continue from entry | service continue | Stops at persisted breakpoint (`reason=breakpoint`) | PASS (production proof) |

## M3b acceptance checklist

- [x] `debug.toggleBreakpoint` registered with default `F9`; no gesture conflict
- [x] F9 unavailable without workspace, saved document, or valid caret line
- [x] Margin click and F9 share ViewModel/service toggle path
- [x] Enabled vs disabled persisted markers projected; adapter verification not shown
- [x] Tab/document switch and close refresh projection safely
- [x] Active-session breakpoint mutations call complete per-source DAP replacement
- [x] Folding, indent guides, search/selection, and tab lifecycle regression tests pass
- [x] Production Linux proof: persist → F5 → breakpoint hit
