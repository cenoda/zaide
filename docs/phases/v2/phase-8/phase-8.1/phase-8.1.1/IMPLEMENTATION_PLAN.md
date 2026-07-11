# Phase 8.1.1: Settings Core — Implementation Plan

## Scope

**Goal:** Implement M1 only: the immutable, recoverable, versioned settings
foundation on which every later 8.1 slice depends.

**Out of scope:** Secrets, `AgentExecutionService` migration, folder closing,
editor/terminal runtime wiring, Settings UI, M2–M6, and all Phase 8.2/8.3 work.

## Entry Gates

- [x] Re-read the Phase 8 umbrella decisions D1–D4a and the 8.1 parent plan.
- [x] Verify `FileStreamOptions.UnixCreateMode` availability; do not implement
      secret storage in this slice.
- [x] Baseline sequentially: `dotnet build Zaide.slnx --no-restore`, then
      `dotnet test Zaide.slnx --no-build`.

## Implementation Contract

- Add deeply immutable `SettingsModel` records, nested editor/LLM/keybinding
  models, defaults, validation errors/results, and `SettingsLoadResult` /
  `SettingsSaveResult` types.
- Add `ISettingsService` and `SettingsService`, registered as a singleton.
  Construction loads synchronously and never leaves `Current` null.
- `Current` is backed by a volatile immutable snapshot. `WhenChanged` emits a
  new snapshot after the in-memory swap; `WriteErrors` emits only disk-write
  failures. Neither observable marshals to the UI thread.
- A private async mutation gate serializes `UpdateAsync`'s complete
  read–modify–validate–publish transaction. `ApplyAsync(expected, next)` uses
  the same gate and returns `Conflict` for a stale candidate instead of
  overwriting a concurrent update. Invalid candidates leave `Current` unchanged.
  `SaveAsync` persists the current snapshot. All accept `CancellationToken`.
- Cancellation before gate acquisition/enqueue leaves state unchanged. Once a
  snapshot is committed or a save is queued, caller cancellation is ignored and
  the writer reports deterministic `Saved`, `Superseded`, or `Failed` results.
- Resolve the settings path according to the 8.1 parent plan. Use JSON schema
  v1, ordered pure migration infrastructure (empty production list),
  same-directory temp-then-rename writes, and last-known-good recovery.
- Use a single generation-aware queued writer. Older queued writes resolve as
  `Superseded`; write failures return a failed save result and publish
  `WriteErrors` without rolling back the committed memory snapshot.

## Required Tests

- Settings round trip; missing, corrupt, invalid-schema, unsupported-old, and
  unknown-future behavior; rejected source files are not overwritten.
- Last-known-good fallback, atomic write, and a synthetic test-only migration.
- Immutable snapshot behavior, whole-candidate validation rejection, ordering
  of `Current` before `WhenChanged`, concurrent disjoint-field updates,
  stale-Apply conflict behavior, queued-write supersession, cancellation before
  and after commit boundaries, write-error publication, and explicit
  `SaveAsync` retry.

## Exit Conditions

- [x] M1 behavior and its tests are green.
- [x] `dotnet build Zaide.slnx --no-restore` has 0 warnings / 0 errors.
- [x] `dotnet test Zaide.slnx --no-build` is green.
- [x] No M2+ or Phase 8.2/8.3 production behavior was added.

## Rollback Plan

- Revert the isolated M1 commit if persistence or recovery behavior is unsafe;
  do not start 8.1.2 until M1 is green.
