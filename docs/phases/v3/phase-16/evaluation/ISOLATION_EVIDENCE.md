# Phase 16 M2b: Isolation Evidence

**Status:** Produced by M2b implementation and verification on **2026-07-23**.
This document records only evidence actually produced in-repository. It does not
authorize upstream candidate execution.

---

## 1. Substrate

| Property | Observed value |
|---|---|
| Host OS | Linux x86_64 (kernel 7.1.3-arch1-1) |
| Bubblewrap | `/usr/bin/bwrap` version 0.11.2 |
| .NET SDK | 10.0.109 |
| Sandbox backend | Bubblewrap `--unshare-all`, `--die-with-parent`, `--ro-bind / /`, explicit `--bind` writable roots only |
| Provider-restricted egress | **Not proven** (see §6) |

---

## 2. Repository-Owned Proof Surface

M2b extended the Phase 16 evaluation tool under
`tools/Phase16NativeHarnessEvaluation/` and tests under
`tests/Zaide.Tests/Phase16Evaluation/`. No `src/` production files changed.

### 2.1 Fake-candidate kind added

| Kind | Purpose |
|---|---|
| `sandbox_probe` | Adversarial repository-owned probes executed through runner-owned Bubblewrap launch (`fakeCandidateId` selects probe) |

Existing M2a kinds (`echo`, `metric_snapshot`) remain offline/in-process.

### 2.2 Probe scripts (repository-owned)

Located in `tools/Phase16NativeHarnessEvaluation/Probes/`:

| Script | Adversarial behavior |
|---|---|
| `spawn_descendant.sh` | Spawns a sleeping descendant for tree-termination proof |
| `output_flood.sh` | Emits >64 KiB stdout for capture-cap proof |
| `emit_secret.sh` | Emits credential-shaped output for redaction proof |
| `write_workspace.sh` | Writes inside the declared workspace |
| `attempt_host_write.sh` | Attempts host-root write (expected denial) |
| `attempt_traversal_write.sh` | Attempts absolute host write outside workspace bind |
| `attempt_traversal_read.sh` | Retained for manual inspection; M2b proof uses write denial |

---

## 3. Evidence Produced (automated)

All rows below passed in focused `Zaide.Tests.Phase16Evaluation` on the M2b
verification host (**75/75** tests).

| Threat-model requirement | Evidence mechanism | Result |
|---|---|---|
| Exact argv (no shell interpolation in runner launch) | `Phase16BubblewrapLauncher` builds `ProcessStartInfo.ArgumentList`; `Phase16SandboxExecutorTests.LaunchAsync_UsesExactArgvWithoutShellInterpolation` | **Proven** |
| Environment deny-by-default + allowlist | `Phase16EnvironmentPolicy`; probe `probe-argv-env` | **Proven** |
| Read-only host except explicit writable roots | Bubblewrap `--ro-bind / /` + per-root `--bind`; probe `probe-writable-root` | **Proven** |
| Writable-root path guard | `Phase16WritableRootGuard`; unit tests | **Proven** |
| Symlink / traversal escape rejection (materialization) | `Phase16SymlinkTraversalGuard`; probe `probe-traversal` | **Proven** |
| Host write denial inside sandbox | probe `probe-traversal` / `attempt_traversal_write.sh` | **Proven** |
| Wall-clock timeout | `Phase16ProcessLifecycleManager` + probe `probe-timeout` | **Proven** |
| Cooperative cancellation + forced tree kill | lifecycle manager + probe `probe-cancel` | **Proven** |
| Descendant termination / orphan absence | `--die-with-parent`, forced `Kill(entireProcessTree: true)`, `/proc` marker scan; probe `probe-descendant` | **Proven** |
| Bounded stdout/stderr capture (64 KiB) | `StreamCaptureBuffer` + probe `probe-output-flood` | **Proven** |
| File inventory / dirty detection / reset | `Phase16WorkspaceManager`; probe `probe-workspace-dirty` | **Proven** |
| Content hash binding (fixture inventory) | `Phase16WorkspaceManager.ComputeInventoryHash` | **Proven** |
| Credential redaction before persistence | `Phase16OutputRedactor`; probe `probe-redaction` | **Proven** |
| Cleanup failure blocks later runs | `Phase16CleanupGate`; `Phase16SandboxProbeTests.RunFakeTrial_BlocksAfterCleanupFailure` | **Proven** |

---

## 4. Verification Commands and Totals

Recorded on **2026-07-23** against M2b commit verification:

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents|FullyQualifiedName~Zaide.Tests.Architecture'
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---|---|
| `dotnet build Zaide.slnx` | Passed (4 existing warnings, 0 errors) |
| Focused Phase16Evaluation | **75 passed**, 0 failed, 0 skipped |
| Focused Agents + Architecture | **387 passed**, 0 failed, 0 skipped |
| Full interactive parallel suite | **2788 passed**, 0 failed, 0 skipped (**2788** total) |
| Serial reproduction (`slow.runsettings`) | **2788 passed**, 0 failed, 0 skipped (**2788** total) |

---

## 5. Parallel Runner Note

An earlier M2b verification pass reported one parallel-only failure in
`LinuxTerminalServiceTests.Restart_DoesNotLeakFileDescriptors` (pre-existing
parallel runner limitation also recorded at Phase 16 M0). The final M2b gate
re-run passed in both default parallel and serial modes (**2788/2788**).

---

## 6. Unresolved Limitations (unchanged locked boundaries)

| Limitation | M2b disposition |
|---|---|
| Provider-restricted egress | **Still unproven.** Host lacks `slirp4netns`, `pasta`, and `socat`; Docker daemon and Podman are unavailable. Bubblewrap proves default-deny full network isolation (`--unshare-all` includes network namespace) but not allowlisted provider HTTPS. |
| Upstream candidate execution | **Still forbidden.** M1 all-blocked eligibility unchanged. |
| Real candidate process launch | **Still forbidden.** Manifest `processLaunchEnabled` remains denied; Bubblewrap launch is runner-owned for repository fake probes only. |
| Production reuse | **Not authorized.** Evaluation controls remain phase-owned. |

No locked M0/M1 boundary was weakened to produce this evidence.

---

## 7. M3 Eligibility Assessment

**M3 execution remains blocked.** M2b proves repository-owned isolation
mechanics only. M1 amendment (2026-07-23) made **Qwen Code** **`eligible for
later M3 qualification`** on the single-candidate observational path
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`). OpenCode and Grok Build remain **blocked
at M1**.

**M3a acquisition-and-inspection completed 2026-07-23** under an explicit
separate grant (`M3A_ACQUISITION_EVIDENCE.md`):

- pinned archive acquired under phase artifact root only; SHA-256 verified;
- tag and in-archive `LICENSE` Apache-2.0 (identical); `NOTICE` and
  `THIRD-PARTY-NOTICES` absent at package root;
- A-02/A-03 resolved from static inspection;
- **upstream binary not launched**.

M3 still additionally requires, per candidate slice:

- project-owner C-05 clearance of missing NOTICE/THIRD-PARTY-NOTICES before
  execution;
- provider-restricted egress proof per accepted C-01/C-02 design (enforcement
  still unproven);
- dedicated DeepSeek sub-key and credential injection (separate execution grant);
- M3 qualification grant and isolation re-check before upstream binary launch.

Next external-side-effect candidates under **separate** grants: egress proof
(**recommended GO to authorize** per M3a §9.2), then credentials/execution only
after egress success (**NO-GO until that gate** per M3a §9.3).

---

*M2b isolation evidence — produced 2026-07-23. M3a acquisition completed
2026-07-23 without binary launch. Upstream execution remains unauthorized.*
