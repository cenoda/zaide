# A3 Clean-Profile Smoke — Consolidation Closeout

**Audit name:** `v1-v3-product-reality`
**Document class:** A3 cross-journey consolidation closeout (evidence only)
**Evidence date:** 2026-08-01
**Authority:** [AUDIT_PLAN.md](../AUDIT_PLAN.md) §A3 · [GOAL_MATRIX.md](../GOAL_MATRIX.md)

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **A3 phase (smoke execution)** | **Evidence complete** — all 57 accepted user-goal rows are either classified in per-journey A3 evidence or explicitly listed as missing-smoke / out-of-scope below |
| **Product readiness** | **Not claimed** — this closeout records documented clean-profile smoke outcomes and blockers only |
| **A4 gap report / V4 proceed decision** | **Not begun** |
| **Stabilization / corrective implementation** | **Out of scope** |
| **Production code, tracked tests, packages, audit policy** | **Not modified** by this closeout |
| **Prior A2 / A3 evidence files** | **Not rewritten** |
| **Smoke scenarios re-run for this closeout** | **No** — consolidation only |

**Inputs read for this closeout:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- Every published `evidence/A3_*.md` slice and preflight note (21 files; see [§5](#5-evidence-index))

---

## 1. Classification legend

A3 uses the vocabulary defined across slice evidence files. This closeout preserves each row’s **authoritative A3 classification** exactly as recorded in its linked evidence document.

| Classification | Meaning in A3 |
|----------------|---------------|
| **WORKS** | Disposable-profile smoke observed the promised user-observable behavior through production composition |
| **WORKS_WITH_FRICTION** | Core behavior observed with documented gaps, silent recovery, blocked sub-paths, or headless limitations |
| **BROKEN** | User-reachable path exercised and failed against the documented success condition |
| **Missing** | No user-reachable surface or historical path is absent (A2-aligned absence confirmed at smoke layer) |
| **UNWIRED** | Infrastructure or navigation exists but the documented user onboarding / configuration entry point is not reachable |
| **BLOCKED** | Smoke could not exercise the positive path because of environmental or product prerequisites (not a wiring-absence verdict by itself) |
| **UNVERIFIED** | Scenario not executed or not observable under the harness constraints |
| **UNVERIFIED-VIS** | Functional path may be proven; pixel paint, pointer chrome, or desktop-only affordances are not claimed |

Sub-path qualifiers (for example positive send **BLOCKED** without backend binding) are preserved in the **Notes** column; parent row classifications are not upgraded.

---

## 2. Row-level matrix (all 57 accepted user goals)

Journey order follows [GOAL_MATRIX.md](../GOAL_MATRIX.md) §1–§14. **A3 evidence** links to the direct slice or preflight file that owns the row classification.

### 2.1 First launch and settings

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-FL-01](../GOAL_MATRIX.md#1-first-launch-and-settings) | **WORKS_WITH_FRICTION** | [A3_FIRST_LAUNCH_AND_SETTINGS.md](./A3_FIRST_LAUNCH_AND_SETTINGS.md) | Multi-column shell + bottom-panel toggle proven; historical Phase 0 three-panel / right-agent layout not observed |
| [A1-FL-02](../GOAL_MATRIX.md#1-first-launch-and-settings) | **WORKS_WITH_FRICTION** | [A3_FIRST_LAUNCH_AND_SETTINGS.md](./A3_FIRST_LAUNCH_AND_SETTINGS.md) | Dark theme + Navy palette resources proven; no user theme switcher; pixel paint **UNVERIFIED-VIS** |
| [A1-FL-03](../GOAL_MATRIX.md#1-first-launch-and-settings) | **WORKS_WITH_FRICTION** | [A3_FIRST_LAUNCH_AND_SETTINGS.md](./A3_FIRST_LAUNCH_AND_SETTINGS.md) · clause [A3_FIRST_LAUNCH_SETTINGS_RESIDUAL.md](./A3_FIRST_LAUNCH_SETTINGS_RESIDUAL.md) | Persist / corrupt→LKG proven; future-schema clause **WORKS** in residual; silent recovery UI friction |
| [A1-FL-04](../GOAL_MATRIX.md#1-first-launch-and-settings) | **WORKS** | [A3_FIRST_LAUNCH_AND_SETTINGS.md](./A3_FIRST_LAUNCH_AND_SETTINGS.md) · clause [A3_FIRST_LAUNCH_SETTINGS_RESIDUAL.md](./A3_FIRST_LAUNCH_SETTINGS_RESIDUAL.md) | Secret-store boundary + env fallback clause **WORKS** in residual |
| [A1-FL-05](../GOAL_MATRIX.md#1-first-launch-and-settings) | **WORKS** | [A3_FIRST_LAUNCH_AND_SETTINGS.md](./A3_FIRST_LAUNCH_AND_SETTINGS.md) | Editor defaults persisted and live-applied |
| [A1-FL-06](../GOAL_MATRIX.md#1-first-launch-and-settings) | *(missing smoke — out of scope)* | [GOAL_MATRIX.md](../GOAL_MATRIX.md#1-first-launch-and-settings) · [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) | Performance budgets are harness/closeout evidence, not an A3 product-smoke row |

### 2.2 Workspace / project opening

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-WO-01](../GOAL_MATRIX.md#2-workspace--project-opening) | **WORKS_WITH_FRICTION** | [A3_WORKSPACE_AND_PROJECT_OPENING.md](./A3_WORKSPACE_AND_PROJECT_OPENING.md) | Tree, ignore rules, hidden toggle, create file/folder proven; native picker **UNVERIFIED-VIS**; file-tree failure not projected to shell status |
| [A1-WO-02](../GOAL_MATRIX.md#2-workspace--project-opening) | **WORKS_WITH_FRICTION** | [A3_WORKSPACE_AND_PROJECT_OPENING.md](./A3_WORKSPACE_AND_PROJECT_OPENING.md) | No-project / single-project / ambiguous truth proven; ambiguous labeled `"Project error"`; multi-project picker **UNWIRED** |
| [A1-WO-03](../GOAL_MATRIX.md#2-workspace--project-opening) | **WORKS_WITH_FRICTION** | [A3_WORKSPACE_AND_PROJECT_OPENING.md](./A3_WORKSPACE_AND_PROJECT_OPENING.md) | Open/close refreshes project context and Source Control; SC refresh coupled to host `RootPath` not direct workspace event |

### 2.3 File navigation and editing

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-FN-01](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **WORKS_WITH_FRICTION** | [A3_FILE_NAVIGATION_AND_EDITING_CORE.md](./A3_FILE_NAVIGATION_AND_EDITING_CORE.md) | Open/edit/save/dirty proven; TextMate syntax paint **UNVERIFIED-VIS** |
| [A1-FN-02](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **WORKS_WITH_FRICTION** | [A3_FILE_NAVIGATION_AND_EDITING_CORE.md](./A3_FILE_NAVIGATION_AND_EDITING_CORE.md) | Splitter 180–**320** px; copy-path Interaction proven; live drag / OS clipboard **UNVERIFIED** |
| [A1-FN-03](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **WORKS** | [A3_FILE_NAVIGATION_AND_EDITING_CORE.md](./A3_FILE_NAVIGATION_AND_EDITING_CORE.md) | Find/replace/wrap/undo-group proven |
| [A1-FN-04](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **WORKS** | [A3_FILE_NAVIGATION_AND_EDITING_CORE.md](./A3_FILE_NAVIGATION_AND_EDITING_CORE.md) | Fold toggle/all/unfold; no fold leak across tabs |
| [A1-FN-05](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **WORKS_WITH_FRICTION** | [A3_FILE_NAVIGATION_AND_EDITING_CORE.md](./A3_FILE_NAVIGATION_AND_EDITING_CORE.md) | Tab lifecycle + dirty Cancel/Discard/Save; pointer tab-drag **UNVERIFIED** |
| [A1-FN-06](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **WORKS** | [A3_FILE_NAVIGATION_AND_EDITING_CORE.md](./A3_FILE_NAVIGATION_AND_EDITING_CORE.md) | Status bar document/caret/selection/search/save projections |
| [A1-FN-08](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **WORKS** | [A3_LANGUAGE_INTELLIGENCE.md](./A3_LANGUAGE_INTELLIGENCE.md) | Diagnostics → Problems + navigation with `csharp-ls` (supersedes preflight **BLOCKED** in [A3_LANGUAGE_INTELLIGENCE_PREFLIGHT.md](./A3_LANGUAGE_INTELLIGENCE_PREFLIGHT.md)) |
| [A1-FN-09](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **BROKEN** | [A3_LANGUAGE_INTELLIGENCE.md](./A3_LANGUAGE_INTELLIGENCE.md) | Completion fails: Avalonia thread-affinity off UI thread |
| [A1-FN-10](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **BROKEN** | [A3_LANGUAGE_INTELLIGENCE.md](./A3_LANGUAGE_INTELLIGENCE.md) | Caret-dwell hover stuck Loading (pointer hover not claimed) |
| [A1-FN-11](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **BROKEN** | [A3_LANGUAGE_INTELLIGENCE.md](./A3_LANGUAGE_INTELLIGENCE.md) | Go to Definition stuck Loading; empty feedback path works |
| [A1-FN-12](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **BROKEN** | [A3_LANGUAGE_INTELLIGENCE.md](./A3_LANGUAGE_INTELLIGENCE.md) | Document symbols Failed / zero symbols |
| [A1-FN-13](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **BROKEN** | [A3_LANGUAGE_INTELLIGENCE.md](./A3_LANGUAGE_INTELLIGENCE.md) | Workspace symbols Failed |
| [A1-FN-14](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **WORKS** | [A3_LANGUAGE_INTELLIGENCE.md](./A3_LANGUAGE_INTELLIGENCE.md) | Format Document atomic edit + one undo |
| [A1-FN-15](../GOAL_MATRIX.md#3-file-navigation-and-editing) | **WORKS_WITH_FRICTION** | [A3_LANGUAGE_INTELLIGENCE.md](./A3_LANGUAGE_INTELLIGENCE.md) | Format on Save reformats disk; save path swallows failures; buffer can lag disk |

### 2.4 Search and command discovery

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-SC-01](../GOAL_MATRIX.md#4-search-and-command-discovery) | **WORKS_WITH_FRICTION** | [A3_SEARCH_AND_COMMAND_DISCOVERY.md](./A3_SEARCH_AND_COMMAND_DISCOVERY.md) | Overrides/conflicts via settings file proven; no keybindings editor; conflicts log-only |
| [A1-SC-02](../GOAL_MATRIX.md#4-search-and-command-discovery) | **WORKS_WITH_FRICTION** | [A3_SEARCH_AND_COMMAND_DISCOVERY.md](./A3_SEARCH_AND_COMMAND_DISCOVERY.md) | Palette filter/execute proven; pointer row reselect gap; focus restore **UNVERIFIED-VIS** |
| [A1-SC-03](../GOAL_MATRIX.md#4-search-and-command-discovery) | **WORKS** | [A3_SEARCH_AND_COMMAND_DISCOVERY.md](./A3_SEARCH_AND_COMMAND_DISCOVERY.md) | Phase 9 command IDs palette-reachable when unbound |

### 2.5 Build / run / test

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-BR-01](../GOAL_MATRIX.md#5-build--run--test) | **WORKS_WITH_FRICTION** | [A3_BUILD_RUN_AND_TEST.md](./A3_BUILD_RUN_AND_TEST.md) | Build/run/test/cancel/gating proven; Output list template NRE mitigated in harness only; scroll paint **UNVERIFIED-VIS** |
| [A1-BR-02](../GOAL_MATRIX.md#5-build--run--test) | **WORKS** | [A3_BUILD_RUN_AND_TEST.md](./A3_BUILD_RUN_AND_TEST.md) | Build diagnostics → Problems + navigation |
| [A1-BR-03](../GOAL_MATRIX.md#5-build--run--test) | **WORKS** | [A3_BUILD_RUN_AND_TEST.md](./A3_BUILD_RUN_AND_TEST.md) | Test Results summary/cases/cancel |
| [A1-BR-04](../GOAL_MATRIX.md#5-build--run--test) | **WORKS** | [A3_BUILD_RUN_AND_TEST.md](./A3_BUILD_RUN_AND_TEST.md) | Output/Test Results/Terminal modes mutually exclusive; PTY retained |

### 2.6 Debugging and output

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-DB-01](../GOAL_MATRIX.md#6-debugging-and-output) | **BLOCKED** | [A3_DEBUGGING_PREFLIGHT.md](./A3_DEBUGGING_PREFLIGHT.md) | Positive breakpoint/step/stack/variables path requires **netcoredbg**; negative paths **WORKS**; gutter/panel paint **UNVERIFIED-VIS** |

### 2.7 Terminal

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-TR-01](../GOAL_MATRIX.md#7-terminal) | **WORKS_WITH_FRICTION** | [A3_TERMINAL.md](./A3_TERMINAL.md) | PTY, alt-screen, scrollback, search, restart proven; cell paint **UNVERIFIED-VIS**; Linux-only |
| [A1-TR-02](../GOAL_MATRIX.md#7-terminal) | **WORKS_WITH_FRICTION** | [A3_TERMINAL.md](./A3_TERMINAL.md) | Multi-tab PTY isolation proven; static `"Terminal"` titles; tab-strip paint **UNVERIFIED-VIS** |

### 2.8 Git workflow

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-GT-01](../GOAL_MATRIX.md#8-git-workflow) | **WORKS** | [A3_GIT_WORKFLOW.md](./A3_GIT_WORKFLOW.md) | Repo discovery + live status; panel/icon paint **UNVERIFIED-VIS** |
| [A1-GT-02](../GOAL_MATRIX.md#8-git-workflow) | **WORKS** | [A3_GIT_WORKFLOW.md](./A3_GIT_WORKFLOW.md) | Unified diff + binary notice; diff coloring **UNVERIFIED-VIS** |
| [A1-GT-03](../GOAL_MATRIX.md#8-git-workflow) | **WORKS** | [A3_GIT_WORKFLOW.md](./A3_GIT_WORKFLOW.md) | Stage/unstage/commit validation |
| [A1-GT-04](../GOAL_MATRIX.md#8-git-workflow) | **WORKS** | [A3_GIT_WORKFLOW.md](./A3_GIT_WORKFLOW.md) | Branch + detached HEAD SHA in status bar |

### 2.9 Townhall / conversations

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-TH-01](../GOAL_MATRIX.md#9-townhall--conversations) | **WORKS** | [A3_TOWNHALL_AND_CONVERSATIONS.md](./A3_TOWNHALL_AND_CONVERSATIONS.md) | Channels, send, switch, All/Chat/Activity filters |
| [A1-TH-02](../GOAL_MATRIX.md#9-townhall--conversations) | **WORKS** | [A3_TOWNHALL_AND_CONVERSATIONS.md](./A3_TOWNHALL_AND_CONVERSATIONS.md) | Private DM find-or-create; no public mirror; unbound send not fabricated |
| [A1-TH-04](../GOAL_MATRIX.md#9-townhall--conversations) | **WORKS** | [A3_TOWNHALL_AND_CONVERSATIONS.md](./A3_TOWNHALL_AND_CONVERSATIONS.md) | Agent Panel chrome absent; Townhall sole DM entry; shell proportions **UNVERIFIED-VIS** |
| [A1-TH-05](../GOAL_MATRIX.md#9-townhall--conversations) | **WORKS_WITH_FRICTION** | [A3_TOWNHALL_AND_CONVERSATIONS.md](./A3_TOWNHALL_AND_CONVERSATIONS.md) | Routing failures on source proven; admitted routed success **BLOCKED** without backend |

### 2.10 Agent creation and backend onboarding

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-AC-01](../GOAL_MATRIX.md#10-agent-creation-and-backend-onboarding) | **UNWIRED** | [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) | Historical Agent Panel create path absent; DM navigation ≠ agent creation |
| [A1-AC-02](../GOAL_MATRIX.md#10-agent-creation-and-backend-onboarding) | **WORKS_WITH_FRICTION** | [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) | Read-only Unbound status; **no** user bind/configure/unbind UI; positive bind **BLOCKED** |

### 2.11 Agent send / response / failure feedback

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-AS-01](../GOAL_MATRIX.md#11-agent-send--response--failure-feedback) | **Missing** | [A3_AGENT_SEND.md](./A3_AGENT_SEND.md) | Historical Agent Panel send; **not re-executed** (A2 **Missing** preserved) |
| [A1-AS-02](../GOAL_MATRIX.md#11-agent-send--response--failure-feedback) | **WORKS_WITH_FRICTION** | [A3_AGENT_SEND.md](./A3_AGENT_SEND.md) | Send reachable; unbound pre-admission rejection honest; no actionable failure in chat; positive response **BLOCKED** |

### 2.12 Tools, permissions, and workspace mutation

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-TP-01](../GOAL_MATRIX.md#12-tools-permissions-and-workspace-mutation) | **BLOCKED** | [A3_TOOLS_PERMISSIONS_PREFLIGHT.md](./A3_TOOLS_PERMISSIONS_PREFLIGHT.md) | No user-reachable mediated action trigger on unbound profile |
| [A1-TP-02](../GOAL_MATRIX.md#12-tools-permissions-and-workspace-mutation) | **BLOCKED** | [A3_TOOLS_PERMISSIONS_PREFLIGHT.md](./A3_TOOLS_PERMISSIONS_PREFLIGHT.md) | Permission management **UNWIRED**; destructive UX path absent |
| [A1-TP-03](../GOAL_MATRIX.md#12-tools-permissions-and-workspace-mutation) | **BLOCKED** | [A3_TOOLS_PERMISSIONS_PREFLIGHT.md](./A3_TOOLS_PERMISSIONS_PREFLIGHT.md) | Multi-file agent edit / rollback **UNWIRED** |

### 2.13 Multi-agent routing

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-MR-01](../GOAL_MATRIX.md#13-multi-agent-routing) | **Missing** | [A3_MULTI_AGENT_ROUTING.md](./A3_MULTI_AGENT_ROUTING.md) | Panel-bound Phase 6 entry absent; non-visual host only |
| [A1-MR-03](../GOAL_MATRIX.md#13-multi-agent-routing) | **WORKS_WITH_FRICTION** | [A3_MULTI_AGENT_ROUTING.md](./A3_MULTI_AGENT_ROUTING.md) | Catalog routing + negative failures; channel bypass; admitted route **BLOCKED** without bind |

### 2.14 Trace, context, memory, persistence, restart, and recovery

| id | A3 classification | A3 evidence | Notes |
|----|---------------------|-------------|-------|
| [A1-TC-01](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery) | **WORKS_WITH_FRICTION** | [A3_CONTEXT_POLICY.md](./A3_CONTEXT_POLICY.md) | Townhall context selector + levels; in-memory overrides; no settings entry; Off manifest on send **BLOCKED** without backend |
| [A1-TC-02](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery) | **Missing** | [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) | Trace commands/View **UNWIRED**; no user inspect path |
| [A1-TC-03](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery) | **Missing** | [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) | Memory management commands/View **UNWIRED** |
| [A1-TC-04](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery) | **WORKS_WITH_FRICTION** | [A3_RESTART_AND_RECOVERY.md](./A3_RESTART_AND_RECOVERY.md) | Conversation snapshot restore; debounced-draft gap on fast exit; silent recovery |
| [A1-TC-05](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery) | **BLOCKED** | [A3_RESTART_AND_RECOVERY.md](./A3_RESTART_AND_RECOVERY.md) | Positive interrupted-run path **BLOCKED** without admitted run/backend; negative no-resume evidence recorded |
| [A1-TC-08](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery) | **Missing** | [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) | Usage/cost commands/View **UNWIRED** |
| [A1-TC-09](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery) | **BLOCKED** | [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) | No end UI/command; no admitted run to terminate on unbound profile |

---

## 3. Classification summary

| A3 classification | Count | Row ids |
|-------------------|------:|---------|
| **WORKS** | 18 | FL-04, FL-05, FN-03, FN-04, FN-06, FN-08, FN-14, SC-03, BR-02, BR-03, BR-04, GT-01…GT-04, TH-01, TH-02, TH-04 |
| **WORKS_WITH_FRICTION** | 22 | FL-01, FL-02, FL-03, WO-01…WO-03, FN-01, FN-02, FN-05, FN-15, SC-01, SC-02, BR-01, TR-01, TR-02, TH-05, AC-02, AS-02, MR-03, TC-01, TC-04 |
| **BROKEN** | 5 | FN-09, FN-10, FN-11, FN-12, FN-13 |
| **Missing** | 5 | AS-01, MR-01, TC-02, TC-03, TC-08 |
| **UNWIRED** | 1 | AC-01 |
| **BLOCKED** | 5 | DB-01, TP-01…TP-03, TC-05, TC-09 |
| **Missing smoke / out of scope** | 1 | FL-06 |

**Total user-goal rows:** 57 (matrix) + 1 explicit out-of-scope row (FL-06) = 58 line items covering all accepted goals.

---

## 4. Missing-smoke and out-of-scope rows

| id | Disposition | Authority |
|----|-----------|-----------|
| **A1-FL-06** | **Out of A3 scope** — performance budgets and release-hardening measurements are harness/closeout evidence, not clean-profile product smoke | [GOAL_MATRIX.md](../GOAL_MATRIX.md) `planned_a3_scenario`; [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) |
| **A1-AS-01** | **Historical missing smoke** — Phase 5 Agent Panel send path retired; classified **Missing** in A2; **not re-executed** in A3 | [A3_AGENT_SEND.md](./A3_AGENT_SEND.md) |

`A1-XX-*` rows ([GOAL_MATRIX.md §15](../GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior)) are not user-goal smoke targets. Scoped A2 dispositions (for example `A1-XX-04` NetCoreDbg host constraint, `A1-XX-05` workspace isolation) inform blockers below but do not receive A3 row classifications.

---

## 5. Evidence index

| A3 evidence file | Rows covered |
|------------------|--------------|
| [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) | H0 automation charter (not a row classifier) |
| [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) | H1 runner POC (not a row classifier) |
| [A3_FIRST_LAUNCH_AND_SETTINGS.md](./A3_FIRST_LAUNCH_AND_SETTINGS.md) | FL-01…FL-05 |
| [A3_FIRST_LAUNCH_SETTINGS_RESIDUAL.md](./A3_FIRST_LAUNCH_SETTINGS_RESIDUAL.md) | FL-03 / FL-04 supplemental clauses |
| [A3_WORKSPACE_AND_PROJECT_OPENING.md](./A3_WORKSPACE_AND_PROJECT_OPENING.md) | WO-01…WO-03 |
| [A3_FILE_NAVIGATION_AND_EDITING_CORE.md](./A3_FILE_NAVIGATION_AND_EDITING_CORE.md) | FN-01…FN-06 |
| [A3_LANGUAGE_INTELLIGENCE_PREFLIGHT.md](./A3_LANGUAGE_INTELLIGENCE_PREFLIGHT.md) | FN-08…FN-15 preflight (superseded for FN-08/14/15 by positive slice) |
| [A3_LANGUAGE_INTELLIGENCE.md](./A3_LANGUAGE_INTELLIGENCE.md) | FN-08…FN-15 (authoritative positive classifications) |
| [A3_SEARCH_AND_COMMAND_DISCOVERY.md](./A3_SEARCH_AND_COMMAND_DISCOVERY.md) | SC-01…SC-03 |
| [A3_BUILD_RUN_AND_TEST.md](./A3_BUILD_RUN_AND_TEST.md) | BR-01…BR-04 |
| [A3_DEBUGGING_PREFLIGHT.md](./A3_DEBUGGING_PREFLIGHT.md) | DB-01 |
| [A3_TERMINAL.md](./A3_TERMINAL.md) | TR-01, TR-02 |
| [A3_GIT_WORKFLOW.md](./A3_GIT_WORKFLOW.md) | GT-01…GT-04 |
| [A3_TOWNHALL_AND_CONVERSATIONS.md](./A3_TOWNHALL_AND_CONVERSATIONS.md) | TH-01, TH-02, TH-04, TH-05 |
| [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) | AC-01, AC-02 |
| [A3_AGENT_SEND.md](./A3_AGENT_SEND.md) | AS-01 (historical), AS-02 |
| [A3_TOOLS_PERMISSIONS_PREFLIGHT.md](./A3_TOOLS_PERMISSIONS_PREFLIGHT.md) | TP-01…TP-03 |
| [A3_MULTI_AGENT_ROUTING.md](./A3_MULTI_AGENT_ROUTING.md) | MR-01, MR-03 |
| [A3_CONTEXT_POLICY.md](./A3_CONTEXT_POLICY.md) | TC-01 |
| [A3_RESTART_AND_RECOVERY.md](./A3_RESTART_AND_RECOVERY.md) | TC-04, TC-05 |
| [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) | TC-02, TC-03, TC-08, TC-09 |

---

## 6. Environmental and product blockers

These blockers explain **BLOCKED**, **Missing**, and **WORKS_WITH_FRICTION** rows. They do **not** authorize corrective work in this session.

| Blocker | Affected rows / areas | Evidence |
|---------|----------------------|----------|
| **Missing NetCoreDbg** (`ZAIDE_NETCOREDBG_PATH` unset; `netcoredbg` not on `PATH`) | `A1-DB-01` positive debug smoke **BLOCKED**; negative adapter/build/target paths exercised | [A3_DEBUGGING_PREFLIGHT.md](./A3_DEBUGGING_PREFLIGHT.md) |
| **Absent backend-binding UI** (no user Native Harness / ACP bind, configure, or persist workflow) | `A1-AC-02`, `A1-AS-02`, `A1-TH-05`, `A1-MR-03`, `A1-TC-01` send/manifest sub-paths, `A1-TP-*` mediation | [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md), [A3_AGENT_SEND.md](./A3_AGENT_SEND.md) |
| **Blocked mediated actions / permissions UX** | `A1-TP-01`…`A1-TP-03` **BLOCKED** on clean unbound profile | [A3_TOOLS_PERMISSIONS_PREFLIGHT.md](./A3_TOOLS_PERMISSIONS_PREFLIGHT.md) |
| **Missing trace / memory / usage surfaces** | `A1-TC-02`, `A1-TC-03`, `A1-TC-08` **Missing** / **UNWIRED** | [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) |
| **Blocked interrupted-run / explicit termination execution** | `A1-TC-05`, `A1-TC-09` **BLOCKED** without admitted backend run | [A3_RESTART_AND_RECOVERY.md](./A3_RESTART_AND_RECOVERY.md), [A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md](./A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md) |
| **LSP positive-path regressions (headless product runtime)** | `A1-FN-09`…`A1-FN-13` **BROKEN** | [A3_LANGUAGE_INTELLIGENCE.md](./A3_LANGUAGE_INTELLIGENCE.md) |
| **Headless visual limitations** | Widespread **UNVERIFIED-VIS** sub-claims (theme paint, diff coloring, terminal cells, etc.) | Per-slice evidence files |

---

## 7. A3 closeout statement

1. **A3 evidence is complete** for the v1–v3 product-reality audit’s clean-profile smoke phase: every accepted `A1-*-NN` row has a recorded A3 classification or an explicit missing-smoke / out-of-scope entry, with per-journey evidence linked above.
2. **Product readiness is not claimed.** Documented smoke results include substantial **BLOCKED**, **Missing**, **UNWIRED**, and **BROKEN** outcomes plus environmental prerequisites that were not manufactured for expedience.
3. **A4** (gap report and V4 proceed decision), **stabilization**, **V4 / successor-roadmap planning**, and **corrective implementation** remain **out of scope** and **not authorized** by this closeout.
4. No smoke scenarios were re-run to produce this document; classifications are consolidated from existing `A3_*.md` evidence only.

---

## 8. Verification

### 8.1 Relative link validation

Every relative Markdown link in this file was checked to resolve to an existing repository path (anchors not validated).

### 8.2 Whitespace

`git diff --check` was run on this file before commit.

---

*Recorded 2026-08-01. A3 clean-profile smoke consolidation closeout; evidence complete with blockers; A4 not begun.*
