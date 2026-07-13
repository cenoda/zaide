# Phase 10: C# Language Intelligence — TOFIX

**Status:** Phase 10 complete (M7 closeout, 2026-07-13). No open findings.

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

*Created: 2026-07-13 (M0 planning audit). Last updated: 2026-07-13 (Phase 10 M7 complete).*
