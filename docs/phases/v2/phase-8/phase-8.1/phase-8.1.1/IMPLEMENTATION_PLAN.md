# Phase 8.1.1: Settings Core — Implementation Plan

## Scope

**Goal:** Implement M1 only: the immutable, recoverable, versioned settings
foundation on which every later 8.1 slice depends.

**Out of scope:** Secrets, `AgentExecutionService` migration, folder closing,
editor/terminal runtime wiring, Settings UI, M2–M6, and all Phase 8.2/8.3 work.

## Entry Gates

- [ ] Re-read the Phase 8 umbrella decisions D1–D4a and the 8.1 parent plan.
- [ ] Verify `FileStreamOptions.UnixCreateMode` availability; do not implement
      secret storage in this slice.
- [ ] Baseline sequentially: `dotnet build Zaide.slnx --no-restore`, then
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
- `UpdateAsync` and `ApplyAsync` validate whole candidates before commit; they
  return field-level errors without changing `Current` when invalid. `SaveAsync`
  persists the current snapshot. All accept `CancellationToken`.
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
  of `Current` before `WhenChanged`, queued-write supersession, write-error
  publication, and explicit `SaveAsync` retry.

## Exit Conditions

- [ ] M1 behavior and its tests are green.
- [ ] `dotnet build Zaide.slnx --no-restore` has 0 warnings / 0 errors.
- [ ] `dotnet test Zaide.slnx --no-build` is green.
- [ ] No M2+ or Phase 8.2/8.3 production behavior was added.

## Rollback Plan

- Revert the isolated M1 commit if persistence or recovery behavior is unsafe;
  do not start 8.1.2 until M1 is green.
