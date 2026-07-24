---
inclusion: always
---

# Documentation Rules

All code changes must keep documentation in sync. Docs live in `docs/` and are
the single source of truth for how the project works. If docs and code disagree,
the code wins — but the docs must be fixed immediately.

---

## 1. Documentation Structure

```
docs/
├── architecture/        # How Zaide is designed (high-level diagrams, subsystems)
│   └── OVERVIEW.md      # Two-layer architecture, agent-to-agent model
├── roadmap/
│   ├── PHASES.md        # Frozen original V1 roadmap record
│   └── VN.md            # Active or later version roadmap, e.g. V2.md
├── phases/              # Versioned phase plans
│   ├── README.md          # Version index and archive policy
│   └── vN/
│       └── phase-N/
│           ├── IMPLEMENTATION_PLAN.md   # Plan before coding (use template)
│           └── TOFIX.md                 # Current work board and phase report
├── refactor/            # Foundation-level refactoring (structural, not feature)
│   └── refactor-N/
│       ├── IMPLEMENTATION_PLAN.md   # Plan before coding (use template)
│       └── TOFIX.md                 # Current work board and refactor report
├── issues/
│   ├── open/            # Active issues (ISSUE-###-short-name.md)
│   ├── closed/          # Resolved issues (moved here)
│   ├── templates/       # Issue file template
│   └── INDEX.md         # Issue index table
├── deferred/
│   ├── open/            # Findings intentionally deferred for later deep work
│   ├── closed/          # Deferred findings resolved or deliberately dropped
│   ├── templates/       # Deferred-finding template
│   └── INDEX.md         # Deferred-finding index table
├── spec/
│   ├── templates/       # Spec file template
│   └── INDEX.md         # Spec index table
├── CONVENTIONS.md       # Coding conventions
├── DESIGN.md            # UI aesthetic rules (glass, animation, spacing, XAML policy)
└── LIBRARIES.md         # NuGet package catalog
```

Create these folders/files when first needed — not all at once.

---

## 2. When to Update Docs

| Trigger | Update |
|---------|--------|
| Defining a successor roadmap | Create `docs/roadmap/VN.md`; preserve completed roadmap records |
| Completing a planned implementation item | Update the current phase or refactor `TOFIX.md`; update the roadmap only if the phase outcome, order, or dependency changed |
| Adding a NuGet package | Add entry to `docs/LIBRARIES.md` |
| Changing architecture (DI, interfaces, new subsystem) | Update `docs/architecture/` |
| Starting a new phase | Create `docs/phases/vN/phase-N/IMPLEMENTATION_PLAN.md` in the current roadmap version |
| Reporting current phase work, a review finding, a blocker, or the next task | Update `docs/phases/vN/phase-N/TOFIX.md` |
| Starting a new refactor | Create `docs/refactor/refactor-N/IMPLEMENTATION_PLAN.md` |
| Completing a refactor implementation item | Update the current refactor `TOFIX.md` |
| Reporting current refactor work, a review finding, a blocker, or the next task | Update `docs/refactor/refactor-N/TOFIX.md` |
| Fixing a convention or adopting a new one | Update `docs/CONVENTIONS.md` |
| Changing UI design rules or patterns | Update `docs/DESIGN.md` |
| Bug not fixed in 2 attempts | Create issue in `docs/issues/open/` |
| Reverting a phase implementation | Create `docs/phases/vN/phase-N/REVERT_LOG.md` |
| Reverting a refactor implementation | Create `docs/refactor/refactor-N/REVERT_LOG.md` |

---

## 3. Feature Phase Planning Convention

This section applies to feature phases (`phases/vN/phase-N/`) only. For structural refactoring, see §4.

Phase plans are grouped by roadmap version. Completed roadmap versions remain
available as historical records. Continue the global phase numbering across
roadmap versions so references such as "Phase 7" remain unambiguous.

`docs/roadmap/PHASES.md` is the frozen legacy V1 roadmap. Successor roadmaps use
explicit version names such as `docs/roadmap/V2.md`. Do not overwrite or
repurpose a completed roadmap file when defining a successor.

Every phase gets an `IMPLEMENTATION_PLAN.md` before coding starts.

### Document ownership

Each document has one job. Do not copy a current status, policy value, or task
list into several documents merely for visibility.

| Document | Owns | Update when |
|----------|------|-------------|
| Roadmap | Phase outcomes, order, dependencies, and product direction | A phase-level decision changes |
| `IMPLEMENTATION_PLAN.md` | Milestone goals, implementation scope, and verification | The phase plan is created or its approved implementation boundary changes |
| `TOFIX.md` | Current work, findings, blockers, completed work, and the next task | Normal work reporting or review changes the current phase/refactor state |
| Evidence or revert record | Historical facts needed to support a completed decision | A durable external, manual, security, or rollback record is required |

Completed phase and refactor documents remain in their versioned folders as
historical records. Do not delete or rewrite that history merely because the
active work has moved on.

### Phase, sub-phase, and milestone boundaries

These terms are not interchangeable:

- **Phase** — a roadmap-level outcome. The roadmap defines its goal, order,
  and dependencies.
- **Sub-phase** — a coherent feature concern inside a phase, used when the
  phase is too large to design, test, and implement as one unit. It is not a
  label for a verification pass or a documentation-only split.
- **Milestone** — a meaningful implementation outcome inside a plan. The plan
  defines its goal, scope, test gate, and completion condition.

Every independently implemented phase or sub-phase starts with an **M0** plan
that verifies live seams, locks its scope and boundaries, identifies milestone
goals and dependencies, and names concrete verification commands. M0 may be
documentation-only. Preserve historical numbering in completed plans.

If a milestone cannot be designed, tested, and implemented as one coherent
outcome, divide the parent phase into sub-phases around the actual feature
concerns before implementation. Do not create slices solely for status,
evidence, or commit mechanics.

### Rules:
1. **Verify against live code** — design docs go stale. Check `src/` before claiming a seam exists.
2. **Verify libraries before depending on them** — confirm API actually works with your stack version.
3. **Build for this phase only (YAGNI)** — no abstractions for a future phase's need.
4. **Prefer documented limitations over edge-case code** — keep a "Phase N Limitations" section.
5. **Make gates verifiable** — entry/exit conditions must be checkable commands, not vibes.
6. **One coherent outcome per milestone** — each milestone should be independently testable and meaningful to review.

### Implementation Plan Template:

```markdown
# Phase N: [Title] — Implementation Plan

## Pre-Implementation Verification
- [ ] Live seams verified against current code
- [ ] Minimal proof-of-concept works when the plan depends on an unproven API or library
- [ ] Dependencies verified compatible

## Scope
**Goal:** What we are building.
**Boundaries:** What we are NOT building.

## Milestones (Incremental)

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate: [conditions] | [command] |
| M1 | ... | ... |
| M2 | ... | ... |

## Limitations (by design)
- ...

## Exit Conditions
- [ ] Build succeeds: `dotnet build`
- [ ] Manual verification works
- [ ] No regressions

## Rollback Plan
- Commit hash to revert to: ...
```

### Revert Log Template

When a phase implementation is reverted (structural reset, not a bug fix),
create `docs/phases/vN/phase-N/REVERT_LOG.md`:

```markdown
# Phase N: Revert Log

## What Was Reverted

- **Reverted from:** `<commit-hash>` (last bad implementation commit)
- **Reverted to:** `<commit-hash>` (last known-good commit)
- **Commits discarded:** `<hash-1>`, `<hash-2>`, ...
- **Files removed:**
  - `src/...`
  - `tests/...`

## Root Cause

_Why was the implementation fundamentally broken — not just bugs, but why
patching forward was the wrong call._

1. ...
2. ...

## Rules Added

_What rules were added to `docs-rules.md` (or other docs) to prevent this
class of failure in the future._

- `docs-rules.md` §N — ...

## Revert Commit

`<hash>` — `git reset --hard <known-good-hash>`
```

This file serves as a permanent record. Any future agent attempting this
phase must read it first.

---

## 4. Refactor Planning Convention

Refactors live in `docs/refactor/refactor-N/`. They follow the same
incremental structure as feature phases, but differ in scope and boundaries:

- **No new features** — refactors restructure existing code without changing
  observable behavior (unless a minor API surface change is required).
- **Exit condition** always includes a regression gate: all existing tests
  must pass before the refactor is complete.
- **Template is identical** to the Feature Phase template in §3, but the
  title line reads `Refactor N: [Title] — Implementation Plan`.

### Refactor families

When one structural program contains multiple independently verifiable
refactors with different risk, rollback, and acceptance boundaries, use a
decimal refactor family such as `refactor-6.1`, `refactor-6.2`, and
`refactor-6.3`.

- An optional umbrella `docs/refactor/refactor-6/BRIEF.md` may record shared
  order, boundaries, and non-goals. A brief is not an implementation plan and
  does not authorize code changes.
- Every independently implemented family member owns its own
  `IMPLEMENTATION_PLAN.md`, M0, milestones, verification gates, commit
  boundaries, and rollback plan.
- A family member is a refactor, not a feature sub-phase and not a milestone.
- If one milestone is still too large, split it into milestone slices such as
  `M2a`/`M2b`; do not create another decimal refactor merely to rename an
  oversized milestone.
- Preserve historical refactor identifiers and reports. Do not repurpose an
  earlier number for a new structural program.

### Rules (from §3, with refactor-specific notes)

1. **Verify against live code** — src/ before claim.
2. **No scope creep** — if a feature is missing and the refactor reveals it,
   stop, file it as a separate issue, and finish the refactor first.
3. **Build for this refactor only (YAGNI)** — don't over-abstract in
   anticipation of a future refactor.
4. **Prefer documented limitations over edge-case code** — keep a
   "Refactor N Limitations" section.
5. **Make gates verifiable** — entry/exit conditions must be checkable
   commands, not vibes.
6. **One concern per milestone** — each milestone independently testable.
7. **All existing tests must pass** — regression is the top priority.
8. **M0 comes first** — every independently implemented refactor or refactor-
   family member must verify live boundaries, scope, dependencies, concrete
   test commands, and rollback points before structural edits begin.

### Revert Log

Same format as §3 Revert Log Template, but saved to
`docs/refactor/refactor-N/REVERT_LOG.md`.

---

## 5. TOFIX Convention

Each phase or refactor has a `TOFIX.md`. It is the current work board and the
normal work-report surface for that phase or refactor, not only a list of code
quality defects.

- **Before starting work**, read the local `TOFIX.md` for the current state,
  blockers, and next task.
- **After work or review**, update it with completed work, findings, blockers,
  and the next task in concise language.
- **When an item is fixed**, mark it `[x]` or move it to a short completed
  section. Keep only useful recent history there; Git and the phase documents
  preserve the full record.
- **Unchecked items are not automatically global blockers.** State whether an
  item blocks the current phase, is intentionally deferred, or belongs to a
  later phase.

---

## 6. Issue Tracking

If a bug fix is not obvious after 2 attempts, create an issue file immediately.

**File:** `docs/issues/open/ISSUE-###-short-name.md`

**Must contain:**
1. Description — what is wrong, expected vs actual
2. Debug Log — every attempt: hypothesis → action → result → error output
3. Resolution — root cause, fix (filled when done; move file to `closed/`)

**After creating/closing an issue**, update `docs/issues/INDEX.md`.

---

## 7. Deferred Findings

Use `docs/deferred/` for observations discovered during testing or exploration
that deserve a deeper future fix but are intentionally out of the current
scope. This is a lightweight capture mechanism, not a claim that the item is
currently blocking or ready for implementation.

- Start from `docs/deferred/templates/FINDING-template.md`.
- Give each finding a stable `DF-###` identifier and add it to `INDEX.md`.
- Record evidence and the current workaround or limitation, but do not invent
  a root cause before it has been investigated.
- Move the file to `closed/` only when the finding is fixed, explicitly dropped,
  or superseded by a phase/issue; update the index at the same time.
- If investigation has already taken two unsuccessful fix attempts, promote it
  to `docs/issues/open/` instead of keeping it only as a deferred finding.

## 8. Library Catalog (`docs/LIBRARIES.md`)

Before implementing non-trivial functionality, check if a library exists.

Every library entry must have:
- **What It Does** — plain-English one-liner
- **Why You Want It** — what you'd build manually without it
- **Phase** — when it becomes relevant

**Rule:** If a catalogued library covers 80%+ of the need, use it. Only build custom when:
- No library exists
- The library is abandoned/broken on your stack version
- The functionality is trivial (< 50 lines)

---

## 9. Architecture Docs

Keep `docs/architecture/OVERVIEW.md` as the canonical description of Zaide's
two layers:

1. **IDE Layer** — editor, file tree, tabs, terminal, git, build
2. **Agent Layer** — agent-to-agent communication, townhall, routing

Update architecture docs when:
- A new subsystem is introduced
- An interface contract changes
- DI registration patterns change

---

## 10. Commit Messages

```
area: short imperative summary
```

Examples: `editor: add tab switching`, `agents: implement townhall logger`, `docs: add phase-1 plan`

---

## 11. Decision Checkpoints (Stop and Ask User)

Stop work and ask when:
1. Architecture change — modifying interfaces or DI setup
2. New dependency — adding a NuGet package not in `LIBRARIES.md`
3. Phase boundary — about to start a new phase or refactor
4. Failed verification that cannot be fixed without a material plan change
5. External side effect — credentials, paid services, upstream acquisition or
   execution, or network egress
6. Destructive or irreversible action
7. Convention conflict — existing code violates `CONVENTIONS.md`
8. Open decision that materially changes the work

Do not invent extra approval gates for routine milestone progression, status
wording, or documentation bookkeeping. Do not require a separate human
"understanding confirmation" before continuing planned work.

---

## 12. Lessons from Aero

These patterns are borrowed from the `cenoda/aero` project and proven in practice:

- **Incremental development** — one milestone at a time, test after each change.
- **Validate before implementing** — create a minimal proof-of-concept before full implementation.
- **Know when to walk away** — two failed attempts at a library = the library is the problem. Pivot.
- **No silent fixes** — never apply a fix without understanding why it works.
- **Debug-friendly code** — add logging from the start, not just when debugging.

---

## 13. Hard Rules (from Phase 2 revert)

These are enforced by code review — no exceptions. They exist because Phase 2
was implemented once, found fundamentally broken, and reverted at commit
`0971113`. The second attempt must not repeat these mistakes.

### 12a. ViewModels must never reference Views

**Forbidden in any ViewModel:**

| Forbidden | Because |
|-----------|---------|
| `Func<T>` or `Action<T>` whose T is a UI type | ViewModel now knows the View layer exists |
| `Func<Window>`, `Func<Control>` | ViewModel can't reason about windows |
| Setting `Func<>` callbacks from the View | Inverts MVVM — View should subscribe to ViewModel, not inject callbacks |
| Any `using` of `Avalonia.Controls` | ViewModel should only reference `ReactiveUI`, `System`, and Models/Services |

**The correct pattern — `Interaction<TInput, TOutput>`:**

```csharp
// ViewModel — pure, no UI knowledge
public Interaction<EditorViewModel, bool> ConfirmClose { get; } = new();

// Close command asks the interaction, doesn't know about dialogs
CloseTabCommand = ReactiveCommand.CreateFromTask(async tab =>
{
    if (tab.IsDirty)
    {
        var shouldSave = await ConfirmClose.Handle(tab);
        if (shouldSave) tab.SaveCommand.Execute().Subscribe();
    }
    OpenTabs.Remove(tab);
});
```

```csharp
// View — subscribes to the interaction, owns the dialog
this.WhenActivated(d =>
{
    d.Add(ViewModel.ConfirmClose.RegisterHandler(async ctx =>
    {
        var dialog = new UnsavedDialog();  // its own ReactiveWindow
        var result = await dialog.ShowDialog<bool>(this);
        ctx.SetOutput(result);
    }));
});
```

### 12b. Every `.Subscribe()` inside `WhenActivated` must use `d.Add()`

```csharp
// WRONG — leaks the subscription
this.WhenAnyValue(x => x.ViewModel)
    .Subscribe(vm => { /* ... */ });

// CORRECT — disposed on deactivation
this.WhenActivated(d =>
{
    d.Add(this.WhenAnyValue(x => x.ViewModel)
        .Subscribe(vm => { /* ... */ }));
});
```

This applies to `Execute().Subscribe()` calls too. If you can't hook it into
`d.Add()`, use `Observable.StartAsync` or reconsider the pattern.

### 12c. One binding pattern per data flow

Don't mix approaches for the same data. If two-way `Bind` creates a feedback
loop, document *why* in a comment and pick a single alternative — don't add
both an event handler AND a `WhenAnyValue` for the same property.

### 12d. No `dynamic` in production code

AvaloniaEdit's `InstallTextMate` returns a concrete type. Cast it. If the type
is internal, wrap it in a typed helper. `dynamic` disables compiler checking
and tells future agents that `dynamic` is acceptable.

### 12e. Dialogs are their own ReactiveWindow

`MainWindow.axaml.cs` must not grow 40-line inline dialog factories. Every
dialog gets its own file (View + ViewModel if needed), even if it's simple.

### 12f. Commit coherent outcomes

Prefer one commit for one coherent implementation outcome. Include the normal
plan and `TOFIX.md` updates with its code and tests. Do not create separate
commits solely for status synchronization, evidence wording, or a policy value
that belongs to the same implementation outcome. Use a docs-only commit when
the documentation change is independently meaningful and has no implementation
change to accompany it.

### 12g. Plan-required tests must exist

If `IMPLEMENTATION_PLAN.md` says `tests/.../EditorTabViewModelTests.cs` must
exist, the file must exist with the listed test methods before the milestone
is marked done. Missing tests = incomplete milestone.

### 12h. All file I/O has error handling

`File.WriteAllText` and `File.ReadAllText` must be wrapped in try/catch.
Unhandled I/O exceptions crash silently and confuse future agents debugging
"why doesn't save work?"

### 12i. Revert early when code is bad

Two commits of bad implementation is cheap to revert. Ten is not. If you realize
the code has fundamental structural problems (not just bugs), revert to the last
known-good commit and re-implement correctly. Patching bad architecture produces
worse architecture.

### 12j. Verify exit conditions concretely

Before marking a milestone `[x]` or a phase complete, run the exact verification
commands and check that every file the plan says should exist actually exists.
"Build passes and tests pass" is not enough — the plan may list test files
that were never created.
