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
├── phases/              # One folder per phase
│   └── phase-N/
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
| Starting a new phase | Create `docs/phases/phase-N/IMPLEMENTATION_PLAN.md` |
| Finding a code quality issue during review | Add to `docs/phases/phase-N/TOFIX.md` |
| Fixing a convention or adopting a new one | Update `docs/CONVENTIONS.md` |
| Changing UI design rules or patterns | Update `docs/DESIGN.md` |
| Bug not fixed in 2 attempts | Create issue in `docs/issues/open/` |

---

## 3. Phase Planning Convention

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

---

## 4. TOFIX Convention

Each phase has a `TOFIX.md` that tracks code quality issues found during review.

- **Before starting work on a phase**, read its `TOFIX.md` and address open items first.
- **After a review session**, add new findings with a clear description and fix hint.
- **When an item is fixed**, mark it `[x]`.
- **Do not move to the next phase** while any `TOFIX.md` item is unchecked.

---

## 5. Issue Tracking

If a bug fix is not obvious after 2 attempts, create an issue file immediately.

**File:** `docs/issues/open/ISSUE-###-short-name.md`

**Must contain:**
1. Description — what is wrong, expected vs actual
2. Debug Log — every attempt: hypothesis → action → result → error output
3. Resolution — root cause, fix (filled when done; move file to `closed/`)

**After creating/closing an issue**, update `docs/issues/INDEX.md`.

---

## 6. Library Catalog (`docs/LIBRARIES.md`)

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

## 7. Architecture Docs

Keep `docs/architecture/OVERVIEW.md` as the canonical description of Zaide's
two layers:

1. **IDE Layer** — editor, file tree, tabs, terminal, git, build
2. **Agent Layer** — agent-to-agent communication, townhall, routing

Update architecture docs when:
- A new subsystem is introduced
- An interface contract changes
- DI registration patterns change

---

## 8. Commit Messages

```
area: short imperative summary
```

Examples: `editor: add tab switching`, `agents: implement townhall logger`, `docs: add phase-1 plan`

---

## 9. Decision Checkpoints (Stop and Ask User)

Stop work and ask when:
1. Architecture change — modifying interfaces or DI setup
2. New dependency — adding a NuGet package not in `LIBRARIES.md`
3. Phase boundary — about to start a new phase
4. Build failure — can't fix in 2 attempts
5. Convention conflict — existing code violates `CONVENTIONS.md`

---

## 10. Lessons from Aero

These patterns are borrowed from the `cenoda/aero` project and proven in practice:

- **Incremental development** — one milestone at a time, test after each change.
- **Validate before implementing** — create a minimal proof-of-concept before full implementation.
- **Know when to walk away** — two failed attempts at a library = the library is the problem. Pivot.
- **No silent fixes** — never apply a fix without understanding why it works.
- **Debug-friendly code** — add logging from the start, not just when debugging.
