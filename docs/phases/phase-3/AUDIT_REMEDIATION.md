# Phase 3 Audit Remediation Summary

This file is a historical note only.

The current source of truth is:
- `docs/phases/phase-3/IMPLEMENTATION_PLAN.md`

## Superseded

Earlier remediation notes in this file referenced decisions that are no longer
current:

- `Mono.Posix.NETStandard` as the primary PTY path
- `forkpty()`-first design and manual `forkpty()` P/Invoke snippet
- `O(1)` front-trim wording for the output buffer

Those points were superseded by the later Phase 3 plan revision.

## Current Direction

The active Phase 3 plan now assumes:

- native libc interop for the Linux PTY path
- no managed post-fork child-branch execution
- stateful UTF-8 decoding across read chunks
- lazy terminal startup on first reveal
- idempotent disposal and single-owner exit signaling
- bounded-cost front trimming acceptable for the MVP buffer size

If this file and the implementation plan disagree, follow the implementation
plan and update or remove stale notes.
