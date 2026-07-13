# Phase 10: C# Language Intelligence — TOFIX

**Status:** Phase 10 complete (M7 closeout, 2026-07-14). Post-closeout audit
findings for M5 symbol surface lifecycle and `LanguageSymbolTests`
hardening resolved (2026-07-14).

---

## Resolved — F1: Stale document-symbol response leaves surface stuck in Loading

**Severity:** Medium
**Resolved:** 2026-07-14
**Area:** `LanguageSymbolService`, `LanguageSymbolTests`

**Fix:** Added `DismissStaleIfCurrentLocked` helper that publishes
`LanguageSymbolState.Cancelled` (which auto-collapses to Idle per F4) when a
stale response is discarded but the surface still belongs to that request.
Applied at every stale-return point in `ExecuteDocumentRequestAsync` and
`ExecuteWorkspaceRequestAsync`: cancellation, active-document validation
failure, session generation mismatch, and `OperationCanceledException` /
cancelled `Exception` catch blocks. The `RequestId`-mismatch lock check
intentionally does not dismiss (a newer request already owns the surface).

**Tests:** `DocumentSymbols_StaleVersionDoesNotUpdateSurface` now asserts
`Idle` and `!IsSurfaceOpen`. `DocumentSymbols_StaleGenerationDoesNotInstallReady`
added for generation-based staleness.

---

## Resolved — F2: Active-tab-change test overclaims service-level dismiss

**Severity:** Low (test quality)
**Resolved:** 2026-07-14
**Area:** `LanguageSymbolTests`

**Fix:** Rewrote `DocumentSymbols_ActiveTabChange_DismissesSurface` to drive
the tab change through `EditorLanguageInputViewModel.ActiveDocumentId`, which
mirrors the production path (`ActiveDocumentId` setter → `DismissAll()` →
`_symbolService.Dismiss()`). Assertions now verify `Current.State == Idle`,
`!IsSurfaceOpen`, and `TryAcceptSelected() == null`.

---

## Resolved — F3: LanguageSymbolTests coverage thinner than M5 plan / navigation suite

**Severity:** Low
**Resolved:** 2026-07-14
**Area:** `tests/Zaide.Tests/Services/LanguageSymbolTests.cs`

**Tests added (8):**

| Test | Contract covered |
|---|---|
| `MoveSelection_WrapsAroundBoundaries` | MoveSelection delta ±1, forward and backward wrap |
| `DocumentSymbols_CapabilityUnsupported_UnavailableThenIdle` | Document capability unsupported → Unavailable then Idle |
| `DocumentSymbols_RequestFailure_FailedThenIdle` | Null document-symbol result → Failed then Idle |
| `DocumentSymbols_StaleGenerationDoesNotInstallReady` | Session generation advance mid-flight, Ready never installed |
| `DocumentSymbols_DocumentClosed_DismissesSurface` | Document close dismisses document-scope surface |
| `SessionLeavesReady_DismissesSymbolSurface` | Session leaves Ready → surface dismissed |
| `WorkspaceSymbols_InvalidLocationsFilteredOut` | Workspace symbols with null Location or empty FilePath filtered; valid survives |
| `WorkspaceSymbols_AllInvalidLocations_Empty` | All workspace symbols filtered → Empty with feedback message |

---

## Resolved — F4: Unused `LanguageSymbolState.Cancelled`

**Severity:** Low
**Resolved:** 2026-07-14
**Area:** `LanguageSymbolState`, `LanguageSymbolService`

**Fix:** Published `Cancelled` via `DismissStaleIfCurrentLocked` (F1) when an
in-flight surface request is discarded due to staleness or cancellation.
`PublishTerminal` now auto-collapses `Cancelled` → `Idle` alongside
`Unavailable` and `Failed`. The enum value is retained — it provides a
transient signal that the UI could observe (e.g. a brief status message)
without changing the locked presentation contract that surfaces collapse to
Idle.

---

## Resolved — M3 StreamJsonRpc LSP params shape (2026-07-13)

**Symptom:** Real csharp-ls initialize failed with
`RemoteInvocationException: Internal error: JsonSerializationException` when
driven through production `CsharpLsSession` (unit tests used fakes and did not
catch this). Root cause: StreamJsonRpc `InvokeWithCancellationAsync(...,
object[])` / positional `NotifyAsync` sent JSON-RPC `params` as an array, while
LSP requires a single parameter object.

**Fix:** Use `InvokeWithParameterObjectAsync` for `initialize` and
`NotifyWithParameterObjectAsync` for document notifications; register
`textDocument/publishDiagnostics` with
`UseSingleObjectParameterDeserialization = true`.

**Evidence:** `docs/phases/v2/phase-10/M3_MANUAL_EVIDENCE.md` and
`tools/Phase10M3DiagnosticsSmoke/`.

---

## Resolved — M2 generation-order defect (2026-07-13)

**Symptom:** `LanguageDocumentBridge.ApplySessionSnapshotAsync` treated any
snapshot whose `Generation` differed from `_syncGeneration` as a forward
advance. When asynchronous snapshot handlers completed out of order, a delayed
older-generation snapshot could cancel the current CTS, reset tracked document
versions/open flags, regress `_syncGeneration`, and resync against a stale
session — leaving the bridge desynchronized from the newest ready generation.

**Fix:** Ignore snapshots with `snapshot.Generation < _syncGeneration` entirely;
advance sync state only when `snapshot.Generation > _syncGeneration`.
Regression test: `StaleOlderGenerationSnapshot_IsIgnoredAndKeepsNewerSync` in
`LanguageDocumentSyncTests`.

---

*Created: 2026-07-13 (M0 planning audit). Last updated: 2026-07-14 (post-closeout
audit: F1–F4 resolved).*
