# V1–V3 Product Reality Audit — Plan

**Audit name:** `v1-v3-product-reality`
**Owner folder:** `docs/audits/v1-v3-product-reality/`
**Audit phases:** A0 (Baseline lock) → A1 (Goal inventory, A1-acceptance gate) →
A2 (Wiring audit) → A3 (Clean-profile smoke) → A4 (Gap report and V4 proceed decision)
**Current phase:** **A1 accepted; A2 complete.** A1 is accepted
([A1_ACCEPTANCE.md](./A1_ACCEPTANCE.md)). A2 as a whole is **complete**
for all accepted user-goal rows. Completed A2 slices:

- **`A2_AGENT_SEND`** — complete and published
  ([evidence/A2_AGENT_SEND.md](./evidence/A2_AGENT_SEND.md)):
  - `A1-AS-01`: **Missing**
  - `A1-AS-02`: **Wired-with-gap**
- **`A2_MULTI_AGENT_ROUTING`** — complete and published
  ([evidence/A2_MULTI_AGENT_ROUTING.md](./evidence/A2_MULTI_AGENT_ROUTING.md)):
  - `A1-MR-01`: **Missing**
  - `A1-MR-03`: **Wired-with-gap**
  - `A1-XX-02`: confirmed absent (scoped disposition only; not a
    user-goal verdict)
- **`A2_TRACE_MEMORY_USAGE_TERMINATION`** — complete and published
  ([evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md](./evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md)):
  - `A1-TC-02`: **Missing**
  - `A1-TC-03`: **Missing**
  - `A1-TC-08`: **Missing**
  - `A1-TC-09`: **Missing**
  - `A1-XX-03`: scoped disposition only (not a user-goal verdict).
    Production appends memory-influence evidence during session
    context assembly; production does not expose user-managed
    lifecycle-memory creation or management UI; trace and usage
    producers and explicit termination UI remain absent.
- **`A2_RESTART_RECOVERY_AND_CONTEXT`** — complete and published
  ([evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md](./evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md)):
  - `A1-TC-01`: **Wired-with-gap** — Townhall direct-conversation
    context selector is user-reachable; no settings entry or
    configurable application default; overrides are in-memory and
    lost on restart; Off produces a zero-item/zero-token manifest
    while policy metadata may still reach the backend
  - `A1-TC-04`: **Wired-with-gap** — conversation snapshot
    load/save/restore is production-composed; persistence failures
    and recovery outcomes are not shown to the user; Zaide’s
    explicit shutdown sequence does not dispose or flush
    `ConversationPersistenceService`; possible later
    framework/root-provider disposal remains unproven
  - `A1-TC-05`: **Wired-with-gap** — startup calls `Reconcile`, not
    `Resume` (no automatic backend re-invocation); stored
    checkpoints may be `Recoverable`; on a normal cold start the
    unpersisted binding store is empty so revalidation returns
    `Indeterminate`; reconciled classification and required re-send
    action are not projected to Townhall
   - `A1-XX-05`: scoped disposition only (not a user-goal verdict).
     Conversation persistence is application/user-config scoped; no
     multi-window synchronization; Phase 21 durable keys are
     path-derived but current production uses process CWD, not a
     proven opened-workspace-root provider
- **`A2_TOOLS_PERMISSIONS`** — complete and published
  ([evidence/A2_TOOLS_PERMISSIONS.md](./evidence/A2_TOOLS_PERMISSIONS.md)):
  - `A1-TP-01`: **Wired-with-gap** — run-scoped Phase 17 broker paths
    exist for tool-capable Native Harness and ACP backends; default
    product has no user-reachable backend-binding workflow;
    `AgentActionFactPayload` and `AgentActionAuditRecord` contain no
    explicit initiating/target actor IDs; several pre-admission and
    early broker returns remain backend-visible only; Townhall
    projects only emitted `ActionResultReported`; ACP lacks
    delete/command mediation
  - `A1-TP-02`: **Wired-with-gap** — five-kind permission model,
    exact-request decisions, expiry, and lifecycle revocation are
    partially wired; dedicated network, Git, secrets, destructive,
    and memory permission dimensions are absent; no selectable
    approval scope or user-reachable permission management/revocation
    UI; ACP `session/request_permission` is automatic and
    reject-preferring (`reject_once` when present, otherwise the
    first supplied option, which may be permissive); this ACP
    protocol handling is not user-reachable, not guaranteed
    fail-closed, and is separate from Phase 17 broker authorization
  - `A1-TP-03`: **Wired-with-gap** — base-revision checks,
    workspace-generation invalidation, and single non-terminal action
    admission are wired; `TryConsume()` is the final authorization
    step, not the final safety check; pre-consume stale detection
    preserves a `Published` decision; post-consume validation can
    fail after the decision becomes `Consumed` without applying the
    effect; multi-file transactions, agent change sets, rollback
    UI/commands, and multi-file partial-apply cancellation semantics
    are absent
- **`A2_AGENT_CREATION_AND_BACKEND_ONBOARDING`** — complete and
  published
  ([evidence/A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./evidence/A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)):
  - `A1-AC-01`: **Missing** — the historical Phase 5 Agent Panel
    creation path is retired; production has no user-reachable
    create, rename, remove, or configure-agent workflow
  - `A1-AC-02`: **Wired-with-gap** — Native Harness and ACP are
    independently composed sibling backends with in-memory
    per-actor binding and pull-based status projection; production
    has no user bind/configure/unbind/persist workflow; ACP
    selection-state authentication is not bridged to the real ACP
    `authenticate` protocol call; negotiated auth methods and
    capability changes are not user-projected
  - `A1-XX-01`: gap confirmed (scoped disposition only; not a
    user-goal verdict). Binding infrastructure and status visibility
    exist, but the supported user onboarding entry point remains
    absent
- **`A2_TOWNHALL_AND_CONVERSATIONS`** — complete and published
  ([evidence/A2_TOWNHALL_AND_CONVERSATIONS.md](./evidence/A2_TOWNHALL_AND_CONVERSATIONS.md)):
  - `A1-TH-01`: **Wired-with-gap** — center-shell channels, activity,
    and All/Chat/Activity filters are user-reachable; presentation
    kinds and filter scope remain limited, and custom channels are
    not user-creatable
  - `A1-TH-02`: **Wired** — People → Zaide Agent opens one private
    direct conversation per unordered pair, with persisted selection,
    drafts, unread state, and read state
  - `A1-TH-04`: **Wired** — Agent Panel chrome is retired and
    Townhall is the sole user-facing direct-conversation re-entry
  - `A1-TH-05`: **Wired-with-gap** — routing failures appear in the
    source conversation, while admitted execution and terminal
    outcomes appear in the target direct conversation; successful
    routed flow is not shown in the source and pre-admission
    rejection remains invisible
- **`A2_FIRST_LAUNCH_AND_SETTINGS`** — complete and published
  ([evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md](./evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md)):
  - `A1-FL-01`: **Wired-with-gap** — current multi-column shell and
    bottom-panel toggles are user-reachable, but the historical
    Phase 0 three-panel/right-agent layout no longer describes it
  - `A1-FL-02`: **Wired-with-gap** — Dark, Fluent, Semi.Avalonia,
    and the Navy palette are composed; historical “Ayaka Violet”
    wording and a user theme switcher are absent
  - `A1-FL-03`: **Wired-with-gap** — schema-v3 load/save/migration
    and status-bar settings UI are production-wired, but load/write
    recovery and disk-write failures are not surfaced to the user
  - `A1-FL-04`: **Wired-with-gap** — API keys use a separate secret
    store with environment fallback, while on-disk permission and
    plaintext-absence behavior remain A3 verification
  - `A1-FL-05`: **Wired** — editor and terminal defaults are
    configurable, persisted, and live-applied
  - `A1-FL-06`: **Wired-with-gap** — settings recovery is
    product-wired, while performance budgets remain harness/closeout
    evidence rather than a product surface
- **`A2_WORKSPACE_AND_PROJECT_OPENING`** — complete and published
  ([evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md](./evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md)):
  - `A1-WO-01`: **Wired-with-gap** — folder open, tree, ignore rules,
    hidden-file toggle, and new file/folder paths are user-reachable,
    but file-tree failure messages are not projected to the UI
  - `A1-WO-02`: **Wired-with-gap** — one production project-context
    service is shared by status, LSP, Build, and Debug; ambiguous
    multi-project selection has no user-reachable picker and is
    mislabeled as “Project error”
  - `A1-WO-03`: **Wired-with-gap** — folder open/close updates
    `WorkspacePath`, emits `WorkspaceFolderChanged`, refreshes
    project context and Source Control; Source Control refresh is
    coupled to the RootPath host path rather than the workspace event
- **`A2_FILE_NAVIGATION_AND_EDITING`** — complete and published
  ([evidence/A2_FILE_NAVIGATION_AND_EDITING.md](./evidence/A2_FILE_NAVIGATION_AND_EDITING.md)):
  - `A1-FN-01`, `A1-FN-03` through `A1-FN-06`, and `A1-FN-09` through
    `A1-FN-14`: **Wired**
  - `A1-FN-02`: **Wired-with-gap** — tree-to-editor opening and
    copy-path actions are user-reachable, but the left splitter is
    constrained to 180–320px rather than the claimed 180–500px
  - `A1-FN-08`: **Wired-with-gap** — the Problems projection and
    navigation path are wired, but only for open tracked documents;
    cold success also requires an eligible project context and an
    external `csharp-ls` binary
  - `A1-FN-10`: **Wired-with-gap** — the hover surface is triggered by
    caret dwell, not pointer hover
  - `A1-FN-15`: **Wired-with-gap** — the persisted default-off setting
    and save path are wired, but save suppresses formatting failures
    and does not share the interactive formatting apply path
- **`A2_SEARCH_AND_COMMAND_DISCOVERY`** — complete and published
  ([evidence/A2_SEARCH_AND_COMMAND_DISCOVERY.md](./evidence/A2_SEARCH_AND_COMMAND_DISCOVERY.md)):
  - `A1-SC-01`: **Wired-with-gap** — command registry, defaults,
    settings overrides, unbind, conflict logging, and live keybinding
    materialization are wired; no user-reachable keybindings editor and
    conflicts remain log-only
  - `A1-SC-02`: **Wired-with-gap** — palette open, filtering, ordering,
    availability, execution, and focus restoration are wired; pointer
    clicking a row does not reselect that row before execution
  - `A1-SC-03`: **Wired** — Phase 9 find/replace, folding, and tab
    commands are registered and remain palette-reachable when unbound
- **`A2_BUILD_RUN_AND_TEST`** — complete and published
  ([evidence/A2_BUILD_RUN_AND_TEST.md](./evidence/A2_BUILD_RUN_AND_TEST.md)):
  - `A1-BR-01`: **Wired** — project target selection, locked build/run/test
    profiles, cancellation, and one-at-a-time operation gating are wired
  - `A1-BR-02`: **Wired** — build diagnostics parse into Problems while
    preserving LSP diagnostics and generation-safe navigation
  - `A1-BR-03`: **Wired** — test parsing, summary/status/case projection,
    dedicated Test Results surface, and shared cancellation are wired
  - `A1-BR-04`: **Wired** — redirected project output is separate from the
    PTY terminal and bottom-panel modes remain mutually exclusive
- **`A2_DEBUGGING_AND_OUTPUT`** — complete and published
  ([evidence/A2_DEBUGGING_AND_OUTPUT.md](./evidence/A2_DEBUGGING_AND_OUTPUT.md)):
  - `A1-DB-01`: **Wired-with-gap** — DAP lifecycle, C# launch handoff,
    breakpoints, execution controls, stack/scopes/variables, current
    location, debug console, panel composition, and recovery are wired;
    NetCoreDbg availability, launch configurability, and several visual
    or interactive capabilities remain gaps
  - `A1-XX-04`: scoped disposition only — DAP validation requires a
    disposable host that supplies NetCoreDbg through `ZAIDE_NETCOREDBG_PATH`
    or `PATH`; this is not a user-goal verdict
- **`A2_TERMINAL`** — complete and published
  ([evidence/A2_TERMINAL.md](./evidence/A2_TERMINAL.md)):
  - `A1-TR-01`: **Wired-with-gap** — bottom-panel terminal, Linux PTY,
    ANSI/CSI parsing, alternate screen, selection, scrollback, search,
    and restart lifecycle are wired; terminal-mode forcing, non-Linux
    backends, visible scroll affordance, and live TUI behavior remain gaps
  - `A1-TR-02`: **Wired-with-gap** — per-tab service ownership, panel
    caching, tab strip, and disposal are wired; static tab titles and
    runtime PTY isolation remain gaps
- **`A2_GIT`** — complete and published
  ([evidence/A2_GIT.md](./evidence/A2_GIT.md)):
  - `A1-GT-01`: **Wired** — repository discovery, live status, truthful
    non-repository/failure labels, and refresh reachability are wired
  - `A1-GT-02`: **Wired** — staged/unstaged unified diffs, binary notices,
    read-only diff tabs, and refresh-safe selection are wired
  - `A1-GT-03`: **Wired** — stage/unstage/stage-all, local commit,
    validation guards, truthful identity/errors, and refresh are wired
  - `A1-GT-04`: **Wired** — branch and detached-HEAD SHA display are
    reflected truthfully in the status bar

**A2 wiring audit is complete.** A3 and cross-slice A2 consolidation are
explicitly not begun. A4 does not begin until A3 evidence is complete.
V4 or successor-roadmap planning does not begin until A4 produces an
explicit proceed decision.
**Proceed authority:** A2 does not begin in the session that recorded
the A1-acceptance proceed decision; A2 begins in a new session. A4
does not begin until A2 and A3 evidence is complete.

---

## 1. Audit Purpose

Roadmap V3 closed its implementation sequence on 2026-07-29 and explicitly
withheld product-readiness acceptance until a cross-version audit compares, for
each user journey:

1. the documented implementation goal;
2. the live code and production wiring;
3. clean-profile user-observable behavior, including discoverability, failure
   feedback, persistence, recovery, and workflow cost.

This audit performs that comparison for V1, V2, and V3 together. It is the gate
that releases (or withholds) authorization for V4 or any successor-roadmap
planning.

This audit is **not** a phase, **not** a refactor, and **not** a regular
review. It does not own feature work, refactor work, or production code edits.
Its job is to inventory, inspect, and report.

---

## 2. Audit Phase Decomposition

| Phase | Name | Inputs | Output | Allowed side effects |
|-------|------|--------|--------|----------------------|
| A0 | Baseline lock | Repository state, `AGENTS.md`, `docs-rules.md`, roadmaps | This `AUDIT_PLAN.md` (initial) | Docs-only: this file and the `docs/audits/` folder registration |
| A1 | Goal inventory (closes with the **A1-acceptance gate**) | V1/V2/V3 roadmaps, `docs/phases/README.md`, every `IMPLEMENTATION_PLAN.md` and `TOFIX.md` for completed phases, `docs/architecture/OVERVIEW.md`, `README.md`, `docs/issues/INDEX.md`, `docs/deferred/INDEX.md` | `GOAL_MATRIX.md` only | No production-code or production-test edits |
| A2 | Wiring audit | Accepted `GOAL_MATRIX.md` + `src/`, `tests/`, DI composition, command registry | `evidence/A2_WIRING_AUDIT.md` plus per-journey evidence files | Read-only: no production-code edits; may add evidence files |
| A3 | Clean-profile smoke | Accepted `GOAL_MATRIX.md` + A2 evidence | `evidence/A3_CLEAN_PROFILE_SMOKE.md` plus per-journey evidence files | Test/runtime only against a disposable isolated profile; never the real user profile or store |
| A4 | Gap report and **V4 proceed decision** (closes with a separate proceed gate) | A0–A3 evidence | `A4_GAP_REPORT.md` and a recorded V4 proceed decision | Docs-only |

### A0 — Baseline lock (this phase)

A0 establishes the audit-only safety rules, registers the audit folder in
`docs-rules.md`, and creates this `AUDIT_PLAN.md`. A0 may not begin A1's
inventory work, may not touch the source tree, and may not run the application,
build, or test suites.

### A1 — Goal inventory

A1 extracts every user-observable promise from the V1, V2, and V3 roadmaps
plus the completed-phase plans and `TOFIX.md` files. Promises are organized by
user journey, not by class or roadmap section. Each row in the goal matrix
carries the metadata required by the audit's own quality gates; A1 does not
assign implementation verdicts.

A1 may not begin A2 work, may not inspect production code beyond the document
read needed to extract a promise's source citation, and may not modify any
production code or test.

### A2 — Wiring audit

A2 inspects the production wiring for each goal matrix row. For each row it
classifies the implementation state as one of:

- **Wired** — the production code path exists, follows the documented contract,
  and is reachable from the documented user entry point.
- **Wired-with-gap** — the code path exists but is incomplete, partial, or
  fails one of the documented exit conditions.
- **Missing** — the code path does not exist in the production tree.
- **Ambiguous** — the source documents do not specify enough to map the
  promise to a code target; A2 cannot make a verdict without A1 first
  resolving the ambiguity.

A2 produces a wiring audit evidence file per journey plus a cross-journey
summary. A2 may not edit production code, may not edit tests, and may not
begin A3.

### A3 — Clean-profile smoke

A3 runs targeted user-behavior scenarios on a disposable isolated profile
only. It never reads, writes, or mutates the real user configuration, settings
file, conversation store, or any other state directory the real application
uses. Every scenario documents the disposable profile location, the exact
entry-point action, the expected observable behavior, and the observed
result. A3 may not begin A4 work and may not modify production code.

### A4 — Gap report and proceed decision

A4 aggregates A0–A3 evidence into a single gap report and a recorded proceed
decision:

- **Proceed** — audit finds no blocking gaps; V4 or successor planning may
  begin.
- **Partial proceed** — audit finds gaps; corrective work is named and
  sequenced before V4 planning may begin.
- **Withhold** — audit finds blocking gaps; V4 planning is not authorized.

A4 may not begin corrective work; corrective work belongs in a separately
authorized phase, refactor, or issue named in the gap report.

---

## 3. Safety and Isolation Rules (Mandatory for A0–A4)

These rules are mandatory for every audit phase and bind every tool, agent,
and human reviewer that touches this folder. They are not relaxable by
expedience.

1. **No real user data.** The audit must never read, write, copy, or mutate
   the real user configuration, settings file, conversation store, or any
   other state directory the real application uses. A real user profile is
   out of scope for every phase.
2. **Disposable profile only.** Any runtime, smoke test, or future
   verification executed for the audit must use a disposable isolated
   profile. The disposable profile is created at scenario start, used for
   that scenario only, and removed at scenario end. No scenario may share
   state with another scenario or with the real user profile.
3. **No application, build, or test execution during A0–A1.** A0 and A1 are
   documentation-only phases. The application is not launched, the build
   is not run, the test suite is not run, and no scenario is executed
   against any profile during A0 or A1.
4. **No issue fixing or production-code modification.** No issue is fixed,
   no production code is edited, and no test is added or changed by any
   audit phase until A4 is accepted and corrective work is explicitly
   authorized in a separate planning decision.
5. **No phase skipping, stabilization, or successor planning in this
   session.** A0–A1 may not begin A2, A3, stabilization work, V4 planning,
   or any successor-roadmap planning in the same session unless an explicit
   proceed decision is recorded in this folder.
6. **Read-only on production code in A1.** A1 may read production code only
   to confirm the document citation for a user-observable promise. It may
   not extract implementation details, may not classify implementation
   state, and may not begin wiring analysis.
7. **A2 reads but does not edit.** A2 may inspect production code and
   tests; it may not edit them. A2 evidence files are the only output.
8. **A3 uses disposable profile only.** A3 may execute runtime, build, and
   test commands only against a disposable profile. A3 may not modify
   production code, and may not touch the real user profile even by
   accident.
9. **A4 is docs-only.** A4 may not begin corrective work. Corrective work
   is named in the gap report and lives in a separate planning decision.

These rules are also recorded in `docs-rules.md` §14 (Cross-Version
Product-Reality Audits). Any change to these rules belongs in
`docs-rules.md` and not in this file.

---

## 4. Inventory Scope — User Journeys

The goal matrix is organized by user journey, not by phase or class. The
audit must cover every journey below. A1 may not skip a journey even when
no completed phase claims to address it; missing journeys are themselves
findings for A4.

1. **First launch and settings** — application boot, default settings,
   settings persistence and recovery, secret handling, environment-variable
   fallback.
2. **Workspace / project opening** — folder picker, recent workspaces, project
   discovery and selection, no-project and ambiguous-project behavior.
3. **File navigation and editing** — file tree, file open, tabs, dirty state,
   save, search, replace, folding, focus and caret, multi-tab lifecycle.
4. **Search and command discovery** — Command Palette, keybindings, key
   conflict resolution, command availability and registration.
5. **Build / run / test** — target selection, structured Output, build
   diagnostics projection into Problems, test results surface, cancellation
   and one-at-a-time policy.
6. **Debugging and output** — DAP launch, breakpoints, step controls, call
   stack, variables, debug console/output, adapter failure recovery.
7. **Terminal** — embedded terminal sessions, multi-tab bottom panel,
   resize, key forwarding, alternate screen, search and selection.
8. **Git workflow** — repo discovery, status, branch display, diff view,
   stage/unstage, local commit. (Push/pull/merge/rebase are out of V1–V3
   scope and remain out of scope for this audit.)
9. **Townhall / conversations** — channels, direct conversations, unified
   conversation model, draft and read state, persistent versus in-memory
   history, migration from the temporary agent panel.
10. **Agent creation and backend onboarding** — agent identity, profile,
    runtime binding, provider configuration, secret handling, capability
    advertisement, Native Harness versus ACP equality of placement.
11. **Agent send / response / failure feedback** — direct send, routed send,
    `@mention` syntax, structured response, error and rejection
    projection, cancellation, timeout, disconnect, conversation
    attribution.
12. **Tools, permissions, and workspace mutation** — read/write operations,
    command execution, permission decisions, audit attribution,
    optimistic concurrency, conflict reconciliation, file-watcher
    reconciliation.
13. **Multi-agent routing** — `@mention` routing, source-to-target identity
    resolution, delegation lineage, Townhall surfacing of routed flow.
14. **Trace, context, memory, persistence, restart, and recovery** — raw
    trace redaction and retention, live IDE context policy, durable
    memory scopes, session resume semantics, restart of interrupted
    runs, retention and deletion contracts, cross-workspace isolation.

---

## 5. Goal Matrix Schema

Every goal matrix row carries:

- `id` — stable audit identifier `A1-<journey-key>-<nn>`. The
  `<journey-key>` is one of the 14 journey abbreviations used in §4
  (for example `FL`, `WO`, `FN`, `SC`, `BR`, `DB`, `TR`, `GT`, `TH`,
  `AC`, `AS`, `TP`, `MR`, `TC`). The `nn` is a zero-padded sequence
  number scoped to the journey. Rows that cannot be translated into a
  user-observable promise use the `XX` key and are recorded in a
  dedicated "cannot be translated" section that is **not** counted
  toward the user goal total. IDs are stable and never reused; a
  retired ID (one whose row was removed, merged into another row, or
  moved to a different journey) leaves a permanent gap in the
  original journey.
- `journey` — one of the 14 journeys above.
- `roadmap_version` — `V1`, `V2`, or `V3`.
- `phase` — the phase or sub-phase that owns the promise.
- `source_document` — clickable repo-relative markdown link plus the
  exact section/heading cited. Backtick-only paths are not acceptable
  citations.
- `promised_outcome` — the user-observable outcome the document claims.
- `user_entry_point` — the user action or surface the document names.
- `success_condition` — the observable behavior that proves the promise.
- `failure_recovery` — the documented failure or recovery behavior the
  user should see when the promise fails.
- `claimed_completion_evidence` — clickable repo-relative links to
  existing evidence files (or "no evidence file cited" if the document
  records a claim without naming a file). The completion claim is
  recorded verbatim from the document.
- `likely_a2_target` — the production code/wiring path A2 will inspect
  (named class, file, command, or service — best current guess).
- `planned_a3_scenario` — the disposable-profile smoke scenario A3 will
  execute (action → expected → observation).

A1 does not assign implementation verdicts. A1 does not conclude that a
documented promise is or is not implemented. A1 reports what the
documents claim and where the evidence file lives. A2 reuses the same
row keys and adds `a2_wiring_verdict` plus per-row evidence pointers.

---

## 6. Quality Gates

The audit passes A0–A1 only when all of the following are true. Quality
gates for A2–A4 are recorded in their own evidence files when those
phases begin.

- Every goal in the matrix cites a real repository document and a
  concrete section. Citations are clickable repo-relative links, not
  paraphrases.
- Duplicate promises across phases are merged into one row, with the
  merged source list recorded in `source_document`.
- Purely internal architecture work is excluded unless it directly
  enables a user-observable outcome; the exclusion reason is recorded in
  the row's notes.
- Promises that cannot yet be translated into user behavior are flagged
  in a dedicated `notes` field and are not silently absorbed.
- The matrix covers every journey in §4. A missing journey is itself a
  finding noted in the A1 closeout summary.
- `git diff --check` runs clean on every commit that lands in this
  folder.
- Every new relative link in the audit folder resolves to an existing
  repository file. Broken links are a quality-gate failure.
- This folder does not commit, push, or modify the production tree.
- No production code, no production test, and no real user data is
  touched during A0–A1.

---

## 7. Gates: A1 Acceptance and A4 V4 Proceed Decision

The audit has two distinct proceed gates. They are recorded separately
because they authorize different things.

### 7.1 A1-acceptance gate (authorizes A2)

A1 is **not** accepted when A1 finishes drafting. A1 becomes accepted
only when the `GOAL_MATRIX.md` meets the §6 quality gates and a recorded
A1-acceptance proceed decision is written in this folder. The
A1-acceptance proceed decision is the only artifact that authorizes A2.

| Condition | Required before A1 acceptance |
|-----------|------------------------------|
| Every `source_document` cell is a clickable repo-relative markdown link to an existing file. | yes |
| Every `claimed_completion_evidence` cell that points to a file is a clickable repo-relative markdown link to an existing file. | yes |
| Every journey in §4 has at least one user-observable row, or the gap is explicitly recorded in the A1 closeout. | yes |
| The `A1-XX-*` "cannot be translated" section is recorded as a separate count, not merged into the user-goal total. | yes |
| No implementation verdict appears in any A1 row's columns. | yes |
| The first A2 wiring-audit slice is named in the A1 closeout summary. | yes |
| The A1 closeout is recorded in `GOAL_MATRIX.md` §17. | yes |

A2 does **not** begin in the same session that records an A1-acceptance
proceed decision. A2 begins in a new session.

### 7.2 A4 V4-proceed decision (authorizes V4 planning)

A4 is **not** accepted when A4 finishes drafting. A4 becomes accepted
only when the gap report meets its own quality gates and a recorded
V4-proceed decision is written in this folder. The V4-proceed decision
is the only artifact that authorizes V4 or successor-roadmap planning.

| Condition | Required before V4 proceed |
|-----------|---------------------------|
| A2 evidence is complete for every accepted goal row, or the missing-evidence row is explicitly listed as a blocker. | yes |
| A3 evidence is complete for every accepted goal row targeted for smoke, or the missing-smoke row is explicitly listed as a blocker. | yes |
| The gap report classifies every A2/A3 finding by severity. | yes |
| The V4-proceed decision is one of `Proceed`, `Partial proceed`, or `Withhold`. | yes |

V4 or successor-roadmap planning does **not** begin in the same session
that records a V4-proceed decision. V4 planning begins in a new session
after the recorded decision.

---

## 8. A1 Closeout and Hand-off

After A1 finishes drafting (or after any A1 corrective round), the A1
closeout summary in this folder records:

- the number of unique user goals inventoried (the `A1-*-NN` count,
  excluding `A1-XX-*`);
- the number of `A1-XX-*` rows that cannot be translated into user
  behavior (recorded separately from the user goal total);
- the coverage by journey (count per journey, with zero-coverage
  journeys flagged);
- unresolved documentation ambiguities that block A2's wiring analysis
  (these become A1's contribution to the A4 gap report);
- the recommended first A2 wiring-audit slice;
- the A1-acceptance status (`accepted` or `not accepted`); if
  `not accepted`, the corrective items still required.

A2 does not begin in the same session that performs A1, regardless of
acceptance status. A2 begins in a new session after a recorded
A1-acceptance proceed decision in this folder.

---

*Created: 2026-07-30 (A0 baseline lock, A1 corrective rounds 1–8).
A1 accepted on 2026-07-30 via
[A1_ACCEPTANCE.md](./A1_ACCEPTANCE.md). Status 2026-07-31: A2 in
progress (not complete as a whole). `A2_AGENT_SEND` complete and
published ([evidence/A2_AGENT_SEND.md](./evidence/A2_AGENT_SEND.md));
`A2_MULTI_AGENT_ROUTING` complete and published
([evidence/A2_MULTI_AGENT_ROUTING.md](./evidence/A2_MULTI_AGENT_ROUTING.md));
`A2_TRACE_MEMORY_USAGE_TERMINATION` complete and published
([evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md](./evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md));
`A2_RESTART_RECOVERY_AND_CONTEXT` complete and published
([evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md](./evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md));
`A2_TOOLS_PERMISSIONS` complete and published
([evidence/A2_TOOLS_PERMISSIONS.md](./evidence/A2_TOOLS_PERMISSIONS.md)).
`A2_AGENT_CREATION_AND_BACKEND_ONBOARDING` complete and published
([evidence/A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./evidence/A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)).
`A2_TOWNHALL_AND_CONVERSATIONS` complete and published
([evidence/A2_TOWNHALL_AND_CONVERSATIONS.md](./evidence/A2_TOWNHALL_AND_CONVERSATIONS.md)).
`A2_FIRST_LAUNCH_AND_SETTINGS` complete and published
([evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md](./evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md)).
`A2_WORKSPACE_AND_PROJECT_OPENING` complete and published
([evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md](./evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md)).
`A2_FILE_NAVIGATION_AND_EDITING` complete and published
([evidence/A2_FILE_NAVIGATION_AND_EDITING.md](./evidence/A2_FILE_NAVIGATION_AND_EDITING.md)).
`A2_SEARCH_AND_COMMAND_DISCOVERY` complete and published
([evidence/A2_SEARCH_AND_COMMAND_DISCOVERY.md](./evidence/A2_SEARCH_AND_COMMAND_DISCOVERY.md)).
`A2_BUILD_RUN_AND_TEST` complete and published
([evidence/A2_BUILD_RUN_AND_TEST.md](./evidence/A2_BUILD_RUN_AND_TEST.md)).
`A2_DEBUGGING_AND_OUTPUT` complete and published
([evidence/A2_DEBUGGING_AND_OUTPUT.md](./evidence/A2_DEBUGGING_AND_OUTPUT.md)).
`A2_TERMINAL` complete and published
([evidence/A2_TERMINAL.md](./evidence/A2_TERMINAL.md)).
`A2_GIT` complete and published
([evidence/A2_GIT.md](./evidence/A2_GIT.md)).
A2 wiring is complete for all accepted user-goal rows. A3 and
cross-slice A2 consolidation are explicitly not begun; A4,
stabilization, and V4 work are not begun.*
