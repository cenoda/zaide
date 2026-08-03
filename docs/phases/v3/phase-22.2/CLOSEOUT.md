# Phase 22.2 Closeout — User Backend-Binding Workflow

## Status

**Phase 22.2 complete; package-2 PASS restored against the live ACP baseline.**

M0–M4 delivered the owned scope: durable schema-v1 store (M1), Native Harness
Townhall workflow (M2), ACP probe/authenticate/logout bridge (M3), and
restart/regression gates with out-of-tree A3 re-smoke (M4).

A post-closeout full package-2 audit identified a blocking ACP runtime-
invalidation defect. Corrective implementation closed that defect and residual
epoch/cache TOCTOU gaps through HEAD `d4a0f34d` (historical package PASS head).
Intervening commit `9c4bb94f` then hardened `AcpStdioProcessHost` process-exit
cancellation before `dfe2bf14`. Independent lifecycle review of that production
delta found no product defect. Targeted ACP `A1-AC-02` evidence was re-smoked
against live HEAD `dfe2bf14` and package-level PASS is restored for A4 package 2
/ Phase 22.2 on that live baseline.

Does **not** mark G5 or V4 ready. Does **not** claim Phase 22.3 tools/send or
Phase 22.4 trace/memory/usage complete. Phase 22 critical path remains in
progress. 22.3 and 22.4 still require separate authorization.

## A1-AC-02

| Backend | Classification | Evidence | Repo head at evidence |
|---------|----------------|----------|------------------------|
| native-harness | **WORKS** | [evidence/A1-AC-02-native-harness.json](./evidence/A1-AC-02-native-harness.json) | prior M4 re-smoke (unaffected by ACP-only corrective) |
| acp | **WORKS** | [evidence/A1-AC-02-acp.json](./evidence/A1-AC-02-acp.json) | **`dfe2bf14`** (live baseline re-smoke; 16/16 pass) |

Live ACP re-smoke drove shipped Townhall binding panel controls
(bind/unbind/probe), isolated disposable HOME/XDG profile and workspace,
production `Program.ConfigureServices`, and the repo-owned ACP fake agent.
Internal `SetBinding` alone was not used as onboarding success. Observed WORKS
gates: bind, probe, restart/revalidation, unbind, and unbind persistence.
Retained A3 producer source was unchanged for the live re-smoke.

## Dependent matrix (honest remaining)

All remaining package-2-adjacent rows: **WORKS_WITH_FRICTION** — binding
reachable; terminal package outcomes remain later phases.

| Scenario | native-harness | acp | Remaining owner |
|----------|----------------|-----|-----------------|
| A1-AS-02 | WORKS_WITH_FRICTION | WORKS_WITH_FRICTION | 22.3 send/tools |
| A1-TH-05 | WORKS_WITH_FRICTION | WORKS_WITH_FRICTION | 22.3 routing/send |
| A1-MR-03 | WORKS_WITH_FRICTION | WORKS_WITH_FRICTION | 22.3 routing/send |
| A1-TC-01 | WORKS_WITH_FRICTION | WORKS_WITH_FRICTION | 22.4 context surfaces |
| A1-TP-01…03 | WORKS_WITH_FRICTION | WORKS_WITH_FRICTION | 22.3 tools/permissions |

Runner retained at `/tmp/zaide-a3-backend-binding/` for this closeout cycle.
The retained A3 producer source was not modified for the evidence refresh.

## Post-closeout corrective + re-audit

Post-M4 audit polish (docs M1 status, logout capability signal, authenticate no
local success stub, hide ACP config when Native is active) landed after package
closeout.

Post-closeout full package-2 audit found blocking ACP runtime invalidation.
Corrective commits through `d4a0f34d` closed onboarding-connection invalidation,
fingerprint/epoch publication guards, advertised-method cache races, empty-
method fail-closed authenticate, and disposal races. Focused regression tests:
`Phase22AcpRuntimeInvalidationTests`, `Phase22AcpEpochCacheTocTouTests`.

Historical independent re-audit after the `d4a0f34d` evidence refresh: **PASS**.
Live-baseline provenance restore (2026-08-03): independent review of intervening
`AcpStdioProcessHost` lifecycle changes through `9c4bb94f` (process-exit
cancellation, sticky terminal states, exit-over-timeout classification, dispose
races) found no product defect; ACP `A1-AC-02` re-smoked at live HEAD
`dfe2bf14` — **16/16 WORKS**. No corrective-only finding set remains for
package 2.

Verification at live package PASS restore (`dfe2bf14`):

| Gate | Result |
|------|--------|
| Independent lifecycle review `d4a0f34d`…`9c4bb94f` | PASS — no product defect |
| ACP `A1-AC-02` live re-smoke | 16/16 WORKS, RepoHead `dfe2bf14` |
| `dotnet build Zaide.slnx --no-incremental` | clean |
| Phase22 binding filter | 71/71 |
| `dotnet test Zaide.slnx --no-build` | 3849/3849 |
| Serial fallback | not required |
| `git diff --check` | clean |

## Explicitly not done

- Phase 22.3, 22.4, 22.5
- G5 (still blocked on packages 3–7 and remaining sub-phases)
- V4 / successor-roadmap planning
- Historical A0–A3 audit rewrites under `docs/audits/v1-v3-product-reality/`
