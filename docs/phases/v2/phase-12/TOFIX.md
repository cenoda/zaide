# Phase 12: C# Debugging (DAP) — TOFIX

**Status:** Phase 12 is complete (M0–M7 closeout, 2026-07-14). Post-closeout
implementation audit recorded the open finding below.

**Source:** Live-code audit of the DAP transport, debug session lifecycle,
breakpoint/command projection, and Phase 12 contracts in
[IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).

**Gates at closeout (2026-07-14):** `dotnet build Zaide.slnx --no-restore`
completed with 0 errors and the pre-existing `CS0067` test warning; `dotnet test
Zaide.slnx --no-build` exited successfully; `git diff --check` was clean.

---

## Resolved — F1: DAP pending-request map is accessed concurrently without synchronization

**Severity:** High (was)
**Area:** `DapContentLengthTransport`
**Resolved:** 2026-07-14

**Problem:** `RequestAsync`, the read loop, and `DisposeAsync` accessed the same
`Dictionary<int, TaskCompletionSource<JsonElement?>>` without synchronization.
Overlapping DAP requests could lose responses, throw during enumeration, or
leave stale pending entries until timeout.

**Implementation:** Replaced `_pending` with
`ConcurrentDictionary<int, TaskCompletionSource<JsonElement?>>`. Response
dispatch, caller cancellation, and `RequestAsync` cleanup each `TryRemove` the
sequence before completing/cancelling so only one path owns a pending entry.
`DisposeAsync` now cancels the read loop, immediately drains pending requests
via key snapshot + `TryRemove`, then tears down the read loop and write gate.
Content-Length writes remain serialized behind `_writeGate` only.

**Tests:** Added `tests/Zaide.Tests/Services/DapContentLengthTransportTests.cs`
with a non-parallel xUnit collection and deterministic harness coverage for:
single request/response completion; 32 overlapping requests with out-of-order
responses; cancellation racing response dispatch (50 iterations with `Barrier`);
and disposal racing eight outstanding requests (50 iterations). Tests assert
correct correlation, terminal completion (success or cancellation), and
`_pending` count returning to zero without collection exceptions.
