# F10 — Icon pack migration + full icon system unification

Full implementation plan for Phase 23 **F10** (reopened 2026-08-06). Replaces
embedded Phosphor `StreamGeometry` + dual render paths with one catalogued
Avalonia icon pack behind the existing `IconFactory` facade.

**Spec / status:** `docs/phases/v3/phase-23/TOFIX.md` (F10)  
**Library policy:** `docs/LIBRARIES.md` (Icons & Assets)  
**Conventions:** `docs/CONVENTIONS.md` (Icons), `docs/DESIGN.md` (icon size)

---

## Problem (current state)

| Layer | Today | Issue |
|-------|-------|-------|
| `Icons.axaml` | Phosphor Regular `StreamGeometry` (256×256 fill paths) | Unreadable at 14–20px after Fill/Stroke/scaling experiments |
| `IconFactory` | `Viewbox` + `Path` over resource geometry | Mushy / hollow glyphs; tests pass but UI fails |
| `NavBar.CreateNavIcon` | Inline 16×16 **stroke** paths | Readable — **second icon system** |
| Call sites | `IconFactory.Create("Icon.*", brush, size)` | ~20+ usages across shell, features |

**Partial F10 already merged:** icon-only tooltip + `AutomationProperties.Name`;
decorative Source Control header glyph removed; `Phase23IconFactoryTests` (paint/a11y
contracts only).

---

## Decision

| Choice | Selection | Rationale |
|--------|-----------|-----------|
| **Primary pack** | **Lucide.Avalonia** (NuGet **0.2.16**, MIT, net10) | Stroke-oriented for small UI; C# `Content` without AXAML xmlns; active maintenance |
| **Fallback** | IconPacks.Avalonia (MahApps) | Only if Lucide POC fails on Avalonia **12.0.5** — verify released Avalonia 12 build, not 1.3.x alone |
| **Integration** | **Facade** — keep `IconFactory.Create` + all `Icon.*` string keys | No feature-wide Lucide type leaks |
| **Unification** | **NavBar** adopts `IconFactory` (drop `CreateNavIcon` inline paths) | One pipeline, one visual language |
| **Removal** | Delete `src/UI/DesignSystem/Icons.axaml` + `App.axaml` merge after migration | No dead Phosphor assets |

**POC gate (M0):** Before bulk migration, prove on Zaide stack (Avalonia 12.0.5,
.NET 10): `RotateCw` (or Lucide refresh), `GitBranch`, `Folder`, `FileCode` at
14px and 16px in Source Control header + status bar + file tree — screenshot
pair vs current build. If Lucide fails at runtime, document and pivot to MahApps
with same POC criteria.

---

## Target architecture

```
Feature / Shell views
    └── IconFactory.Create("Icon.ArrowClockwise", brush, 16)
            └── internal map: Icon.* → LucideIconKind (+ stroke width policy)
            └── returns Control (Lucide-backed), IsHitTestVisible = false
FileIconKeyResolver.GetIconKey(...)  → still returns "Icon.Code", etc.
NavBar                               → IconFactory (same keys or new Icon.Explorer / Icon.SourceControl)
```

**Paint contract (unified):**
- Stroke icons: `Foreground` brush on stroke; `StrokeWidth` scaled from size
  (e.g. `size / 8`, clamp ~1.25–2.0) — match readable NavBar weight (~1.8 at 16px).
- No `Viewbox` wrapping 256×256 paths.
- `SetForeground(icon, brush)` must update the live brush on pack controls.

**Resource keys (keep stable):** All keys in current `Icons.axaml` plus nav keys:

| `Icon.*` key | Lucide kind (verify in POC) | Used at |
|--------------|----------------------------|---------|
| `Icon.ArrowClockwise` | `RotateCw` | SC refresh, terminal restart |
| `Icon.GitBranch` | `GitBranch` | Status bar branch |
| `Icon.Folder` | `Folder` | File tree header/rows |
| `Icon.Code` | `FileCode` or `Code` | File type, status language |
| `Icon.Text` | `FileText` | Status document, file type |
| `Icon.Image` | `Image` | File type |
| `Icon.Config` | `Settings` | Status settings |
| `Icon.Markup` | `CodeXml` or `Braces` | File type (if resolver uses) |
| `Icon.Project` | `Box` or `AppWindow` | Status project, file type |
| `Icon.Unknown` | `File` | Fallback file type |
| `Icon.X` | `X` | Close buttons |
| `Icon.Plus` | `Plus` | New terminal tab |
| `Icon.Search` | `Search` | Terminal find |
| `Icon.Terminal` | `Terminal` | Terminal header |
| `Icon.Broom` | `Eraser` or `Brush` | Terminal clear |
| `Icon.ChevronDown` | `ChevronDown` | Terminal latest |
| `Icon.ChevronLeft` | `ChevronLeft` | (audit callers) |
| `Icon.ArrowUp` | `ArrowUp` | Townhall send |
| `Icon.Selection` | `TextCursor` or `Crosshair` | Status caret |
| `Icon.Bell` | `Bell` | Townhall People |
| `Icon.Info` | `Info` | Townhall chat |
| `Icon.Pin` | `Pin` | Townhall nav |
| `Icon.Warning` | `TriangleAlert` | Warnings |
| `Icon.CheckCircle` | `CircleCheck` | (audit callers) |
| **New** `Icon.Explorer` | `FolderTree` or `Folders` | NavBar explorer (replaces inline path) |
| **New** `Icon.SourceControl` | `GitBranch` | NavBar SC (replaces inline path) |

Audit grep for every `Icon.*` reference and every `CreateNavIcon` before locking
the table; add rows for any missing key.

---

## Milestones

### M0 — POC + package admission

- [ ] Add `Lucide.Avalonia` to `Directory.Packages.props` + `src/Zaide.csproj`.
- [ ] Minimal probe (temporary or test-only surface): 4 icons at 14px / 16px;
  app launches on Linux without `TypeLoadException` / `MissingMethodException`.
- [ ] Screenshot evidence in PR description (not committed to repo).
- [ ] Update `docs/LIBRARIES.md`: move Lucide from “recommended” to “in use”
  with pinned version; mark Phosphor embed **removed** when M3 completes.

**Stop:** If POC fails, try MahApps per `LIBRARIES.md` fallback; do not proceed
with Phosphor path edits.

### M1 — `IconFactory` rewrite (facade)

- [ ] `IconFactory.Create` returns Lucide-backed `Control` (same signature).
- [ ] Internal `IconKey → Lucide` map (single file, e.g. `IconLucideMap.cs` in
  `App/Shell` or beside `IconFactory`).
- [ ] `SetForeground` updates brush on returned control.
- [ ] `IsHitTestVisible = false` on icon control (buttons own hit targets).
- [ ] Remove `Viewbox` + manual `Path` Phosphor pipeline from factory.

### M2 — Call-site sweep (no pack types in features)

Grep `IconFactory.Create` and verify visual + a11y at 14–20px:

| Area | Files |
|------|-------|
| Shell | `StatusBar.cs`, `NavBar.cs` |
| Source control | `SourceControlPanel.cs` |
| Workspace | `FileTreeView.cs` |
| Editor | `EditorTabBar.cs`, `EditorView.cs` |
| Terminal | `TerminalPanel.cs`, `TerminalTabStrip.cs` |
| Townhall | `TownhallInputArea.cs`, `TownhallChatPanel.cs`, `TownhallPeoplePanel.cs`, `TownhallNavigationPanel.cs` |

- [ ] No new `Icons.axaml` entries.
- [ ] Icon-only controls retain tooltip + `AutomationProperties.Name` (already on
  SC refresh, file tree close, editor tab close, terminal tab close/new).

### M3 — Unify NavBar + remove legacy assets

- [ ] Replace `CreateNavIcon` inline SVG strings with `IconFactory.Create`
  (`Icon.Explorer`, `Icon.SourceControl` or mapped keys).
- [ ] Delete `CreateNavIcon` if unused.
- [ ] Remove `src/UI/DesignSystem/Icons.axaml`.
- [ ] Remove `Icons.axaml` merge from `App.axaml` (or DesignSystem dictionary).
- [ ] Grep repo: zero `Icons.axaml`, zero Phosphor path strings in `IconFactory`.

### M4 — Tests + docs closeout

- [ ] Update `Phase23IconFactoryTests`:
  - Assert Lucide-backed contract (not `Path.Fill` — adapt to pack control).
  - Keep a11y source-grep tests.
  - Optional: map completeness test — every `Icon.*` in map resolves.
- [ ] Run `dotnet build Zaide.slnx` + `dotnet test Zaide.slnx --no-build`.
- [ ] Mark F10 **Fixed** in `TOFIX.md` with test names + manual screenshot note.
- [ ] `docs/LIBRARIES.md`, `CONVENTIONS.md`, `DESIGN.md` already aligned — tweak
  only if implementation differs from plan.

---

## Verification checklist (manual)

Capture **before/after** at 14px and 16px (PR attachments, not committed):

1. Source Control — refresh button  
2. Status bar — settings, branch, document, language icons  
3. File tree — folder header + colored file-type row icons  
4. Nav rail — Explorer + Source Control (post-unification)  
5. Terminal toolbar — Find, Clear, Restart (icon+label buttons)  
6. Editor tab close glyph (hover)

**Done when:** User can name each icon without hovering (except where tooltip
is the only label for icon-only controls).

---

## Out of scope

- F3 empty panel chrome, F7 bottom panel purpose, F8 image preview, F5 settings
  migration, F9 file-tree tail (unless regressions from icon swap).
- Icon **color** theme rework (file-tree category brushes stay; map to Lucide
  stroke/fill brush only).
- Per-extension file icons (stay category glyphs via `FileIconKeyResolver`).
- Semi.Avalonia theme icon overrides.
- Committing screenshot assets to `docs/` or `temp-screenshots/`.

---

## Suggested commit shape

One reviewable commit (or two if docs-heavy):

```
fix(phase-23): migrate icons to Lucide.Avalonia and unify IconFactory (F10)
```

Include `TOFIX.md` F10 fixed entry and `LIBRARIES.md` version pin in the same
commit as implementation.

---

## Risk register

| Risk | Mitigation |
|------|------------|
| Lucide.Avalonia incompatible with Avalonia 12.0.5 | M0 POC; MahApps fallback |
| `LucideIcon` API differs in C# vs XAML | Use programmatic API in `IconFactory` only |
| Colored file-tree icons | Apply `Fill`/`Stroke` brush per category on Lucide control |
| Headless tests cannot render Lucide | Source-grep + map tests; paint tests on control properties |
| `TownhallInputArea` fallback text when resource missing | Remove Phosphor fallback path; pack always provides icon |
