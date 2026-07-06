# Refactor 4: TOFIX

Code quality issues found during the Visual Polish Pass refactor.
Items here must be addressed before moving to the next phase.

Convention: see `docs-rules.md` §5.

---

## Open Items

### ⚠️ Pre-Flight Finding (M0 verification completed)

- [ ] **Palette mismatch - gray VS Code-like vs navy tokens** — *`src/App.axaml`*
      The current palette (`#1E1E1E`, `#252526`, `#181818`, `#0E6AEB`, `#5B94F5`) is gray VS Code-like, not the navy tokens documented in DESIGN.md §7.
      Expected by plan: `SurfaceBaseBrush=#0A0F19`, `PrimaryAccentBrush=#066ADB`, etc.
      Fix hint: M0.5-A - re-apply the navy palette from DESIGN.md §7 to App.axaml.

- [ ] **Missing Spacing/Radius tokens** — *`src/App.axaml`*
      Only color brushes exist; no `Spacing*` or `Radius*` resource keys documented in Refactor 3 M0.5.
      Fix hint: M0.5-B - add the `Spacing*` and `Radius*` resource keys to App.axaml.

---

## Resolved Items

- [x] **M0 verification artifacts captured** — *`docs/refactor/refactor-4/verification/`*
      Baseline build log, test results, and both required screenshots (`m0-default.png`, `m0-min.png`) were captured. M0 verification is complete.

- [x] **Refactor 4 plan warning baseline drift** — *`docs/refactor/refactor-4/IMPLEMENTATION_PLAN.md`*
      The implementation plan now matches the captured M0 baseline (`0 Warning(s) / 0 Error(s)`), while preserving the `xUnit2013` fallback as a regression procedure if it reappears.

---

## Notes for Future Agents

- When adding a new item, follow this template:

  ```
  - [ ] **[brief title]** — *<file:line area>*
        What is wrong and why.
        Fix hint: ...
  ```

- Mark an item `[x]` and move it to **Resolved Items** with a one-line
  resolution summary when fixed.
- Do not delete items from **Resolved Items** — they form the audit
  trail for this refactor.
- If an item blocks a milestone, flag it in the PR description and
  link back to this file.

---

## M0 Verification Summary (completed)

| Check | Status |
|-------|--------|
| `dotnet build Zaide.slnx` passes | ✅ PASS (0 warnings) |
| `dotnet test Zaide.slnx --no-build` passes | ✅ PASS (402/402 tests) |
| Build log captured to `verification/m0-warnings.txt` | ✅ Done |
| Test results captured to `verification/m0-test-results.txt` | ✅ Done |
| Screenshot at `m0-default.png` | ✅ Done (`1280x800`) |
| Screenshot at `m0-min.png` (960px) | ✅ Done (`960x800`) |

---

## Findings

### Palette Colors - Gray VS Code Theme, Not Navy

Live palette in `src/App.axaml`:
- `PrimaryAccentBrushColor`: `#0E6AEB` (VS Code blue)
- `SurfaceBaseBrushColor`: `#1E1E1E` (VS Code dark)
- `SurfacePanelBrushColor`: `#252526` (VS Code lighter gray)
- `PanelDeepBrushColor`: `#181818` (darker VS Code)

Plan assumption (M0.5):
- `PrimaryAccentBrush`: `#066ADB` ("Ayaka Blue" per DESIGN.md)
- `SurfaceBaseBrush`: `#0A0F19` (near-black navy)
- `SurfacePanelBrush`: `#0B121D` (lighter navy)

**Impact:** M1 onward requires navy palette. Must be fixed before M1 starts.

### Missing Spacing and Radius Tokens

Only color resources exist in `App.axaml`. No spacing tokens (`SpacingXs`, `SpacingSm`, `SpacingMd`, etc.) or radius tokens (`RadiusSm`, `RadiusMd`, `RadiusLg`) are defined.

**Impact:** M3 (file tree polish), M5 (status bar density), and other milestones depend on these tokens.

### Warning Baseline Drift

The captured M0 build baseline is clean (`0 warnings`), recorded in
`docs/refactor/refactor-4/verification/m0-warnings.txt`.

Earlier audit snapshots intermittently showed `xUnit2013` at
`TownhallViewModelTests.cs:325`, so this refactor should continue to
treat that warning as a possible regression to fix in place rather than
as an allowed baseline.

---

*Last updated: 2026-07-06*
