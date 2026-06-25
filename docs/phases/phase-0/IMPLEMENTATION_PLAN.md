# Phase 0: Foundation & Layout — Implementation Plan

## Pre-Implementation Verification

- [x] .NET 10.0 SDK compatibility confirmed (Builds successfully)
- [x] Central Package Management (CPM) configured and verified
- [x] Semi.Avalonia 12.0.3 theme applies (dark mode)
- [x] ReactiveUI.Avalonia 12.0.3 resolves and builds
- [x] DI bridge (Microsoft.Extensions.DependencyInjection 10.0.8) works
- [x] Directory structure created (`src/Models`, `src/Services`, `src/ViewModels`, `src/Views`)
- [x] "Ayaka Violet" palette extracted and documented in `DESIGN.md`
- [ ] Grid layout with 3 columns renders correctly (M1)
- [ ] Bottom panel toggle works (M3)

---

## Scope

**Goal:** Establish the 3-panel window layout, DI container, ReactiveUI wiring, and "Ayaka Violet" theme resources so all future phases have a solid skeleton to build into.

**Boundaries (NOT building):**
- No file tree content (Phase 1)
- No editor (Phase 2)
- No terminal emulation (Phase 3)
- No agent logic (Phase 4+)
- No glass/blur effects yet — solid dark panels first, polish later

---

## Current State (already done)

- .NET 10.0 Upgrade ✅
- Central Package Management (CPM) setup ✅
- Avalonia 12 project scaffold ✅
- Semi.Avalonia dark theme applied ✅
- ReactiveUI.Avalonia + DI bridge in csproj ✅
- Directory structure created ✅
- Builds with 0 warnings ✅

---

## Milestones (Incremental)

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate: `dotnet build` passes, window opens | `dotnet run` shows window |
| M1 | Define "Ayaka Violet" brushes in `App.axaml` | Visual inspection of window background |
| M2 | 3-panel grid layout (left sidebar, center, right agent area) | Window shows 3 distinct colored regions |
| M3 | Bottom panel with Ctrl+` toggle | Press Ctrl+`, panel slides up/down |
| M4 | DI container wired + MainWindowViewModel via ReactiveUI | ViewModel resolves from DI, binds to window |
| M5 | Window chrome: title "Zaide", default size 1280×800 | Window opens 1280×800, title shows "Zaide" |

### M1: Theme Resources ("Ayaka Violet")

Define the following brushes in `App.axaml` as application-wide resources:
- `PrimaryAccent`: `#8FA5DE` (Periwinkle Violet)
- `SoftAccent`: `#C2C2E5` (Soft Cryo Lavender)
- `DeepBase`: `#142043` (Midnight Blue-Purple)
- `TextActive`: `#E3E4F4` (Pale Ice Blue-White)

### M2: 3-Panel Grid Layout

Build `MainWindow` content as a C# view (per DESIGN.md §1):

```
┌──────────┬────────────────────────┬──────────────────┐
│ Sidebar  │       Center           │   Agent Area     │
│  260px   │       (star)           │     320px        │
│          │                        │                  │
│          │                        ├──────────────────┤
│          │                        │   Agent B slot   │
│          │                        │                  │
├──────────┴────────────────────────┴──────────────────┤
│  Bottom panel (hidden by default)                     │
└──────────────────────────────────────────────────────┘
```

- Left column: `Width = 260` (GridLength.Pixel), resizable via GridSplitter
- Center column: `Width = *` (fills remaining)
- Right column: `Width = 320` (GridLength.Pixel), resizable via GridSplitter
- Bottom row: `Height = 0` (collapsed), expands to 250 on toggle
- All panels get placeholder `Border` with distinct background color + label text

### M3: Bottom Panel Toggle

- `MainWindowViewModel.IsBottomPanelVisible` (reactive property)
- KeyBinding: Ctrl+` (or Ctrl+OemTilde) toggles the property
- Grid row height animates between 0 and 250 (or instant for M3, animate in polish later)

### M4: DI Container

- `App.axaml.cs` → `OnFrameworkInitializationCompleted`:
  - Build `ServiceCollection`
  - Register `MainWindowViewModel` (Singleton)
  - Register `UseReactiveUIWithMicrosoftDependencyResolver()`
  - Resolve `MainWindowViewModel`, assign as `DataContext`

### M5: Window Chrome

- `Title = "Zaide"`
- `Width = 1280, Height = 800`
- `WindowStartupLocation = CenterScreen`
- `MinWidth = 800, MinHeight = 600`
- `WindowStartupLocation = CenterScreen`
- OS-native title bar (default Avalonia behavior, no custom chrome)

---

## Limitations (by design)

- No GridSplitter drag yet — fixed column widths. Splitters come with Phase 1 when there's content to resize.
- No glass/blur — solid `#1E1E23` panels. Visual polish is iterative.
- No animation on bottom panel toggle — instant show/hide. Animation comes later.
- Right panel not split into 2 agent slots yet — single placeholder. Split in Phase 5.
- `MainWindow.axaml` kept minimal (just `<Window>` root) per XAML tier 1. Layout built in C#.

---

## Exit Conditions

- [ ] `dotnet build` succeeds with 0 warnings
- [ ] `dotnet run` opens a 1280×800 window titled "Zaide"
- [ ] Window shows 3 colored panel regions (left, center, right)
- [ ] Ctrl+` toggles bottom panel visibility
- [ ] `MainWindowViewModel` is resolved from DI container
- [ ] Directory structure matches plan (Models/, Services/, ViewModels/, Views/)
- [ ] No XAML beyond `App.axaml` and minimal `MainWindow.axaml` shell

---

## Rollback Plan

- Commit hash to revert to: current HEAD (pre-Phase 0 layout work)
- What to preserve: `.editorconfig`, `docs/`, csproj package references
- What to discard on failure: Views/, ViewModels/, Services/, Models/ content

---

*Based on IMPLEMENTATION_TEMPLATE. Last updated: 2025-06-25*
