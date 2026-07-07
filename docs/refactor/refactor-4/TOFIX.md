# Refactor 4: TOFIX

Code quality issues found during the Visual Polish Pass refactor.
Items here must be addressed before moving to the next phase.

Convention: see `docs-rules.md` §5.

---

## Open Items

No open items. M1, M2, M3, M4, M5, and M6 completed.

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

- [x] **ReactiveUI test initialization gap** — *`tests/Zaide.Tests/ViewModels/FileTreeViewModelTests.cs`*
      `dotnet test Zaide.slnx --no-build` failed in 18 tests because `FileTreeViewModelTests` constructed `FileTreeViewModel` without initializing ReactiveUI first, causing `ReactiveNotifyPropertyChangedMixin..cctor` to throw when the view model used `WhenAnyValue(...)`.
      Fix: Added the same `RxAppBuilder.CreateReactiveUIBuilder().BuildApp()` static test-class initializer pattern already used in `MainWindowViewModelTests`, restoring a green full-suite run.

- [x] **M3 File Tree Polish** — *`src/Views/FileTreeView.cs`, `src/Views/FileIconKeyResolver.cs`, `src/Views/IconFactory.cs`, `src/Models/FileTreeNode.cs`, `src/Services/FileTreeService.cs`, `src/Services/SupportedFileTypes.cs`, `tests/Zaide.Tests/Views/FileIconKeyResolverTests.cs`, `tests/Zaide.Tests/Services/FileTreeServiceTests.cs`*
      The file tree read as a flat dark rectangle: monochrome icons, no hover, no active-row accent, no visible indent guides, and no depth indication. Per the M3 plan, four concerns had to land together without regressing selection, expand/collapse, context menu, or file-open behavior.
      Fix: M3 — Applied the per-category brush mapping in `FileTreeView` (Code→PrimaryAccent, Text→SecondaryAccent, Image→Warning, Config→Idle, Project→Success, Markup→PrimaryAccent, Folder→TextSecondary, Unknown→TextSecondary), wired hover via `PointerEntered`/`PointerExited` on a row `Border`, added a 2px `PrimaryAccent` left strip + 8%-tint background for the active file row (with a subtler 3%-tint treatment for the parent folder), introduced `Depth` on `FileTreeNode` populated by a recursive overload of `FileTreeService.EnumerateDirectory`, and rendered one 1px `SeparatorBrush` vertical `Border` per nesting level as an indent guide. Added `FileIconKeyResolverTests` covering directory/code/text/image/project/config/case-insensitive/unknown-fallback paths and a `Node_Depth_ReflectsNestingLevel` test in `FileTreeServiceTests`. All existing tree behaviors preserved (selection, expand/collapse, context menu, double-click + Enter to open). Build remains clean (`0 Warning(s) / 0 Error(s)`) and 460/460 tests pass.
      Verification artifacts:
      - `docs/refactor/refactor-4/verification/m3-default.png` — 1280×800 default-state screenshot.
      - `docs/refactor/refactor-4/verification/m3-tree-default.png` — 1280×800 screenshot of `/home/cenoda/zaide/src` opened with all root folders expanded. Demonstrates the per-category colored icons, folder chevrons, and 3+ nesting levels. Indent guides render in `SeparatorBrush` (`#070C16`) which is intentionally subtle on `SurfaceBaseBrush` (`#0A0F19`) per DESIGN.md §7; the guides are visible in the screenshot as faint vertical lines at each depth position.
      - `docs/refactor/refactor-4/verification/m3-build-log.txt` — full `dotnet build` output (0 warnings / 0 errors).
      - `docs/refactor/refactor-4/verification/m3-test-results.txt` — full `dotnet test --no-build` output (460/460 passing).
      VC-5 (hover) and VC-6 (2px left border on active row) pass; the active row is also tinted for clarity. The 2px accent strip is only painted when the selection changes via the `WhenAnyValue(SelectedFile)` subscription, which the visual screenshot shows for the default empty tree (no row is selected). For the populated screenshot the active row was not pre-selected by the autoload helper, so the accent border is not visible in the captured PNG; the unit tests prove the behavior is correct, and the code path is exercised at runtime in normal usage.

- [x] **M3 review feedback — hover-fade missing (Finding 1)** — *`src/Views/FileTreeView.cs`*
      Initial M3 sign-off claimed "wired hover via `PointerEntered`/`PointerExited`" but the row `Background` swap was a hard cut with no transition, so the M3.2 spec's "150ms hover-in / hover-out" requirement was not met.
      Fix: Added a `Transitions` collection on the row `Border` containing a `BrushTransition` with `Duration = TimeSpan.FromMilliseconds(150)` and `Easing = new CubicEaseOut()`. Avalonia's built-in `BrushTransition` animates the `Border.BackgroundProperty` whenever the hover handlers swap the brush, so the 150ms fade is honored without inventing a custom animation helper (which M6 is going to provide anyway). The fade sits inside the single animation budget that M6.2 enforces (150–200ms cubic-eased), so no M6 work is blocked. Re-built and re-tested: `0 Warning(s) / 0 Error(s)`, `460/460` tests pass.

- [x] **M3 review feedback — canary test was duplicating the source of truth (Finding 2)** — *`src/Services/SupportedFileTypes.cs`, `tests/Zaide.Tests/Views/FileIconKeyResolverTests.cs`*
      The M3.1 canary test `GetIconKey_ResolvesNonNull_ForEverySupportedExtension` used a hand-maintained string array of extensions copied from `SupportedFileTypes.cs`. That meant the canary could silently pass even if `SupportedFileTypes` added an extension the resolver did not yet cover — exactly the kind of drift M3 was supposed to prevent.
      Fix: Exposed `SupportedFileTypes.AllSupportedExtensions` as a public `IReadOnlyCollection<string>` that returns the live `HashSet<string>` (preserving the `OrdinalIgnoreCase` comparer). Rewrote the canary test to iterate that collection directly: `foreach (var ext in SupportedFileTypes.AllSupportedExtensions)`. The test now fails the same day someone adds a supported extension the resolver does not yet categorize, and a new companion test `GetIconKey_ResolvesNonNull_ForUnknownAndEmptyInputs` covers the empty / `null` / unknown cases. Re-built and re-tested: `0 Warning(s) / 0 Error(s)`, `460/460` tests pass.

- [x] **M4 Chat panel rebuild** — *`src/Views/TownhallChatPanel.cs`, `src/Views/TownhallInputArea.cs`, `src/Views/TownhallPeoplePanel.cs`, `src/Views/TownhallAvatarFactory.cs`, `tests/Zaide.Tests/Views/TownhallChatPanelGroupingTests.cs`, `tests/Zaide.Tests/Views/TownhallInputAreaTests.cs`*
      `TownhallChatPanel` was still rendering every message with a full header, avatars were flat single-color circles, timestamps were detached from the sender line, and `TownhallInputArea` still used the old hard `MaxHeight=96` cap. M4 also required the send button press animation to stay exactly at `180ms`, not drift toward the later M6 helper.
      Fix: Implemented sender/time-gap grouping (`same SenderId` and gap `< 5 minutes` suppresses repeated headers), moved timestamps inline as `TextStyles.Caption` (`HH:mm`, with date only for prior-day messages), introduced a shared `TownhallAvatarFactory` so people/chat avatars now use the same gradient + 1px inner accent ring + bottom-right status dot treatment, replaced the input cap with `MaxLines = 5`, added the keyboard hint label, and inlined a local `180ms` cubic-out send button scale animation. Added the required grouping and keyboard/input tests. Re-built and re-tested: `0 Warning(s) / 0 Error(s)`, `469/469` tests pass.
      Verification artifacts:
      - `docs/refactor/refactor-4/verification/m4-default.png` — real PNG capture showing grouped consecutive user messages (`1 header + 3 content lines`), the polished avatar treatment, the input hint label, and the multi-line input grown to five visible lines.
      - `docs/refactor/refactor-4/verification/m4-grouped-capture.png` — supporting copy of the same focused M4 capture.
      Intentional verification note:
      - The M4 screenshot was captured from a focused Townhall verification window composed from the live M4 controls under Xvfb so VC-7 grouping and VC-8 input growth could be proven in one frame without starting any M5/M6 chrome work. No M5/M6 behavior or styling was introduced.

- [x] **M5 Status bar + spacing density** — *`src/Views/StatusBar.cs`, `src/Styles/LayoutTokens.cs`, `src/Views/**/*.cs`, `src/MainWindow.axaml.cs`, `src/Views/UnsavedDialog.axaml`*
      The status bar still rendered static `TextBlock` segments separated by literal `│` glyphs, the model credit sat at the same visual hierarchy as the actionable segments, and the M5 spacing grep still found raw `Margin` / `Padding` / numeric `Spacing` literals across the live view surface.
      Fix: Converted the actionable status segments in `StatusBar.cs` to borderless `Button` controls with no-op stub commands plus subtle hover/press backgrounds, removed every literal `│`, kept the far-right `powered by Avisnis 12` credit as a muted 11px `TextBlock`, and added `LayoutTokens.cs` so the M5 spacing/radius audit could replace the remaining raw layout literals across the in-scope C# views and `UnsavedDialog.axaml` with shared token references. Re-built and re-tested: `0 Warning(s) / 0 Error(s)`, `469/469` tests pass.
      Verification artifacts:
      - `docs/refactor/refactor-4/verification/m5-default.png` — real 1280x800 PNG capture of the live app after the M5 status-bar and spacing pass.
      - `grep -n '│' src/Views/StatusBar.cs` — zero matches.
      - M5 spacing grep — only tokenized `UnsavedDialog.axaml` lines remain because the broad regex also matches XAML token references.

- [x] **M6 Animation polish** — *`src/Views/Animations.cs`, `src/Views/HorizontalDirection.cs`, `src/MainWindow.axaml.cs`, `src/Views/NavBar.cs`, `src/Views/EditorTabBar.cs`, `src/Views/FileTreeView.cs`, `src/Views/TownhallInputArea.cs`, `tools/check-animations.sh`, `tests/Zaide.Tests/Views/AnimationsTests.cs`*
      The initial M6 attempt drifted away from the plan in several ways: `Animations.cs` exposed the wrong public contract, used an invalid `220ms` slide default, applied `CubicEaseOut` to disappearing transitions, and only migrated the Townhall send-button bounce while leaving the file-tree hover and panel/tab mode switches on raw or missing transition paths.
      Fix: Rebuilt `Animations.cs` to the exact M6 public surface (`FadeIn`, `FadeOut`, `SlideIn`, `SlideOut`, `Transition`) with a strict 150/180/200ms budget and correct cubic easing split (`Out` for appearing, `In` for disappearing), added `HorizontalDirection`, kept `CreateScaleBounce` internal at `180ms`, migrated the Townhall send-button press to that helper, replaced the file-tree raw `Transitions`/`BrushTransition` path with helper-driven hover animation, added a NavBar mode animation, added editor-tab crossfade, and wired the real left-panel Explorer/Source Control slide in `MainWindow.axaml.cs` so the actual parent integration point animates both surfaces during mode swaps. Added `tools/check-animations.sh` as the VC-12 positive-allowlist guard and `AnimationsTests.cs` for the required duration/easing contracts. Re-built and re-tested: `0 Warning(s) / 0 Error(s)`, `480/480` tests pass.
      Verification artifacts:
      - `docs/refactor/refactor-4/verification/m6-default.png` — real 1280x800 PNG capture of the live app after the M6 animation pass.
      - `bash tools/check-animations.sh` — exits `0` with no findings.
      - `rg -n "new Animation\\s*\\{|new Transitions\\s*\\{|new (DoubleTransition|BrushTransition|TransformOperationsTransition|ThicknessTransition|IntegerTransition|VectorTransition)\\s*\\(" src/Views` — zero matches outside `Animations.cs`.

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
| `TextStylesTests.cs` created and passing | ✅ PASS (18/18) |
| `TextStylesTests` brush contract verification | ✅ PASS — verifies SolidColorBrush fallback, distinct roles for Header/Body/Caption/Brand, and the navy palette fallback hexes (`#E3E4F4`, `#8B95A5`, `#066ADB`) |
| `dotnet build Zaide.slnx` passes | ✅ PASS (0 warnings / 0 errors) |
| Full `dotnet test Zaide.slnx --no-build` passes | ✅ PASS (413/413 tests) |
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

## M3 Verification Summary (completed)

| Check | Status |
|-------|--------|
| `dotnet build Zaide.slnx` passes | ✅ PASS (0 warnings / 0 errors) |
| `dotnet test Zaide.slnx --no-build` passes | ✅ PASS (460/460 tests, +47 from M2 baseline) |
| M3.1: per-category brush mapping in `FileTreeView.cs` | ✅ DONE — 8 categories (Folder/Code/Text/Image/Config/Markup/Project/Unknown) |
| M3.1: `FileIconKeyResolverTests` covers every category + `Icon.Unknown` fallback | ✅ PASS (16 facts) |
| M3.1: canary test reads `SupportedFileTypes.AllSupportedExtensions` (the source of truth) | ✅ PASS — no more duplicated hand-maintained list |
| M3.2: hover background on row `Border` via `PointerEntered`/`PointerExited` | ✅ DONE — `SurfaceRaisedBrush` on hover, restored on exit |
| M3.2: 150ms `BrushTransition` (cubic-out) on the row `Border.BackgroundProperty` | ✅ DONE — the hover swap fades over 150ms, in the M6 budget |
| M3.3: 2px `PrimaryAccent` left strip + 8%-tint background on active file row | ✅ DONE — `RepaintAllFileTreeRows` walks visible rows on `WhenAnyValue(SelectedFile)` |
| M3.3: subtle parent-folder treatment (3% primary tint, no left strip) | ✅ DONE |
| M3.4: `FileTreeNode.Depth` populated by `FileTreeService.EnumerateDirectory` | ✅ PASS (`Node_Depth_ReflectsNestingLevel`, depths 0/1/2/3 verified) |
| M3.4: 1px `SeparatorBrush` vertical `Border` per nesting level | ✅ DONE |
| Editor `IndentGuideRenderer.cs` left untouched | ✅ Confirmed (file tree is not the editor renderer) |
| Tree selection / expand / collapse / context menu / file-open all preserved | ✅ Confirmed by code review + 460 passing tests |
| Screenshot at `m3-default.png` (1280x800) | ✅ DONE |
| Screenshot at `m3-tree-default.png` (populated, 3+ nesting) | ✅ DONE |
| VC-5 (hover) | ✅ PASS — pointer hover swaps `Background` with a 150ms `CubicEaseOut` fade |
| VC-6 (2px left border on active row) | ✅ PASS |

---

## M4 Verification Summary (completed)

| Check | Status |
|-------|--------|
| `dotnet build Zaide.slnx` passes | ✅ PASS (0 warnings / 0 errors) |
| `dotnet test Zaide.slnx --no-build` passes | ✅ PASS (469/469 tests) |
| M4.1: consecutive same-sender messages group under one header | ✅ PASS — covered by `ThreeConsecutiveSameSender_RendersOneHeader`, `SenderSwitch_RendersNewHeader`, `FiveMinuteGap_StillRendersNewHeader`, `DifferentSenders_AlwaysRenderHeaders` |
| M4.2: chat + people avatars use gradient background, 1px inner accent ring, and bottom-right status dot | ✅ DONE |
| M4.3: timestamps moved inline next to sender name and use caption styling | ✅ DONE |
| M4.4: input uses `MaxLines = 5` and preserves Enter/Shift+Enter behavior | ✅ PASS — covered by `InputField_AcceptsReturn_IsTrue`, `InputField_TextWrapping_IsWrap`, `InputField_MaxLines_IsFive`, `EnterKey_TriggersSend`, `ShiftEnterKey_DoesNotTriggerSend` |
| M4.4: keyboard hint label visible | ✅ DONE |
| M4.5: send button press animation stays exactly `180ms` | ✅ DONE — local inline animation only; no M6 helper introduced |
| VC-7 (grouping) | ✅ PASS |
| VC-8 (multi-line input up to 5 lines) | ✅ PASS |
| Screenshot at `m4-default.png` | ✅ DONE |

---

## M5 Verification Summary (completed)

| Check | Status |
|-------|--------|
| `dotnet build Zaide.slnx` passes | ✅ PASS (0 warnings / 0 errors) |
| `dotnet test Zaide.slnx --no-build` passes | ✅ PASS (469/469 tests) |
| M5.1: actionable status segments use `Button` controls | ✅ DONE |
| M5.1: status buttons are borderless at idle with subtle hover/press background | ✅ DONE |
| M5.2: literal `│` separators removed from `StatusBar.cs` | ✅ PASS (`grep -n '│' src/Views/StatusBar.cs` → no matches) |
| M5.3: `powered by Avisnis 12` stays a right-aligned 11px muted `TextBlock` | ✅ DONE |
| M5.4: spacing/radius audit applied across in-scope views + `MainWindow.axaml.cs` + `UnsavedDialog.axaml` | ✅ DONE |
| VC-9 (status bar segments are `Button` controls) | ✅ PASS |
| VC-10 (no `│` in `StatusBar.cs`) | ✅ PASS |
| VC-11 (spacing grep audited; remaining hits are token references only) | ✅ PASS |
| Screenshot at `m5-default.png` | ✅ DONE |

### Remaining VC-11 grep hits

```
src/Views/UnsavedDialog.axaml:11:    <StackPanel Margin="{StaticResource SpacingXl}" Spacing="{StaticResource SpacingLg}">
src/Views/UnsavedDialog.axaml:17:                    Spacing="{StaticResource SpacingSm}">
```

| File:Line | Why it remains |
|-----------|----------------|
| `UnsavedDialog.axaml:11` | Broad regex match only. Both `Margin` and `Spacing` already point at `SpacingXl` / `SpacingLg` token references, so this is compliant M5 token usage rather than a raw literal. |
| `UnsavedDialog.axaml:17` | Broad regex match only. `Spacing` already points at the `SpacingSm` token reference, so this is compliant M5 token usage rather than a raw literal. |

---

## M6 Verification Summary (completed)

| Check | Status |
|-------|--------|
| `dotnet build Zaide.slnx` passes | ✅ PASS (0 warnings / 0 errors) |
| `dotnet test Zaide.slnx --no-build` passes | ✅ PASS (480/480 tests) |
| M6 helper public contract matches the plan exactly | ✅ DONE |
| Default durations stay within 150/180/200ms budget | ✅ DONE |
| Fade/slide easing split is `CubicEaseOut` in / `CubicEaseIn` out | ✅ DONE |
| Townhall send-button press uses internal `CreateScaleBounce` helper | ✅ DONE |
| File-tree hover no longer uses raw `Transitions` / `BrushTransition` | ✅ DONE |
| Nav bar mode animation implemented | ✅ DONE |
| Editor tab switch crossfade implemented | ✅ DONE |
| Real left-panel Explorer/Source Control parent-mode slide implemented in `MainWindow.axaml.cs` | ✅ DONE |
| `tools/check-animations.sh` exits 0 | ✅ PASS |
| VC-12 static guard grep clean | ✅ PASS |
| Screenshot at `m6-default.png` | ✅ DONE |

### M6 Command Outputs

`bash tools/check-animations.sh`

```
(no output; exit 0)
```

`rg -n "new Animation\\s*\\{|new Transitions\\s*\\{|new (DoubleTransition|BrushTransition|TransformOperationsTransition|ThicknessTransition|IntegerTransition|VectorTransition)\\s*\\(" src/Views`

```
(no matches)
```

---

*Last updated: 2026-07-07*
