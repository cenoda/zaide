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
│   └── PHASES.md        # Ordered phase checklist (current progress)
├── phases/              # Versioned phase plans
│   ├── README.md          # Version index and archive policy
│   └── vN/
│       └── phase-N/
│           ├── IMPLEMENTATION_PLAN.md   # Plan before coding (use template)
│           └── TOFIX.md                 # Code quality issues found in review
├── refactor/            # Foundation-level refactoring (structural, not feature)
│   └── refactor-N/
│       ├── IMPLEMENTATION_PLAN.md   # Plan before coding (use template)
│       └── TOFIX.md                 # Code quality issues found in review
├── issues/
│   ├── open/            # Active issues (ISSUE-###-short-name.md)
│   ├── closed/          # Resolved issues (moved here)
│   ├── templates/       # Issue file template
│   └── INDEX.md         # Issue index table
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
| Completing a phase checklist item | Mark `[x]` in `docs/roadmap/PHASES.md` |
| Adding a NuGet package | Add entry to `docs/LIBRARIES.md` |
| Changing architecture (DI, interfaces, new subsystem) | Update `docs/architecture/` |
| Starting a new phase | Create `docs/phases/vN/phase-N/IMPLEMENTATION_PLAN.md` in the current roadmap version |
| Finding a code quality issue during review | Add to `docs/phases/vN/phase-N/TOFIX.md` |
| Starting a new refactor | Create `docs/refactor/refactor-N/IMPLEMENTATION_PLAN.md` |
| Completing a refactor milestone | Mark `[x]` in refactor plan |
| Finding a code quality issue during refactor | Add to `docs/refactor/refactor-N/TOFIX.md` |
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

Every phase gets an `IMPLEMENTATION_PLAN.md` before coding starts.

### Rules:
1. **Verify against live code** — design docs go stale. Check `src/` before claiming a seam exists.
2. **Verify libraries before depending on them** — confirm API actually works with your stack version.
3. **Build for this phase only (YAGNI)** — no abstractions for a future phase's need.
4. **Prefer documented limitations over edge-case code** — keep a "Phase N Limitations" section.
5. **Make gates verifiable** — entry/exit conditions must be checkable commands, not vibes.
6. **One concern per milestone** — each milestone should be independently testable.

### Implementation Plan Template:

```markdown
# Phase N: [Title] — Implementation Plan

## Pre-Implementation Verification
- [ ] Library/tool understanding confirmed
- [ ] Minimal proof-of-concept works
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

### Revert Log

Same format as §3 Revert Log Template, but saved to
`docs/refactor/refactor-N/REVERT_LOG.md`.

---

## 5. TOFIX Convention

Each phase or refactor has a `TOFIX.md` that tracks code quality issues
found during review.

- **Before starting work on a phase**, read its `TOFIX.md` and address open items first.
- **After a review session**, add new findings with a clear description and fix hint.
- **When an item is fixed**, mark it `[x]`.
- **Do not move to the next phase** while any `TOFIX.md` item is unchecked.

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

## 7. Library Catalog (`docs/LIBRARIES.md`)

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

## 8. Architecture Docs

Keep `docs/architecture/OVERVIEW.md` as the canonical description of Zaide's
two layers:

1. **IDE Layer** — editor, file tree, tabs, terminal, git, build
2. **Agent Layer** — agent-to-agent communication, townhall, routing

Update architecture docs when:
- A new subsystem is introduced
- An interface contract changes
- DI registration patterns change

---

## 9. Commit Messages

```
area: short imperative summary
```

Examples: `editor: add tab switching`, `agents: implement townhall logger`, `docs: add phase-1 plan`

---

## 10. Decision Checkpoints (Stop and Ask User)

Stop work and ask when:
1. Architecture change — modifying interfaces or DI setup
2. New dependency — adding a NuGet package not in `LIBRARIES.md`
3. Phase boundary — about to start a new phase or refactor
4. Build failure — can't fix in 2 attempts
5. Convention conflict — existing code violates `CONVENTIONS.md`

---

## 11. Lessons from Aero

These patterns are borrowed from the `cenoda/aero` project and proven in practice:

- **Incremental development** — one milestone at a time, test after each change.
- **Validate before implementing** — create a minimal proof-of-concept before full implementation.
- **Know when to walk away** — two failed attempts at a library = the library is the problem. Pivot.
- **No silent fixes** — never apply a fix without understanding why it works.
- **Debug-friendly code** — add logging from the start, not just when debugging.

---

## 12. Hard Rules (from Phase 2 revert)

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

### 12f. Every milestone gets its own commit

Never batch milestones into one commit (e.g., `M1-M3` or `M4-M6`). Each
milestone is one commit with its own tests. If M4 is sloppy, you revert only
M4–M6, not everything.

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
