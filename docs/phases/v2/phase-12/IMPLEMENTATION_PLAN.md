# Phase 12: C# Debugging (DAP) — Implementation Plan

## Status

**M0 complete** (planning and live adapter/transport proof, 2026-07-14).
Evidence: [M0_DAP_ADAPTER_TRANSPORT_PROOF.md](M0_DAP_ADAPTER_TRANSPORT_PROOF.md).

No Phase 12 production code, package reference, DI registration, command, or UI
has been added by M0. M1 must start from the locked contracts below.

**Prerequisite:** Phase 11 is complete. `IProjectContextService` is the sole
project-target owner; `ICommandRegistry` is the sole command/keybinding
surface; Phase 11 owns Build/Run/Test and the structured Output surface.

## Scope

**Goal:** Deliver one Linux-validated C# launch-debug workflow over DAP with
breakpoints, execution control, thread/call-stack/scopes/variables projection,
and truthful adapter/output/error lifecycle state.

### Included

- A DAP client that launches one `netcoredbg --interpreter=vscode` child
  adapter over stdio with `Content-Length` framing.
- Default C# launch resolution from the selected Phase 8.3 `ProjectContext`.
- Persistent source breakpoints plus verified/pending visual state.
- Debug start, continue, pause, stop, step over, step into, and step out.
- Thread, stack-frame, scope, and variable requests only while stopped.
- A Debug Console / debug-output projection distinct from terminal and Phase 11
  Build/Run/Test Output.
- Structured startup, disconnect, adapter-crash, protocol-error, cancellation,
  and unsupported-context states.

### Boundaries (YAGNI)

- Linux x64 and C# are the V2 validation target. This plan makes no
  Windows/macOS parity promise.
- No attach-to-process, remote debugging, compound sessions, data/function
  breakpoints, exception configuration UI, watch expressions, REPL evaluation,
  source maps, multi-language adapters, `launch.json`, or user-configurable
  launch profiles.
- No second project discovery model and no use of `Workspace.WorkspacePath` as
  a debug-target fallback.
- No DAP traffic through `ITerminalService` / PTY; no replacement of the
  Phase 11 managed Build/Run/Test process runner.
- `F5` is not claimed as Debug until M3 registers and proves it.

## Pre-Implementation Verification (M0)

- [x] Read live roadmap, documentation rules, Phase 11 closeout/plan, and no
      Phase 12 `TOFIX.md` exists to clear first.
- [x] Verified live ownership: `Program.ConfigureServices`, `App.OnExit`,
      `IProjectContextService`, `ICommandRegistry`, `MainWindowViewModel`,
      `EditorTabViewModel`, Phase 10 `CsharpLsSession`, and Phase 11 workflow.
- [x] Verified `StreamJsonRpc` 2.22.23 is already centrally pinned and the
      Phase 10 session already uses `HeaderDelimitedMessageHandler` over child
      stdin/stdout with `SystemTextJsonFormatter` camel case.
- [x] Proved selected adapter and stdio transport against a real Linux C#
      executable: initialize, launch, set breakpoint, configurationDone,
      stopped event, threads, stackTrace, scopes, continue, and disconnect.
- [x] Locked contracts 1–8, milestones M1–M7, test gates, limitations, and
      rollback below. M0 is documentation/proof only.
- [x] `git diff --check` clean for M0 docs.

## Locked Contracts

### 1. Adapter and transport

**Selected M1 adapter:** NetCoreDbg **3.2.0-1092** (`netcoredbg-linux-amd64`)
launched as:

```text
netcoredbg --interpreter=vscode
```

The M0 proof used its released Linux x64 archive only under `/tmp`; it is not a
repository dependency and is not bundled, installed, or auto-downloaded by M0.
M1 must define a locator with an explicit configured/local executable path and
return `AdapterUnavailable` when it cannot resolve an executable. Do not silently
download an adapter or fall back to an unrelated debugger.

The transport is one adapter process per debug session with redirected stdin,
stdout, and stderr. stdin/stdout carry only DAP `Content-Length` framed JSON.
stderr is drained independently and becomes structured diagnostic output; it is
never parsed as protocol JSON. Reuse `StreamJsonRpc`'s
`HeaderDelimitedMessageHandler` and `SystemTextJsonFormatter` rather than a
hand-rolled frame parser. DAP method parameters are single JSON objects, so the
session must use the same single-object request pattern proven by Phase 10.

### 2. Target, build handoff, and launch contract

The single debug-eligibility predicate is
`ProjectTargetResolver.IsEligible(context) && context.SelectedProject?.Kind == CSharpProject`.
`Solution` and `SolutionX` fail it and return `RejectedContext`; they have no
default debug target in Phase 12. The target's normalized absolute `.csproj`
path and parent directory come from `SelectedProject.FilePath` only.

M3a must lock and test one deterministic build/launch handoff. Default policy:
ask `IProjectWorkflowService.StartBuildAsync` to build the selected project,
and launch DAP only after its `Succeeded` result. The existing result exposes
the target `.csproj` path and exit code, **not** an output DLL path; it cannot
be treated as an assembly-location contract.

After a successful build, a narrow `IProjectDebugTargetResolver` must query the
same absolute `.csproj` with `dotnet msbuild <csproj> -getProperty:TargetPath`.
It accepts exactly one non-empty, normalized absolute existing path whose
extension is `.dll`; it must not enumerate or guess under `bin/`. An empty,
relative, missing, non-DLL, malformed, or multiply-valued result is
`UnsupportedLaunchTarget` and starts no adapter. The resolver's property-query
process must be injected/testable and use the existing managed redirected
process policy rather than a view-owned process. Phase 12 does not add an
`OutputType` or framework matrix UI. The property query uses the same default
configuration as the immediately preceding `dotnet build` (Debug unless an
already-supported workflow contract changes it). Multi-target projects are
supported only when that one default evaluation yields exactly one valid target
path; target-framework selection UI and configuration selection remain out of
scope.

If the workflow is already active, `StartBuildAsync` returns
`RejectedConcurrent`; Debug Start must surface **Workflow busy** and start no
adapter. It must never silently cancel/kill an active Build, Run, or Test, and
must not own a duplicate debug-build process. Thus Ctrl+F5 Run and F5 Debug
remain distinct; an active Run makes F5 reject truthfully until the user stops
or the workflow completes.

The exclusion is mutual from M3a onward. M3a extracts Phase 11's admission
into one UI-independent `IProjectOperationGate` shared by workflow and debug.
Build, Run, Test, and Debug Start acquire the same lease; Debug Start retains
its lease across its required build→TargetPath→adapter handoff, so no workflow
operation can slip into the gap and replace the target DLL. A competing request
returns structured `RejectedConcurrent` with either `Workflow busy` or `Debug
session active` and starts no process. The existing workflow service must use
the gate rather than retain a parallel one-operation lock. The user must Stop
Debugging before a workflow operation can start.

The proven DAP launch fields are `program` (absolute DLL), `cwd` (project
directory), `stopAtEntry`, and `console: "internalConsole"`. M1 must make a
debug launch explicitly request a fresh Build; Phase 11's optional smart
`--no-build` policy remains out of scope.

### 3. Session state, ownership, and lifecycle

`IDebugSessionService` (singleton) owns exactly one DAP session, its adapter
process, request ordering, generation, cancellation, and immutable snapshots.
Views/ViewModels only project snapshots and invoke commands. `IDebugAdapterLocator`
and `IDebugAdapterSessionFactory` are narrow test seams; no view owns a process
or protocol object.

State is one of `Idle`, `Starting`, `Running`, `Stopped`, `Stopping`, `Failed`,
or `Unavailable`. Outcome/failure kinds must distinguish `RejectedConcurrent`,
`RejectedContext`, `AdapterUnavailable`, `BuildFailed`, `StartupFailed`,
`ProtocolFailed`, `AdapterExited`, `Cancelled`, and `StoppedByUser`.

Only one debug session may be active. A Start while active is a structured
`RejectedConcurrent`; Stop is idempotent. A project-context change away from
the selected project cancels/stops the session and invalidates its generation.
Late replies/events from an old generation are ignored. App disposal order is:
debug session (disconnect, bounded wait, kill adapter process tree), debug
projection services, then existing Phase 11 workflow, language stack, project
context, and terminal host. M1 must update `App.OnExit` accordingly.

### 4. DAP sequencing and request policy

The minimum successful launch sequence is:

```text
initialize → launch → setBreakpoints (all persisted source files)
→ configurationDone → stopped/continued/exited events
```

`setBreakpoints` replaces the full breakpoint set for each source path. The
client must preserve requested breakpoints and project the returned
`verified/message/line` result; a rejected or pending breakpoint is not shown
as verified. Send `configurationDone` only after all initial breakpoint replies
are received. While stopped, request `threads`, then `stackTrace(threadId)`;
request `scopes(frameId)` and `variables(variablesReference)` only after a user
selects a live frame/scope. `continue`, `next`, `stepIn`, `stepOut`, `pause`,
and `disconnect(terminateDebuggee: true)` are valid only for the appropriate
state and must be unavailable otherwise.

Unexpected disconnect, process exit, malformed/failed RPC, or a request timeout
must leave a truthful terminal snapshot, retain diagnostic output, clear stale
live-frame data, and allow a later Start. The M1 constants are: initialize
15 seconds, launch/configuration 15 seconds, ordinary request 10 seconds, and
disconnect 5 seconds before process-tree kill. No operation may wait silently
without one of these bounds.

M1 registers local DAP event handlers before calling `JsonRpc.StartListening`:
`stopped`, `continued`, `output`, `terminated`, and `exited` use explicit
`JsonRpc.AddLocalRpcMethod` registrations with single-object parameter
deserialization, following the Phase 10 notification seam. The session service,
not fire-and-forget callers, serializes the sequence above and correlates all
replies/events to its active generation.

### 5. Breakpoints and editor ownership

`IBreakpointService` owns persistent, workspace-scoped source breakpoint
records (`absolute normalized file path`, one-based line, enabled, adapter
verification/message). M2 extends the app-global Phase 8 settings model to
schema **v3** with an immutable `Debug` settings member containing
`BreakpointsByWorkspaceRoot`: an ordinal-normalized absolute workspace-root key
to immutable breakpoint-list map. It adds an explicit v2→v3 migration that
creates an empty map; it does not create a workspace-side JSON file. A missing
or unselected workspace cannot persist a breakpoint. Adapter verification is
session state and must not make a user breakpoint disappear.

The M2 settings checklist is mandatory: add immutable `DebugSettings` and its
JSON shape to `SettingsModel`; advance `Defaults.SchemaVersion` to 3; raise the
`SettingsSerializer` accepted ceiling to 3 while preserving unknown-future
rejection; implement/register `SettingsMigrationV2ToV3` after V1→V2 in the
production `SettingsService` migrator; and update constructor/serialization,
v2→v3, v3 round-trip, and unknown-v4-rejection tests. A field addition without
the serializer, production migration chain, and test updates is not M2-complete.

M3 adds the editor-margin toggle and visual projection. It must subscribe to
the existing document/tab lifecycle, normalize paths, handle an unopened file,
and remove/move all requested breakpoints for a source when DAP reports its
replacement response. No breakpoint behavior is hidden in `EditorView` itself.

### 6. Commands and UI surfaces

All commands register via `ICommandRegistry`:

| ID | Name | Default gesture | First milestone |
|---|---|---|---|
| `debug.startOrContinue` | Start Debugging / Continue | `F5` | M3 |
| `debug.pause` | Pause | _(none)_ | M4 |
| `debug.stop` | Stop Debugging | `Shift+F5` | M4 |
| `debug.stepOver` | Step Over | `F10` | M4 |
| `debug.stepInto` | Step Into | `F11` | M4 |
| `debug.stepOut` | Step Out | `Shift+F11` | M4 |
| `debug.toggleBreakpoint` | Toggle Breakpoint | `F9` | M3 |

`debug.startOrContinue` is the **only** command carrying default F5. It is one
state-dispatching command: it starts in Idle/Failed/Unavailable and continues in
Stopped; it is unavailable in all other states. `debug.start` and
`debug.continue` are not separate registered F5 commands. This is required
because the live `CommandRegistry.ResolveKeyBindings` produces one static
gesture→command map and `MainWindow` rematerializes it only for settings
snapshots, not session state. M3 must prove the one-command F5 dispatch and
that no registry gesture conflict is introduced.

M4 adds exactly one `BottomPanelMode.Debug`. Its fixed composition is a Debug
Console and Call Stack section in the bottom panel; M5 adds Variables to that
same Debug mode. The terminal, Phase 11 Output, Problems, and Test Results stay
separate modes with their existing owners. M4 must freeze this composition in
its entry evidence before XAML changes; it is not an optional layout decision.

### 7. Verification strategy

All full gates run sequentially:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

M1+ use fake adapter/session seams for deterministic failure, ordering,
generation, and disposal tests. **M1's first real-adapter gate uses the actual
production `StreamJsonRpc` session**, not the M0 disposable frame client, to
prove `initialize → launch → setBreakpoints → configurationDone → stopped →
threads → stackTrace → scopes → continue → disconnect` against NetCoreDbg.
Real adapter evidence uses `tests/fixtures/workflow-console/` and records the
adapter version, executable origin/path, command argv, SDK, target DLL, and
observed events. The M0 proof establishes the framing baseline, but every
lifecycle or transport change must repeat the production-session proof.

### 8. Adapter location and rollback

M1 adapter discovery has no persisted settings change: it resolves an absolute
executable from `ZAIDE_NETCOREDBG_PATH` first, then `netcoredbg` on `PATH`.
The environment value must name an executable file; invalid/missing candidates
produce `AdapterUnavailable` with the actionable status text: `NetCoreDbg was
not found. Set ZAIDE_NETCOREDBG_PATH or add netcoredbg to PATH.` Linux smoke
machines install/provide the adapter outside the repository and record its
version/path in manual evidence. There is no automatic download, bundling, or
well-known-directory scan in V2.

## Phase 12 Limitations

Phase 12 validates launch-debugging a locally built C# executable only. It does
not promise test debugging, library debugging, attach, remote, external
console/TTY behavior, arbitrary launch configuration, data/conditional/log
breakpoints, exception settings, watch/evaluate, or platform parity. There is
no `OutputType`/runnable probe: a class library or other non-runnable project
may reach adapter launch after valid `TargetPath` resolution and must surface a
structured `StartupFailed` (or `UnsupportedLaunchTarget` when resolution fails).
Breakpoints always address the on-disk normalized path and one-based line; M3
does not auto-save dirty buffers, while Start's fresh build consumes the saved
project state. Current execution location is included: M5 owns the stopped
instruction-pointer line projection for the selected live stack frame. Its view
may host a renderer or margin, but session data, selection, and lifecycle remain
service/ViewModel owned.

Each milestone has one focused commit. If a milestone must be reverted, revert
only its recorded commit after preserving a `REVERT_LOG.md` when the reset is
structural. The M1 revert target is the committed M0 baseline: `6222ea5`.

## Milestones (Incremental)

| Milestone | Scope and independent completion condition | Focused verification | Commit boundary |
|---|---|---|---|
| **M0** ✅ | Docs/proof only: live seams, NetCoreDbg 3.2.0-1092 stdio lifecycle proof, contracts 1–8, decomposition. No production code. | Recorded proof; `git diff --check` | `docs(phase-12): M0 DAP plan and proof` |
| **M1** | UI-independent DAP core: locator (`ZAIDE_NETCOREDBG_PATH`, then PATH), session factory/service, pre-listening event registrations, Content-Length `StreamJsonRpc` transport, locked timeouts, state/generation/outcomes, stderr capture, disconnect/crash/timeout/dispose behavior, DI and shutdown ordering. No breakpoint persistence or UI commands. | `DebugSessionServiceTests`, fake-session ordering/event/failure tests, locator tests, DI/dispose tests; **production StreamJsonRpc** NetCoreDbg lifecycle proof | `debug: add DAP session lifecycle core` |
| **M2** | Schema-v3 workspace-root-keyed breakpoint persistence: `DebugSettings`, serializer ceiling, v2→v3 production migration registration, path/line normalization, full-source replacement request policy, verified/pending mapping. No editor chrome or F5. | `BreakpointServiceTests`, v2→v3 migration/round-trip/unknown-v4 tests, `SetBreakpoints` fake-session tests | `debug: add persistent breakpoint service` |
| **M3a** | Extract the shared project-operation gate; build-before-debug handoff under one gate lease using the locked MSBuild `TargetPath` query; `debug.startOrContinue` F5 command. No F9/editor chrome. | target-resolution + workflow-busy/debug-active + handoff-no-gap + F5 registry dispatch tests; Linux smoke: fresh build, F5 launch | `debug: start launch debugging` |
| **M3b** | `debug.toggleBreakpoint` / F9 plus editor breakpoint margin/projection over M2 persistence and M3a session state. | breakpoint command + editor projection/path-identity tests; Linux smoke: F9, F5, breakpoint hit | `debug: add editor breakpoint projection` |
| **M4** | Continue/pause/stop/step commands and DAP state gating; fixed Debug bottom mode with Debug Console + Call Stack; adapter error projection. | command availability/state transition and Debug-mode composition tests; Linux smoke: F5/F10/F11/Shift+F11/Shift+F5 + output | `debug: add execution controls and debug console` |
| **M5** | Threads, call stack, frames, scopes, variables, selected-frame current-execution-location projection, and stale-data clearing on resume/end. | `DebugStackProjectionTests`, scope/variable/current-location tests, stale-generation tests; Linux smoke: stop → frame → scope → variable | `debug: project stack and variables` |
| **M6** | Error/recovery hardening: missing adapter, build failure, launch failure, breakpoint rejected, adapter crash/disconnect, context change, rapid start/stop. | lifecycle/error regression tests; recorded Linux failure-path smoke | `debug: harden DAP recovery` |
| **M7** | Closeout: sequential full regression, accessibility/keyboard/manual evidence, docs truth-sync, limitations review. | full sequential gates + `M7_MANUAL_EVIDENCE.md` | `docs(phase-12): M7 closeout` |

### Milestone dependencies

```text
M0 → M1 → M2 → M3a → M3b → M4 → M5 → M6 → M7
```

M3 is deliberately split by default. M3a owns the build-to-debug handoff and
F5 without editor chrome; M3b owns F9/margin path identity after the launch
contract is proven. `F5` cannot be exposed before M3a can produce truthful
launch state, and M3b cannot start before M3a is complete.

## Exact Next Step

Phase 12 M0 is complete. The next permitted implementation slice is **M1 only**:
the UI-independent adapter locator/session lifecycle core. Do not add
breakpoints, F5, editor UI, stack panes, or a bundled adapter in M1.

## Exit Conditions

- [ ] On Linux, a selected supported C# project can build, F5-launch, and stop
      at a persistent source breakpoint.
- [ ] The selected stopped frame projects current execution location, call
      stack, scopes, and variables; continue/step/stop clear stale data.
- [ ] F5, F9, F10, F11, Shift+F11, and Shift+F5 have the documented registry
      behavior with no gesture conflict.
- [ ] Missing adapter, workflow-busy, invalid target path, adapter exit,
      protocol failure, and context change remain truthful and recoverable.
- [ ] Sequential full verification succeeds: `dotnet build Zaide.slnx --no-restore`,
      then `dotnet test Zaide.slnx --no-build`, then `git diff --check`.
- [ ] M7 manual evidence records the Linux adapter, fixture, lifecycle, and
      accessibility/keyboard smoke; roadmap and architecture docs are
      truth-synced.
