# Phase 13 M5: Release Closeout Evidence

**Status: Phase 13 COMPLETE WITH EXPLICIT LIMITATIONS (2026-07-16)**
**Decision: GO** — every Phase 13 exit condition is met by live evidence, with
honest environment-limited and not-validated rows retained (never converted to
passes). No production code change. No Phase 13 `TOFIX.md`. No fabricated DAP
remeasurement.

**Scope:** Evidence, remeasurement, audit, and documentation truth-sync only.
No new feature, UI, performance production fix, recovery production change, or
refactor work.

---

## 1. Commit baseline and environment

| Item | Recorded value |
|---|---|
| Pre-M5 HEAD (M4b closeout; preserved, not rewritten) | `83e8295a3ba0b8b75709ec3fb6e694c8367ca8da` — `docs(phase-13): record release smoke evidence` |
| Structural Phase 13 rollback target (M0) | `f312516a83d8ffdc5e8f24e0c05202efb941d195` |
| Branch state at M5 start | `master` clean and equal to `origin/master` at `83e8295` (M4b already published; no unpushed M4b commit to protect beyond history preservation) |
| Host | Linux `arch`, x86_64; kernel `7.1.3-arch1-1` |
| Desktop | `XDG_SESSION_TYPE=wayland`; `DISPLAY=:1`; `WAYLAND_DISPLAY=wayland-0`; `XDG_CURRENT_DESKTOP=KDE` |
| .NET SDK | `10.0.109` |
| C# language server | `/home/cenoda/.dotnet/tools/csharp-ls`; `0.25.0 (Punia)+19a9574d7577521555f49bf49e94688a3ba67dd2` |
| Debug adapter | **Absent.** `ZAIDE_NETCOREDBG_PATH` unset; no `netcoredbg` on `PATH`; `/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` missing. Nothing was downloaded or bundled. |
| Phase 13 TOFIX | None exists before or after M5 |
| Quiet-machine observation | No concurrent `dotnet test Zaide.slnx` suite run during app-internal measurement. Ambient VS Code C# Dev Kit / Roslyn hosts present (same class as accepted M0 quiet-machine snapshot). |

### Fixture identity (unchanged from M0)

| Fixture | SHA-256 |
|---|---|
| `tests/fixtures/workflow-console/WorkflowConsole.csproj` | `cdd6f0b4e4b9c72196431282fc7f42508e52e0f6a10b88a948a157bbdabd220b` |
| `tests/fixtures/workflow-console/Program.cs` | `617a20b62997f6cbed8a0658a011ac1de1b59d68f999381866ac7ca20bee7020` |
| `/tmp/zaide-phase13/large-file-8MiB.txt` (regenerated) | `0a014ac760b7eb31cd7b75b2aa1a897b7fe430571a5ac874a3c8706c54c9ffd9` |

Generator command:
`python3 tools/phase13-generate-large-file.py /tmp/zaide-phase13/large-file-8MiB.txt`

---

## 2. Verification commands and actual results

### Required builds

| Command | Result |
|---|---|
| `dotnet build Zaide.slnx --no-restore` (pre-measurement) | **PASS** — 0 errors; 1 pre-existing `CS0067` warning in `ProjectDebugTargetResolverTests.FakeManagedProcessRunner.ProcessStarted`; ~5.4 s |
| `dotnet build tools/Phase10M4CompletionHoverSmoke/Phase10M4CompletionHoverSmoke.csproj --no-restore` | **PASS** — 0 warnings, 0 errors; ~0.7 s |

### App-internal editor / large-file (20-sample quiet-machine)

```bash
python3 tools/phase13-measure.py --areas editor large-file \
  --output /tmp/zaide-phase13/measurements/m5-p95-20260716T100010Z
```

| Area | Samples | Median | p95 | Min–max | Budget | Gate |
|---|---:|---:|---:|---:|---:|---|
| Editor open/edit/save/restore | 20 / 20 functional | 0.304 ms | **0.380 ms** | 0.267–0.417 | p95 `< 50` ms | **PASS** |
| Large-file document load | 20 / 20 functional | 12.612 ms | **19.168 ms** | 5.985–20.140 | p95 `< 50` ms | **PASS** |

Raw (untracked): `/tmp/zaide-phase13/measurements/m5-p95-20260716T100010Z/`

**Explicit non-claim:** command-path latency only. Not Avalonia render, UX,
keyboard routing, or desktop responsiveness.

### Process / desktop runner areas (Startup, LSP, Build, Run, Test)

```bash
python3 tools/phase13-measure.py --areas startup lsp build run test \
  --output /tmp/zaide-phase13/measurements/m5-process-20260716T100100Z
```

| Area | Samples (ms) | Median | Min–max | Range | Budget | Gate |
|---|---|---:|---:|---:|---:|---|
| Startup | 927.008, 928.306, 927.732, 906.577, 930.168 | 927.732 | 906.577–930.168 | 23.590 | ≤ 1,000 ms; range ≤ 10% median | **PASS** |
| LSP | 5706.687, 5695.135, 5702.209, 5696.254, 5701.087 | 5701.087 | 5695.135–5706.687 | 11.552 | ≤ 8,000 ms; range ≤ 10% median | **PASS** |
| Build cold | 1909.414 | 1909.414 | — | 0 | ≤ 2,500 ms | **PASS** |
| Build warm | 392.778, 390.206, 381.334, 384.795 | 387.501 | 381.334–392.778 | 11.443 | ≤ 600 ms; range ≤ 10% median | **PASS** |
| Run | 503.239, 498.970, 503.805, 496.413, 501.369 | 501.369 | 496.413–503.805 | 7.392 | ≤ 1,000 ms; range ≤ 10% median | **PASS** |
| Test (first combined run) | 988.561, 823.821, 819.012, 818.508, 826.793 | 823.821 | 818.508–988.561 | 170.054 | ≤ 1,500 ms; range ≤ 10% median | **FAIL variance** (all samples still ≤ absolute budget) |

**Test variance recovery (accepted M5 comparable set):** first combined-run sample
1 was a cold-path outlier (same pattern as the historical M0 process sample set
`958, 838, …`). Per methodology, fixture/setup is outside the timed action. An
untimed warm of `workflow-tests-pass` was run, then:

```bash
python3 tools/phase13-measure.py --areas test \
  --output /tmp/zaide-phase13/measurements/m5-test-rerun-20260716T100400Z
```

| Area | Samples (ms) | Median | Min–max | Range | Budget | Gate |
|---|---|---:|---:|---:|---:|---|
| Test (accepted re-run) | 818.910, 824.369, 827.113, 821.342, 812.379 | **821.342** | 812.379–827.113 | 14.734 | ≤ 1,500 ms; range ≤ 10% median | **PASS** |

Both the rejected cold-first set and the accepted re-run are retained under
`/tmp/zaide-phase13/measurements/`. No silent outlier removal.

### DAP remeasurement

| Item | Status |
|---|---|
| Attempted this session? | **No** — real NetCoreDbg binary not available through the documented environment |
| Evidence action | Did **not** download or bundle an adapter |
| Prior locked evidence | M0/M1a DAP five-sample median **1337–1345 ms** ≤ 2,000 ms budget with real adapter at `/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` (that path is now absent) |
| M4a critical-path debug/stop | **not re-run / environment-limited**; cites Phase 12 `M4DebugExecutionProofTests` / `M6DebugRecoveryProofTests` |
| M5 disposition | **Unavailable — not remeasured.** Prior PASS retained as historical locked evidence only. Not claimed as a new M5 pass. |

### Full sequential regression gate

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---|---|
| `dotnet build Zaide.slnx --no-restore` | **PASS** — 0 errors (post-measurement rebuild ~0.5 s; 0 warnings on this clean rebuild) |
| `dotnet test Zaide.slnx --no-build` | **PASS** — **2172** passed / 0 failed / 0 skipped; ~33 s |
| `git diff --check` | **PASS** — no whitespace errors |

No intermittent failure observed in this full-suite sample. This remains a
single full-suite observation, not a universal non-flaky claim.

---

## 3. M0–M4b milestone disposition (live audit)

| Milestone | Disposition | Evidence / live truth |
|---|---|---|
| **M0** | Complete | `M0_RELEASE_BASELINE_PROOF.md`; ownership, fixtures, budgets, matrices, carry-over triage locked |
| **M1a** | Complete | `M1A_MEASUREMENT_RUNNER.md`; `tools/phase13-measure.py`; process five-sample evidence |
| **M1b** | **Skipped** | All locked budgets already met; zero production performance slices; M5 remeasurement confirms all available budgets still met |
| **M2** | Complete (evidence-only) | `Phase8ProofOfConceptTests.OrphanTemp_WithValidPrimary_PrimaryRemainsAuthoritative`; production already Phase 8 D2; no `src/` change |
| **M3a** | Complete (evidence-only) | Workflow/process recovery inventory green via existing tests; no production gap |
| **M3b** | Complete (evidence-only) | Language-session / LSP recovery inventory green via existing Phase 10 tests; no production gap |
| **M3c** | Complete (evidence-only) | DAP / debug-session recovery inventory green via existing Phase 12 tests; no production gap; real-adapter proofs require env |
| **M4a** | Complete | `M4A_CRITICAL_PATH_EVIDENCE.md`; open/edit/save headless PASS; LSP/build/run real-child PASS; debug/stop environment-limited |
| **M4b** | Complete with explicit limitations | `M4_RELEASE_SMOKE_EVIDENCE.md`; Release launch/render PASS; input-/adapter-dependent rows **not validated** with exact reasons; no fail rows |
| **M5** | Complete (this document) | Remeasurement + full gate + truth-sync; no production change |

No unchecked Phase 13 `TOFIX.md` items (file does not exist; no finding required one).

---

## 4. Performance: locked budgets vs M5 remeasurement

| Area | Locked M0 budget | M5 comparable result | Gate |
|---|---|---|---|
| Startup | ≤ 1,000 ms; range ≤ 10% median | median 927.732 ms; range 23.590 ms | **PASS** |
| Editor command path | p95 `< 50` ms (20 functional) | p95 **0.380 ms** (20/20) | **PASS** |
| Large-file command path | p95 `< 50` ms (20 functional) | p95 **19.168 ms** (20/20) | **PASS** |
| LSP | ≤ 8,000 ms; range ≤ 10% median | median 5701.087 ms; range 11.552 ms | **PASS** |
| Build cold | ≤ 2,500 ms | 1909.414 ms | **PASS** |
| Build warm | ≤ 600 ms; range ≤ 10% median | median 387.501 ms; range 11.443 ms | **PASS** |
| Run | ≤ 1,000 ms; range ≤ 10% median | median 501.369 ms; range 7.392 ms | **PASS** |
| Test | ≤ 1,500 ms; range ≤ 10% median | accepted re-run median **821.342 ms**; range 14.734 ms | **PASS** (cold-first set retained as non-accepted) |
| DAP | ≤ 2,000 ms; range ≤ 10% median | **not remeasured** — adapter absent | **Unavailable** (prior M0/M1a PASS cited; not a new pass) |

**M1b remains skipped:** no locked-budget miss requires a production performance fix.

---

## 5. Platform / desktop / adapter limitations (unchanged truth)

| Boundary | Status | Must not claim |
|---|---|---|
| Linux x64 Release launch/render | **pass** (M4b) | Full interactive workflow smoke |
| Windows / macOS | **not validated** | Parity |
| Keyboard / focus / most desktop action rows | **not validated** (M4b; no non-synthetic input path) | Avalonia command routing or focus proof |
| Debug desktop UI / Phase 12 live display rows | **not validated** (adapter + input absent) | Live debug UI pass |
| App-internal editor/large-file timings | command-path only | UX / render / keyboard |
| M4a open/edit/save/LSP/build/run | automated command-path / real-child | Desktop smoke |
| M4a debug/stop | not re-run / environment-limited | New session pass without adapter |
| DAP M5 remeasurement | unavailable | Fabricated pass |

No existing **not validated** desktop/adapter row was converted into a pass during M5.

---

## 6. V2 exit-condition mapping

| V2 exit condition | Phase 13 / M5 evidence | Status |
|---|---|---|
| Phase 8–13 plans and independent closure | Plans exist; Phase 13 closed by this M5 evidence | **Met** |
| V1 workflows remain regression-covered | Full suite **2172** passed | **Met** |
| C# edit → understand → build → run/test → debug loop | M4a automated composition: open/edit/save/LSP/build/run **PASS**; debug/stop environment-limited with Phase 12 real-adapter proofs cited when adapter present; M4b desktop debug **not validated** | **Met with documented environment limitation** |
| Commands discoverable and keyboard configurable | Phase 8.2/9 automated registry/keybinding delivery; M4b keyboard matrix cells remain **not validated** (no non-synthetic input) | **Met at service/registry layer; desktop keyboard smoke not validated** |
| Credentials absent from ordinary plaintext settings | `SecretStoreTests` / settings JSON shape (M0 inventory / M2 matrix) | **Met** |
| Full settings schema compatibility/recovery | M2 matrix including orphan-temp D2 proof | **Met** |
| UI-independent services, cancellation, structured results, observable state | M3a–M3c recovery inventories + M4a critical path | **Met** |
| Exact automated/manual documentation and explicit limitations | M0–M5 evidence set; platform/desktop rows explicit | **Met** |

---

## 7. Phase 13 exit-condition checklist

- [x] M0–M5 complete with named focused tests and evidence artifacts (including skipped M1b and evidence-only M2–M3c).
- [x] Every available M0 performance budget met by comparable M5 remeasurement; DAP unavailable status and prior evidence recorded honestly (not fabricated).
- [x] Every settings compatibility/recovery matrix row has automated evidence.
- [x] LSP, workflow-process, and DAP lifecycle/recovery paths have focused automated evidence plus documented real-child environment requirements.
- [x] Critical C# path has automated evidence per M0 step matrix; manual Linux rows have explicit status (including not validated / environment-limited).
- [x] Keyboard/focus/status and Linux release smoke matrices recorded with pass/fail/unsupported/not validated per row (M4b; unchanged).
- [x] Full sequential build/test pass, `git diff --check` clean, no Phase 13 `TOFIX.md` items, docs truth-synced (`V2.md`, `README.md`, `OVERVIEW.md`, `phases/README.md`; `PHASES.md` historical with no Phase 13 claim; `LIBRARIES.md` / `CONVENTIONS.md` / `DESIGN.md` unchanged because Phase 13 did not alter their truth).

---

## 8. Final decision

### **GO — Phase 13 COMPLETE WITH EXPLICIT LIMITATIONS**

Justification:

1. All remeasurable locked budgets pass under comparable M5 methodology.
2. DAP was not remeasured because the documented real adapter is absent; prior
   evidence is cited and no pass was fabricated.
3. Full sequential regression gate is green (2172 / 0 / 0).
4. M0–M4b dispositions remain consistent with live code and prior evidence;
   no production hardening gap was discovered that requires a Phase 13 fix.
5. Desktop/input/adapter limitations remain explicit **not validated** or
   environment-limited rows — never silently upgraded.
6. No Phase 13 `TOFIX.md` items exist.

### What this does **not** claim

- Universal multi-platform desktop parity.
- Full interactive keyboard/focus audit completion.
- New DAP real-adapter pass without a present binary.
- That app-internal timings prove UX or Avalonia rendering.
- V3 features or post-V2 product work.

### Exact next project milestone

**Roadmap V2 is closed by Phase 13 completion.** There is no further Phase 13
milestone. Post-V2 work (for example a V3 plan, deferred findings DF-001–DF-004,
or platform validation) is outside Phase 13 and must be scheduled separately.
Do not start new feature, UI, performance, recovery, or refactor work under a
Phase 13 label.
