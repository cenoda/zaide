# Phase 18: Live IDE Context for Agent Runs — Implementation Plan

## Status and authorization

**Phase 18 status:** M0 planning document created for review. No milestone
implementation has started.

**Authorized work:** This M0 planning document only. No production code, tests,
UI, backend wiring, persistence, memory, raw traces, provider-specific prompt
tuning, or Phase 19/20 work is authorized.

**Phase 18 implementation is unauthorized until this M0 plan is explicitly
reviewed and accepted by the user.** Acceptance of M0 authorizes M1 work to
begin. Starting M1 before M0 acceptance is a stop condition.

**Planning baseline:**

| Check | Verified result |
|-------|-----------------|
| Branch | `master` |
| `HEAD` | `32a04cde` |
| Working tree before plan creation | Clean |
| Phase 17 dependency | Complete, accepted, and closed (2026-07-26) |
| Build baseline | Succeeded with 0 errors and 0 warnings |
| Architecture tests | 32 passed, 0 failed |
| Verification date | 2026-07-26 |

---

## Pre-implementation verification (M0)

- [x] Read `AGENTS.md`, `docs-rules.md`, `docs/CONVENTIONS.md`,
      `docs/DESIGN.md`, `docs/roadmap/V3.md`, and the accepted Phase 17 plan,
      TOFIX, and closeout evidence.
- [x] Verify the live checkout: branch `master`, `HEAD` at `32a04cde`, clean
      working tree.
- [x] Audit the Phase 17 delivered seams: `AgentSessionService`,
      `AgentBackendRequest`, `AgentBackendExecutionContext`, `IAgentBackend`,
      `AgentCapabilitySnapshot`, `AgentEventKind`, `IAgentActionBroker`, and
      the shipped-inert boundary.
- [x] Audit all candidate IDE context sources: workspace model, editor state,
      language diagnostics, build diagnostics, project workflow, test results,
      source control, debug session, terminal, and project context.
- [x] Confirm that no context assembly, system prompt, or multi-turn history
      infrastructure currently exists in the codebase.
- [x] Lock the phase scope, milestone dependency order, verification commands,
      stop conditions, and rollback boundaries.
- [x] Run `dotnet build Zaide.slnx --no-restore` — succeeded, 0 errors,
      0 warnings.
- [x] Run architecture tests — 32 passed, 0 failed.
- [x] Run `git diff --check` — clean.

No new library is required or authorized by M0. Any later dependency proposal
must include a focused proof, compatibility evidence, and an amendment to this
plan before adoption.

---

## Accepted M0 implementation decisions

### P18-D01: Feature ownership

Phase 18 context types live under `src/Features/Agents/` following the
existing feature-first structure. Context assembly is an Agents-owned concern
that reads from other features through their contracts. No new top-level
feature folder is created.

### P18-D02: Dependency direction

Context assembly reads from other features (Editor, Language, ProjectSystem,
SourceControl, Debugging, Terminal, Workspace) through their **Contracts** or
read-only service interfaces. It does not take dependencies on their
Presentation or Infrastructure layers.

### P18-D03: Backend-neutral design

The context policy, assembly, and manifest are backend-agnostic. Any future
backend (Phase 19 Native Harness, Phase 20 ACP) may consume the assembled
context through the same neutral boundary. No provider-specific prompt tuning
is included.

### P18-D04: Trust boundary — context disclosure

Phase 18 establishes a trust boundary **separate from Phase 17's action
permission boundary**. Context disclosure governs what IDE state is visible
to the backend. Action permission governs what the backend may do. These are
independent: enabling context does not grant action permission, and granting
action permission does not imply context disclosure.

### P18-D05: Policy model

The user-configurable context policy uses the five-level model from the
roadmap (§10.1):

| Level | Automatic context attached |
|-------|----------------------------|
| **Off** | No automatic IDE context sharing |
| **Minimal** | Failures, exceptions, and essential task state only |
| **Standard** | Active file path, relevant diagnostics, and normal execution state |
| **Detailed** | Stack frames, selected variables, richer activity, broader file context |
| **Custom** | Per-source and per-event user configuration |

The application default is deferred to M1 design and must be tuned through
implementation evidence. `Off` disables automatic injection; it does not
silently grant or deny explicit file or tool access.

### P18-D06: Policy precedence

```text
Session override
  > Agent policy assignment
  > Project policy
  > Application default
```

Phase 18 implements Application default and Session override. Agent and Project
levels are deferred to a later phase when the configuration surface justifies
their complexity.

### P18-D07: Hard exclusions

The following categories are excluded from automatic context attachment at all
policy levels unless the user explicitly selects or allows them:

- Raw terminal scrollback content
- Debug variable trees and watch expressions
- Environment variables and process secrets
- Full LSP protocol internals
- Binary file content
- File content containing detected secrets (redaction boundary)

### P18-D08: Run-scoped context

Context is assembled per run and recipient, not broadcast to every participant
merely because they share a conversation. Each run context manifest records
source, scope, version/fingerprint, redaction state, destination trust
boundary, and token budget.

### P18-D09: Shipped inert

Phase 18 ships inert. The only production backend
(`LegacyOpenAiCompatibleAgentBackend`) does not consume structured context.
The context assembly and policy infrastructure is registered and testable but
is not reachable by any production user flow. The test-only
`FakeActionRequesterBackend` (or a Phase 18 context-test double) validates
the assembly path. Production activation belongs to Phase 19 or Phase 20.

### P18-D10: No multi-turn history replay

Phase 18 does not implement multi-turn conversation history replay for the
backend. The existing single-message pipeline remains. History assembly belongs
to the first production backend phase.

### P18-D11: New library prohibition

No new NuGet dependency is authorized by M0. Any proposal must include a
focused proof, compatibility evidence, and a plan amendment.

### P18-D12: Verification baseline

All verification runs in an interactive terminal using `dotnet test
Zaide.slnx --no-build`. If fast mode fails or hangs, reproduce with the serial
fallback before treating the result as a regression. Architecture inventory
and bypass ratchets are updated for each production type addition.

---

## Scope

**Goal:** Build a backend-neutral, Zaide-owned IDE context disclosure boundary
that reads selected IDE state from existing immutable snapshots, assembles an
attributed context manifest under a visible user policy with explicit exclusion
and precedence rules, applies a token budget with deterministic truncation, and
makes the assembled context available to an admitted Agent Session run — all
shipped inert until a production backend consumes it.

The Phase 18 trust boundary is:

```text
existing IDE snapshot services
  -> context policy evaluation (level, exclusions, precedence)
  -> context assembly (read, filter, redact, budget)
  -> attributed context manifest (source, scope, version, redaction, trust)
  -> run-scoped context attachment
  -> neutral consumption boundary for backends
  -> disclosure event for audit and visibility
```

### In scope

- Typed contracts for context sources, context items, context manifests,
  policy levels, exclusion rules, and provenance metadata.
- A context assembly service that reads from existing IDE snapshot services
  (workspace, editor, diagnostics, workflow, test, source control, debug,
  project, terminal) and produces an attributed manifest.
- Policy evaluation with the five-level model, session override, and hard
  exclusions.
- Token budget with deterministic truncation per source priority.
- Privacy filtering and redaction for detected secrets and sensitive content.
- Extension of `AgentBackendRequest` (or its context record) with an optional
  context slot that backends may read.
- New `AgentEventKind` value(s) for context disclosure audit.
- New `AgentCapabilityId` value for IDE context support.
- A minimal disclosure indicator showing the user what context category and
  volume is attached to the current run.
- A minimal policy selector for session-level override.
- Architecture ratchets preventing context bypass around the policy boundary.
- Test doubles and integration tests validating assembly under each policy
  level.

### Out of scope

- Implementing or adapting a Native Harness, ACP client/server, or any other
  production backend.
- Multi-turn conversation history assembly or system prompt construction for
  the backend (Phase 19/20 concern).
- Cross-workspace context synchronization.
- Durable context storage, memory, or session resume (Phase 21 concern).
- Raw model or protocol trace disclosure (Phase 21 concern).
- Provider-specific prompt tuning, prompt engineering, or model selection.
- Context persistence across application restarts.
- Per-file or per-folder context configuration beyond the five-level policy.
- Agent-level or project-level policy assignment (deferred per P18-D06).
- Visible range or viewport state for editors (not currently tracked; deferred
  to M1 design if it proves necessary).
- Workspace mutation, file I/O, or action control (Phase 17 owns that
  boundary).
- Terminal PTY raw content exposure.
- Debug variable tree expansion beyond the `Detailed` policy boundary.
- LSP protocol internals or language server configuration.
- File system watching or change detection.
- Network, HTTP, or provider communication.

---

## Verified live facts

### Agent session and backend pipeline

| Concern | Live owner | Verified state |
|---------|------------|----------------|
| Backend request | `AgentBackendRequest` | 7 immutable properties: SessionId, RunId, ConversationId, InitiatingActorId, TargetActorId, MessageEntryId, MessageText. **No context slot.** |
| Execution context | `AgentBackendExecutionContext` | `record(AgentBackendRequest Request, IAgentActionBroker Actions)`. No context member. |
| Backend interface | `IAgentBackend.ExecuteAsync(AgentBackendExecutionContext, CancellationToken)` | Receives the thin context record. Single production implementation (`LegacyOpenAiCompatibleAgentBackend`) reads only `MessageText`. |
| HTTP body | `AgentExecutionService` | Constructs a single `{ role = "user", content = userMessage }` array. No system role, no history, no context. |
| Run creation | `AgentSessionService.BeginAdmittedRunLocked` | Creates `AgentBackendRequest` from raw `messageText`. No context assembly. |
| Capabilities | `AgentCapabilitySnapshot` / `AgentCapabilityId` | 10 capabilities defined. No context-related capability. |
| Events | `AgentEventKind` | 25 values defined. No context-related event kind. |
| Conversation store | `IConversationStore` / `AgentConversationEventProjection` | Write-only projection. History is stored for UI but never read back for backend consumption. |
| Shipped inert | `AgentSessionService.CreateExecutionContextLocked` | Only backends implementing `IAgentActionRequestCapableBackend` get a real broker. Legacy backend gets `UnavailableAgentActionBroker`. |

### IDE context sources (all available as immutable, observable snapshots)

| Source | Type | Observable via |
|--------|------|----------------|
| Workspace | `Workspace` (Documents, ActiveDocument, WorkspacePath) | Events: WorkspaceFolderChanged, DocumentOpened, DocumentClosed |
| Editor state | `EditorViewModel` (CaretLine, CaretColumn, SelectionStart/Length/Text, FilePath, IsDirty) | Reactive properties |
| Open tabs | `EditorTabViewModel.OpenTabs` (ObservableCollection\<EditorViewModel\>) | Reactive collection |
| Language diagnostics | `LanguageDiagnosticsSnapshot` (State, Diagnostics[]) | `ILanguageDiagnosticsService.Current` / `.WhenChanged` |
| Build diagnostics | `BuildDiagnosticsSnapshot` (LastOutcome, Diagnostics[]) | `IBuildDiagnosticsService.Current` / `.WhenChanged` |
| Build/test workflow | `ProjectWorkflowSnapshot` (State, ActiveOperation, LastOutcome, OutputLines[]) | `IProjectWorkflowService.Current` / `.WhenChanged` |
| Test results | `TestResultsSnapshot` (Summary, Cases[]) | `ITestResultsService.Current` / `.WhenChanged` |
| Source control | `RepositoryStatusSnapshot` (CurrentBranch, Changes[], AheadBy, BehindBy) | `ISourceControlSnapshotOrchestrator.Refresh()` |
| Debug session | `DebugSessionSnapshot` (State, StopInfo, DiagnosticOutput[]) | `IDebugSessionService.Current` / `.WhenChanged` |
| Project context | `ProjectContext` (State, SelectedProject, Candidates[]) | `IProjectContextService.Current` / `.WhenChanged` |
| Terminal | `TerminalSnapshot` (Lines[], ScrollbackLines[]) | View-layer projection |

### Context assembly gaps

| Gap | Current state | Phase 18 consequence |
|-----|---------------|----------------------|
| No context aggregate | No single type composes the above snapshots | M1 must define the context manifest contract; M2 must build the assembly service |
| No system prompt | Never constructed anywhere | Phase 18 does not build one (P18-D10); it delivers context items that a later backend may embed |
| No history replay | Conversation store is write-only from backend perspective | Deferred to Phase 19/20 |
| No viewport state | EditorView does not push scroll position to ViewModel | Deferred; M1 may add if design shows it is needed for `Standard` or `Detailed` policy |
| No document language ID | `Document` does not track grammar scope | M1 may add a lightweight `LanguageId` if needed for context source attribution |
| No token budget mechanism | No budget, truncation, or counting exists | M2 must design and implement a budget model |

---

## Locked Phase 18 contracts

These contracts are locked at M0 and may be refined (not replaced) at M1.

### Context sources and items

A context source is a typed identifier for an IDE state category (e.g.
`ActiveFile`, `LanguageDiagnostics`, `BuildResult`, `TestResult`,
`SourceControlStatus`, `DebugState`, `TerminalSummary`). Each source
produces zero or more context items with attributed provenance.

A context item carries:
- Source identifier
- Content (text or structured data)
- Scope descriptor (what it covers)
- Version or fingerprint (for staleness detection)
- Redaction state (none, partial, full)
- Estimated token count

### Context manifest

A context manifest is the assembled output for one run. It carries:
- Run correlation (SessionId, RunId, ConversationId)
- Policy level applied
- Ordered list of context items
- Total estimated token count
- Budget applied and truncation decisions recorded
- Exclusion decisions recorded
- Timestamp

### Policy

A context policy carries:
- Level (Off, Minimal, Standard, Detailed, Custom)
- Hard exclusions (always enforced regardless of level)
- Session override (nullable, takes precedence)
- Application default

### Provenance

Every context item records:
- Source service identity
- Snapshot generation or version
- Whether the source was read from a live snapshot or a cached value
- Redaction applied (boolean + reason if redacted)

### Consumption boundary

The context manifest is attached to the run through an extension of
`AgentBackendRequest` or `AgentBackendExecutionContext`. The consumption
boundary is read-only: backends may inspect the manifest but cannot modify
it or request additional context through the same channel during the run.

---

## Milestones

| Milestone | Description | Depends on | Test gate |
|-----------|-------------|------------|-----------|
| M0 | Planning acceptance: verify seams, lock scope, publish plan | — | `git diff --check` clean; build succeeds; this document reviewed and accepted by user |
| M1 | Context contracts and policy model: typed context sources, items, manifest, policy levels, exclusion rules, capability and event kinds | M0 | Unit tests for all contract types; architecture inventory ratchet updated; build clean |
| M2 | Policy evaluation and context assembly service: read from IDE snapshots, apply policy, budget, and produce attributed manifest with provenance and redaction | M1 | Unit tests per policy level with snapshot fixtures; budget enforcement tests; redaction tests; build clean |
| M3 | Run integration and consumption boundary: extend backend request with context slot, assemble context during run creation, attach manifest to execution context | M2 | Integration tests: run with each policy level produces correct manifest; inert delivery verified (legacy backend ignores context); build clean |
| M4 | Audit event and disclosure indicator: new `AgentEventKind` for context disclosure, minimal visible indicator showing context category and volume | M3 | Event emission tests; UI accessibility tests; build clean |
| M5 | Session policy override and minimal UI: session-level policy selector, override precedence, reset to application default | M3 | UI interaction tests; precedence tests (session > application); build clean |
| M6 | Closeout: adversarial review, architecture ratchets, full-suite verification, context bypass ratchets, documentation truth-sync | M4, M5 | Full fast suite; serial fallback; adversarial context leak tests; architecture inventory ratchet; `git diff --check` clean |

### Milestone dependency graph

```text
M0 (planning)
  └── M1 (contracts)
       └── M2 (assembly service)
            └── M3 (run integration)
                 ├── M4 (audit event, disclosure UI)
                 └── M5 (policy override UI)
                      └── M6 (closeout, depends on M4 + M5)
```

M4 and M5 are independent of each other and may proceed in parallel. M6
depends on both.

### Expected commit boundaries

Each milestone targets one reviewable commit with its ordinary documentation
updates included. Corrective passes may add commits within the same milestone.

---

## Verification strategy

### Per implementation milestone

| Milestone | Primary verification |
|-----------|---------------------|
| M1 | `dotnet build Zaide.slnx --no-restore` succeeds; contract unit tests pass; architecture inventory updated |
| M2 | Assembly service unit tests pass per policy level; budget and redaction tests pass |
| M3 | Integration tests verify run → context → manifest pipeline; inert delivery confirmed |
| M4 | Event emission tests pass; disclosure indicator accessible |
| M5 | Policy override UI tests pass; precedence verified |
| M6 | Full fast suite + serial fallback; adversarial tests; architecture ratchet |

### Full regression gate

```bash
# Fast suite (interactive terminal only — redirected output can reproduce
# the known parallel-runner hang)
dotnet test Zaide.slnx --no-build

# Serial fallback (if fast mode fails or hangs)
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
```

### Required test layers

| Layer | Purpose |
|-------|---------|
| Contract tests | Context source, item, manifest, and policy type correctness and immutability |
| Assembly service tests | Per-policy-level assembly with deterministic snapshot fixtures |
| Budget and truncation tests | Token counting, deterministic truncation order, boundary conditions |
| Redaction tests | Secret detection, content filtering, exclusion enforcement |
| Integration tests | End-to-end run → context → manifest → consumption boundary |
| Inert delivery tests | Legacy backend ignores context; no context reaches production user flow |
| Bypass ratchet tests | Context assembly cannot be reached without going through the policy boundary |
| Adversarial tests (M6) | Context leak when policy is Off; budget overflow; exclusion bypass; redaction bypass |

---

## Limitations (by design)

- **No multi-turn history:** The backend receives a single user message plus
  optional context items. Conversation history is not replayed. This is a
  Phase 19/20 concern.
- **No system prompt construction:** Phase 18 delivers structured context
  items. How a backend embeds them (system role, tool result, metadata) is
  backend-specific and deferred.
- **Inert in production:** No production backend consumes the context. The
  infrastructure is testable but unreachable by a real user.
- **No persistent policy:** Policy state is in-memory for the application
  lifetime. Persistence belongs to a later phase.
- **No agent/project policy:** Only application default and session override
  are implemented. Agent-level and project-level policy assignment are
  deferred.
- **No viewport state:** Editor visible range is not tracked. If `Detailed`
  policy requires it, M1 design must decide whether to add it.
- **Redaction is best-effort:** Secret detection uses pattern matching, not a
  guaranteed filter. The disclosure indicator must communicate this
  limitation.
- **Token budget is approximate:** Token counting is heuristic (character-based
  or word-based). Exact model-specific tokenization is out of scope.

---

## Stop conditions

Stop work and ask the user if any of the following occur:

1. **Verification failure:** `dotnet build` or `dotnet test` fails and the
   root cause is a Phase 18 change that cannot be fixed within the milestone
   scope.
2. **Material scope conflict:** A milestone requires work that belongs to
   Phase 17 (already closed), Phase 19/20, or Phase 21.
3. **Architecture ratchet violation:** A new type or dependency weakens an
   existing architecture baseline and cannot be justified within Phase 18
   scope.
4. **External side effect:** Context assembly requires network I/O, file
   system mutation, or process execution beyond reading snapshots.
5. **Destructive action:** A proposed change removes or modifies Phase 17
   contracts, events, or types in a way that breaks the shipped-inert
   boundary.
6. **Open decision:** A design choice materially changes the milestone
   sequence, trust model, or delivery boundary and has not been accepted in
   this plan.
7. **New dependency required:** A library or tool not listed in this plan is
   needed to complete a milestone.
8. **Phase 19/20 overlap:** Context consumption or prompt engineering for a
   specific backend becomes necessary to verify Phase 18.

---

## Exit conditions

- [ ] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] All milestone test gates pass
- [ ] Full fast suite passes: `dotnet test Zaide.slnx --no-build`
- [ ] Serial fallback passes (if fast mode has known flake)
- [ ] Architecture inventory and bypass ratchets updated and passing
- [ ] Context policy produces correct manifests at each level (Off, Minimal,
      Standard, Detailed) with test evidence
- [ ] Hard exclusions enforced at all policy levels with test evidence
- [ ] Token budget enforced with deterministic truncation and test evidence
- [ ] Redaction boundary tested with representative secret patterns
- [ ] Inert delivery confirmed: legacy backend receives no context
- [ ] Disclosure indicator accessible and truthful
- [ ] Session policy override works with correct precedence
- [ ] `git diff --check` clean
- [ ] No Phase 19/20 work implemented
- [ ] Documentation truth-sync complete

---

## Rollback plan

Phase 18 is documentation-only at M0. If M0 is rejected, this file is deleted
and no code changes are reverted.

If a later milestone implementation is reverted, the rollback target is the
last known-good commit before Phase 18 implementation began. All Phase 18
production and test files are additive; reverting removes them without
affecting Phase 17 contracts.

- **Revert to:** `32a04cde` (Phase 17 accepted closeout)
- **Files to remove:** All files created under Phase 18 milestones
- **Architecture ratchets:** Revert to Phase 17 closeout baselines
