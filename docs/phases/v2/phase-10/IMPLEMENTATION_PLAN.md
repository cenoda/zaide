# Phase 10: C# Language Intelligence — Implementation Plan

## Status

**Phase 10 complete** (M7 closeout, 2026-07-13).
**M7 complete** (capability/status feedback, accessibility integration, docs truth-sync, 2026-07-13).
**M6 complete** (whole-document formatting + Format on Save, 2026-07-14).
**M5 complete** (Go to Definition + document/workspace symbols, 2026-07-13).
**M4 complete** (active-document completion + hover, 2026-07-13).
**M3 complete** (structured diagnostics + Problems projection, 2026-07-13).
**M2 complete** (document synchronization bridge, 2026-07-13).
**M1 complete** (language session service + DI wiring, 2026-07-13).
**M0 complete** (live-code baseline + executable technology proof).
Selected stack: **csharp-ls 0.25.0** + **StreamJsonRpc 2.22.23** (stdio
Content-Length JSON-RPC). Evidence:
[M0_DISCOVERY_PROOF.md](M0_DISCOVERY_PROOF.md) §7–§15;
[M3_MANUAL_EVIDENCE.md](M3_MANUAL_EVIDENCE.md);
[M4_MANUAL_EVIDENCE.md](M4_MANUAL_EVIDENCE.md);
[M5_MANUAL_EVIDENCE.md](M5_MANUAL_EVIDENCE.md);
[M6_MANUAL_EVIDENCE.md](M6_MANUAL_EVIDENCE.md);
[M7_MANUAL_EVIDENCE.md](M7_MANUAL_EVIDENCE.md).
Standalone proof: `tools/Phase10M0LanguageIntelligenceProof/`.
Production: `ILanguageSessionService` / `LanguageSessionService`,
`ILanguageDocumentBridge` / `LanguageDocumentBridge`,
`ILanguageDiagnosticsService` / `LanguageDiagnosticsService`,
`ILanguageCompletionService` / `LanguageCompletionService`,
`ILanguageHoverService` / `LanguageHoverService`,
`ILanguageNavigationService` / `LanguageNavigationService`,
`ILanguageSymbolService` / `LanguageSymbolService`,
`ILanguageFormattingService` / `LanguageFormattingService`,
`EditorLanguageInputViewModel`, and `ProblemsViewModel` registered in
`Program.ConfigureServices`; focused tests in
`LanguageSessionServiceTests`, `LanguageSessionServiceDiTests`,
`LanguageDocumentSyncTests`, `LanguageDiagnosticsServiceTests`,
`LanguageCompletionTests`, `LanguageHoverTests`, `EditorLanguageInputRoutingTests`,
`LanguageNavigationTests`, `LanguageSymbolTests`,
`LanguageFormattingTests`, `FormatOnSaveTests`, `FormatDocumentCommandTests`,
`EditorFormattingApplyTests`,
`ProblemsViewModelTests`, `ProblemsNavigationProjectionTests`,
`LanguageSessionStatusPolicyTests`, and `LanguageCommandAvailabilityTests`.

## Scope

**Goal:** Provide C# language intelligence through one verified Language Server
Protocol implementation: lifecycle, diagnostics and Problems projection,
completion, hover, definition navigation, document/workspace symbols, and safe
whole-document formatting.

### Included

- C# LSP server process/client lifecycle tied to the authoritative Phase 8.3
  project context
- Ordered open/change/close document synchronization with cancellation and
  stale-result protection
- Structured diagnostics and truthful Problems projection
- Completion, hover, Go to Definition, document/workspace symbols
- `textDocument/formatting`, applied atomically through normal editor/document
  state, plus `Format Document` registered as `editor.formatDocument` with
  default `Ctrl+Shift+I`
- Optional Format on Save setting, default disabled, only after M6 formatting
  correctness is established

### Boundaries

- C# is the only Phase 10 language. No multi-language server framework,
  semantic rename, code actions, range formatting, format on type, build/test,
  DAP, or agent automation.
- `IProjectContextService` remains the sole project discovery/selection owner;
  `Workspace.WorkspacePath` is not a language-project fallback.
- Views and ViewModels project language state and route editor input only. They
  do not launch servers, own JSON-RPC, select projects, or retain protocol
  state.
- Do not replace the current `Document`/`Workspace`/tab ownership model or
  broadly rearrange `src/`. Any minimal move requires the proof condition in
  `M0_DISCOVERY_PROOF.md` §5.
- No separate formatting engine. A missing or unsupported server capability is
  presented as unavailable and leaves text unchanged.

## Pre-Implementation Verification (M0)

- [x] Re-run and update the live ownership/lifecycle evidence in
      `M0_DISCOVERY_PROOF.md` after selecting a server and client library.
- [x] Compile and run the exact technology proof described there; record real
      versions, process behavior, capability support, licenses, and limitations.
      Proof: 24/24 checks passed (`csharp-ls` 0.25.0 + `StreamJsonRpc` 2.22.23).
- [x] Lock URI/version, context-generation, request-cancellation, and
      document-close semantics before adding production behavior
      (see M0 proof §13).
- [x] Lock position encoding (client advertised `utf-16`/`utf-8`; server omitted
      selection → **utf-16** default; non-BMP 🎉 round-trip confirmed).
- [x] Verify server compatibility with `.slnx` solution files; parent-directory
      workspace is acceptable; **no eligibility-rule amendment** required.
- [x] Verify AvaloniaEdit position-encoding semantics (`Document.GetLocation`/
      `GetOffset` — UTF-16 columns) and document the conversion function
      (M0 proof §10).
- [x] Confirm proof packages build in isolation; production DI registration is
      deferred to M1 (no app-host change at M0). Catalog updated in
      `docs/LIBRARIES.md` for planned StreamJsonRpc + csharp-ls.
- [x] **Formatting prerequisites:** whole-document undo group PASS; caret/
      selection mapping rule locked; Format-on-Save schema lock unchanged
      (`EditorSettings.FormatOnSave` / `formatOnSave`, default `false`,
      `SchemaVersion` 1→2 migration, `SettingsSerializer` ceiling at M6,
      UI deferred to M6) — see M0 proof §4 / §12.

## Milestones

| Milestone | Scope and independent completion condition | Focused verification |
|---|---|---|
| **M0** | Evidence and executable technology proof only. Select and prove one server/client pair; publish its actual lifecycle/capability findings, exact service contracts, document/version rules, command inventory, test list, and source-layout decision. No user-facing language behavior. | **Standalone proof project** (not part of `Zaide.Tests` — a separate console app or script outside the main solution) named `Phase10M0LanguageIntelligenceProof` that compiles, runs the isolated process proof, and records server/library versions. Plus `git diff --check`. |
| **M1** | Add the UI-independent language-session service and DI wiring. It consumes only `IProjectContextService`, owns server process/transport initialization, shutdown, restart, cancellation, error state, and generation-safe teardown. Unsupported/ambiguous/no-project states never start a server. | `LanguageSessionServiceTests` and DI tests cover valid start, all non-ready context states, cancellation, context replacement, server exit, restart, disposal, structured errors, and no old-generation events. |
| **M2** | Add the document synchronization bridge between existing document/tab lifecycle and M1: didOpen, ordered versioned didChange, didClose, reconnect resync, and stale-result rejection. Active-tab changes must not cause duplicate opens or cross-document state. | `LanguageDocumentSyncTests` cover open/edit/save-independent dirty state/tab switch/close/reopen, ordering, monotonic versions, reconnect, cancellation, inactive tabs, and stale context/document/version callbacks. |
| **M3** ✅ | Add structured diagnostics state and Problems projection. Diagnostics are replaced per URI/version, cleared on close/context teardown, and map safely to editor spans. Problems is truthful for unavailable/loading/failure states and supports navigation only to a still-live document/location. | `LanguageDiagnosticsServiceTests`, `ProblemsViewModelTests`, and editor-projection tests cover publish/clear, multi-file updates, invalid ranges, stale diagnostics, close/reload, navigation, and no-project/server-failure states. Manual Linux smoke: open a deliberately invalid C# file, correct it, and confirm Problems clears — evidence in [M3_MANUAL_EVIDENCE.md](M3_MANUAL_EVIDENCE.md). |
| **M4** ✅ | Add active-document completion and hover. Define trigger policy, request cancellation/replacement, deterministic selection/commit behavior, capability-unavailable behavior, and strict document/version matching. No editor mutation occurs from stale or failed requests. | `LanguageCompletionTests`, `LanguageHoverTests`, and `EditorLanguageInputRoutingTests` cover explicit/automatic triggers, debounce/cancel/retrigger, empty/unsupported/failed results, active-tab switches, stale versions/generation, selection commit, hover replacement/dismissal, and non-BMP positions. Manual Linux smoke: [M4_MANUAL_EVIDENCE.md](M4_MANUAL_EVIDENCE.md). |
| **M5** ✅ | Add Go to Definition and document/workspace symbols through registered commands/surfaces. Define multiple/zero result behavior, same-file/cross-file navigation, unavailable/failure feedback, and URI/range validation. Navigation opens files through the existing tab/workspace path. | `LanguageNavigationTests`, `LanguageSymbolTests`, and command-registration tests cover same/cross file, zero/multiple/invalid locations, stale responses, document/workspace symbol ordering, cancellation, and command availability. Manual Linux smoke: [M5_MANUAL_EVIDENCE.md](M5_MANUAL_EVIDENCE.md). |
| **M6** ✅ | Add `textDocument/formatting` and `editor.formatDocument` (`Ctrl+Shift+I`). Validate and atomically apply only safe, current-version edits as one undoable operation; preserve a deterministic caret/selection mapping and normal dirty-state behavior. Add optional `"editor.formatOnSave"` setting (`boolean`, default `false`), enabled only after explicit-save formatting passes. Execution follows the locked M0 contract: format before write, failure/cancellation still saves, `EditorViewModel.SaveAsync` coordinates via a formatting service, no re-trigger loop. **Update `SettingsSerializer` schema-version ceiling from `> 1` to `> 2`; register the v1→v2 `ISettingsMigration` in the production `SettingsService` constructor.** | `LanguageFormattingTests`, `FormatOnSaveTests`, `FormatDocumentCommandTests`, and `EditorFormattingApplyTests` cover no edits, valid edits, invalid/overlap/stale edits, cancellation/failure/unsupported no-op, one undo, dirty state, caret/selection mapping, disabled/enabled save behavior, and the execution contract (format-before-save, failure-still-saves, no re-trigger). Manual Linux smoke: [M6_MANUAL_EVIDENCE.md](M6_MANUAL_EVIDENCE.md). |
| **M7** ✅ | Integrate capability/status feedback, complete accessibility and keyboard smoke evidence, truth-sync docs/architecture/library catalog if changed, and close out only with full regression green. | All Phase 10 focused tests; sequential full build/test; `git diff --check`; recorded Linux smoke for lifecycle, diagnostics, completion/hover, definition/symbols, and formatting. Evidence: [M7_MANUAL_EVIDENCE.md](M7_MANUAL_EVIDENCE.md). |

## Locked Lifecycle and State Rules

1. **Project eligibility** — A language session may start only when the
   `ProjectContext.State` is one of:
   - `SingleProject` — auto-selected single candidate (any `ProjectKind`:
     `Solution`, `SolutionX`, or `CSharpProject`).
   - `Selected` — user-selected candidate from the current snapshot (any
     `ProjectKind`).
   
   All other states (`Unloaded`, `Loading`, `NoProject`, `Unsupported`,
   `Ambiguous`, `Failed`) must prevent a session and publish a truthful
   non-ready state.
   
   **What is passed to the server:** the winning `ProjectCandidate.FilePath`
   (normalised absolute path). The LSP `initialize` request uses the
   *parent directory* as the `workspaceFolders[].uri` / `rootUri` — this is the
   protocol contract. Any server-specific mechanism (e.g. passing the solution
   file path via `initializationOptions`) is deferred to the executable M0
   proof and must not be assumed or hard-coded at planning time. The server
   receives exactly one workspace folder; there is no aggregation or multi-root
   workspace.
2. Session generation changes on project-context replacement, reload, explicit
   restart, process exit, and disposal. Every completion, diagnostic, hover,
   navigation, symbol, and formatting result is checked against generation,
   document URI, and version before use.
3. `Document.Content` remains the durable text/dirty-state owner. LSP changes
   neither bypass it nor introduce a competing text model.
4. Opening a file through existing tabs activates it but does not alone define
   project context. Closing a tab removes its language state and sends didClose
   when a session is live.
5. The shared `EditorView` is presentation infrastructure. It must reset
   transient completion/hover/diagnostic presentation on active-tab transitions
   and may never apply outgoing-tab results to the newly active tab.
6. Every long-running operation accepts cancellation and exposes structured
   ready/loading/unavailable/failed/cancelled state; status text is a projection
   rather than the source of truth.
7. **Format-on-Save execution** (locked at M0, implemented at M6): formatting
   runs before the file write on the in-memory document; a formatting
   failure/cancellation/timeout/unsupported server still saves the current
   content; the coordination lives in `EditorViewModel.SaveAsync` via a
   formatting service interface (not direct LSP coupling); formatting must not
   re-trigger save (see M0 proof for full contract).

## Test and Verification Strategy

- Prefer fake transport/server tests for service behavior; use a real selected
  server only in M0 proof and bounded Linux integration/manual smoke tests.
- Add tests in the milestone that owns their behavior. A named missing test file
  prevents that milestone from closing.
- Run build then test sequentially, never concurrently:

  ```bash
  dotnet build Zaide.slnx --no-restore
  dotnet test Zaide.slnx --no-build
  ```

- Before every milestone handoff, run its focused tests and:

  ```bash
  git diff --check
  ```

- M3 through M7 require recorded Linux manual evidence because real server
  capability negotiation, editor overlays, and navigation cannot be proven by
  unit tests alone.

## Phase 10 Limitations

- Selected pair (M0): **csharp-ls 0.25.0** + **StreamJsonRpc 2.22.23** over
  stdio; see M0 proof for acquisition and limitations.
- Unsaved documents have no Phase 10 language guarantee (M0 used on-disk URIs
  only).
- Range formatting, format on type, code actions, rename, semantic folding,
  and multi-language support are deferred.
- Format on Save remains disabled by default and is unavailable when the server
  does not advertise document-formatting capability.

## Exit Conditions

- [x] M0 through M7 complete with the named focused tests and recorded evidence.
- [x] C# project context drives one clean, cancellable LSP session lifecycle.
- [x] Diagnostics, completion, hover, definitions, symbols, and document
      formatting reject stale results and remain truthful across tabs/context changes.
- [x] Formatting is atomic, undoable, and leaves the document unchanged on all
      failure, cancellation, unsupported, stale, or invalid-edit paths.
- [x] No second project/document ownership model or unproven broad source
      reorganization was introduced.
- [x] `dotnet build Zaide.slnx --no-restore`, `dotnet test Zaide.slnx --no-build`,
      and `git diff --check` pass at closeout.

## Rollback Plan

- Each milestone receives one focused commit after its verification gate.
- Revert only the completed milestone commit(s); preserve M0 discovery evidence
  and record a `REVERT_LOG.md` if a structural Phase 10 implementation reset is
  required.
