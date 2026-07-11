# Phase 8.1.2: Secrets and Live LLM Configuration — Implementation Plan

## Scope

**Goal:** Implement M2 only: a separate file-backed secret boundary and live,
per-request LLM configuration resolution.

**Dependencies:** Phase 8.1.1 is complete and green.

**Out of scope:** Settings UI, workspace close behavior, editor/terminal
settings application, command registry, project context, M3–M6, and Phase
8.2/8.3 work.

## Implementation Contract

- Add synchronous `ISecretStore` (`Get`, `Set`, `Delete`) and `FileSecretStore`.
  API keys never appear in `settings.json`.
- On Linux create the secret temporary file with
  `FileStreamOptions.UnixCreateMode` set to `0600` before writing bytes; rename
  preserves that mode. Repair an existing loose-mode file on load and log a
  warning. Keep the parent plan's platform-default ACL limitation elsewhere.
- Change `AgentExecutionService` to receive `HttpClient`, `ISettingsService`,
  and `ISecretStore`. Build an immutable `AgentExecutionOptions` immediately
  before each `ExecuteAsync` request; do not retain a startup snapshot.
- Preserve precedence: `AGENT_API_URL`, `AGENT_API_KEY`, and `AGENT_MODEL`
  environment values override secret store (API key) and saved settings. Remove
  `AgentExecutionOptions` as a DI singleton while preserving existing request
  validation and structured results.
- Update direct test construction and DI registration only as required by this
  slice. `AgentPanelState` remains free of provider/credential configuration.

## Required Tests

- Secret get/set/delete; settings JSON never contains API-key values.
- Linux restrictive creation, mode retention after rename, and loose-mode
  repair tests.
- `LiveLlmConfigTests`: after a committed settings/secret change, the next
  `ExecuteAsync` uses the new URL, model, and key without service recreation.
- Environment variables override persisted values; existing request validation
  and error-result tests remain green.

## Exit Conditions

- [x] M2 tests and all existing agent-execution tests are green.
- [x] Build and test gates pass with no M3+ or Phase 8.2/8.3 behavior added.

## Closeout (2026-07-11)

**Build:** `dotnet build Zaide.slnx --no-restore` — 0 warnings, 0 errors.
**Tests:** `dotnet test Zaide.slnx --no-build` — 865 passed, 0 failed, 0 skipped
(843 baseline + 22 new M2 tests).

### Security-contract fix

`RepairPermissionsIfNeeded()` is called inside `Load()` (before `File.ReadAllText`,
while `_gate` is held), not only in the constructor. This ensures that an external
`chmod` after startup is repaired on the next `Get()`/`Set()`/`Delete()`, closing
the singleton-lifetime gap.

### New test files

- `tests/Zaide.Tests/Services/SecretStoreTests.cs` — get/set/delete, persistence,
  settings.json isolation (10 tests).
- `tests/Zaide.Tests/Services/FileSecretStorePermissionTests.cs` — Linux 0600
  creation, mode retention after rename, loose-mode repair, external-chmod
  repair on subsequent Get (6 tests; no-op on non-Linux).
- `tests/Zaide.Tests/Services/LiveLlmConfigTests.cs` — committed settings/secret
  change picked up by next `ExecuteAsync` without service recreation; env-var
  overrides for URL, model, and API key (6 tests).
- `tests/Zaide.Tests/TestSecretStore.cs` — in-memory `ISecretStore` for tests.

### Changed production files

- `src/Services/ISecretStore.cs` — synchronous secret boundary interface.
- `src/Services/FileSecretStore.cs` — file-backed implementation with Linux 0600
  temp creation, atomic rename, and loose-mode repair.
- `src/Services/SettingsPathResolver.cs` — added `GetSecretsPath()` and
  `GetSecretsTempPath()`.
- `src/Services/AgentExecutionOptions.cs` — demoted to immutable per-call value
  carrier (`init`-only properties).
- `src/Services/AgentExecutionService.cs` — constructor takes `HttpClient`,
  `ISettingsService`, `ISecretStore`; builds fresh options per `ExecuteAsync`.
- `src/Program.cs` — removed `AgentExecutionOptions` singleton; registered
  `ISecretStore` as `FileSecretStore`.

### Changed test files

- `tests/Zaide.Tests/Services/AgentExecutionServiceTests.cs` — updated to new
  constructor via `CreateSettingsAndSecrets` helper.
- `tests/Zaide.Tests/ViewModels/AgentExecutionCoordinatorTests.cs` — same
  constructor update.

## Rollback Plan

- Revert the isolated M2 commit and restore the M1 green baseline if secret
  permissions or request configuration behavior is incorrect.
