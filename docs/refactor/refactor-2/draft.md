# Refactor 2: Layer Boundary Cleanup — Draft

## Intent

This draft documents the rationale for a layer boundary cleanup pass that was
prepared during Phase 3 completion. The actual implementation plan is in
`IMPLEMENTATION_PLAN.md`.

The current repository keeps `Models`, `Services`, `ViewModels`, and `Views`
inside a single project: `src/Zaide.csproj`.

A future split into multiple projects may become useful, but the current
refactor is intentionally limited to boundary cleanup — not a multi-project
split.

## Current Decision

Phase 3 is complete. Phase 4 (agent panels, Townhall, Router) is next.
The boundary cleanup in `IMPLEMENTATION_PLAN.md` is a preparation pass that
should be evaluated before starting Phase 4, since Phase 4 work is likely to
increase coupling pressure.

## Why Not A Full Split Now

- The codebase is still small enough for a single-project structure.
- A project split now would create churn in DI, tests, namespaces, and docs.
- The real boundaries will be clearer after Phase 4 agent work begins.
- The cleanup pass removes the most obvious cross-layer violations without
  the overhead of a full project split.

## Best Timing

The cleanup pass should be done before starting Phase 4, or at the latest
during early Phase 4 when coupling pressure first becomes noticeable.

That is the point where Townhall, Agent Panels, and Router work are likely to
increase coupling pressure enough to justify the boundary cleanup.

## Likely Target Shape

- `Zaide.Core`
  - Models
  - Value objects
  - Pure interfaces
  - No Avalonia, no ReactiveUI, no filesystem code
- `Zaide.Application`
  - Use-case coordination
  - Non-UI workflow services
  - Document/workspace orchestration if it grows beyond simple models
- `Zaide.Infrastructure`
  - File system access
  - Persistence
  - External integrations
- `Zaide.UI`
  - Avalonia views
  - ViewModels
  - UI composition

## Trigger Conditions

Move this from draft to a real `IMPLEMENTATION_PLAN.md` when one or more of the
following become true:

- Build/test time starts becoming a real productivity problem.
- Agent/Townhall/Terminal work causes messy cross-layer dependencies.
- Models or services begin referencing UI concerns more often.
- Test setup becomes awkward because too much logic stays inside UI-facing
  classes.

## Watchlist After Phase 3.5

Before starting Phase 4, re-check whether these concerns are still contained:

- `MainWindow` owns too much UI composition or command wiring.
- Status/error reporting is still ad hoc instead of routed through a clear
  app-level surface.
- Workspace/document lifecycle needs a stronger home before Townhall and agent
  actions depend on it.
- Terminal, editor, file tree, and future agent panels need shared command/focus
  conventions.

## First Migration Targets

When this refactor starts, check these areas first:

- `src/Models/Document.cs`
  - The model currently knows about `IFileService`.
- `src/Services/FileTreeService.cs`
  - Pure tree logic and file-system watching likely want different homes.
- `src/ViewModels/*`
  - Watch for workflow logic that should move below the UI layer.

## Non-Goals For The Future Refactor

- No feature work mixed into the split.
- No speculative plugin architecture expansion.
- No large API redesign unless the current structure truly blocks the split.
