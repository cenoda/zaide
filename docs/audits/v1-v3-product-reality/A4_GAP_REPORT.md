# V1–V3 Product Reality Audit — A4 Gap Report and V4 Proceed Decision

**Audit name:** `v1-v3-product-reality`
**Owner folder:** `docs/audits/v1-v3-product-reality/`
**Audit plan:** [AUDIT_PLAN.md](./AUDIT_PLAN.md)
**Goal matrix:** [GOAL_MATRIX.md](./GOAL_MATRIX.md)
**A1 acceptance:** [A1_ACCEPTANCE.md](./A1_ACCEPTANCE.md)
**A3 closeout:** [evidence/A3_CLEAN_PROFILE_SMOKE.md](./evidence/A3_CLEAN_PROFILE_SMOKE.md)
**Report date:** 2026-08-01
**Document class:** A4 gap report and recorded V4-proceed decision (docs only)

---

## 0. Charter and scope

This report reconciles **A0** (baseline lock), **A1** (goal inventory), **A2** (wiring audit), and **A3** (clean-profile smoke) without rewriting prior evidence files. It classifies material gaps, records a blocker ledger, and issues the sole artifact that authorizes or withholds V4 / successor-roadmap planning per [AUDIT_PLAN.md §7.2](./AUDIT_PLAN.md#72-a4-v4-proceed-decision-authorizes-v4-planning).

This session does **not** begin V4 planning, stabilization, or corrective implementation.

---

## 1. Preserved inventory counts (A1)

Counts are taken from [A1_ACCEPTANCE.md §2](./A1_ACCEPTANCE.md#2-preserved-counts) and [GOAL_MATRIX.md §17.1](./GOAL_MATRIX.md#171-counts). They are **not** merged.

| Quantity | Count | Notes |
|----------|------:|-------|
| Unique user-observable goal rows (`A1-*-NN`, §1–§14) | **57** | Entered A2 and A3 |
| Rows that cannot be translated into user behavior (`A1-XX-*`, §15) | **5** | Scoped dispositions only; **not** user-goal verdicts |
| Total matrix rows | **62** | 57 + 5 |

The five `A1-XX-*` rows are reconciled in [§6](#6-a1-xx-scoped-dispositions-not-user-goal-verdicts). They do not change the 57 user-goal total.

---

## 2. Phase reconciliation (A0 → A3)

| Phase | Artifact | Status | Role in this report |
|-------|----------|--------|---------------------|
| **A0** | [AUDIT_PLAN.md](./AUDIT_PLAN.md) | Baseline lock (2026-07-30) | Safety rules, journey scope, gate definitions |
| **A1** | [GOAL_MATRIX.md](./GOAL_MATRIX.md), [A1_ACCEPTANCE.md](./A1_ACCEPTANCE.md) | **Accepted** 2026-07-30 | 57 promises + 5 `A1-XX-*` ambiguities |
| **A2** | Fifteen `evidence/A2_*.md` slices | **Complete** 2026-07-31 | Wiring verdict per user-goal row ([GOAL_MATRIX.md §17.8](./GOAL_MATRIX.md#178-current-a2-progress)) |
| **A3** | Twenty-one `evidence/A3_*.md` files + [A3_CLEAN_PROFILE_SMOKE.md](./evidence/A3_CLEAN_PROFILE_SMOKE.md) | **Evidence complete** 2026-08-01 | Clean-profile smoke per row ([A3 closeout §2](./evidence/A3_CLEAN_PROFILE_SMOKE.md#2-row-level-matrix-all-57-accepted-user-goals)) |

**A2 evidence completeness:** All 57 accepted user-goal rows have a primary A2 wiring verdict. No missing-evidence row is listed as a blocker to this report.

**A3 evidence completeness:** All 57 rows have an A3 classification or an explicit missing-smoke / out-of-scope entry ([A3 closeout §0](./evidence/A3_CLEAN_PROFILE_SMOKE.md#0-charter-and-status)). `A1-FL-06` is explicitly out of A3 scope (performance budgets).

---

## 3. Verdict rollup (A2 × A3)

### 3.1 A2 wiring summary (57 user goals)

| A2 verdict | Count | Row ids |
|------------|------:|---------|
| **Wired** | 20 | FN-01, FN-03…FN-06, FN-09, FN-11…FN-14, SC-03, BR-01…BR-04, GT-01…GT-04, TH-02, TH-04, FL-05 |
| **Wired-with-gap** | 30 | Remaining rows with production paths but documented incompleteness |
| **Missing** | 7 | AS-01, MR-01, AC-01 (retired panel path), TC-02, TC-03, TC-08, TC-09 |

Authoritative per-row A2 verdicts: [GOAL_MATRIX.md §17.8](./GOAL_MATRIX.md#178-current-a2-progress) and each `evidence/A2_*.md` slice.

### 3.2 A3 smoke summary (57 user goals)

| A3 classification | Count | Row ids |
|-------------------|------:|---------|
| **WORKS** | 18 | FL-04, FL-05, FN-03, FN-04, FN-06, FN-08, FN-14, SC-03, BR-02…BR-04, GT-01…GT-04, TH-01, TH-02, TH-04 |
| **WORKS_WITH_FRICTION** | 22 | FL-01…FL-03, WO-01…WO-03, FN-01, FN-02, FN-05, FN-15, SC-01, SC-02, BR-01, TR-01, TR-02, TH-05, AC-02, AS-02, MR-03, TC-01, TC-04 |
| **BROKEN** | 5 | FN-09, FN-10, FN-11, FN-12, FN-13 |
| **Missing** | 5 | AS-01, MR-01, TC-02, TC-03, TC-08 |
| **UNWIRED** | 1 | AC-01 |
| **BLOCKED** | 5 | DB-01, TP-01…TP-03, TC-05, TC-09 |
| **Out of A3 scope** | 1 | FL-06 |

Authoritative per-row A3 classifications: [A3_CLEAN_PROFILE_SMOKE.md §2](./evidence/A3_CLEAN_PROFILE_SMOKE.md#2-row-level-matrix-all-57-accepted-user-goals).

### 3.3 Cross-phase tension (material only)

Where A2 said **Wired** or **Wired-with-gap** but A3 found **BROKEN**, **BLOCKED**, or **Missing**, the A3 product-runtime observation takes precedence for product-readiness. Where A3 could not exercise a positive path because of a documented prerequisite (NetCoreDbg, backend binding), the row remains **BLOCKED** at smoke layer without upgrading A2’s wiring-absence verdict.

---

## 4. Finding-type legend

| Type | Meaning |
|------|---------|
| **implementation gap** | Production code incomplete vs documented contract; infrastructure may exist |
| **runtime failure** | User-reachable path exercised and failed against success condition |
| **environmental prerequisite** | Smoke blocked by host/tooling not present in disposable profile |
| **missing user entry point** | No user-reachable UI, command, or workflow to reach the promised behavior |
| **visual-only unverified claim** | Functional path may be proven; pixel paint, pointer chrome, or desktop-only affordances not claimed |
| **out-of-scope item** | Explicitly excluded from A3 or V1–V3 audit scope |

---

## 5. Severity-classified findings

Severity applies to **product-readiness impact** for V1–V3 promised outcomes. Every material finding links to direct evidence.

### 5.1 Blocker

| ID | Goal / area | A2 | A3 | Type | Finding | Evidence |
|----|-------------|----|----|------|---------|----------|
| BL-01 | `A1-FN-09` | Wired | **BROKEN** | runtime failure | LSP completion fails: Avalonia thread-affinity off UI thread; completion list never becomes Ready | [A3_LANGUAGE_INTELLIGENCE.md](./evidence/A3_LANGUAGE_INTELLIGENCE.md) |
| BL-02 | `A1-FN-10` | Wired-with-gap | **BROKEN** | runtime failure | Caret-dwell hover stuck Loading; no observable content within timeout | [A3_LANGUAGE_INTELLIGENCE.md](./evidence/A3_LANGUAGE_INTELLIGENCE.md) |
| BL-03 | `A1-FN-11` | Wired | **BROKEN** | runtime failure | Go to Definition stuck Loading on known symbol (empty feedback path works) | [A3_LANGUAGE_INTELLIGENCE.md](./evidence/A3_LANGUAGE_INTELLIGENCE.md) |
| BL-04 | `A1-FN-12` | Wired | **BROKEN** | runtime failure | Document symbols Failed with zero symbols on multi-type file | [A3_LANGUAGE_INTELLIGENCE.md](./evidence/A3_LANGUAGE_INTELLIGENCE.md) |
| BL-05 | `A1-FN-13` | Wired | **BROKEN** | runtime failure | Workspace symbols Failed; no cross-file results | [A3_LANGUAGE_INTELLIGENCE.md](./evidence/A3_LANGUAGE_INTELLIGENCE.md) |
| BL-06 | Backend binding | Wired-with-gap (`A1-AC-02`) | **WORKS_WITH_FRICTION** / positive **BLOCKED** | missing user entry point | No user Native Harness / ACP bind, configure, unbind, or persist workflow; clean profile stays Unbound | [A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./evidence/A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md), [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./evidence/A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md), [A1-XX-01](./GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior) |
| BL-07 | Agent send positive path | Wired-with-gap (`A1-AS-02`) | **WORKS_WITH_FRICTION** / positive **BLOCKED** | missing user entry point | Send reachable; pre-admission rejection honest; no assistant response; no actionable failure in chat without bind | [A2_AGENT_SEND.md](./evidence/A2_AGENT_SEND.md), [A3_AGENT_SEND.md](./evidence/A3_AGENT_SEND.md) |
| BL-08 | Tools / permissions | Wired-with-gap (`A1-TP-01`…`03`) | **BLOCKED** | missing user entry point | No user-reachable mediated action trigger on unbound profile; permission management and rollback UX absent | [A2_TOOLS_PERMISSIONS.md](./evidence/A2_TOOLS_PERMISSIONS.md), [A3_TOOLS_PERMISSIONS_PREFLIGHT.md](./evidence/A3_TOOLS_PERMISSIONS_PREFLIGHT.md) |
| BL-09 | Trace inspection | **Missing** (`A1-TC-02`) | **Missing** | missing user entry point | Zero trace commands/Views; trace inspect path UNWIRED | [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md), [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./evidence/A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) |
| BL-10 | Memory management | **Missing** (`A1-TC-03`) | **Missing** | missing user entry point | No memory CRUD commands/Views; management surface UNWIRED | [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md), [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./evidence/A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) |
| BL-11 | Usage / cost | **Missing** (`A1-TC-08`) | **Missing** | missing user entry point | No usage/cost commands/Views; capture disabled on probe | [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md), [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./evidence/A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) |
| BL-12 | Positive debugging | Wired-with-gap (`A1-DB-01`) | **BLOCKED** | environmental prerequisite | Positive breakpoint/step/stack/variables path requires **netcoredbg** (`ZAIDE_NETCOREDBG_PATH` or `PATH`); negative paths exercised | [A2_DEBUGGING_AND_OUTPUT.md](./evidence/A2_DEBUGGING_AND_OUTPUT.md), [A3_DEBUGGING_PREFLIGHT.md](./evidence/A3_DEBUGGING_PREFLIGHT.md), [A1-XX-04](./GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior) |
| BL-13 | Interrupted-run recovery | Wired-with-gap (`A1-TC-05`) | **BLOCKED** | environmental prerequisite | Positive interrupted-run path not exercised without admitted backend run; negative no-resume evidence recorded | [A2_RESTART_RECOVERY_AND_CONTEXT.md](./evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md), [A3_RESTART_AND_RECOVERY.md](./evidence/A3_RESTART_AND_RECOVERY.md) |
| BL-14 | Explicit termination | **Missing** (`A1-TC-09`) | **BLOCKED** | missing user entry point | No end UI/command; `EndAsync` exists per A2 but no production caller; nothing to terminate on unbound profile | [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md), [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./evidence/A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) |

### 5.2 High

| ID | Goal / area | A2 | A3 | Type | Finding | Evidence |
|----|-------------|----|----|------|---------|----------|
| HI-01 | `A1-AC-01` | **Missing** | **UNWIRED** | implementation gap | Historical Phase 5 Agent Panel create path retired; no replacement create/rename/remove/configure workflow | [A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./evidence/A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md), [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./evidence/A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) |
| HI-02 | `A1-MR-01` | **Missing** | **Missing** | implementation gap | Panel-bound Phase 6 `@mention` entry absent; catalog routing (`A1-MR-03`) partially substitutes | [A2_MULTI_AGENT_ROUTING.md](./evidence/A2_MULTI_AGENT_ROUTING.md), [A3_MULTI_AGENT_ROUTING.md](./evidence/A3_MULTI_AGENT_ROUTING.md) |
| HI-03 | `A1-MR-03` | Wired-with-gap | **WORKS_WITH_FRICTION** / admitted route **BLOCKED** | missing user entry point | Catalog routing and negative failures work; admitted routed success blocked without backend bind | [A2_MULTI_AGENT_ROUTING.md](./evidence/A2_MULTI_AGENT_ROUTING.md), [A3_MULTI_AGENT_ROUTING.md](./evidence/A3_MULTI_AGENT_ROUTING.md) |
| HI-04 | `A1-TH-05` | Wired-with-gap | **WORKS_WITH_FRICTION** / admitted route **BLOCKED** | implementation gap | Routing failures on source proven; successful routed flow not shown in source; admitted success blocked without bind | [A2_TOWNHALL_AND_CONVERSATIONS.md](./evidence/A2_TOWNHALL_AND_CONVERSATIONS.md), [A3_TOWNHALL_AND_CONVERSATIONS.md](./evidence/A3_TOWNHALL_AND_CONVERSATIONS.md) |
| HI-05 | `A1-AS-01` | **Missing** | **Missing** | implementation gap | Historical Agent Panel send retired; not re-executed in A3 (A2-aligned) | [A2_AGENT_SEND.md](./evidence/A2_AGENT_SEND.md), [A3_AGENT_SEND.md](./evidence/A3_AGENT_SEND.md) |
| HI-06 | `A1-TC-01` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | Townhall context selector user-reachable; no settings entry; overrides in-memory; Off manifest on send blocked without backend | [A2_RESTART_RECOVERY_AND_CONTEXT.md](./evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md), [A3_CONTEXT_POLICY.md](./evidence/A3_CONTEXT_POLICY.md) |
| HI-07 | `A1-TC-04` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | Conversation snapshot restore works; debounced-draft gap on fast exit; persistence failures silent to user | [A2_RESTART_RECOVERY_AND_CONTEXT.md](./evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md), [A3_RESTART_AND_RECOVERY.md](./evidence/A3_RESTART_AND_RECOVERY.md) |
| HI-08 | Send failure projection | Wired-with-gap (`A1-AS-02`) | **WORKS_WITH_FRICTION** | implementation gap | Pre-admission rejection not projected as actionable failure in Townhall chat (per A1 ambiguity) | [A2_AGENT_SEND.md](./evidence/A2_AGENT_SEND.md), [A3_AGENT_SEND.md](./evidence/A3_AGENT_SEND.md), [ISSUE-008](../../issues/open/ISSUE-008-agent-response-not-showing.md) |
| HI-09 | Permission model depth | Wired-with-gap (`A1-TP-02`) | **BLOCKED** (smoke) | implementation gap | Five-kind model partial; no network/Git/secrets/destructive/memory dimensions; ACP auto-reject not user-reachable | [A2_TOOLS_PERMISSIONS.md](./evidence/A2_TOOLS_PERMISSIONS.md) |
| HI-10 | Workspace mutation | Wired-with-gap (`A1-TP-03`) | **BLOCKED** (smoke) | implementation gap | No multi-file transactions, change sets, rollback UI, or partial-apply cancellation | [A2_TOOLS_PERMISSIONS.md](./evidence/A2_TOOLS_PERMISSIONS.md) |

### 5.3 Medium

| ID | Goal / area | A2 | A3 | Type | Finding | Evidence |
|----|-------------|----|----|------|---------|----------|
| MD-01 | `A1-WO-02` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | Ambiguous multi-project labeled `"Project error"`; no user picker | [A2_WORKSPACE_AND_PROJECT_OPENING.md](./evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md), [A3_WORKSPACE_AND_PROJECT_OPENING.md](./evidence/A3_WORKSPACE_AND_PROJECT_OPENING.md) |
| MD-02 | `A1-WO-01` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | File-tree failure messages not projected to shell status | [A2_WORKSPACE_AND_PROJECT_OPENING.md](./evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md), [A3_WORKSPACE_AND_PROJECT_OPENING.md](./evidence/A3_WORKSPACE_AND_PROJECT_OPENING.md) |
| MD-03 | `A1-WO-03` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | Source Control refresh coupled to host `RootPath` not direct workspace event | [A2_WORKSPACE_AND_PROJECT_OPENING.md](./evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md), [A3_WORKSPACE_AND_PROJECT_OPENING.md](./evidence/A3_WORKSPACE_AND_PROJECT_OPENING.md) |
| MD-04 | `A1-FL-03` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | Settings persist and corrupt→LKG work; load/write failures not user-visible | [A2_FIRST_LAUNCH_AND_SETTINGS.md](./evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md), [A3_FIRST_LAUNCH_AND_SETTINGS.md](./evidence/A3_FIRST_LAUNCH_AND_SETTINGS.md) |
| MD-05 | `A1-SC-01` | Wired-with-gap | **WORKS_WITH_FRICTION** | missing user entry point | Keybinding overrides via settings file only; no keybindings editor; conflicts log-only | [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./evidence/A2_SEARCH_AND_COMMAND_DISCOVERY.md), [A3_SEARCH_AND_COMMAND_DISCOVERY.md](./evidence/A3_SEARCH_AND_COMMAND_DISCOVERY.md) |
| MD-06 | `A1-FN-02` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | Splitter max 320px not documented 500px | [A2_FILE_NAVIGATION_AND_EDITING.md](./evidence/A2_FILE_NAVIGATION_AND_EDITING.md), [A3_FILE_NAVIGATION_AND_EDITING_CORE.md](./evidence/A3_FILE_NAVIGATION_AND_EDITING_CORE.md) |
| MD-07 | `A1-FN-15` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | Format on Save reformats disk; save swallows format failures; buffer can lag disk | [A2_FILE_NAVIGATION_AND_EDITING.md](./evidence/A2_FILE_NAVIGATION_AND_EDITING.md), [A3_LANGUAGE_INTELLIGENCE.md](./evidence/A3_LANGUAGE_INTELLIGENCE.md) |
| MD-08 | `A1-TR-01` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | `Ctrl+\`` toggles visibility without forcing Terminal mode; Linux-only backend | [A2_TERMINAL.md](./evidence/A2_TERMINAL.md), [A3_TERMINAL.md](./evidence/A3_TERMINAL.md) |
| MD-09 | `A1-TR-02` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | Multi-tab PTY isolation proven; static `"Terminal"` titles | [A2_TERMINAL.md](./evidence/A2_TERMINAL.md), [A3_TERMINAL.md](./evidence/A3_TERMINAL.md) |
| MD-10 | `A1-FL-01` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | Multi-column shell proven; historical Phase 0 three-panel / right-agent layout not observed | [A2_FIRST_LAUNCH_AND_SETTINGS.md](./evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md), [A3_FIRST_LAUNCH_AND_SETTINGS.md](./evidence/A3_FIRST_LAUNCH_AND_SETTINGS.md) |
| MD-11 | `A1-FL-02` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | Dark theme + Navy palette; no user theme switcher | [A2_FIRST_LAUNCH_AND_SETTINGS.md](./evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md), [A3_FIRST_LAUNCH_AND_SETTINGS.md](./evidence/A3_FIRST_LAUNCH_AND_SETTINGS.md) |
| MD-12 | `A1-BR-01` | Wired | **WORKS_WITH_FRICTION** | runtime failure | Build/run/test/cancel work; Output list template NRE mitigated in harness only | [A2_BUILD_RUN_AND_TEST.md](./evidence/A2_BUILD_RUN_AND_TEST.md), [A3_BUILD_RUN_AND_TEST.md](./evidence/A3_BUILD_RUN_AND_TEST.md) |
| MD-13 | `A1-TC-05` (negative) | Wired-with-gap | partial evidence | implementation gap | Startup `Reconcile` not `Resume`; classification not projected to Townhall | [A2_RESTART_RECOVERY_AND_CONTEXT.md](./evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md), [A3_RESTART_AND_RECOVERY.md](./evidence/A3_RESTART_AND_RECOVERY.md) |

### 5.4 Low

| ID | Goal / area | A2 | A3 | Type | Finding | Evidence |
|----|-------------|----|----|------|---------|----------|
| LO-01 | `A1-SC-02` | Wired-with-gap | **WORKS_WITH_FRICTION** | implementation gap | Palette works; pointer click does not reselect row before execute | [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./evidence/A2_SEARCH_AND_COMMAND_DISCOVERY.md), [A3_SEARCH_AND_COMMAND_DISCOVERY.md](./evidence/A3_SEARCH_AND_COMMAND_DISCOVERY.md) |
| LO-02 | `A1-FN-10` (A2) | Wired-with-gap | **BROKEN** at runtime | implementation gap | A2 noted caret-dwell not pointer hover; A3 proved hover path broken | [A2_FILE_NAVIGATION_AND_EDITING.md](./evidence/A2_FILE_NAVIGATION_AND_EDITING.md) |
| LO-03 | `A1-FN-08` | Wired-with-gap | **WORKS** | environmental prerequisite | Problems projection works with `csharp-ls`; cold success needs eligible project + external binary | [A2_FILE_NAVIGATION_AND_EDITING.md](./evidence/A2_FILE_NAVIGATION_AND_EDITING.md), [A3_LANGUAGE_INTELLIGENCE.md](./evidence/A3_LANGUAGE_INTELLIGENCE.md) |
| LO-04 | `A1-TH-01` | Wired-with-gap | **WORKS** | implementation gap | Channels and filters work; custom channels not user-creatable | [A2_TOWNHALL_AND_CONVERSATIONS.md](./evidence/A2_TOWNHALL_AND_CONVERSATIONS.md), [A3_TOWNHALL_AND_CONVERSATIONS.md](./evidence/A3_TOWNHALL_AND_CONVERSATIONS.md) |
| LO-05 | Visual sub-claims | n/a | **UNVERIFIED-VIS** | visual-only unverified claim | Widespread headless limitation: theme paint, diff coloring, terminal cells, gutter chrome | [A3_CLEAN_PROFILE_SMOKE.md §6](./evidence/A3_CLEAN_PROFILE_SMOKE.md#6-environmental-and-product-blockers) |
| LO-06 | `A1-FL-06` | Wired-with-gap | out of scope | out-of-scope item | Performance budgets are harness/closeout evidence, not A3 product smoke | [GOAL_MATRIX.md §1](./GOAL_MATRIX.md#1-first-launch-and-settings), [A3_AUTOMATION_READINESS.md](./evidence/A3_AUTOMATION_READINESS.md) |

### 5.5 Rows with no material gap (A3 WORKS, A2 Wired)

These 18 rows met documented success conditions under clean-profile smoke without a material gap finding above: `A1-FL-04`, `A1-FL-05`, `A1-FN-03`, `A1-FN-04`, `A1-FN-06`, `A1-FN-08`, `A1-FN-14`, `A1-SC-03`, `A1-BR-02`, `A1-BR-03`, `A1-BR-04`, `A1-GT-01`…`A1-GT-04`, `A1-TH-01`, `A1-TH-02`, `A1-TH-04`. Evidence: [A3_CLEAN_PROFILE_SMOKE.md §2](./evidence/A3_CLEAN_PROFILE_SMOKE.md#2-row-level-matrix-all-57-accepted-user-goals).

---

## 6. `A1-XX-*` scoped dispositions (not user-goal verdicts)

The five §15 rows are preserved separately from the 57 user goals. A2 recorded scoped dispositions only; A3 did not assign row classifications to these ids.

| id | A2 disposition | A4 reconciliation | Evidence |
|----|----------------|-------------------|----------|
| `A1-XX-01` | Gap **confirmed** — binding infrastructure exists; supported user onboarding entry point absent | **Blocker** (same as BL-06); aligns with `A1-AC-02` smoke | [A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./evidence/A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) |
| `A1-XX-02` | **Confirmed absent** — no specialized debate/disagreement surface | **Low** — documented non-goal; not a V1–V3 promise gap | [A2_MULTI_AGENT_ROUTING.md](./evidence/A2_MULTI_AGENT_ROUTING.md) |
| `A1-XX-03` | Trace/memory/usage producers and user surfaces absent at wiring layer | **Blocker** — subsumed by BL-09…BL-11, BL-14 | [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md) |
| `A1-XX-04` | DAP validation requires disposable host with NetCoreDbg | **Blocker** for positive debug (BL-12); environmental prerequisite | [A2_DEBUGGING_AND_OUTPUT.md](./evidence/A2_DEBUGGING_AND_OUTPUT.md) |
| `A1-XX-05` | Conversation persistence application-scoped; CWD not proven workspace-root provider | **Medium** — internal constraint observed; not a missing user promise | [A2_RESTART_RECOVERY_AND_CONTEXT.md](./evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md) |

---

## 7. Blocker ledger

Mandatory ledger entries required by the A4 charter. Each row maps to [§5.1](#51-blocker) findings.

| Ledger item | Severity | Affected goals / areas | Type | Status | Primary evidence |
|-------------|----------|------------------------|------|--------|------------------|
| **BROKEN language-intelligence rows** | blocker | `A1-FN-09`…`A1-FN-13` (5 rows) | runtime failure | Open | [A3_LANGUAGE_INTELLIGENCE.md](./evidence/A3_LANGUAGE_INTELLIGENCE.md) (BL-01…BL-05) |
| **Missing netcoredbg for positive debugging** | blocker | `A1-DB-01` positive path | environmental prerequisite | Open — negative paths **WORKS** | [A3_DEBUGGING_PREFLIGHT.md](./evidence/A3_DEBUGGING_PREFLIGHT.md) (BL-12) |
| **Absent backend-binding workflow** | blocker | `A1-AC-02`, `A1-XX-01`; cascades to send, routing, context manifest, tools | missing user entry point | Open | [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./evidence/A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) (BL-06) |
| **Blocked positive agent send / routing paths** | blocker | `A1-AS-02`, `A1-MR-03`, `A1-TH-05` admitted-success sub-paths | missing user entry point | Open | [A3_AGENT_SEND.md](./evidence/A3_AGENT_SEND.md), [A3_MULTI_AGENT_ROUTING.md](./evidence/A3_MULTI_AGENT_ROUTING.md) (BL-07, HI-03, HI-04) |
| **Blocked tools / permissions paths** | blocker | `A1-TP-01`…`A1-TP-03` | missing user entry point | Open | [A3_TOOLS_PERMISSIONS_PREFLIGHT.md](./evidence/A3_TOOLS_PERMISSIONS_PREFLIGHT.md) (BL-08) |
| **Missing trace / memory / usage surfaces** | blocker | `A1-TC-02`, `A1-TC-03`, `A1-TC-08` | missing user entry point | Open | [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./evidence/A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) (BL-09…BL-11) |
| **Blocked interrupted-run termination / recovery** | blocker | `A1-TC-05` positive path, `A1-TC-09` | environmental prerequisite + missing user entry point | Open | [A3_RESTART_AND_RECOVERY.md](./evidence/A3_RESTART_AND_RECOVERY.md), [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./evidence/A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) (BL-13, BL-14) |
| **`A1-FL-06` performance budgets out of A3 scope** | low (out-of-scope) | `A1-FL-06` | out-of-scope item | Recorded — not an A3 smoke blocker | [GOAL_MATRIX.md](./GOAL_MATRIX.md), [A3_AUTOMATION_READINESS.md](./evidence/A3_AUTOMATION_READINESS.md) (LO-06) |

**Blocker count:** 14 blocker-severity findings (BL-01…BL-14). Seven ledger themes; language intelligence counts as one theme with five row-level blockers.

---

## 8. Journey readiness summary

| Journey | Goals | A3 WORKS | Material blockers | Readiness |
|---------|------:|---------:|-------------------|-----------|
| First launch / settings | 6 | 2 (+1 out of scope) | None at blocker level | **Partial** — friction on layout/theme/recovery UI |
| Workspace / project | 3 | 0 | None at blocker level | **Partial** — friction on errors and ambiguous project |
| File nav / editing | 14 | 4 | **5 BROKEN** LSP rows | **Blocked** on language intelligence |
| Search / commands | 3 | 1 | None at blocker level | **Mostly ready** |
| Build / run / test | 4 | 3 | None at blocker level | **Mostly ready** |
| Debugging | 1 | 0 | NetCoreDbg prerequisite | **Blocked** on positive path |
| Terminal | 2 | 0 | None at blocker level | **Partial** — friction only |
| Git | 4 | 4 | None | **Ready** |
| Townhall | 4 | 3 | Admitted routing blocked without bind | **Partial** — shell ready, agent flows blocked |
| Agent creation | 2 | 0 | No bind UI | **Blocked** |
| Agent send | 2 | 0 | Positive path blocked | **Blocked** |
| Tools / permissions | 3 | 0 | All **BLOCKED** on clean profile | **Blocked** |
| Multi-agent routing | 2 | 0 | Historical Missing + bind cascade | **Blocked** |
| Trace / context / memory / recovery | 7 | 0 | Trace/memory/usage Missing; termination blocked | **Blocked** |

---

## 9. Corrective work required before V4 planning

Sequencing is **dependency-ordered**. No implementation is authorized by this report; a separate planning decision is required.

| Order | Work package | Closes (ledger / findings) | Depends on |
|------:|--------------|----------------------------|------------|
| 1 | **LSP runtime fixes** — thread-affinity for completion; hover, definition, document/workspace symbol pipelines | BL-01…BL-05 | None |
| 2 | **User backend-binding workflow** — Native Harness and ACP bind/configure/unbind/persist UI; bridge ACP `authenticate` | BL-06, `A1-XX-01`, enables BL-07 cascade | None (parallel with 1) |
| 3 | **Agent send / routing failure projection** — actionable rejection and outcome visibility in Townhall | BL-07, HI-04, HI-08 | 2 |
| 4 | **Trace / memory / usage user surfaces** — commands, Views, redaction/retention/capture state per Phase 21 contracts | BL-09…BL-11, `A1-XX-03` | 2 (backend producers) |
| 5 | **Explicit session termination UI** — wire `EndAsync` to user command; terminal-state projection | BL-14 | 2 |
| 6 | **Tools / permissions smoke path** — mediated action trigger, permission UX, dimensions per Phase 17 | BL-08, HI-09, HI-10 | 2 |
| 7 | **Interrupted-run positive smoke** — admitted run + force-quit + reconcile projection | BL-13, MD-13 | 2 |
| 8 | **Debug positive-path validation** — supply NetCoreDbg in disposable CI/host; re-run `A1-DB-01` positive smoke | BL-12, `A1-XX-04` | Host tooling |
| 9 | **Medium-friction backlog** — ambiguous project picker, settings error surfacing, keybindings editor, etc. | §5.3 | 1–2 as needed |

**Re-smoke gate:** After packages 1–7 (minimum), repeat affected A3 slices on disposable profiles before authorizing V4 planning. Package 8 can proceed in parallel if debug is not on the V4 critical path.

---

## 10. Recorded V4-proceed decision

Per [AUDIT_PLAN.md §7.2](./AUDIT_PLAN.md#72-a4-v4-proceed-decision-authorizes-v4-planning):

### V4-proceed decision: **Partial proceed**

**Rationale:**

1. A0–A3 phases completed with full evidence for all 57 user goals ([§2](#2-phase-reconciliation-a0--a3)).
2. A substantial IDE foundation **works** under clean-profile smoke (18 **WORKS** rows; Git, build/test, core editor, Townhall shell).
3. **Fourteen blocker-severity findings** remain, concentrated in V3 agent platform surfaces (backend binding, trace/memory/usage, tools/permissions), five **BROKEN** LSP runtime failures, and environmental gates (NetCoreDbg, admitted-run prerequisites).
4. **Proceed** is not supported — blocking gaps exist.
5. **Withhold** is not required — gaps are named, evidenced, and sequenced; corrective work can close them without reopening the audit inventory.

**Authorization:**

| Action | Authorized by this decision |
|--------|----------------------------|
| V4 / successor-roadmap **planning** | **No** — not until corrective work packages in [§9](#9-corrective-work-required-before-v4-planning) (minimum 1–7) complete and affected A3 rows re-smoke |
| Corrective implementation | **No** — requires separate authorized phase, refactor, or issue |
| A4 gap report as audit closeout | **Yes** |

V4 or successor-roadmap planning does **not** begin in the session that records this decision ([AUDIT_PLAN.md §7.2](./AUDIT_PLAN.md#72-a4-v4-proceed-decision-authorizes-v4-planning)).

---

## 11. A4 quality gates

| Gate | Status |
|------|--------|
| A2 evidence complete for every accepted goal row | **Pass** — [GOAL_MATRIX.md §17.8](./GOAL_MATRIX.md#178-current-a2-progress) |
| A3 evidence complete for every targeted smoke row | **Pass** — [A3_CLEAN_PROFILE_SMOKE.md](./evidence/A3_CLEAN_PROFILE_SMOKE.md) |
| Every A2/A3 material finding classified by severity | **Pass** — [§5](#5-severity-classified-findings) |
| V4-proceed decision is exactly one of Proceed / Partial proceed / Withhold | **Pass** — **Partial proceed** ([§10](#10-recorded-v4-proceed-decision)) |
| 57 user goals and 5 `A1-XX-*` rows preserved separately | **Pass** — [§1](#1-preserved-inventory-counts-a1) |
| Prior evidence not rewritten | **Pass** — consolidation only |
| Relative links resolve | **Pass** — [§12](#12-verification) |
| `git diff --check` clean | **Pass** — [§12](#12-verification) |

---

## 12. Verification

### 12.1 Relative link validation

Every relative Markdown link in this file was checked to resolve to an existing repository path (anchors not validated). Checked paths include:

- [AUDIT_PLAN.md](./AUDIT_PLAN.md)
- [GOAL_MATRIX.md](./GOAL_MATRIX.md)
- [A1_ACCEPTANCE.md](./A1_ACCEPTANCE.md)
- [evidence/A3_CLEAN_PROFILE_SMOKE.md](./evidence/A3_CLEAN_PROFILE_SMOKE.md)
- All `evidence/A2_*.md` and `evidence/A3_*.md` files cited in [§5](#5-severity-classified-findings)
- [ISSUE-008](../../issues/open/ISSUE-008-agent-response-not-showing.md)

### 12.2 Whitespace

`git diff --check` was run on this file before commit.

---

*Recorded 2026-08-01. A4 gap report and V4-proceed decision (**Partial proceed**); corrective work sequenced in §9; V4 planning not authorized in this session.*
