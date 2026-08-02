# Phase 22.2 Closeout — User Backend-Binding Workflow

## Status

**Phase 22.2 package 2 complete** for the owned scope: durable schema-v1 store
(M1), Native Harness Townhall workflow (M2), ACP probe/authenticate/logout
bridge (M3), restart/regression gates and out-of-tree A3 re-smoke (M4).

Does **not** mark G5 or V4 ready. Does **not** claim Phase 22.3 tools/send or
Phase 22.4 trace/memory/usage complete.

## A1-AC-02

| Backend | Classification | Evidence |
|---------|----------------|----------|
| native-harness | **WORKS** | [evidence/A1-AC-02-native-harness.json](./evidence/A1-AC-02-native-harness.json) |
| acp | **WORKS** | [evidence/A1-AC-02-acp.json](./evidence/A1-AC-02-acp.json) |

Re-smoke drove shipped Townhall binding panel controls (bind/unbind/probe),
isolated disposable profiles, production `Program.ConfigureServices`, loopback
Native provider env, and repo-owned ACP fake agent. Internal `SetBinding` alone
was not used as onboarding success.

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

## Post-closeout polish

Post-M4 audit polish (docs M1 status, logout capability signal, authenticate
no local success stub, hide ACP config when Native is active) landed after
package closeout; package 2 status is unchanged.

## Explicitly not done

- Phase 22.3, 22.4, 22.5, V4, G5
- Historical A0–A3 audit rewrites under `docs/audits/v1-v3-product-reality/`
