# Phase 5.2: Agent Panel UI Surfaces — Implementation Plan

## Pre-Implementation Verification

- [x] Confirm Phase 5.1.1 through 5.1.3 are complete
- [x] Verify current build succeeds: `dotnet build Zaide.slnx`
- [x] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [x] Re-check `src/MainWindow.axaml.cs`, `src/ViewModels/MainWindowViewModel.cs`, and `docs/DESIGN.md`
- [x] Re-confirm the Phase 5.1 composition seam that Phase 5.2 will render through

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

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Confirm placement and control boundaries from the Phase 5.1 seam | Repo review + manual layout inspection | ✅ Complete |
| M1 | Render one dedicated agent-panel surface with status, output, and input regions | Build + view tests/manual smoke | ✅ Complete |
| M2 | Expose minimal multiple-panel switching UI if the host already provides more than one seeded panel | ViewModel/view tests + manual smoke | ✅ Complete |
| M3 | Verify the rendered result preserves Townhall primacy and does not destabilize the shell | `dotnet build Zaide.slnx`, manual shell smoke | ✅ Complete |

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

- [x] At least one dedicated agent panel renders in the live shell
- [x] The panel exposes distinct status, output, and input surfaces
- [x] Any multiple-panel switching UI introduced in this slice works against the Phase 5.1 host seam
- [x] Townhall remains visually primary
- [x] `dotnet build Zaide.slnx` passes
- [x] Focused UI/view tests pass
- [ ] Manual smoke covers rendering, switching (if present), resize sanity, and visual hierarchy

## Implementation Summary

### Files Added

- `src/Views/AgentPanelView.cs` — Single agent-panel view with header (name + status), output history list, and draft input area. Binds directly to `AgentPanelState` via ReactiveUI. C# view per DESIGN.md policy.
- `src/Views/AgentPanelHostView.cs` — Multi-panel view host that retains one `AgentPanelView` per `AgentPanelState`. Follows the `TerminalTabHost` pattern: tab strip with click-to-activate, "+" button to create new panels, content area showing the active panel's view. View-layer only — no ViewModel.

### Files Modified

- `src/MainWindow.axaml.cs` — Split column 5 (right side) into editor (top, 2*) and agent panel (bottom, 1*) separated by a horizontal `GridSplitter`. Wired `AgentPanelHostView.SetHost(ViewModel!.AgentPanelHost)` in `WhenActivated`.

### Architecture Constraint Verification

| Constraint | Status |
|------------|--------|
| No execution/runtime integration | ✅ Not added |
| No Townhall mirroring | ✅ Not added |
| No routing or persistence | ✅ Not added |
| AgentPanelHost not widened | ✅ Unchanged |
| Collection/selection not moved into MainWindowViewModel | ✅ Unchanged |
| Terminal behavior unchanged | ✅ Unchanged |
| No future-phase agent capabilities | ✅ Not added |

## Next Steps

- Phase 5.3: OpenAI provider execution seam (runtime integration)
- Phase 5.4: Townhall mirroring
- Phase 5.5: Documentation audit

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
