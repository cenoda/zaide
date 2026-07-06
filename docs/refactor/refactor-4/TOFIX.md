# Refactor 4: TOFIX

Code quality issues found during the Visual Polish Pass refactor.
Items here must be addressed before moving to the next phase.

Convention: see `docs-rules.md` §5.

---

## Open Items

No open items. M1 and M2 completed.

---

## Resolved Items

- [x] **Palette mismatch - gray VS Code-like vs navy tokens** — *`src/App.axaml`*
      The current palette (`#1E1E1E`, `#252526`, `#181818`, `#0E6AEB`, `#5B94F5`) was gray VS Code-like, not the navy tokens documented in DESIGN.md §7.
      Fix: M0.5-A - Replaced all 12 palette colors with DESIGN.md §7 navy values (`SurfaceBaseBrush=#0A0F19`, `PrimaryAccentBrush=#066ADB`, etc.)

- [x] **Missing Spacing/Radius tokens** — *`src/App.axaml`*
      Only color brushes existed; no `Spacing*` or `Radius*` resource keys documented in Refactor 3 M0.5.
      Fix: M0.5-B - Added 7 spacing tokens (`SpacingXxs`, `SpacingXs`, `SpacingSm`, `SpacingMd`, `SpacingLg`, `SpacingXl`, `SpacingXxl`) and 5 radius tokens (`RadiusSm`, `RadiusMd`, `RadiusLg`, `RadiusXl`, `RadiusFull`)

- [x] **M0 verification artifacts captured** — *`docs/refactor/refactor-4/verification/`*
      Baseline build log, test results, and both required screenshots (`m0-default.png`, `m0-min.png`) were captured. M0 verification is complete.

- [x] **Refactor 4 plan warning baseline drift** — *`docs/refactor/refactor-4/IMPLEMENTATION_PLAN.md`*
      The implementation plan now matches the captured M0 baseline (`0 Warning(s) / 0 Error(s)`), while preserving the `xUnit2013` fallback as a regression procedure if it reappears.

- [x] **M2 Typography System completion** — *`src/Styles/TextStyles.cs`, `src/Views/*.cs`, `src/MainWindow.axaml.cs`, `src/Views/UnsavedDialog.axaml`*
      Raw `FontSize=` / `FontWeight=` literals remained on several `TextBlock` instances across the views. The placeholder at `docs/refactor/refactor-4/verification/m2-default.png` was ASCII text, not a real screenshot, and `TOFIX.md` falsely claimed it was done.
      Fix: M2 - Replaced all in-scope `TextBlock` typography literals with `TextStyles.Header/Body/Caption/Brand`, preserved reactive `.Text` mutation for dynamic text, removed the fake placeholder, and captured a real 1280x800 PNG screenshot of the running app.

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

## M2 Verification Summary (completed)

| Check | Status |
|-------|--------|
| `TextStyles.cs` created with Header, Body, Caption, Brand methods | ✅ DONE |
| `TextStylesTests.cs` created and passing | ✅ PASS (11/11 tests) |
| `dotnet build Zaide.slnx` passes | ✅ PASS (0 warnings) |
| Typography literals in view code updated with TextStyles usage | ✅ DONE |
| VC-4 grep clean except documented intentional exceptions | ✅ PASS |
| Screenshot at `docs/refactor/refactor-4/verification/m2-default.png` | ✅ DONE (real 1280x800 PNG) |
| `TOFIX.md` updated | ✅ DONE |

### Remaining VC-4 grep hits (intentional exceptions only)

```
src/Views/SourceControlPanel.cs:61:            FontSize = 13
src/Views/SourceControlPanel.cs:96:            FontSize = 13
src/Views/SourceControlPanel.cs:108:            FontSize = 13,
src/Views/SourceControlPanel.cs:200:            statusText.FontWeight = FontWeight.Bold;
src/Views/SourceControlPanel.cs:229:                FontSize = 12,
src/Views/StatusBar.cs:64:            FontSize = 12,
src/Views/EditorView.cs:47:            FontSize = 14,
src/Views/TerminalPanel.cs:54:            FontSize = 12,
```

| File:Line | Control | Why it is an intentional exception |
|-----------|---------|-----------------------------------|
| `SourceControlPanel.cs:61` | `_branchSelector` (ComboBox) | Control-level `FontSize`, not a `TextBlock` typography literal. |
| `SourceControlPanel.cs:96` | `_commitInput` (TextBox) | Control-level `FontSize`, not a `TextBlock` typography literal. |
| `SourceControlPanel.cs:108` | `_commitButton` (Button) | Control-level `FontSize`, not a `TextBlock` typography literal. |
| `SourceControlPanel.cs:200` | `statusText` (TextBlock) | Created via `TextStyles.Caption(statusChar)`; `FontWeight.Bold` is an intentional override so the A/M/D status monogram remains legible inside the 20px badge. |
| `SourceControlPanel.cs:229` | `stageButton` (Button) | Control-level `FontSize`, not a `TextBlock` typography literal. |
| `StatusBar.cs:64` | `Separator()` local function | The `│` glyph is a visual divider, not semantic text. It uses `SeparatorBrush` and is intentionally sized to align with the 24px status bar. |
| `EditorView.cs:47` | `_textEditor` (TextEditor) | Control-level `FontSize` on the AvaloniaEdit code editor, not a `TextBlock` typography literal. |
| `TerminalPanel.cs:54` | `_toggleViewButton` (Button) | Control-level `FontSize`, not a `TextBlock` typography literal. |

---

## M1 Verification Summary (completed)

| Check | Status |
|-------|--------|
| `dotnet build Zaide.slnx` passes | ✅ PASS (0 warnings) |
| `dotnet test Zaide.slnx --no-build` passes | ✅ PASS (402/402 tests) |
| Luminance gate (VC-3): `SurfaceBase(#0A0F19)` → `SurfacePanel(#1A2540)` | ✅ PASS (ΔL* = 10.72 ≥ 8.00) |
| Luminance gate (VC-3): `SurfaceBase(#0A0F19)` → `SurfaceRaised(#243352)` | ✅ PASS (ΔL* = 17.10 ≥ 8.00) |
| Screenshot at `m1-default.png` | ✅ Done (captured by user) |
| `TOFIX.md` updated | ✅ Done |
| `TransparencyLevelHint` on MainWindow + UnsavedDialog | ✅ Done |
| Elevation contrast bump applied | ✅ Done |
| 1px Border separators removed (panel contrast only) | ✅ Done (GridSplitter preserved) |
| `SurfaceRaisedBrush` applied to all file-tree MenuItems | ✅ Done |

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

*Last updated: 2026-07-06*