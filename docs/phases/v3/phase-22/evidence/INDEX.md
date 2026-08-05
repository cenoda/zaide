# Phase 22 G5 Evidence Index

**Gate:** G5 — critical-path completion and full affected A3 re-smoke  
**Repo head at evidence capture:** `4eb7e15ed3e15c97fe481a1fb6a4889a0c60bb52`  
**Date:** 2026-08-05  
**Contract:** out-of-tree Avalonia.Headless harness, disposable HOME/XDG,
production `Program.ConfigureServices`, no real user profiles, no Zaide repo
as runtime workspace.

This index points at current G5 evidence. It does not rewrite historical A0–A3
audit files under `docs/audits/v1-v3-product-reality/`.

## Package ledger (1–7)

| A4 package | Owner | Status | Closeout / evidence head |
|------------|-------|--------|---------------------------|
| 1 — LSP runtime | 22.1 | Complete | M4 re-smoke `35ebf683`; post-closeout projection fix `2f5e9315` |
| 2 — User backend binding | 22.2 | Complete | Package PASS restore `dfe2bf14` |
| 3 — Send/routing failure projection | 22.3 | Complete | Closure GO `87e455a1` |
| 4 — Trace/memory/usage surfaces | 22.4 | Complete | Closeout `4eb7e15e` |
| 5 — Explicit termination UI | 22.3 | Complete | Closure GO `87e455a1` |
| 6 — Tools/permissions smoke path | 22.3 | Complete | Closure GO `87e455a1` |
| 7 — Interrupted-run positive smoke | 22.3 | Complete | Closure GO `87e455a1` |

Package 8 / Phase 22.5 remains optional and outside G5. Package 9 remains
outside the Phase 22 critical path.

## Evidence roots

| Slice | Directory | Producer (locked by owner) |
|-------|-----------|----------------------------|
| Language intelligence | [lang/](./lang/) | Out-of-tree `/tmp/zaide-a3-lang/` (session-recovered M4 harness; fixture not the Zaide repo) |
| Backend binding | [backend-binding/](./backend-binding/) | Out-of-tree `/tmp/zaide-a3-backend-binding/` |
| Agent path (product rows) | [agent-path/](./agent-path/) | `tests/a3-agent-path/runner/` via `scripts/run-m5-a3-matrix.sh` |
| Transparency | [transparency/](./transparency/) | `tests/a3-transparency/runner/` published out of tree |

## Full affected matrix (G5)

| Goal ID | Backends | Classification | Evidence | Notes |
|---------|----------|----------------|----------|-------|
| A1-FN-09 | n/a (editor/LSP) | **WORKS** 8/8 | [lang/A1-FN-09.json](./lang/A1-FN-09.json) | Completion positive path |
| A1-FN-10 | n/a | **WORKS_WITH_FRICTION** 6/6 | [lang/A1-FN-10.json](./lang/A1-FN-10.json) | Hover Ready with content; residual: product schedules caret dwell (450ms), not pointer hover |
| A1-FN-11 | n/a | **WORKS** 6/6 | [lang/A1-FN-11.json](./lang/A1-FN-11.json) | Go to Definition |
| A1-FN-12 | n/a | **WORKS** 8/8 | [lang/A1-FN-12.json](./lang/A1-FN-12.json) | Document symbols |
| A1-FN-13 | n/a | **WORKS** 7/7 | [lang/A1-FN-13.json](./lang/A1-FN-13.json) | Workspace symbols |
| A1-AC-02 | native-harness | **WORKS** 17/17 | [backend-binding/A1-AC-02-native-harness.json](./backend-binding/A1-AC-02-native-harness.json) | Townhall bind/inspect/restart/unbind |
| A1-AC-02 | acp | **WORKS** 16/16 | [backend-binding/A1-AC-02-acp.json](./backend-binding/A1-AC-02-acp.json) | Same; ACP fake agent |
| A1-AS-02 | native-harness | **WORKS** 18/18 | [agent-path/A1-AS-02-native-harness.json](./agent-path/A1-AS-02-native-harness.json) | Product send/routing path (22.3 producer) |
| A1-AS-02 | acp | **WORKS** 18/18 | [agent-path/A1-AS-02-acp.json](./agent-path/A1-AS-02-acp.json) | |
| A1-TH-05 | native-harness | **WORKS** 17/17 | [agent-path/A1-TH-05-native-harness.json](./agent-path/A1-TH-05-native-harness.json) | Invalid mention → routing failure entry |
| A1-TH-05 | acp | **WORKS** 18/18 | [agent-path/A1-TH-05-acp.json](./agent-path/A1-TH-05-acp.json) | |
| A1-MR-03 | native-harness | **WORKS** 17/17 | [agent-path/A1-MR-03-native-harness.json](./agent-path/A1-MR-03-native-harness.json) | Mention resolution routing |
| A1-MR-03 | acp | **WORKS** 17/17 | [agent-path/A1-MR-03-acp.json](./agent-path/A1-MR-03-acp.json) | |
| A1-TC-01 | native-harness | **WORKS_WITH_FRICTION** 8/9 | [backend-binding/A1-TC-01-native-harness.json](./backend-binding/A1-TC-01-native-harness.json) | Backend-bound context-manifest sub-path only |
| A1-TC-01 | acp | **WORKS_WITH_FRICTION** 8/9 | [backend-binding/A1-TC-01-acp.json](./backend-binding/A1-TC-01-acp.json) | Same residual on both backends |
| A1-TP-01 | native-harness | **WORKS** 16/16 | [agent-path/A1-TP-01-native-harness.json](./agent-path/A1-TP-01-native-harness.json) | See residual tools isolation note below |
| A1-TP-01 | acp | **WORKS** 15/15 | [agent-path/A1-TP-01-acp.json](./agent-path/A1-TP-01-acp.json) | |
| A1-TP-02 | native-harness | **WORKS** 15/15 | [agent-path/A1-TP-02-native-harness.json](./agent-path/A1-TP-02-native-harness.json) | |
| A1-TP-02 | acp | **WORKS** 15/15 | [agent-path/A1-TP-02-acp.json](./agent-path/A1-TP-02-acp.json) | |
| A1-TP-03 | native-harness | **WORKS** 15/15 | [agent-path/A1-TP-03-native-harness.json](./agent-path/A1-TP-03-native-harness.json) | |
| A1-TP-03 | acp | **WORKS** 15/15 | [agent-path/A1-TP-03-acp.json](./agent-path/A1-TP-03-acp.json) | |
| A1-TC-02 | native-harness + acp | **WORKS** (matrix) | [transparency/A1-TC-02-03-08-*.json](./transparency/) | Combined matrix with TC-03/08 |
| A1-TC-03 | native-harness + acp | **WORKS** (matrix) | same | Memory lifecycle surface |
| A1-TC-05 | native-harness | **WORKS** 44/44 | [agent-path/A1-TC-05-native-harness.json](./agent-path/A1-TC-05-native-harness.json) | Force-quit / interrupted-run |
| A1-TC-05 | acp | **WORKS** 45/45 | [agent-path/A1-TC-05-acp.json](./agent-path/A1-TC-05-acp.json) | |
| A1-TC-08 | native-harness + acp | **WORKS** (matrix) | [transparency/](./transparency/) | Usage/cost surface |
| A1-TC-09 | native-harness | **WORKS** 16/16 | [agent-path/A1-TC-09-native-harness.json](./agent-path/A1-TC-09-native-harness.json) | Explicit termination |
| A1-TC-09 | acp | **WORKS** 16/16 | [agent-path/A1-TC-09-acp.json](./agent-path/A1-TC-09-acp.json) | |

Transparency combined matrix:

| Backend | Assertions | Evidence |
|---------|------------|----------|
| native-harness | 55/55 exit 0 | [transparency/A1-TC-02-03-08-native-harness.json](./transparency/A1-TC-02-03-08-native-harness.json) |
| acp | 54/54 exit 0 | [transparency/A1-TC-02-03-08-acp.json](./transparency/A1-TC-02-03-08-acp.json) |

Backend-binding dependent preflight rows (`A1-AS-02`…`A1-TP-03` under
`backend-binding/`) remain on disk for completeness and still classify
**WORKS_WITH_FRICTION** as binding-only preflight. G5 authoritative product
classifications for those goals are the agent-path producer rows above.

## Residual limitations (truthful)

1. **A1-FN-10** — Hover positive path passes; product schedules hover on caret
   dwell rather than pointer hover (`hover.friction` in evidence).
2. **A1-TC-01** — Backend-bound context-manifest sub-path only; not a full
   context-policy product closeout row.
3. **A1-TP-01…03 (native-harness)** — A3 text-only loopback proves the prompt
   reached the provider; broker/permission tool-call branches remain covered by
   `Phase22MediatedActionPathTests` / Phase 17 tests (recorded
   `tools.isolation_note` in producer evidence when present).
4. **A1-TP-01…03 (ACP)** — Fake agent proves sibling invocation; full
   `fs/read_text_file` / `fs/write_text_file` JSON-RPC tool mediation remains
   unit/integration proven rather than A3 text-loopback proven.
5. **Package 8 / A1-DB-01** — Optional Phase 22.5; not required for G5.
6. **Package 9** — Outside Phase 22 critical path.

## Not claimed

- Product readiness
- V4 / successor-roadmap start
- Phase 22.5 authorization
- Silent PASS for missing matrix rows (none missing)
