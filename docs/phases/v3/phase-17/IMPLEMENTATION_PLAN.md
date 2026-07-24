# Phase 17: Agent Action Control Plane and Workspace Mutation — Implementation Plan

## Status and authorization

**Phase 17 status:** M0 accepted on 2026-07-24. M1 complete on 2026-07-24. M2 is
authorized as the next bounded implementation milestone.

**Authorized work:** M2 workspace capture and bounded reads only. M3 and later
milestones remain gated by predecessor completion and the repository's
automatic progression and stop rules.

**Explicit exclusions:** Native Harness and ACP backends, Phase 16 candidate
work, live IDE-context disclosure, durable memory, raw traces, session resume,
provider registries, unrestricted shell access, and silent background
mutation.

**Planning baseline:**

| Check | Verified result |
|-------|-----------------|
| Branch | `master` |
| `HEAD` | `86db33a6563affcce42e563fab96d18f2d0742e0` |
| `origin/master` | `86db33a6563affcce42e563fab96d18f2d0742e0` |
| Working tree before plan creation | Clean |
| Phase 15 dependency | Complete and closed |
| Phase 16 relationship | Parked historical evaluation; not a dependency |
| Build baseline | Succeeded with 0 errors and 4 existing warnings |
| Fast test baseline | 2788 passed, 0 failed, 0 skipped |
| Verification date | 2026-07-24 |
| M0 acceptance | Accepted by the user on 2026-07-24 |

---

## Pre-implementation verification (M0)

- [x] Read `AGENTS.md`, `docs-rules.md`, `docs/CONVENTIONS.md`,
      `docs/architecture/OVERVIEW.md`, `docs/phases/README.md`, and the active
      Phase 17 section of `docs/roadmap/V3.md`.
- [x] Verify the live checkout, branch, remote-tracking commit, and Phase 15
      closeout status.
- [x] Audit the Phase 15 Agent Session, run, event, capability, evidence,
      cancellation, and composition seams against live source.
- [x] Audit current workspace identity, document ownership, editor file I/O,
      project-process execution, and DI seams against live source.
- [x] Confirm that no production action-control or permission-decision
      abstraction currently exists.
- [x] Lock the phase scope, milestone dependency order, verification commands,
      rollback boundaries, and expected commit boundaries.
- [x] Run the build and default fast-test baselines in an interactive terminal.
- [x] Review and accept M0 before any production implementation.

No new library is required or authorized by M0. Any later dependency proposal
must include a focused proof, compatibility evidence, and an amendment to this
plan before adoption.

---

## Accepted M0 implementation decisions

These decisions were accepted with M0 on 2026-07-24.

| ID | Decision | Locked outcome |
|----|----------|----------------|
| P17-D01 | Backend request seam | Zaide creates one run-scoped `IAgentActionBroker` and includes it in the admitted backend call context. A backend never resolves action services or mints authority. |
| P17-D02 | Control-plane ownership | `Features/Agents` owns action identities, contracts, policy, orchestration, audit snapshots, executors, permission presentation, and Agent event integration. |
| P17-D03 | Workspace authority | `Features/Workspace` owns a typed workspace identity/generation snapshot and invalidation notification exposed through Contracts. |
| P17-D04 | Document reconciliation | `Features/Editor` owns a Contracts/Application façade for observing and reconciling open documents. The Agent control plane never consumes Editor Presentation or Infrastructure. |
| P17-D05 | Process ownership | The Phase 17 command adapter is Agent-owned Infrastructure. It may use BCL process APIs behind the Agent application contract; it does not expose or repurpose ProjectSystem workflow services as backend tools. |
| P17-D06 | Decision scope | Every permission decision authorizes one exact immutable request only. Run-scoped and persistent grants are deferred. |
| P17-D07 | Read policy | One bounded regular-file read within the captured workspace is `AllowedByLockedPolicy`; all mutations and commands require one explicit user decision. |
| P17-D08 | Identity/idempotency | Zaide mints action and attempt ids. A backend may provide an optional opaque correlation key; duplicate keys are scoped to the authoritative run and exact request fingerprint. |
| P17-D09 | Projection ownership | Action and permission facts use the Phase 15 `AgentEventStream`. Only `AgentConversationEventProjection`, deliberately extended, writes their conversation/Townhall representation. |
| P17-D10 | Operational budgets | The concrete M0 budget table below is mandatory. A change is a reviewed plan amendment, not an implementation detail. |
| P17-D11 | Command environment | Commands receive the enumerated Zaide baseline only. Phase 17 accepts no backend/request-supplied environment variables. |
| P17-D12 | Command containment claim | Working-directory validation is not an OS sandbox. Phase 17 reports commands as explicitly approved Zaide-executed processes, not as filesystem- or network-contained execution. |

### Ownership and dependency locks

| Concern | Owner / layer | Allowed dependencies | Forbidden placement or dependency |
|---------|---------------|----------------------|-----------------------------------|
| Action ids, request/result payloads, proposal values, state, budget values | `Features/Agents/Domain` | BCL and existing Agent/Conversation domain identities | Workspace, Editor, ProjectSystem, Townhall, UI, process, or file-system implementations |
| `IAgentActionBroker`, broker call context, executor/reconciliation ports | `Features/Agents/Contracts` | BCL and stable Domain value types | Concrete Infrastructure, Presentation, DI, `IServiceProvider` |
| Policy, fingerprinting, orchestration, revocation, audit snapshots | `Features/Agents/Application` | Agent Contracts/Domain plus Workspace and Editor contracts | Cross-feature Presentation/Infrastructure, `System.IO`, `Process`, DI |
| Canonical path/file and command adapters | `Features/Agents/Infrastructure` | Agent Contracts/Domain, Workspace contracts, BCL file/process APIs | Editor/Workspace/ProjectSystem Presentation or Infrastructure |
| Permission review | `Features/Agents/Presentation` | Agent Application/Contracts/Domain | Shell-owned policy, direct file/process execution, service location |
| Workspace identity, generation, active-root snapshot, invalidation | `Features/Workspace/Domain` and `Features/Workspace/Contracts`, with an Application owner only if coordination requires it | BCL and Workspace Domain | Agent-specific request/policy types, Presentation as authority |
| Open-document observation/reconciliation façade | `Features/Editor/Contracts` and `Features/Editor/Application` | Editor Domain and Workspace identity value as required | Agent types in Editor Domain; Agent consumption of Editor Presentation/Infrastructure |
| Conversation/Townhall action projection | Existing `AgentConversationEventProjection` in `Features/Agents/Application` | Agent events and Conversation application boundary | A second control-plane writer, direct Townhall write, backend write |
| Registration and activation | `App/Composition` | Concrete Agent Infrastructure/Presentation and exposed contracts | Business policy or request execution in composition |

Root `Infrastructure/` remains unadmitted. All new production types are
internal by default; only the smallest cross-feature contracts may be public.
Architecture tests must encode these ownership decisions before M8 closes.

### Backend action request seam

The Phase 17 backend call path is locked as follows:

1. After authoritative session/run admission, Zaide captures the active
   workspace identity/generation and creates one run-scoped
   `IAgentActionBroker`.
2. `IAgentBackend.ExecuteAsync` receives a Zaide-created execution context
   containing the existing immutable `AgentBackendRequest` and that broker.
   M1 implements this exact conceptual shape:

   ```csharp
   internal sealed record AgentBackendExecutionContext(
       AgentBackendRequest Request,
       IAgentActionBroker Actions);

   internal interface IAgentActionBroker
   {
       ValueTask<AgentActionResult> RequestAsync(
           AgentActionPayload payload,
           string? correlationKey,
           CancellationToken cancellationToken);
   }

   IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
       AgentBackendExecutionContext context,
       CancellationToken cancellationToken);
   ```

   Names may change only through M0 amendment; the single run-scoped broker,
   Zaide-owned ids, payload-only caller input, result shape, and cancellation
   semantics may not.
3. The broker is already bound to session, run, conversation, actors, backend,
   and workspace generation. Its request methods accept action payloads and an
   optional opaque backend correlation key, not caller-supplied authority ids.
4. The broker re-resolves authoritative run and workspace state before
   classification, before consuming a permission decision, and immediately
   before execution.
5. Run cancellation/terminalization, session end, workspace close/switch,
   application shutdown, or broker disposal revokes the broker and every
   pending request. Revocation is terminal and cannot be reversed.
6. The legacy backend is passed a broker whose capability remains unavailable
   and never calls it. M8 uses a repository-owned fake requester only; it is
   not registered as a production backend.

Backends never receive `IServiceProvider`, executor ports, policy services,
workspace/editor services, or permission presentation objects.

### Locked operational budgets

All sizes are measured in UTF-8 bytes unless a row states otherwise.

| Budget | Locked Phase 17 value | Enforcement point |
|--------|-----------------------|-------------------|
| Regular-file read | 1 MiB maximum | Before allocation when length is available and while streaming |
| Proposed create/replace text | 1 MiB maximum | Before fingerprinting and again before apply |
| Permission/file preview summary | 64 KiB maximum and 2,000 displayed lines | Before decision publication |
| Command stdout | 1 MiB and 10,000 lines maximum | While reading redirected stdout |
| Command stderr | 1 MiB and 10,000 lines maximum | While reading redirected stderr |
| Command execution time | 120 seconds | From successful process start until timeout-triggered termination |
| Process-tree cleanup | 5 seconds | After cancellation, timeout, output-budget termination, or shutdown |
| Permission decision lifetime | 5 minutes | From decision publication; expiry denies |
| Non-terminal actions | 1 per run | At broker admission; pending permission, read, apply, and command states share one slot, and a concurrent request is rejected rather than queued |
| Stored audit summary text | 8 KiB per fact | Before event/audit publication |
| Content revision | Lowercase SHA-256 over exact bytes | At read/proposal and immediate pre-apply revalidation |
| Backend correlation key | 128 UTF-8 bytes maximum | At broker request admission |

Truncation is never silent. A file/proposal over budget is rejected. When
either command stream exceeds its byte or line budget, Zaide kills the complete
process tree, drains only within the cleanup budget, and returns a truncated
terminal result that never claims complete output.

### Locked command environment

Phase 17 constructs a new environment and does not pass through the parent
environment wholesale. The only inherited names are:

- `PATH`, used to resolve the executable before permission review and process
  start;
- `LANG`, `LC_ALL`, and `TZ`, when present, for stable text/locale behavior;
- `HOME` and `TMPDIR`, when present, because common developer tools require
  them, while the permission UI explicitly states that working-directory
  scope is not filesystem containment.

Zaide sets `NO_COLOR=1`, `DOTNET_NOLOGO=1`, and
`DOTNET_CLI_TELEMETRY_OPTOUT=1`. No backend/request-supplied environment
variables are accepted in Phase 17. Names or values matching Zaide's secret
configuration are not copied into events, audit records, or permission
previews.

M7 must deny executables whose resolved basename is `sh`, `bash`, `dash`,
`zsh`, `fish`, `csh`, `tcsh`, `ksh`, `sudo`, `doas`, `su`, or `pkexec`.
Aliases, symbolic links, and resolved targets are checked. This denylist
enforces the Phase 17 no-shell/no-privilege-escalation boundary; it is not
represented as a general executable allowlist or sandbox.

This is process hygiene, not a sandbox. An approved executable can access
resources permitted to the Zaide process, including paths outside the
workspace or the network. The permission surface must disclose that fact.
Phase 17 does not claim to prevent those effects; OS sandboxing and command
allowlist policy require a later explicit roadmap decision.

---

## Scope

**Goal:** Build a backend-neutral, Zaide-owned action control plane that accepts
typed action requests from an admitted Agent Session run; validates identity,
workspace scope, policy, and freshness; obtains an explicit permission
decision when required; executes only through constrained Zaide adapters; and
emits attributable results that reconcile with the existing workspace and
document model.

The Phase 17 trust boundary is:

```text
admitted Agent Session run
  -> typed action request
  -> validation and policy classification
  -> permission decision
  -> constrained Zaide executor
  -> attributable action result
  -> document/workspace reconciliation
  -> normalized Agent event projection
```

Later backends may request actions through this boundary. They do not receive
`IFileService`, `IManagedProcessRunner`, `System.IO`, `Process`, editor
presentation state, or a general service provider.

### In scope

- Typed identities for actions, attempts, requests, permission decisions, and
  results, correlated to Agent Session, run, conversation, actor, backend, and
  workspace identity.
- A closed Phase 17 action taxonomy for:
  - reading a regular file within the active workspace;
  - proposing a whole-file create, replace, or delete change without mutating;
  - applying an accepted proposal with stale-base protection;
  - executing an explicitly represented command under a constrained process
    policy.
- Canonical path containment and symbolic-link escape defense.
- Deterministic size, output, time, concurrency, and cancellation budgets.
- Permission classification and an explicit decision lifecycle with
  deny-by-default behavior.
- A minimal visible permission-review surface for affected paths, command
  details, risk, scope, and the exact action being approved or denied.
- Zaide-owned file and process adapters that return structured evidence.
- Optimistic concurrency for workspace mutation using a content revision or
  digest captured during read/proposal.
- Reconciliation with open clean and dirty documents after disk mutation.
- Ordered, typed action and permission events integrated with the Phase 15
  event stream and truthful evidence levels.
- Deterministic in-memory audit snapshots for the current application
  lifetime, sufficient for Townhall/action-status projection and tests.
- Architecture ratchets preventing bypass from later Agent backends.

### Out of scope

- Implementing or adapting a Native Harness, ACP client/server, or any other
  production backend.
- Giving the legacy non-streaming OpenAI-compatible backend action capability.
- Selecting, packaging, qualifying, or executing Phase 16 candidates.
- Supplying selected editor text, diagnostics, git state, terminal state,
  memory, or other live IDE context to a backend; Phase 18 owns that boundary.
- Durable audit/event storage, cross-restart recovery, replay, resume, or
  automatic continuation of interrupted side effects.
- Raw model or protocol traces, token/cost reporting, provider configuration,
  credential brokering, or secret disclosure.
- Arbitrary shell scripts, shell expansion, pipelines, redirection, interactive
  commands, PTY access, privilege escalation, or background daemons.
- Network, package-install, source-control mutation, settings mutation,
  workspace switching, or direct file-control-plane writes outside the active
  workspace. Approved commands are not an OS sandbox and may have broader side
  effects; the review surface must disclose this limitation.
- Multi-file transactions with atomic all-or-nothing commit.
- Automatic conflict resolution or overwriting dirty editor buffers.
- A persistent allowlist, "always allow", or run-scoped grant; Phase 17
  decisions apply only to one exact immutable request.

---

## Verified live facts

### Agent Session and event foundation

| Concern | Live owner | Planning consequence |
|---------|------------|----------------------|
| Backend execution | `IAgentBackend.ExecuteAsync(AgentBackendRequest, CancellationToken)` | The request carries run and actor correlation but no action broker. Phase 17 must extend the neutral run boundary without binding it to a concrete backend. |
| Session ownership | `IAgentSessionService` / `AgentSessionService` | Only an admitted active run may request an action. Session/run terminalization and cancellation must revoke pending action authority. |
| Event ordering | `AgentEventStream` and per-run event sequencing | Action events must use the same authoritative ordering owner; a second unrelated activity stream would create contradictory histories. |
| Capability truth | `AgentCapabilitySnapshot` | `Tools` and `Permissions` are currently unavailable on the legacy backend. Phase 17 infrastructure alone must not make that backend claim support. |
| Evidence truth | `AgentActivityEvidenceLevel` | A successfully mediated action can be `ZaideMediated` or `ZaideExecuted`; a backend claim without Zaide execution cannot be promoted. |
| Event taxonomy | `AgentEventKind` / typed payloads | No action or permission kinds exist. Phase 17 must add typed payloads and preserve the exact-one-payload invariant. |
| Persistence | Agent sessions and events are in-memory | Phase 17 audit state is also in-memory. Interrupted or restarted side effects are terminal or indeterminate, never auto-resumed. |

### Workspace, document, and process seams

| Concern | Live owner | Verified limitation / required boundary |
|---------|------------|-----------------------------------------|
| Active workspace | `Workspace.WorkspacePath` | A nullable path exists, but there is no typed workspace identity, generation, trust state, or action lease. Workspace close/switch must invalidate pending requests. |
| Open documents | `Workspace.Documents`, `Workspace.OpenDocument`, and `Document` | Documents track content and dirty state, but paths are mutable strings and external-change reconciliation is not an application contract. |
| Editor file I/O | `IFileService` / `FileService` | Editor-owned read/write calls accept unrestricted paths and whole text. They are not an agent security boundary and must not be exposed to backends. |
| Editor save | `EditorViewModel.SaveAsync` | Save writes presentation-owned content and marks it clean. Agent mutation needs a separate application boundary that cannot silently overwrite a dirty buffer. |
| Process execution | `IManagedProcessRunner` / `ManagedProcessRunner` | ProjectSystem owns redirected process lifecycle and tree-kill behavior. Its start request uses executable plus one argument string and is not a permission or command-sandbox contract. |
| Composition | `AgentsServiceCollectionExtensions` | Later registration may wire action-control services, but action logic must not resolve through `IServiceProvider` or consume concrete cross-feature infrastructure/presentation types. |
| Architecture | `docs/CONVENTIONS.md` and Architecture tests | Cross-feature use must go through Contracts or an application façade; new types are internal by default and inventory/visibility baselines must be updated intentionally. |

---

## Locked Phase 17 contracts

### Authority and identity

1. Zaide mints every action id and execution-attempt id. Every request is bound
   to one non-default action id, session id, run id, conversation id,
   initiating actor id, target actor id, backend id, workspace identity, and
   workspace generation.
2. The control plane resolves authoritative session/run/workspace state itself.
   Backend-supplied identity is correlation input, not proof of authority.
3. Only the currently admitted, non-terminal run may request or consume a
   permission decision.
4. Workspace close/switch, run terminalization, cancellation, decision expiry,
   or application shutdown invalidates unexecuted authority.
5. One terminal result is emitted for every accepted request. A backend may
   supply one bounded opaque correlation key. Reuse within the same run and
   exact request fingerprint returns the recorded terminal result; reuse with a
   different fingerprint is rejected. Neither case repeats a side effect.

### Paths and files

1. All requested paths are workspace-relative, normalized by Zaide, and
   resolved against a captured canonical workspace root.
2. Absolute paths, empty paths, traversal, alternate-root paths, device files,
   directories where a regular file is required, and symbolic-link escapes are
   rejected before permission review.
3. Reads are bounded and return content plus an exact revision/digest. Binary
   files and files over the locked M0 budget are rejected rather than partially
   interpreted as text.
4. Proposals are immutable and non-mutating. Their preview records operation,
   path, base existence, base revision, proposed revision, and bounded summary.
5. Apply performs a fresh containment and revision check immediately before
   mutation. A stale base produces `Conflict`; it is never force-overwritten.
6. Writes use a same-directory temporary file and atomic replace/rename where
   the platform supports it. Delete revalidates the base revision first.
7. Dirty open documents are never overwritten. Clean open documents are
   reloaded through an explicit reconciliation contract after successful disk
   mutation; unopened files remain unopened.

### Commands

1. Commands are represented as an executable and an argument vector, never as
   an interpolated shell command.
2. Zaide resolves the executable to one canonical absolute path before
   permission review. That resolved path, its denylist result, the argument
   vector, and working directory are part of the immutable request fingerprint
   and permission display; execution revalidates and starts that same resolved
   target rather than repeating a mutable `PATH` lookup.
3. `UseShellExecute`, shell expansion, pipelines, redirection, interactive
   input, PTY, privilege escalation, detached/background execution, and
   arbitrary environment inheritance are forbidden.
4. The working directory is canonicalized within the captured workspace.
5. Environment variables use only the locked Zaide baseline. Phase 17 accepts
   no backend/request-supplied variables. Secret values are never copied into
   results or audit payloads.
6. Every command requires visible user approval in Phase 17. There is no
   command allowlist or automatic command approval in this phase.
7. Time, output bytes, line count, and one-non-terminal-action-per-run
   concurrency are bounded. Cancellation and shutdown kill the complete
   process tree.
8. The result distinguishes exit, cancellation, timeout, startup failure,
   truncation, and indeterminate cleanup.

### Permission decisions

1. Classification is deterministic and separate from presentation:
   `DeniedByPolicy`, `RequiresUserDecision`, or `AllowedByLockedPolicy`.
2. One bounded regular-file read that passes every workspace, path, generation,
   type, binary, and size check is `AllowedByLockedPolicy` without a prompt.
   File create/replace/delete, command execution, and every ambiguous request
   require a user decision.
3. The decision binds to the complete immutable request fingerprint and its
   displayed scope. Editing the request creates a new decision requirement.
4. Closing or dismissing the review denies the request. Timeout, cancellation,
   stale workspace generation, and unavailable UI also deny.
5. A decision authorizes one exact immutable request only. Run-scoped,
   path-prefix, directory, path-set, action-kind, persistent, and "always
   allow" grants do not exist in Phase 17.
6. Backend capability or preference can never lower Zaide policy.

### Audit and evidence

1. Request, classification, decision, execution start, terminal result,
   reconciliation result, and revocation are distinct typed facts.
2. Audit records contain stable identities, timestamps, normalized targets,
   policy/result codes, bounded summaries, and causation links. They do not
   contain secrets or unrestricted file/command output.
3. Zaide reports `ZaideExecuted` only for an operation actually performed by a
   Zaide executor and `ZaideMediated` only when the complete decision/execution
   path was mediated. Rejected requests are not executed capability evidence.
4. Event projection is append-only and ordered for the application lifetime.
   Observer failure cannot corrupt the control-plane state machine.
5. Only the existing `AgentConversationEventProjection`, extended deliberately,
   may project Phase 17 Agent events into conversation/Townhall entries. The
   broker, policy, executors, and permission presentation never write those
   entries directly.

---

## Milestones

| Milestone | Outcome | Primary verification |
|-----------|---------|----------------------|
| M0 | Accept this live-seam audit, threat boundary, contracts, budgets, milestone order, and rollback plan. No production changes. | `git diff --check`; human review |
| M1 | Add action/workspace identity, request/result taxonomy, policy classifications, immutable proposal model, budgets, state transitions, and duplicate/terminal invariants. No file or process execution. | `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase17ActionContracts'` |
| M2 | Implement canonical workspace capture and bounded read-only file access with traversal, symlink, binary, size, cancellation, workspace-generation, and time-of-check/time-of-use defenses. | `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase17WorkspaceRead'` |
| M3 | Implement permission classification, decision lifecycle, revocation, exact-request fingerprints, and a minimal visible review surface. No mutation or process execution yet. | `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase17Permission'` plus manual review evidence |
| M4 | Implement immutable create/replace/delete proposals, bounded diff/summary presentation, stale-base detection, and explicit accept/deny flow. Proposal creation remains non-mutating. | `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase17Proposal'` plus manual preview evidence |
| M5 | Apply accepted file proposals through a constrained adapter with immediate revalidation, safe write/delete behavior, exactly-once terminalization, and failure classification. | `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase17WorkspaceMutation'` |
| M6 | Reconcile successful disk mutations with clean/dirty open documents through a Workspace/Editor application contract; never overwrite dirty buffers or depend on Editor Presentation. | `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase17DocumentReconciliation'` |
| M7 | Execute explicitly approved non-shell commands through a Phase 17 adapter with constrained working directory/environment, output/time budgets, process-tree cancellation, and structured results. | `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase17CommandExecution'` |
| M8 | Integrate action/permission events with Phase 15 session/run ordering, evidence levels, capability truth, Townhall projection, cancellation, workspace invalidation, and in-memory audit snapshots. Add bypass-prevention architecture tests. | `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase17|FullyQualifiedName~Architecture'` |
| M9 | Complete adversarial, integration, accessibility/manual, shutdown, non-deletion, architecture, full-suite, and documentation closeout. | `dotnet build Zaide.slnx --no-restore`; `dotnet test Zaide.slnx --no-build`; `git diff --check` |

### M0 — Review and acceptance publication

M0 acceptance closes the planning gate. After a verified GO, M1 follows the
repository's automatic milestone-progression rule unless the user explicitly
restricts authorization or a stop condition applies.

When M0 is accepted, the same docs-only commit must:

- record the acceptance date and accepted baseline in this plan;
- create Phase 17 `TOFIX.md` with M1 as the next task;
- update `docs/roadmap/V3.md` so "Current Next Step" no longer says to prepare
  this plan;
- update `docs/phases/README.md` to index Phase 17 planning status;
- update Phase 16 `TOFIX.md` so its next-task text points to the accepted Phase
  17 planning gate rather than an uncreated plan;
- update `README.md` and `docs/architecture/OVERVIEW.md` only where their
  current-status wording would otherwise become false;
- run `git diff --check`, inspect the exact staged docs scope, and keep
  implementation files out of the acceptance commit.

**Completion condition:** P17-D01–P17-D12 and the full M0 checklist are
human-accepted, all status surfaces agree that the plan exists, and M1 routing
is stated truthfully under the repository's progression and stop rules.

### M1 — Contracts and deterministic state

M1 must be implementation-free with respect to disk and process side effects.
Its tests lock:

- identity parsing, equality, default rejection, and correlation;
- request-kind/payload exact matching;
- action lifecycle and allowed transitions;
- immutable request fingerprints and duplicate handling;
- permission-reviewable immutable payloads and bounded display summaries for
  every read, create, replace, delete, and command request kind;
- result and failure taxonomy;
- proposal revision/digest validation;
- policy classifications and decision scope;
- positive, finite budget validation;
- redacted bounded audit summaries.

**Completion condition:** Invalid or ambiguous requests cannot become
executable objects; every non-read request has a complete display-ready summary
and exact fingerprint before M3; and the state model has exactly one terminal
outcome without touching the file system or process APIs.

### M2 — Workspace capture and reads

M2 introduces the first executor, restricted to reads. Tests must use temporary
workspaces and cover:

- no workspace, workspace close, workspace switch, and stale generation;
- `.` / `..`, absolute paths, separator variants, case behavior appropriate to
  the host, and paths sharing only a textual prefix with the root;
- file and directory symbolic links, including a link retargeted between
  validation and open;
- missing, directory, special, binary, oversized, unreadable, and changing
  files;
- cancellation before open and during read;
- digest stability and bounded content/result behavior;
- duplicate request non-reexecution.

M2 must not reuse editor `IFileService` as the security boundary.

**Completion condition:** A run can obtain an attributable, bounded snapshot of
one regular workspace file, while every escape or stale-authority case is
rejected without external mutation.

### M3 — Permission policy and visible decisions

M3 owns policy and user decisions before any mutation executor exists. The
presentation must display:

- requesting actor/backend and correlated run;
- action kind and normalized target;
- file operation with bounded change summary, or command executable,
  arguments, and working directory;
- the fixed scope: this exact request only;
- both the normalized workspace-relative target and the resolved absolute path
  confirmed beneath the captured root;
- explicit Allow and Deny controls with keyboard navigation, visible focus,
  screen-reader names, and deny-on-dismiss behavior.

Tests cover exact fingerprint binding, decision expiry, cancellation,
workspace/run invalidation, concurrent requests, repeated decisions, UI
unavailability, observer failure, and backend attempts to self-approve.

**Completion condition:** Every non-read action is either denied by policy or
blocked on a visible Zaide-owned decision, and no decision can authorize a
different or stale request.

Manual evidence for this milestone is recorded in
`docs/phases/v3/phase-17/M3_PERMISSION_REVIEW_EVIDENCE.md`.

### M4 — Non-mutating change proposals

M4 creates previews only. It must cover create, replace, and delete with:

- captured base existence and digest;
- normalized proposed text and proposed digest where applicable;
- bounded unified summary or equivalent affected-region representation;
- explicit new/deleted-file treatment;
- binary/oversized/unsupported rejection;
- immutable content after permission review begins;
- stale-base detection before a decision may be consumed.

No disk write/delete API is called in M4.

**Completion condition:** The user can inspect the exact bounded file action
that a later milestone could apply, and acceptance is inseparable from that
proposal's fingerprint and base revision.

### M5 — Safe workspace mutation

M5 adds the mutation executor behind the accepted proposal boundary. Tests
must cover:

- create when absent and conflict when newly created;
- replace/delete only when the current digest matches the captured base;
- safe temporary-file cleanup on success, failure, and cancellation;
- permission loss and workspace generation change immediately before apply;
- symlink retarget and parent-directory replacement attacks;
- partial-write/rename/delete failures and truthful result classification;
- duplicate delivery and cancellation races;
- exactly one terminal result and no success claim before the filesystem
  confirms the operation.

**Completion condition:** Accepted proposals mutate only their verified target
once, conflicts preserve external content, and every failure remains
attributable without a false success event.

### M6 — Document reconciliation

M6 adds a narrow Workspace/Editor-owned application contract. It must define
and test:

- clean open document: reload the confirmed disk result and keep it clean;
- dirty open document: preserve buffer content and dirty state, surface an
  external-conflict result, and require a later human choice;
- deleted clean document: surface deletion without silently inventing content;
- deleted dirty document: preserve the buffer and flag disk absence;
- unopened document: do not open a tab;
- workspace close/switch while a document remains open: revoke the action
  broker immediately; retained documents are not agent-mutable targets without
  a new active workspace generation;
- UI-thread dispatch and observer failure isolation;
- file changes occurring again between apply and reconciliation.

The action-control application layer may consume only the exposed contract, not
`EditorViewModel`, `EditorTabViewModel`, or another presentation type.

**Completion condition:** Disk truth and editor truth cannot silently diverge
or overwrite one another after a Zaide-mediated action.

### M7 — Constrained command execution

M7 introduces command execution only after permission and cancellation are
proven for file actions. The adapter may reuse lower-level process-lifecycle
behavior only through a contract appropriate to Phase 17; it must not expose
the Project Workflow runner directly to a backend.

Tests must cover:

- executable plus argument-vector preservation without shell parsing;
- empty, relative, absolute, missing, and disallowed executable handling;
- policy rejection for shell interpreters and privilege-escalation helpers
  named by the locked M7 denylist, with no `-c`-style shell escape;
- workspace-contained working directory and symlink retarget defense;
- the exact locked environment construction, no request-local variables, and
  secret redaction;
- startup failure, non-zero exit, timeout, cancellation, truncation, and normal
  completion;
- stdout/stderr separation, byte and line limits, and invalid text;
- complete process-tree termination and cleanup verification;
- concurrent command rejection for one run;
- decision expiry or run/workspace cancellation before start;
- duplicate request non-reexecution.

**Completion condition:** One explicitly approved, bounded, non-interactive
command can run under Zaide ownership and produce a structured, redacted,
truthful result. Its permission review states that working-directory scope is
not filesystem or network sandboxing.

### M8 — Session/event integration and bypass ratchets

M8 connects the independently tested control plane to Phase 15 without
implementing a real tool-using backend. Use a repository-owned fake action
requester for integration tests.

It must:

- bind action authority to authoritative active-session/run state;
- propagate run cancellation and terminalization to pending decisions,
  file work, and process trees;
- emit typed request, decision, start, result, reconciliation, and revocation
  events through the Phase 15 ordering owner;
- keep legacy backend `Tools` and `Permissions` capability rows unavailable;
- distinguish Zaide-executed/mediated facts from backend reports;
- extend only `AgentConversationEventProjection` to project a bounded
  human-readable activity summary without raw content or secrets;
- prove that the control plane, broker, executors, permission presentation,
  and fake requester have no direct conversation/Townhall write path;
- expose current-lifetime audit snapshots without introducing persistence;
- add architecture tests preventing `IAgentBackend` implementations and Agent
  application code from directly consuming editor file I/O, project workflow
  process runners, concrete infrastructure, presentation types, `System.IO`,
  `System.Diagnostics.Process`, or `IServiceProvider`.

**Completion condition:** A fake requester can exercise the full Phase 17
boundary, every action is ordered and attributable to its run, and the legacy
backend remains behaviorally and capability-compatible.

### M9 — Closeout

M9 requires:

- adversarial test review for path, symlink, time-of-check/time-of-use,
  duplicate, cancellation, workspace-switch, redaction, and process-tree cases;
- manual evidence for allow, deny, dismiss, stale proposal, dirty-buffer
  conflict, cancellation, output truncation, keyboard-only flow, screen-reader
  naming, narrow/wide layout, and shutdown during a pending action;
- non-deletion verification for the Phase 15 session/event foundation and the
  legacy OpenAI-compatible path;
- architecture inventory/visibility updates with no unexplained ratchet
  weakening;
- full build, default fast suite, serial fallback only if required by the
  repository test policy, and whitespace checks;
- truth-sync of `README.md`, `docs/architecture/OVERVIEW.md`,
  `docs/phases/README.md`, `docs/roadmap/V3.md`, this plan, and Phase 17
  `TOFIX.md`.

The consolidated closeout record is
`docs/phases/v3/phase-17/M9_CLOSEOUT_EVIDENCE.md`; it links rather than
duplicates the M3 permission-review evidence.

**Completion condition:** Phase 17 is reviewable as a trustworthy
backend-neutral action boundary, with no production backend or Phase 18
context-disclosure work hidden in the closeout.

---

## Verification strategy

### Per implementation milestone

Run in an interactive terminal:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase17'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Architecture'
git diff --check
```

Focused filters must be introduced with their owning test slices and kept
stable through M9.

### Full regression gate

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

If the default fast suite fails or hangs, reproduce before classifying a
regression:

```bash
dotnet test Zaide.slnx --no-build \
  --settings tests/Zaide.Tests/slow.runsettings
```

### Required test layers

- Pure domain tests for identities, request/result payload matching, state
  transitions, policy, fingerprints, budgets, and redaction.
- Temporary-workspace tests for canonical containment, symbolic links,
  revisions, writes, deletes, failures, and reconciliation.
- Fake-clock/fake-decision tests for expiry and race determinism.
- Fake-process tests for policy and result mapping, plus Linux integration
  tests for real process-tree cancellation and output budgets.
- Repository-owned fake requester tests for session/event integration; no
  upstream or network execution.
- Architecture tests for ownership, visibility, DI, and backend bypass
  prevention.
- Manual evidence only where visual, accessibility, or OS behavior cannot be
  asserted reliably in the automated suite.

---

## Expected commit boundaries

Prefer one reviewable commit for each coherent milestone:

| Milestone | Expected commit |
|-----------|-----------------|
| M0 | Plan and acceptance record only |
| M1 | Action contracts and state model |
| M2 | Workspace capture and bounded reads |
| M3 | Permission policy and review surface |
| M4 | Non-mutating file proposals |
| M5 | Safe file mutation |
| M6 | Open-document reconciliation |
| M7 | Constrained command execution |
| M8 | Agent Session/event integration and bypass ratchets |
| M9 | Closeout evidence and documentation truth-sync |

Ordinary milestone documentation belongs in the milestone commit. Split only
when a boundary is independently reversible and reviewable; do not split for
status or mechanical convenience.

---

## Limitations by design

- Phase 17 supports one active workspace and application-lifetime authority.
- Audit snapshots and permission decisions are not durable across restart.
- Read and mutation support is text-file-oriented and budgeted.
- File proposals apply individually; there is no multi-file atomic transaction.
- Conflicts stop the action; there is no automatic merge or force apply.
- Dirty open documents always require later human reconciliation.
- Commands are non-interactive, non-shell, individually approved, and start in
  a workspace-contained directory; they are explicitly not claimed to be
  filesystem- or network-sandboxed.
- There is no network/package/source-control/settings permission class.
- There is no persistent trust store or "always allow" rule.
- No current production backend gains tool or permission capability in this
  phase.
- Phase 18 decides what live IDE context may be disclosed to a run.

---

## Stop conditions

Stop the current milestone and record the blocker in Phase 17 `TOFIX.md` when:

- a request cannot be bound to an authoritative active run and workspace
  generation;
- a required path-containment or symbolic-link defense cannot be made
  deterministic on a supported platform;
- a decision surface cannot show the exact immutable scope being authorized;
- an apply path can overwrite a stale file or dirty buffer;
- a command path requires a shell, unrestricted environment, unbounded output,
  or incomplete process-tree ownership;
- event integration would require falsely advertising a backend capability;
- the work requires Phase 18 context disclosure, a production backend,
  persistence/resume, a new external dependency, network/credentials, or
  destructive migration;
- focused or full verification fails and the serial fallback confirms a
  regression;
- the implementation would weaken an architecture ratchet without a separately
  reviewed amendment.

---

## Exit conditions

- [ ] M0 is reviewed and accepted before production implementation.
- [ ] M1–M8 completion conditions and focused gates pass.
- [ ] All accepted action requests are bound to authoritative run/workspace
      state and have exactly one terminal result.
- [ ] File reads and mutations cannot escape the captured workspace or bypass
      revision checks.
- [ ] Dirty buffers are never silently overwritten.
- [ ] Commands are non-shell, bounded, explicitly approved, and fully owned
      through process-tree termination.
- [ ] Permission dismissal, cancellation, expiry, workspace change, run
      terminalization, and shutdown revoke pending authority.
- [ ] Audit/event facts are ordered, attributable, bounded, redacted, and use
      truthful evidence levels.
- [ ] The legacy backend behavior and unavailable Tools/Permissions capability
      claims remain unchanged.
- [ ] No Native Harness, ACP, Phase 16 candidate, Phase 18 context, persistence,
      resume, raw trace, provider registry, or durable memory work is present.
- [ ] Manual permission, conflict, cancellation, accessibility, layout, and
      shutdown evidence is recorded.
- [ ] Architecture ratchets pass without unexplained weakening.
- [ ] `dotnet build Zaide.slnx --no-restore` succeeds.
- [ ] `dotnet test Zaide.slnx --no-build` passes, or any fast-suite failure is
      reproduced and resolved under the documented serial fallback.
- [ ] `git diff --check` passes.
- [ ] Phase 17 status surfaces are truth-synced at closeout.

---

## Rollback plan

The pre-Phase-17 implementation baseline is:

```text
86db33a6563affcce42e563fab96d18f2d0742e0
```

Each milestone is expected to remain independently revertible. If a milestone
fails its gate:

1. Stop before beginning the next milestone.
2. Record the finding and affected action contract in Phase 17 `TOFIX.md`.
3. Revert only the failing milestone through a normal revert commit.
4. Re-run the build, focused Phase 17 tests, Architecture tests, full suite,
   and whitespace check.
5. If the accepted phase boundary itself must change, amend and re-review this
   plan before resuming.

Do not use destructive history rewriting for rollback. If Phase 17 is
structurally abandoned, create `docs/phases/v3/phase-17/REVERT_LOG.md` using the
repository template and preserve the historical plan.
