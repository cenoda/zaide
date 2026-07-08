# Phase 5.2: Agent Panel UI Surfaces — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 5.1.1 through 5.1.3 are complete
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] Re-check `src/MainWindow.axaml.cs`, `src/ViewModels/MainWindowViewModel.cs`, and `docs/DESIGN.md`
- [ ] Re-confirm the Phase 5.1 composition seam that Phase 5.2 will render through

## Scope

**Goal:** Render the first dedicated agent panel UI surfaces inside the existing shell without weakening Townhall's visual primacy.

**In scope:**

- One first-class agent-panel view/control built to the existing C#-view policy
- Status area
- Output area
- Direct input area
- Minimal panel-selection UI if the host already exposes multiple seeded panels
- The narrowest shell placement work needed to make the panel surfaces visible

**Out of scope:**

- Real provider execution (`phase-5.3`)
- Townhall logging behavior (`phase-5.4`)
- Agent-to-agent routing
- Shell experimentation beyond the agreed placement decision
- Provider/model picker UI

## Placement Decision

For the first Phase 5 UI pass, treat this placement as settled unless live
implementation proves a concrete blocker:

- Townhall stays in the center unchanged as the primary shared workspace.
- No new top-level shell column is added.
- Agent panels render inside the existing right-side shell column (`MainWindow`
  column 5) through the composition seam established in Phase 5.1.
- Phase 5.2 may split or compose inside that existing right column, but it must
  not reopen the outer shell grid or treat Townhall as a replaceable surface.

Phase 5.2 should not reopen whole-shell exploration.

## UI Decision Constraints

- Prefer C# views, per `docs/DESIGN.md`.
- Use existing resource tokens and `TextStyles`; no hardcoded colors or ad-hoc typography.
- Keep the first panel visually simple and testable.
- Do not design UI for provider switching, routing, or debate features yet.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Confirm placement and control boundaries from the Phase 5.1 seam | Repo review + manual layout inspection |
| M1 | Render one dedicated agent-panel surface with status, output, and input regions | Build + view tests/manual smoke |
| M2 | Expose minimal multiple-panel switching UI if the host already provides more than one seeded panel | ViewModel/view tests + manual smoke |
| M3 | Verify the rendered result preserves Townhall primacy and does not destabilize the shell | `dotnet build Zaide.slnx`, manual shell smoke |

## Test Budget

At minimum, budget tests for:

- Agent-panel view creation/rendering
- Binding of status/output/input surfaces to the panel ViewModel
- Host-driven panel switching if multi-panel UI is rendered in this slice
- Basic layout sanity at the existing shell size constraints

Likely files to extend or add:

- `tests/Zaide.Tests/Views/` new agent-panel view test files
- `tests/Zaide.Tests/ViewModels/` new panel-selection tests if 5.2 adds UI-facing selection behavior
- `tests/Zaide.Tests/MainWindowViewModelTests.cs` only if composition-facing behavior changes

## Limitations (by design)

- The first panel UI may render seeded/static output only
- The first panel UI may support only a fixed small number of seeded panels
- The shell exposure point may still look provisional until real execution arrives in `phase-5.3`
- No streaming, provider settings, or transcript persistence UI in this slice

## Exit Conditions

- [ ] At least one dedicated agent panel renders in the live shell
- [ ] The panel exposes distinct status, output, and input surfaces
- [ ] Any multiple-panel switching UI introduced in this slice works against the Phase 5.1 host seam
- [ ] Townhall remains visually primary
- [ ] `dotnet build Zaide.slnx` passes
- [ ] Focused UI/view tests pass
- [ ] Manual smoke covers rendering, switching (if present), resize sanity, and visual hierarchy

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
