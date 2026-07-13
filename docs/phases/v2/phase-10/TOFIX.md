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

---

## Resolved — F5: Blocking Dispose in LanguageSessionService

**Severity:** Low
**Resolved:** 2026-07-14
**Area:** `src/Services/LanguageSessionService.cs`, `src/Services/ILanguageSessionService.cs`

**Fix:** Implemented `IAsyncDisposable` on `ILanguageSessionService` and
`LanguageSessionService`. The synchronous `Dispose()` now offloads teardown I/O
via `Task.Run` to avoid capturing a UI-thread `SynchronizationContext`,
preventing a potential deadlock. `DisposeAsync()` performs the same teardown
with proper `await` semantics for DI containers that support async disposal.
Updated all 12 test fakes of `ILanguageSessionService` to implement the new
`DisposeAsync()` method.

---

## Open — F6: SemaphoreSlim gate held across async LSP I/O in LanguageDocumentBridge

**Severity:** Low (throughput trade-off, not a bug)
**Opened:** 2026-07-14
**Area:** `src/Services/LanguageDocumentBridge.cs`

**Issue:** `HandleContentChangedAsync`, `TryOpenDocumentAsync`, and similar
methods hold the `_gate` SemaphoreSlim across `await session.NotifyDidChangeAsync(...)`
calls. This serializes all document operations for LSP message ordering
correctness (didOpen before didChange before didClose), but means a slow LSP
server blocks all document sync operations.

**Suggested fix:** Acceptable as-is for correctness. If throughput becomes an
issue, consider a two-phase approach: acquire gate for state read, release,
send notification, re-acquire for state update. However, this risks
reordering, so the current design is safer.

---

## Resolved — F7: Dead `immediate` parameter in LanguageCompletionService

**Severity:** Trivial
**Resolved:** 2026-07-14
**Area:** `src/Services/LanguageCompletionService.cs`

**Fix:** Removed the unused `bool immediate` parameter from the private methods
`BeginRequestLocked` and `ExecuteRequestAsync`, along with the
`_ = immediate;` discard. The two call sites (`RequestExplicit` and
`DebouncedAutomaticAsync`) now pass only `filePath` and `caretOffset`.
No behavioral change.

---

## Resolved — F8: LanguageDiagnosticRange reused for formatting edit ranges

**Severity:** Trivial (naming clarity)
**Resolved:** 2026-07-14
**Area:** `src/Services/LanguageDiagnosticRange.cs` → `src/Services/LspRange.cs`

**Fix:** Renamed `LanguageDiagnosticRange` to `LspRange` across all 18 source
and test files. The file was renamed from `LanguageDiagnosticRange.cs` to
`LspRange.cs` via `git mv`. No behavioral change — the type is a general-purpose
LSP range (line/character positions) used by diagnostics, completion, formatting,
and navigation.

---

## Open — F9: Unused LanguageSessionFailureKind enum values

**Severity:** Trivial
**Opened:** 2026-07-14
**Area:** `src/Services/LanguageSessionFailureKind.cs`

**Issue:** `ProcessStartFailed` and `ShutdownFailed` enum values are defined
but no code path creates them. `LanguageSessionService` only produces
`MissingServerBinary`, `InitializeFailed`, and `ServerExited`.

**Suggested fix:** Either remove the unused values (if YAGNI applies) or add
code paths that use them (e.g., catch `Win32Exception` in session start and
publish `ProcessStartFailed`, catch exceptions in `ShutdownAsync` and publish
`ShutdownFailed`). The values are reasonable forward-looking additions, so
keeping them is acceptable.

---

## Open — F10: CsharpLsSession is a 669-line monolith

**Severity:** Info (maintainability)
**Opened:** 2026-07-14
**Area:** `src/Services/CsharpLsSession.cs`

**Issue:** `CsharpLsSession` mixes transport setup (Process + StreamJsonRpc),
LSP protocol marshalling (request/notification methods), diagnostic parsing,
capability parsing, and process lifecycle management in a single 669-line file.
This is the largest file in the Phase 10 implementation.

**Suggested fix:** Decompose into focused classes if a second server backend is
added or if the file becomes harder to maintain. Candidates for extraction:
`LspTransport` (process + JSON-RPC setup), `LspProtocolMarshaller` (request/
notification serialization), `LspDiagnosticParser`, `LspCapabilitiesParser`.
Not urgent — the file is well-structured internally and hidden behind
`ILanguageServerSession`.

---

## Open — F11: FindDocumentByPath is O(n) per diagnostics publish

**Severity:** Info (performance)
**Opened:** 2026-07-14
**Area:** `src/Services/LanguageDiagnosticsService.cs`

**Issue:** `FindDocumentByPath` performs a linear scan of `_workspace.Documents`
on every `textDocument/publishDiagnostics` notification. For a small number of
open documents this is negligible, but it could be optimized with a dictionary
lookup if the document count grows large.

**Suggested fix:** Add a `Dictionary<string, Document>` index keyed by path in
`Workspace` or in `LanguageDiagnosticsService` itself. Update on document
open/close. Not urgent — current performance is acceptable for typical usage.

---

*Created: 2026-07-13 (M0 planning audit). Last updated: 2026-07-14 (post-closeout
audit: F1–F4 resolved; F5–F11 opened).*
