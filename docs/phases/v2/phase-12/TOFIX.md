# Phase 12: C# Debugging (DAP) — TOFIX

**Status:** Phase 12 is complete (M0–M7 closeout, 2026-07-14). Post-closeout
implementation audit recorded the open finding below.

**Source:** Live-code audit of the DAP transport, debug session lifecycle,
breakpoint/command projection, and Phase 12 contracts in
[IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).

**Gates at audit time (2026-07-14):** `dotnet build Zaide.slnx --no-restore`
completed with 0 errors and the pre-existing `CS0067` test warning; `dotnet test
Zaide.slnx --no-build` exited successfully; `git diff --check` was clean before
this documentation change.

---

## Open — F1: DAP pending-request map is accessed concurrently without synchronization

**Severity:** High
**Area:** `DapContentLengthTransport`

`RequestAsync` adds and removes entries in `_pending` on caller threads, while
the read-loop thread performs response lookups. `DisposeAsync` also enumerates
and clears the same `Dictionary`. `Dictionary<TKey, TValue>` does not support
these concurrent reads/writes. Real overlapping DAP requests can therefore
lose a response, throw during dictionary access/enumeration, or corrupt the
pending-request bookkeeping; the affected request then waits until its timeout
and can incorrectly fail the entire debug session.

This conflicts with the Phase 12 request/recovery contract: protocol failures
must produce a truthful terminal state and a later Start must remain possible.
It is especially reachable while stopped, where stack/scopes/variables loads
and a user selection can overlap, and during teardown racing an outstanding
request.

**Fix hint:** Make pending-request ownership thread-safe. Either guard all
add/remove/lookup/snapshot-and-clear operations with one dedicated lock, or use
`ConcurrentDictionary<int, TaskCompletionSource<JsonElement?>>` and remove each
entry atomically before completing/cancelling it. Keep writes serialized only
for stream framing; do not serialize request completion behind the write gate.
Make disposal atomically detach/cancel a stable snapshot of pending requests,
then add deterministic tests that race multiple outstanding requests, response
dispatch, cancellation, and disposal.

**Evidence:** `src/Services/DapContentLengthTransport.cs` lines 64, 83, 110–112,
and 156 access `_pending` from independent request, disposal, and read-loop
paths without a shared synchronization mechanism.
