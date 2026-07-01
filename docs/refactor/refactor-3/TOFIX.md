# Refactor 3: TOFIX

**Status:** Agent-first concept proven and suitable as the new baseline, but not
yet stabilized.

---

## High Priority

### H1: Bottom panel no longer spans the full workspace
- **File:** `src/MainWindow.axaml.cs`
- **Issue:** The terminal/log panel currently starts at the Townhall column and
  spans only Townhall + Editor. This weakens the "shared runtime/log surface"
  model and makes the bottom area feel attached to the content region instead of
  the whole workspace.
- **Expected fix:** Re-evaluate bottom-row span and alignment so the panel reads
  as a shared operational surface for the workspace, not as an editor-adjacent
  utility.
- **Status:** ⬜ Not started

### H2: Townhall view uses manual refresh wiring instead of reactive bindings
- **Files:** `src/Views/TownhallView.cs`, `src/Views/TownhallChannelPanel.cs`, `src/Views/TownhallChatPanel.cs`, `src/Views/TownhallPeoplePanel.cs`, `src/Views/TownhallInputArea.cs`
- **Issue:** The current concept implementation pushes values into subviews and
  calls `Refresh()` manually after state changes. It works for the spike, but it
  is brittle and does not follow the repo's normal ReactiveUI lifecycle/binding
  patterns.
- **Expected fix:** Replace manual synchronization with explicit reactive
  bindings and predictable view lifecycle wiring.
- **Status:** ⬜ Not started

### H3: Refactor-3 docs no longer match the implemented shell exactly
- **Files:** `docs/refactor/refactor-3/IMPLEMENTATION_PLAN.md`, `docs/architecture/OVERVIEW.md`, `README.md`
- **Issue:** The branch implementation includes a `NavBar` plus a mode-switched
  left panel, while the written plan still describes the simpler
  `file tree | townhall | editor` shell as the implementation target. The docs
  capture the direction, but not the exact current shape.
- **Expected fix:** Decide whether `NavBar + mode-switched left panel` is
  official refactor-3 scope. Then update docs to match the real baseline.
- **Status:** ⬜ Not started

---

## Medium Priority

### M1: Townhall state is still concept-seeded
- **Files:** `src/ViewModels/TownhallViewModel.cs`, `src/Models/TownhallState.cs`
- **Issue:** The current Townhall state is still built around seeded demo data
  and imperative concept behavior. This was useful for proving the direction,
  but it is not yet a clear transitional foundation for later agent work.
- **Expected fix:** Separate concept seed data from the actual state model and
  clarify which parts are temporary scaffolding.
- **Status:** ⬜ Not started

### M2: Editor/Townhall relationship is visually implied but not structurally clear
- **Files:** `src/MainWindow.axaml.cs`, `src/Views/EditorTabBar.cs`, `src/Views/TownhallView.cs`
- **Issue:** The new shell already reads agent-first, but the editor-to-townhall
  relationship is still mostly visual. The implementation needs a clearer
  intentional bridge so the editor feels like a focused execution surface tied
  to the active workspace, not just "the panel on the right."
- **Expected fix:** Add or refine lightweight structural cues without turning
  this into full bidirectional integration.
- **Status:** ⬜ Not started

### M3: Focused UX regression pass still needed
- **Files:** `src/MainWindow.axaml.cs`, `src/Views/*`, related view models
- **Issue:** Build/tests are green, but the new shell changes visual hierarchy,
  panel ownership, and minimum-width behavior enough that targeted manual checks
  are still required.
- **Expected fix:** Run a focused regression pass for file tree, editor, bottom
  panel, sizing, and Townhall visual hierarchy. Record concrete failures rather
  than vague polish requests.
- **Status:** ⬜ Not started

---

## Low Priority

### L1: Left-side weight is acknowledged but intentionally deferred
- **Files:** layout/system-level concern
- **Issue:** The current shell is left-heavy because it now includes navigation
  rail, mode-switched left panel, Townhall, and editor. This is real, but it is
  a future workspace-composition problem, not a reason to undo the pivot.
- **Expected fix:** Handle later with panel switching, window switching, or more
  flexible workspace composition.
- **Status:** ⏸️ Deferred by design

---

## Merge Guidance

This TOFIX assumes:

- the branch is merged as the new product baseline
- refactor-3 is **not** declared complete at merge time
- follow-up work happens as stabilization and cleanup, not as a rollback to the
  old editor-first shell

---

*Last updated: 2026-07-01*
