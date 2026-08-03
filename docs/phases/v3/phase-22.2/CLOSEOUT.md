# Phase 22.2 Closeout — User Backend-Binding Workflow

## Status

**Phase 22.2 complete; package-2 PASS restored.**

M0–M4 delivered the owned scope: durable schema-v1 store (M1), Native Harness
Townhall workflow (M2), ACP probe/authenticate/logout bridge (M3), and
restart/regression gates with out-of-tree A3 re-smoke (M4).

A post-closeout full package-2 audit identified a blocking ACP runtime-
invalidation defect. Corrective implementation closed that defect and residual
epoch/cache TOCTOU gaps through HEAD `d4a0f34d`. Targeted ACP `A1-AC-02`
evidence was refreshed and an independent package-2 re-audit passed. Package-
level PASS is therefore restored for A4 package 2 / Phase 22.2.

Does **not** mark G5 or V4 ready. Does **not** claim Phase 22.3 tools/send or
Phase 22.4 trace/memory/usage complete. 22.3 and 22.4 still require separate
authorization.

## A1-AC-02

| Backend | Classification | Evidence | Repo head at evidence |
|---------|----------------|----------|------------------------|
| native-harness | **WORKS** | [evidence/A1-AC-02-native-harness.json](./evidence/A1-AC-02-native-harness.json) | prior M4 re-smoke (unaffected by ACP-only corrective) |
| acp | **WORKS** | [evidence/A1-AC-02-acp.json](./evidence/A1-AC-02-acp.json) | **`d4a0f34d`** (refreshed evidence; 16/16 pass) |

Refreshed ACP re-smoke drove shipped Townhall binding panel controls
(bind/unbind/probe), isolated disposable HOME/XDG profile and workspace,
production `Program.ConfigureServices`, and the repo-owned ACP fake agent.
Internal `SetBinding` alone was not used as onboarding success. Observed WORKS
gates: bind, probe, restart/revalidation, unbind, and unbind persistence.

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

Independent re-audit after evidence refresh: **PASS**. No corrective-only
finding set remains for package 2.

Verification at package PASS restore:

| Gate | Result |
|------|--------|
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
