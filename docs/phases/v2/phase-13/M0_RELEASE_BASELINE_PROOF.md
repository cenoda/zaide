# Phase 13 M0: Release Baseline Proof

## Gate Result

**Status: NO-GO for M1a–M5.** This M0 evidence pass verified the live baseline,
ownership, reusable tests, fixtures, recovery coverage, and carry-over work. It
records five comparable samples for startup, real-server LSP, real-child
workflow operations, and real-adapter DAP. It does **not** yet record desktop
editor or large-file edit samples. Consequently, no production hardening or
M1a harness work may start.

This is a deliberate truthful M0 result, not a Phase 13 implementation failure.
The required unblocked next action is the measurement session in §8 on the
recorded Linux desktop. M0 introduced no `src/` or test-project production
behavior change.

## 1. Repository and Environment Baseline

| Item | Recorded value |
|---|---|
| Baseline commit / structural rollback target | `f312516a83d8ffdc5e8f24e0c05202efb941d195` — `test(debug): improve breakpoint proof reliability and diagnostic visibility` |
| Git baseline | `master...origin/master`, clean before M0 work |
| Host | Linux `arch`, x64; kernel `7.1.3-arch1-1` |
| Desktop environment | `XDG_SESSION_TYPE=wayland`; `DISPLAY=:1`; `WAYLAND_DISPLAY=wayland-0` |
| .NET SDK / runtime | SDK `10.0.109`; runtime `10.0.9`; `global.json` requests `10.0.108` with `latestFeature` roll-forward |
| Avalonia backend | `Program.BuildAvaloniaApp()` uses `UsePlatformDetect()`; the actual selected Wayland/X11 backend is **not yet measured** in a launched release-smoke session |
| C# language server | `/home/cenoda/.dotnet/tools/csharp-ls`; `0.25.0 (Punia)+19a9574d7577521555f49bf49e94688a3ba67dd2` |
| Debug adapter | External proof artifact `/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg`; `NET Core debugger 3.2.0-1 (9744e1f, Release)` (Phase 12 records release `3.2.0-1092`) |
| Adapter configuration | `ZAIDE_NETCOREDBG_PATH=/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` when a real-adapter proof is run; no adapter is bundled or downloaded by M0 |
| Phase 13 TOFIX | None exists; there are therefore no Phase 13 items to clear before implementation |

## 2. Live Ownership and Shutdown Inventory

| Concern | Live owner / evidence | M0 conclusion |
|---|---|---|
| Settings load, migration, recovery | `SettingsService` constructs `SettingsMigrator`; `SettingsSerializer` accepts schema 1–3; `SettingsPathResolver` owns `settings.json`, `.tmp`, `.lastknowngood`, and `secrets.json` paths | Reuse Phase 8 / Phase 10 settings tests; no new recovery owner |
| Build / Run / Test processes | `ProjectWorkflowService` owns operation state and uses the singleton `IManagedProcessRunner` / `ManagedProcessRunner` | Reuse Phase 11 workflow and runner proofs; M3a is gap-only |
| C# language process | `LanguageSessionService` owns the LSP session; `LanguageDocumentBridge` owns ordered document sync | Reuse Phase 10 lifecycle proofs; M3b is gap-only |
| DAP adapter / debuggee | `DebugSessionService` owns session lifecycle; `DebugAdapterLocator` resolves `ZAIDE_NETCOREDBG_PATH` before `PATH` | Reuse Phase 12 recovery proofs; M3c is gap-only |
| Registration | `Program.ConfigureServices` registers the services above as UI-independent singletons | No DI ownership change authorized |
| Exit order | `App.DisposeServicesOnExit`: debug session and projections → workflow → workflow projections → language services → project context → terminal | `ProjectWorkflowProjectionShutdownTests` already proves ordering; no new shutdown policy authorized |

## 3. Fixture Manifest

| Fixture | Identity / command | Existing contract | Phase 13 disposition |
|---|---|---|---|
| Workflow console | `tests/fixtures/workflow-console/WorkflowConsole.csproj` SHA-256 `cdd6f0b4e4b9c72196431282fc7f42508e52e0f6a10b88a948a157bbdabd220b`; source SHA-256 `617a20b62997f6cbed8a0658a011ac1de1b59d68f999381866ac7ca20bee7020` | Build/Run/Test critical project and one-line DAP breakpoint target | Reuse for M4. It has a single thread and shallow frame only; accept this limitation unless the M4 step matrix exposes a real gap. |
| Intentional build/test failures | `tests/fixtures/workflow-fail-build/`, `workflow-tests-pass/`, `workflow-tests-fail/` | Phase 11 workflow success/failure evidence | Reuse; no fixture change in M0 |
| Large text | `tools/phase13-generate-large-file.py`; `python3 tools/phase13-generate-large-file.py /tmp/zaide-phase13/large-file-8MiB.txt` | Deterministic editor/load fixture outside app settings | Generated 8,388,608 bytes, seed `13013`, SHA-256 `0a014ac760b7eb31cd7b75b2aa1a897b7fe430571a5ac874a3c8706c54c9ffd9`; M1a must use this exact command or record a new identity. |

The large-file generator emits text lines up to 95 ASCII bytes, then a newline;
it is deterministic and does not commit a multi-megabyte fixture. It is an M0
fixture artifact, not a production measurement hook.

## 4. Automated Baseline and Flakiness Assessment

Executed sequentially at the baseline revision before documentation/tool edits:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result | Raw timing / notes |
|---|---|---|
| `dotnet build Zaide.slnx --no-restore` | PASS, 0 errors | `2.58 s`; one pre-existing `CS0067` warning in `ProjectDebugTargetResolverTests.FakeManagedProcessRunner.ProcessStarted` |
| `dotnet test Zaide.slnx --no-build` | PASS, 2053 passed / 0 failed / 0 skipped | `32 s` test-host duration |
| `git diff --check` | PASS | No whitespace errors |

No intermittent failure occurred in the sequential baseline. This is a single
full-suite sample, so it is **not** a claim that the suite is non-flaky; M5 must
repeat the full gate and record any intermittent failure by test name/pattern.

## 5. Reuse / Gap Inventory

| Existing seam | Proven contract | Phase 13 disposition / exact focused command |
|---|---|---|
| `Phase8ProofOfConceptTests` | Defaults, corrupt and unsupported settings fallback, source-not-overwritten, LKG, temp-then-rename atomic write, synthetic migration | Reuse for settings rows. `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase8ProofOfConceptTests'` |
| `FormatOnSaveTests` | v1→v2→v3 migration, v3 round trip, future schema rejection, format-on-save behavior | Reuse for schema matrix. `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~FormatOnSaveTests'` |
| `SecretStoreTests` / `FileSecretStorePermissionTests` | API key absent from ordinary settings JSON; Linux secret-file mode and repair | Reuse for secret absence / permissions. `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~SecretStoreTests|FullyQualifiedName~FileSecretStorePermissionTests'` |
| `ManagedProcessRunnerTests` | Startup failure, cancellation/tree kill, explicit kill/dispose, concurrent-start rejection, final-line capture | Reuse for M3a. `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~ManagedProcessRunnerTests'` |
| `ProjectWorkflowServiceTests` | Context/concurrency rejection, cancellation, generation safety, disposal and runner kill | Reuse for M3a. `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~ProjectWorkflowServiceTests'` |
| `ProjectWorkflowProjectionShutdownTests` | Exit ordering, subscriptions, shared process-runner ownership | Reuse for M3a / shutdown. `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~ProjectWorkflowProjectionShutdownTests'` |
| `LanguageSessionServiceTests` | Eligible-context start, restart/cancellation, process exit, stale events, dispose, missing-server recovery | Reuse for M3b. `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~LanguageSessionServiceTests'` |
| `DebugSessionServiceTests` / `M6DebugRecoveryProofTests` | DAP launch, lifecycle, failure/restart, stale-event safety, dispose, real-adapter stop/recover/restart and missing-adapter recovery | Reuse for M3c. `ZAIDE_NETCOREDBG_PATH=/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~DebugSessionServiceTests|FullyQualifiedName~M6DebugRecoveryProofTests'` |

### Settings Compatibility Matrix

| Input / recovery row | Live evidence | M0 disposition |
|---|---|---|
| v1 → v3 | `FormatOnSaveTests.Migration_ViaSettingsService_LoadsV1FileAsV3` | Green; reuse |
| v2 → v3 | `FormatOnSaveTests.Migration_V2ToV3_AddsEmptyBreakpointMap_PreservesOtherFields` | Green; reuse |
| v3 round trip | `FormatOnSaveTests.Serializer_V3RoundTrip_IncludesFormatOnSave` and `Serializer_V3RoundTrip_IncludesDebugBreakpoints` | Green; reuse |
| unknown future v4 / no overwrite | `FormatOnSaveTests.Serializer_FutureSchema_Rejected`; `Phase8ProofOfConceptTests.UnknownFutureVersion_FallsBackToDefaults` and `RejectedSourceFile_IsNotOverwrittenDuringFallback` | Green; M2 names these proofs unless a later audit finds a composed gap |
| corrupt primary | `Phase8ProofOfConceptTests.CorruptFile_FallsBackToLastKnownGood` and `CorruptFile_NoLastKnownGood_UsesDefaults` | Green; reuse |
| orphan `.tmp`, primary intact | `Phase8ProofOfConceptTests.AtomicWrite_WritesTempThenRenames` establishes D2 temp-then-rename; no dedicated orphan-temp load test was found | **Gap:** M2 must add a focused load test proving an orphan `settings.json.tmp` leaves a valid primary authoritative. No new recovery mode is authorized. |
| LKG path | `Phase8ProofOfConceptTests.LastKnownGood_UpdatedOnSuccessfulLoad` and `SyntheticMigration_LastKnownGood_IsMigratedCopy` | Green; reuse |
| plaintext secret absence | `SecretStoreTests.SettingsJson_NeverContainsApiKey_WhenSecretStoreIsUsed` and `SettingsJson_LlmSection_HasNoApiKeyField` | Green; reuse |

`SettingsLoadResult` is currently a service result, not a user-visible recovery
surface. M0 accepts that as an intentional non-surface; M2 must not claim new
user-visible recovery state unless a separately approved gap requires it.

## 6. Carry-over Triage

| Source | Item | Decision | Reason / owner |
|---|---|---|---|
| Phase 9 limitations | Parameterized palette commands, workspace/regex/semantic search, semantic folding, multi-cursor, keybinding editor, broader status writers | Not a Phase 13 concern | V3/product-scope work, not hardening evidence |
| Phase 10 limitations | Unsaved URI guarantee; range/on-type formatting, code actions, rename, semantic folding, multi-language; format-on-save availability | Accept as limitation | Existing C#-only release boundary remains explicit |
| Phase 10 TOFIX F6 | LSP document-sync gate spans async I/O | Accept as limitation | Low-severity correctness/throughput trade-off; only revisit if M1a LSP evidence misses its locked budget |
| Phase 10 TOFIX F10 | `CsharpLsSession` 669-line monolith | Defer | Info-level maintainability refactor; no measured release defect |
| Phase 10 TOFIX F11 | O(n) document lookup for diagnostics | Defer | Info-level; adopt only if M1a measurement identifies it as the bounded cause |
| Phase 11 limitations / F3 | No overall operation timeout | Accept as limitation | User cancellation remains the locked behavior; adding automatic timeout changes product policy |
| Phase 11 F7 / F8 | Non-English test parse; non-English/multiline build diagnostic limitations | Accept as limitation | Linux release evidence must record the English CLI invariant; locale/parser redesign is out of M0 |
| Phase 11 F9 | Untitled dirty tab blocks workflow | Accept as limitation | Conservative data-integrity policy; no Phase 13 redesign |
| Phase 11 F10 | Single owner of `IManagedProcessRunner` | Accept as limitation | Current ownership is explicit and tested; no second owner exists |
| Phase 12 limitations | Launch-only local C# debugging; no attach/remote/test debugging, data/conditional/log breakpoints, parity, etc. | Accept as limitation | V2 scope boundary remains explicit |
| Phase 12 M7 display rows | Glyph pixel rendering, instruction pointer appearance, panel proportions, console colors, live keyboard routing, multi-thread picker | Defer to M4b | Re-smoke on Linux desktop; every row must become pass/fail/not validated with evidence |
| DF-001 | Agent surface / Townhall tab | Not a Phase 13 concern | Product redesign |
| DF-002 | Korean IBUS input | Defer | Existing high-priority UI finding remains separate; no reproduction links it to the bounded V2 release path |
| DF-003 | Settings alignment | Not a Phase 13 concern | Visual polish outside adopted M4b rows |
| DF-004 | Settings scrolling | Not a Phase 13 concern | Deferred UI enhancement |

No new deferred finding was created: the one real settings test gap is already
named and bounded for M2, and the measurement block is a required M0 gate rather
than a separately discovered product issue.

## 7. Platform, Accessibility, and Critical-path Matrices

### Platform Matrix

| Platform | Status | Evidence / required follow-up |
|---|---|---|
| Linux x64 (Arch) | not validated | Automated baseline is green; M4b must record real desktop release smoke |
| Windows x64 | not validated | No current evidence |
| macOS | not validated | No current evidence |

### Keyboard / Focus / Readability Matrix

| Surface / path | Keyboard | Visible focus | Readable status | Status |
|---|---|---|---|---|
| Command Palette and configured commands | not validated | not validated | not validated | not validated |
| Editor search / replace / tabs | not validated | not validated | not validated | not validated |
| Output and Test Results Build / Run / Test / Cancel | not validated | not validated | not validated | not validated |
| Debug start, stop, step, Console, stack, variables | not validated | not validated | not validated | not validated |
| Phase 12 display-dependent visual / gesture rows | not validated | not validated | not validated | not validated |

### Linux Desktop Observation (2026-07-14)

| Observation | Status | Evidence / limitation |
|---|---|---|
| Launch and main-window presentation | pass | Five fresh app processes each produced a `Zaide` window in the locked startup budget. |
| Open `workflow-console` folder | pass | Real desktop view showed `Program.cs` and `WorkflowConsole.csproj` in the file tree, with `C# · Ready` and `WorkflowConsole` in the status bar. |
| Open `Program.cs` | pass | Real desktop view showed selected `Program.cs`, its two source lines, `Opened: Program.cs`, and `C# · Ready`. |
| Create, edit, and save a new C# file | pass (manual smoke) | Zaide created `/tmp/zaide-phase13/testM0.cs`; post-save filesystem inspection found a 111-byte C# source file with the expected `Hello, World!` program. The app window was subsequently closed. No elapsed samples were captured, so this does not lock the editor numeric budget. |
| Open 8 MiB generated text fixture | pass (manual smoke) | Real desktop view showed `/tmp/zaide-phase13/large-file-8MiB.txt` open as a 87,382-line text document with intact rendering and no observed hang/crash. The user reported it felt fast. No stopwatch samples were captured, so this does not lock the large-file numeric budget. |
| Live completion invocation (`Ctrl+Space`) | not validated | Remote-control delivery was unavailable. A direct XWayland `xdotool` injection then emitted the expected XTEST Control+Space press/release events to the Zaide window, but no completion popup appeared. Synthetic X11 input therefore does not prove Avalonia command routing on this Wayland desktop. This is not evidence of an LSP failure; existing Phase 10 real-server proof remains the automated evidence. |

### Critical C# Path Step Matrix

| Step | Evidence type | Existing seam / requirement | Maximum wall-clock | Status |
|---|---|---|---|---|
| Open selected C# project | Deterministic headless seam | Project-context tests | 10 s | not validated end-to-end |
| Edit and save | Deterministic headless seam | Editor / format-on-save tests | 10 s | not validated end-to-end |
| LSP result | Real-child integration where server is available | `csharp-ls` on PATH; Phase 10 production proofs | 30 s | PASS as focused real-server smoke; UI presentation remains unvalidated |
| Build | Real-child integration | workflow console fixture | 60 s | PASS as focused CLI child process; UI projection remains unvalidated |
| Run or test | Real-child integration | workflow console or pass-test fixture | 60 s | PASS as focused CLI child process; UI projection remains unvalidated |
| Debug to one breakpoint | Real-child integration / Linux manual | `ZAIDE_NETCOREDBG_PATH` and workflow-console fixture | 60 s | PASS as real-adapter focused proof; UI presentation remains unvalidated |
| Stop and verify cleanup | Existing focused real-adapter proof | `M6DebugRecoveryProofTests` | 30 s | automated proof reusable; end-to-end recheck pending |

M4a may compose these existing seams and must not create a broad UI automation
suite. M4b owns the real desktop rows.

## 8. Performance Measurement Method and Budget Gate

### Locked methodology

- Run at least five samples per area on the exact machine/fixture in §1–§3.
  Record every sample, median, max/min, variance, fixture hash, cold/warm
  classification, and concurrent-load observation. Never silently remove an
  outlier; retry only an explicitly invalid sample and retain its reason.
- Use an external monotonic desktop timer for startup/editor interaction and an
  in-process or test-host clock only when M1a adds a truthful, local-only hook.
  No production telemetry or persistent collection is allowed.
- Exclude fixture generation, build/restore, and adapter/server acquisition
  from the timed action. Include the LSP/server, adapter, and `dotnet` child
  startup required by the user action. Cap those external tools at the
  critical-path limits in §7.
- Quiet-machine rule: no active build/test/IDE workload besides the sample.
  Retry samples invalidated by unrelated load; retain the invalidation record.
- A budget miss requires named human approval before an accepted limitation is
  recorded. M1b may change production behavior only for an M0-locked miss.

### Automated Process Samples (2026-07-15)

These use existing real child-process / server / adapter proofs, not new
production instrumentation. They are valid process budgets, but do not replace
the remaining desktop editor and large-file evidence.

| Area | Exact process / fixture | Five samples (ms) | Median | Result |
|---|---|---|---:|---|
| LSP completion + hover + stale-result smoke | `dotnet tools/Phase10M4CompletionHoverSmoke/bin/Debug/net10.0/Phase10M4CompletionHoverSmoke.dll <temporary copy of Phase10M0 fixture>` | 5724, 5708, 5713, 5706, 5706 | 5708 | All PASS; real `csharp-ls`, completion, hover, and stale-result paths |
| Build cold | `dotnet build tests/fixtures/workflow-console/WorkflowConsole.csproj --no-restore` | 1905 | 1905 | PASS |
| Build warm | same command, following samples | 418, 403, 411, 415 | 413 | All PASS |
| Run | `dotnet run --project tests/fixtures/workflow-console/WorkflowConsole.csproj --no-restore` | 539, 537, 532, 528, 540 | 537 | All PASS |
| Test | `dotnet test tests/fixtures/workflow-tests-pass/WorkflowTestsPass.csproj --no-restore` | 958, 838, 830, 826, 832 | 832 | All PASS |
| DAP breakpoint → step → stop | `ZAIDE_NETCOREDBG_PATH=/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName=Zaide.Tests.Services.M4DebugExecutionProofTests.ProductionProof_LaunchBreakpointStepAndStop'` | 1318, 1346, 1337, 1320, 1339 | 1337 | All PASS; real NetCoreDbg adapter and debuggee |

### Budget Matrix

| Area | Measurement site / fixture | Samples | Median baseline | Numeric budget | Gate |
|---|---|---:|---:|---:|---|
| Startup to usable main window | External monotonic timer (`date +%s%N`) to a new XWayland `Zaide` window owned by the newly launched PID; normal settings | 5 / 5 | 620 ms (`621, 620, 620, 621, 620`) | ≤ 1,000 ms; maximum accepted sample variance ≤ 10% of median (62 ms) | PASS; measured 1 ms range |
| Editor open/edit/save | External timer; workflow-console source | 0 / 5 | not recorded | **not locked** | blocks M1a |
| Large file open/edit | External timer; generated 8 MiB file / hash in §3 | 0 / 5 | not recorded | **not locked** | blocks M1a |
| LSP ready / first result | Real `csharp-ls`; temporary copy of the Phase 10 proof fixture; completion + hover + stale-result smoke | 5 / 5 | 5708 ms | ≤ 8,000 ms; maximum accepted sample variance ≤ 10% of median (571 ms) | PASS; measured 18 ms range |
| Build | Real child `dotnet`; workflow-console fixture | 1 cold + 4 warm | 1905 ms cold; 413 ms warm | ≤ 2,500 ms cold; ≤ 600 ms warm; maximum accepted variance ≤ 10% of each median | PASS; cold/warm explicitly separated |
| Run | Real child `dotnet`; workflow-console fixture | 5 / 5 | 537 ms | ≤ 1,000 ms; maximum accepted sample variance ≤ 10% of median (54 ms) | PASS; measured 12 ms range |
| Test | Real child `dotnet`; workflow pass fixture | 5 / 5 | 832 ms | ≤ 1,500 ms; maximum accepted sample variance ≤ 10% of median (83 ms) | PASS; measured 132 ms range |
| DAP launch to breakpoint / step / stop | Real NetCoreDbg; workflow-console fixture | 5 / 5 | 1337 ms | ≤ 2,000 ms; maximum accepted sample variance ≤ 10% of median (134 ms) | PASS; measured 28 ms range |

The startup budget rounds the observed 620 ms median up to a 1,000 ms release
envelope; a sample above it requires named human approval before an accepted
limitation can be recorded. The full-suite `32 s` result in §4 is a regression
baseline only; it is not a substitute for any UX budget. When five samples
exist, lock a numeric release budget at the recorded median plus the approved
maximum variance (rather than subjective responsiveness), then update this
table before M1a begins.

## 9. M1a–M5 Handoff

| Slice | M0 handoff |
|---|---|
| M1a | Blocked pending §8 numeric budgets. It may add only the named local performance harnesses/measurement hooks after this proof is updated to GO. |
| M1b | Potentially zero slices; decide only after M1a comparable remeasurement. |
| M2 | One identified gap: orphan `.tmp` with valid primary. All other settings/secret rows name reusable tests. |
| M3a | Evidence-only unless focused workflow/process inventory identifies a regression. |
| M3b | Evidence-only unless locked LSP measurements/recovery recheck expose a real gap. |
| M3c | Evidence-only unless focused DAP recovery recheck exposes a real gap. |
| M4a | Compose the bounded §7 path after M2/M3 gates or documented no-ops. |
| M4b | Required Linux desktop smoke, platform status, keyboard/focus/status, and all Phase 12 display rows. |
| M5 | Re-run comparable budgets and the full sequential gate; truth-sync only after every matrix row has an explicit status. |

## 10. M0 Exit Checklist

- [x] Live production ownership and exit ordering verified.
- [x] Structural rollback baseline, environment, server, adapter, and fixture identity recorded.
- [x] Reuse/gap inventory and settings compatibility matrix recorded.
- [x] Carry-over triage and explicit platform/accessibility/critical-path matrices recorded.
- [x] Sequential automated baseline recorded: 2053 passed, 0 failed, 0 skipped.
- [x] Deterministic large-file fixture generator added and generated outside settings/repository data.
- [ ] Five comparable performance samples and numeric budgets locked for every area (startup, LSP, workflow, and DAP complete; desktop editor and large-file pending).
- [ ] Linux desktop release/keyboard/focus measurements recorded.

**M0 remains open and M1a is blocked until the two unchecked gate items are
completed in this proof.**
