# Phase 10: C# Language Intelligence — TOFIX

**Status:** M2 remediation complete (2026-07-13). No open findings.

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

*Created: 2026-07-13 (M0 planning audit). Last updated: 2026-07-13 (M2 remediation).*