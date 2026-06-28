# Refactor 2: Project Boundary Split — Draft

## Intent

This is a reminder draft for a future structural refactor.

The current repository keeps `Models`, `Services`, `ViewModels`, and `Views`
inside a single project: `src/Zaide.csproj`.

A future split into multiple projects may become useful, but it should not be
started yet just because the idea sounds cleaner on paper.

## Current Decision

Do not start the multi-project split during the current Phase 2 / Phase 2.1
window.

Prefer a smaller preparation pass first:

- Remove service dependencies from model types where possible.
- Keep dependency direction clean: UI -> application/services -> core.
- Separate pure logic from infrastructure-heavy code inside existing services.
- Avoid adding new cross-layer shortcuts while implementing future phases.

## Why Not Now

- The codebase is still small enough for a single-project structure.
- The immediate next work is feature delivery, not architecture migration.
- A project split now would create churn in DI, tests, namespaces, and docs.
- The real boundaries will be clearer after Terminal and before the agent-heavy
  phases begin.

## Best Timing

Re-evaluate this refactor after Phase 3 is complete, or at the latest before
starting Phase 4.

That is the point where Townhall, Agent Panels, and Router work are likely to
increase coupling pressure enough to justify physical project boundaries.

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
