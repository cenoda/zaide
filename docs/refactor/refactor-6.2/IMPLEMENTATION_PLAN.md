# Refactor 6.2: Mechanical Feature-First Migration — Implementation Plan

## Status and authorization

**Current milestone:** M7a Debugging application **complete**
(pending human review / commit). M0 accepted at `8fae71d`. M1 DesignSystem
committed at `2259b81`. M2 Settings committed at `a13be5a`. M3 Workspace
committed at `ac75fe5`. M4 Editor committed at `0015101`. M5a ProjectSystem
discovery committed at `faa6e2f`. M5b ProjectSystem workflow committed at
`e2928a5`. M5c ProjectSystem diagnostics committed at `5e22020`. M6a Language
application/contracts committed at `ffbec92`. M6b Language LSP infrastructure
committed at `518979b`. Do not start M7b+ until M7a is accepted.

**M0 acceptance status:** **GO** (human acceptance 2026-07-17). First draft was
NO-GO (underspecified M5 and pattern-defined M6a); the amendment closed those
blockers with mandatory **M5a / M5b / M5c** and authoritative full-path lists.
Accepting M0 authorizes **M1 DesignSystem only**, not later slices.

**Authorization boundary:** This document may be the only change for M0 (plus
an optional read-only evidence note if added later). M0 does **not** authorize:

- moving, renaming, or editing production or product-test source
- namespace changes, AXAML/resource path changes, DI registration edits
- lifetime, visibility, public surface, or behavior changes
- exception-list growth
- Refactor 6.3 dependency inversion, Refactor 7 agent domain, Refactor 8 UI
  extraction, or V3 feature work

**Prerequisite (re-verified 2026-07-17):** Refactor 6.1 is closed on
`master` at `9a0a83f` (`docs: close refactor 6.1 architecture baseline`).
Working tree clean; `HEAD == origin/master`. Refactor 6.1 supplies rules,
dispositions, and executable ratchets; this plan owns mechanical movement only.

## Goal

Rehome every current production and matching test source file into the approved
feature-first taxonomy **inside the existing `Zaide` assembly**, using
movement-only changes: path, namespace, `using` updates, AXAML/resource
includes, and architecture-admission path keys. Preserve all Refactor 6.1
exception dispositions. Do not invent catch-all roots or redesign behavior.

## Hard boundaries (entire Refactor 6.2)

1. **Assembly:** keep a single production assembly (`src/Zaide.csproj` →
   `Zaide`). No project or assembly split.
2. **Movement-only:** no logic, DI lifetime, constructor signature, registration
   set, visibility, or public-API surface changes except those forced by
   namespace/path renames (e.g. `using` and fully-qualified names).
3. **No new catch-all roots:** do not create `Common/`, `Helpers/`, `Utils/`,
   `Shared/` (except approved `UI/Shared` and root `Infrastructure` under
   admission rules), or empty ceremonial layer folders.
4. **Preserve 6.1 exceptions:** every `R61-V##` / `R61-LT##` disposition stays
   in force. Movement must not “fix” 6.3/7/8 debt by rewriting types.
5. **Allowlist non-growth:** the frozen FindingId set of nine entries must not
   grow. Path keys may be rewritten to the post-move relative path in the
   **same** rollback unit as the move (remove+add of `MatchKey` only).
6. **Public baseline non-growth:** 348 public full names / 393 total / 45
   internal ceilings remain; no intentional public additions in 6.2.
7. **Matching production + tests:** each migration slice moves owned production
   files with their tests and required namespace/AXAML/resource updates.
8. **Architecture tests co-move:** every slice that introduces target folders
   must update admission ratchets / inventory expectations in
   `tests/Zaide.Tests/Architecture/` in the **same** commit so no new admission
   violation appears. That is harness adaptation, not new product debt.

---

## M0 live re-verification

### Evidence date

2026-07-17 (this M0 session).

### Git state

| Check | Result |
|-------|--------|
| Branch | `master` |
| `HEAD` | `9a0a83f5a9f92595988fb9730ca7fd184d3fd2e8` |
| Tip message | `docs: close refactor 6.1 architecture baseline` |
| vs `origin/master` | up to date |
| Working tree | clean (before this M0 doc) |

### Projects and assembly (unchanged from 6.1)

| Project | Role |
|---------|------|
| `src/Zaide.csproj` | Single `net10.0` `WinExe` production assembly `Zaide` |
| `tests/Zaide.Tests/Zaide.Tests.csproj` | xUnit; `ProjectReference` to production; `InternalsVisibleTo` |
| `Zaide.slnx` | Only the two projects above |

### Production tree (tracked)

| Folder | Tracked C# | Notes |
|--------|----------:|-------|
| `src/` root | 3 | `Program.cs`, `App.axaml.cs`, `MainWindow.axaml.cs` |
| `src/Models` | 22 | technical Models |
| `src/Services` | 224 | technical Services |
| `src/Styles` | 2 | design tokens C# |
| `src/ViewModels` | 53 | technical ViewModels |
| `src/Views` | 52 | technical Views |
| **Total C#** | **356** | matches Refactor 6.1 M0 |

Tracked AXAML (4): `App.axaml`, `MainWindow.axaml`, `Styles/Icons.axaml`,
`Views/UnsavedDialog.axaml`. Combined production sources for migration
classification: **360** path entries (356 C# + 4 AXAML).

No `src/Features/`, `src/App/`, `src/Infrastructure/`, or `src/UI/` trees exist
yet (except planned targets). Namespaces still match technical folders
(`Zaide.Models`, `Zaide.Services`, `Zaide.ViewModels`, `Zaide.Views`,
`Zaide.Styles`, root `Zaide`).

### Test tree (tracked)

| Folder | Tracked C# |
|--------|----------:|
| root | 14 |
| `Architecture/` | 17 (added by Refactor 6.1 M2–M4) |
| `DI/` | 6 |
| `Integration/` | 1 |
| `Models/` | 5 |
| `Services/` | 79 |
| `ViewModels/` | 39 |
| `Views/` | 26 |
| **Total** | **187** |

Product tests excluding Architecture harness: **170** (matches 6.1 M0 product
test count). Phase/milestone-named product tests: **23** (R61-V04), to be
rehomed/renamed durably with their feature slices.

### Architecture ratchets / allowlist (live)

| Control | Live state |
|---------|------------|
| Architecture tests | **21** passed |
| NamespaceDirection allowlist | 5 entries (R61-V02, V05×2, V06, V07) |
| LocatorSite allowlist | 4 entries (Program, App, SourceControlDiffTabService, EditorTabViewModel) |
| Approved FindingIds | **9** (exact set frozen; must not grow) |
| Public baseline | `PublicProductionTypeBaseline.txt` **348** lines |
| Root admission (C# only) | Technical folders `Models`/`Services`/`Styles`/`ViewModels`/`Views` + three root composition C# files; deny-by-default elsewhere |
| Root `Infrastructure/` / `UI/Shared/` | empty allowlist (deny-by-default) |

Allowlisted production paths (must be path-rewritten when those files move):

- `src/Models/SourceControlState.cs`
- `src/Services/ITerminalSessionFactory.cs`
- `src/Services/TerminalSessionFactory.cs`
- `src/Services/MentionParser.cs`
- `src/Services/SourceControlDiffTabService.cs`
- `src/Program.cs`
- `src/App.axaml.cs`
- `src/ViewModels/EditorTabViewModel.cs`

### Verified forbidden edges still present

Same five technical-namespace edges as Refactor 6.1 (Services→ViewModels ×4,
Models→Services ×1). No new edges found in this audit beyond the allowlist.

### Composition sizes (spot-check)

| File | Lines |
|------|------:|
| `Program.cs` | 163 |
| `App.axaml.cs` | 135 |
| `MainWindow.axaml.cs` | 983 |
| `MainWindowViewModel.cs` | 608 |

### M0 verification commands and results

Run sequentially from repository root:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
git status --short --branch
```

| Command | Result (2026-07-17) |
|---------|---------------------|
| `dotnet build Zaide.slnx --no-restore` | **Succeeded**, 0 errors, **0 warnings** |
| Architecture filter | **21 passed**, 0 failed, 0 skipped |
| `dotnet test Zaide.slnx --no-build` | **2,193 passed**, 0 failed, 0 skipped (conclusive completion) |
| `git diff --check` | clean (pre-doc) |
| `git status` | clean `master...origin/master` (pre-doc) |

**Full-suite gate:** conclusive. M0 may define migration milestones. No
implementation is authorized until human acceptance of this plan.

### Refactor 6.1 disposition map (binding)

| Class | IDs | 6.2 action |
|-------|-----|------------|
| Movement-only | R61-V01, V03, V04 | Own and complete via slices |
| Move but keep debt | R61-V02, V05–V09 | Move files; keep allowlist entries (path update only) |
| Not 6.2 | R61-V10–V14, V17 | Documented; 6.3 |
| Deferred 7/8 | R61-V15–V16, V18–V20, LT01–LT03 | Preserve behavior; no redesign |

---

## Target taxonomy (feature-first, single assembly)

### Folder map

Create a folder only when a slice places owned types into it. Do **not**
pre-create empty layer trees.

```text
src/
  App/
    Composition/          # Program, DI registration surface, command registry
    Shell/                # MainWindow, shell VMs/views, chrome
  Features/
    Editor/
    Workspace/
    Townhall/
    Agents/
    Settings/
    SourceControl/
    ProjectSystem/
    Language/
      Infrastructure/
        Lsp/              # protocol/session/parser/transport only
    Debugging/
      Infrastructure/
        Dap/              # protocol/adapter/session/parser/transport only
    Terminal/
  Infrastructure/         # admit only under root rules (deny-by-default)
    FileSystem/
    Processes/
    Persistence/
  UI/
    DesignSystem/         # tokens, icons, typography (from Styles)
    Shared/               # feature-neutral UI primitives only (deny-by-default)
```

### Namespace rule

`namespace` **must match** folder path under `src/` with a `Zaide.` prefix and
`.` separators:

| Path | Namespace |
|------|-----------|
| `src/UI/DesignSystem/LayoutTokens.cs` | `Zaide.UI.DesignSystem` |
| `src/Features/Settings/Domain/SettingsModel.cs` | `Zaide.Features.Settings.Domain` |
| `src/Features/Language/Infrastructure/Lsp/CsharpLsSession.cs` | `Zaide.Features.Language.Infrastructure.Lsp` |
| `src/App/Composition/Program.cs` | `Zaide.App.Composition` |
| `src/App/Shell/MainWindow.axaml.cs` | `Zaide.App.Shell` |

Partial classes and Avalonia `x:Class` must be updated with the file.

### Optional per-feature layers

A feature may use a **flat** feature root **or** any subset of:

| Layer | Owns |
|-------|------|
| **Domain** | Feature truth, identities, invariants (no UI/IO/DI frameworks) |
| **Application** | Use-case coordination, policies, snapshots produced by the feature |
| **Infrastructure** | FS, process, protocol, persistence, OS, libraries |
| **Presentation** | Views, ViewModels, reactive UI state |
| **Contracts** | Minimal interfaces/boundary values for other layers/features |

Do **not** create every layer ceremonially. Prefer the layer used in this plan’s
classification tables. Cross-feature consumers depend on **Contracts** (or a
deliberately exposed Application façade), never another feature’s Presentation
or Infrastructure **as a design goal**—existing 6.1 allowlisted edges may
remain until 6.3.

### Cross-feature contracts

- Prefer existing interfaces already used across features (`ISettingsService`,
  `IFileService`, etc.).
- 6.2 does **not** invent new contracts to fix 6.3 debt (e.g. no new
  editor-session factory for V07/V08).
- Snapshots remain owned by the producing feature; consumers may project them.

### Shared UI (`UI/Shared`) and design system (`UI/DesignSystem`)

| Root | Admission |
|------|-----------|
| `UI/DesignSystem` | Current `Styles` assets only in M1; tokens/icons/typography |
| `UI/Shared` | **Deny-by-default.** Admit only when ≥2 presentation owners consume a feature-neutral primitive with no feature workflow ownership. No admissions scheduled in M1–M12; candidates are classified with primary owners (see deferrals) |

### Root infrastructure

**Deny-by-default.** No M1–M12 slice places files under `src/Infrastructure/`.
Candidates with multi-feature consumers (notably `IFileService`/`FileService`)
are **parked with their primary owner (Editor)** and listed as named deferrals
for a possible post-feature **M13** admission review—not automatic migration.

### Protocol types

| Protocol | Owner path |
|----------|------------|
| LSP | `Features/Language/Infrastructure/Lsp` |
| DAP | `Features/Debugging/Infrastructure/Dap` |

Not root infrastructure (R61-V03).

### AXAML and resources

| Asset | Target |
|-------|--------|
| `Styles/Icons.axaml` | `UI/DesignSystem/Icons.axaml`; update `App.axaml` include |
| `Views/UnsavedDialog.axaml(+.cs)` | `Features/Editor/Presentation/` |
| `App.axaml` / `MainWindow.axaml` | `App/Composition` / `App/Shell` with `x:Class` updates |
| `Zaide.csproj`, `app.manifest` | stay at `src/` project root (not feature-owned) |

Non-C# admission remains outside the M3/M4 C# ratchet (6.1 limitation). Slices
must still keep AXAML compile/include paths correct.

### Test placement

Mirror production feature ownership:

```text
tests/Zaide.Tests/
  Architecture/                 # stays; harness only (not a product feature)
  Features/<Feature>/...        # preferred target for product tests
  App/...                       # shell/composition tests
  UI/DesignSystem/...           # design-system tests
```

Namespaces: `Zaide.Tests.Features.<Feature>...` (or flat `Zaide.Tests.<Feature>`
if a slice keeps a shallow folder—pick one scheme per slice and stay
consistent). Phase/milestone file names (**23**) get durable feature-oriented
names in the owning slice (assertions unchanged).

Architecture tests remain under `tests/Zaide.Tests/Architecture/` for the whole
of 6.2 unless a later refactor relocates them.

---

## Global movement rules (every migration milestone)

1. Move exact file list for the slice; update namespace + all references.
2. Move matching tests; rename phase-named tests durably when in scope.
3. Update AXAML `x:Class`, resource includes, and any path strings.
4. Update allowlist `MatchKey` paths for moved allowlisted files (FindingId set
   size unchanged).
5. Update architecture admission so **new** target folders are expected for
   moved files (technical folders remain allowed until empty). Prefer:
   expanding approved folder prefixes to include `App`, `Features`, `UI` (and
   nested paths), without admitting unowned catch-alls.
6. Do **not** change DI lifetimes, registration counts, or constructor graphs
   except type name/namespace resolution.
7. Prove: build; focused Architecture tests; full suite; `git diff --check`.
8. One commit per milestone/slice; revert = one commit rollback.

### Per-slice verification contract

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
git status --short --branch
```

Exit criteria for each slice:

- [ ] Exact file scope moved; no extras
- [ ] Namespaces match folders
- [ ] Matching tests moved/renamed; no assertion rewrites for “cleanup”
- [ ] Architecture: 0 new violations; FindingId count still 9; public count still 348
- [ ] Full tests pass with conclusive totals
- [ ] `git diff --check` clean

---

## Named deferrals (not migration targets in M1–M12)

| ID | Item | Rationale |
|----|------|-----------|
| R62-D01 | Root `Infrastructure/FileSystem` rehome of `IFileService`/`FileService` | Multi-feature consumers (Editor + Source Control) exist, but 6.1 forbids early root admission; park under Editor; optional M13 after both features land |
| R62-D02 | Root `Infrastructure/Processes` for `ManagedProcess*` | Only Project System owns current consumers; remains under ProjectSystem |
| R62-D03 | `UI/Shared` admissions (`Animations`, `IconFactory`, multi-use chrome) | Primary owner Shell for 6.2; Shared only if separate reviewed admission proves ≥2 neutral consumers |
| R62-D04 | `FileIconKeyResolver` as Shared | Multi-use presentation helper; parked with Workspace primary consumer |
| R62-D05 | Architecture harness layout | Stays `tests/Zaide.Tests/Architecture/`; not a product feature |
| R62-D06 | `XunitSettings.cs` test host plumbing | Not a product feature; leave at test root |
| R62-D07 | `.gitkeep` placeholders under Models/Services/Views | Delete only when parent technical folder is emptied by a slice that owns the cleanup; no behavior |
| R62-D08 | All R61-V10–V20 / LT01–LT03 non-movement work | Owned by 6.3 / 7 / 8 as in 6.1 matrix |
| R62-D09 | Non-C# root-admission ratchet expansion | Future decision; not required to complete 6.2 movement |
| R62-D10 | Assembly split | Explicitly rejected for 6.2 |

---

## Proposed slice order

Order follows Refactor 6.1 guidance, re-validated against live ownership. ProjectSystem is **pre-split** into M5a/M5b/M5c at M0 (not deferred to implementation time).

| Order | Milestone | Feature / module | Prod files | Notes |
|------:|-----------|------------------|------------:|-------|
| 0 | **M0** | Plan + audit | 0 | This document |
| 1 | **M1** | UI/DesignSystem | 3 | Styles → UI/DesignSystem |
| 2 | **M2** | Settings | 24 | Cohesive; secrets stay Settings-owned |
| 3 | **M3** | Workspace | 7 | Folder/tree |
| 4 | **M4** | Editor | 24 | Includes FileService (R62-D01) |
| 5 | **M5a** | ProjectSystem — discovery/context/gate/targets | 31 | Mandatory slice; own rollback |
| 6 | **M5b** | ProjectSystem — workflow/process/output | 20 | Includes ManagedProcess (R62-D02) |
| 7 | **M5c** | ProjectSystem — diagnostics/test results/problems | 20 | Problems + Test Results projections |
| 8 | **M6a** | Language (application) | 53 | Explicit full-path list |
| 9 | **M6b** | Language/Lsp | 22 | Protocol types; production-only tests |
| 10 | **M7a** | Debugging application | 18 | Session/breakpoints |
| 11 | **M7b** | Debugging/Dap | 19 | Protocol types |
| 12 | **M7c** | Debugging presentation | 18 | Panels/margins/IP |
| 13 | **M8** | SourceControl | 28 | Carry R61-V02, V07 allowlist paths |
| 14 | **M9** | Terminal | 24 | Carry R61-V05 allowlist paths |
| 15 | **M10** | Townhall | 11 | Preserve R61-V16 behavior |
| 16 | **M11** | Agents | 16 | Carry R61-V06; preserve string protocol |
| 17 | **M12** | App Composition + Shell | 22 | Last; Program/App/MainWindow |
| — | **M13** (optional) | Root Infrastructure / UI.Shared | 0 scheduled | Only after admission review |

**Total production paths in M1–M12 (including M5a/b/c):** 360 (complete coverage; zero unclassified).

**Mandatory ProjectSystem split:** M5a, M5b, and M5c are first-class milestones defined at M0 with exact production and test path lists. Implementation must not re-collapse them or invent further deferred splits without amending this plan.

---

## Milestone scopes

### M0 — Plan and live re-verification (this milestone)

**Scope:** Create this plan after read-only audit. No production/test code edits.

**Exit:**

- [x] 6.1 boundaries, ratchets, tree, ownership, git state re-verified
- [x] Build, Architecture, full suite conclusive
- [x] Taxonomy, rules, deferrals, complete classification, slice order defined
- [x] M5a/M5b/M5c mandatory exact scopes defined (no deferred split)
- [x] Every migration slice has authoritative full-path production and test lists (no globs/complements)
- [x] M0 human-acceptance gate stated
- [x] **Human accepts M0** (2026-07-17); M1 DesignSystem only is authorized

**Rollback:** delete or restore this file only.

**Exact files changed in M0:**

| File | Change |
|------|--------|
| `docs/refactor/refactor-6.2/IMPLEMENTATION_PLAN.md` | Created, amended, and accepted (this plan only) |

No separate evidence document required: counts and commands are embedded above
and match live `git ls-files` / test output.

---

### M1 — UI/DesignSystem

**Target:** `src/UI/DesignSystem/` · namespace `Zaide.UI.DesignSystem`

**Authoritative production scope (3 paths — complete, no globs):**

| Pre-move path | Post-move path |
|---------------|----------------|
| `src/Styles/Icons.axaml` | `src/UI/DesignSystem/Icons.axaml` |
| `src/Styles/LayoutTokens.cs` | `src/UI/DesignSystem/LayoutTokens.cs` |
| `src/Styles/TextStyles.cs` | `src/UI/DesignSystem/TextStyles.cs` |

**Authoritative test scope (1 path — complete, no globs):**

| Pre-move path | Post-move path |
|---------------|----------------|
| `tests/Zaide.Tests/Views/TextStylesTests.cs` | `tests/Zaide.Tests/UI/DesignSystem/TextStylesTests.cs` |

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

#### M1 completion record

**Scope executed:** Mechanical rehome of DesignSystem only. Namespace
`Zaide.Styles` → `Zaide.UI.DesignSystem`. `App.axaml` Icons include updated.
All production/test `using Zaide.Styles` updated. Public full-name baseline
rewrote the two type names (count still 348). Architecture admission admits
`src/UI/DesignSystem/` C# only under top-level `UI` (not `UI/Shared`).
Inventory tests updated for folder/namespace truth. CONVENTIONS + OVERVIEW
truthful current tree notes. No DI, visibility, or behavior changes.

**Verification (2026-07-17):**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---------|--------|
| build | Succeeded; 0 errors; 1 existing CS0067 in ProjectDebugTargetResolverTests (pre-existing) |
| Architecture | 21 passed, 0 failed |
| full suite | 2,193 passed, 0 failed, 0 skipped |
| `git diff --check` | clean |

**FindingId allowlist:** unchanged (9 entries). **Public count:** 348.

**Next:** stop after M1; human review/commit of M1 only. Do not start M2 without
authorization.

### M2 — Settings

**Target:** `src/Features/Settings/{Domain,Contracts,Infrastructure,Presentation}/` · namespace `Zaide.Features.Settings.*`

**Authoritative production scope (24 paths — complete, no globs):**

- `src/Models/SettingsLoadResult.cs`
- `src/Models/SettingsModel.cs`
- `src/Models/SettingsMutationResult.cs`
- `src/Models/SettingsSaveError.cs`
- `src/Models/SettingsSaveResult.cs`
- `src/Models/SettingsValidationError.cs`
- `src/Models/SettingsValidator.cs`
- `src/Services/FileSecretStore.cs`
- `src/Services/ISecretStore.cs`
- `src/Services/ISettingsMigration.cs`
- `src/Services/ISettingsService.cs`
- `src/Services/SettingsMigrationV1ToV2.cs`
- `src/Services/SettingsMigrationV2ToV3.cs`
- `src/Services/SettingsMigrator.cs`
- `src/Services/SettingsPathResolver.cs`
- `src/Services/SettingsSerializer.cs`
- `src/Services/SettingsService.cs`
- `src/ViewModels/SettingsViewModel.cs`
- `src/Views/FontPickerEntry.cs`
- `src/Views/InstalledFontCatalog.cs`
- `src/Views/SettingsBinding.cs`
- `src/Views/SettingsFontPicker.cs`
- `src/Views/SettingsPanelView.cs`
- `src/Views/SettingsSubscription.cs`

**Authoritative test scope (13 paths — complete, no globs):**

- `tests/Zaide.Tests/Services/FileSecretStorePermissionTests.cs`
- `tests/Zaide.Tests/Services/M9aKeyBindingMaterializationTests.cs`
- `tests/Zaide.Tests/Services/M9bSettingsDrivenRefreshTests.cs`
- `tests/Zaide.Tests/Services/SecretStoreTests.cs`
- `tests/Zaide.Tests/Services/SettingsCoreTests.cs`
- `tests/Zaide.Tests/Services/SettingsKeybindingsSchemaTests.cs`
- `tests/Zaide.Tests/TestSecretStore.cs`
- `tests/Zaide.Tests/ViewModels/SettingsViewModelTests.cs`
- `tests/Zaide.Tests/Views/InstalledFontCatalogTests.cs`
- `tests/Zaide.Tests/Views/M5SettingsUiTests.cs`
- `tests/Zaide.Tests/Views/Phase814SettingsTests.cs`
- `tests/Zaide.Tests/Views/SettingsFontPickerTests.cs`
- `tests/Zaide.Tests/Views/SettingsPanelViewTests.cs`

**Notes / non-goals:** Settings still manually composed from shell (R61-V17) — do not change construction ownership.

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

#### M2 completion record

**Scope executed:** Mechanical rehome of Settings only into
`src/Features/Settings/{Domain,Contracts,Infrastructure,Presentation}/` and
matching `tests/Zaide.Tests/Features/Settings/...`. Namespaces
`Zaide.Models` / `Zaide.Services` / `Zaide.ViewModels` / `Zaide.Views` →
`Zaide.Features.Settings.*` for the 24 production paths. Phase/milestone-named
tests renamed durably without assertion changes:

| Pre-move test | Durable name |
|---------------|--------------|
| `M9aKeyBindingMaterializationTests` | `KeyBindingMaterializationTests` |
| `M9bSettingsDrivenRefreshTests` | `SettingsDrivenKeyBindingRefreshTests` |
| `M5SettingsUiTests` | `SettingsUiTests` |
| `Phase814SettingsTests` | `SettingsPersistenceUiTests` |

Public full-name baseline rewrote Settings type names (count still 348).
Architecture admission admits `src/Features/Settings/` C# only under top-level
`Features`. Inventory tests updated for folder/namespace truth. CONVENTIONS +
OVERVIEW truthful current-tree notes. No DI, visibility, shell-composition, or
behavior changes. FindingId allowlist unchanged (9). No allowlist path rewrites
required for this slice.

**Verification (2026-07-17):**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---------|--------|
| build | Succeeded; 0 errors; 1 existing CS0067 in ProjectDebugTargetResolverTests (pre-existing) |
| Architecture | 21 passed, 0 failed |
| full suite | 2,193 passed, 0 failed, 0 skipped |
| `git diff --check` | clean |

**FindingId allowlist:** unchanged (9 entries). **Public count:** 348.

**Next:** stop after M2; human review/commit of M2 only. Do not start M3 without
authorization.

### M3 — Workspace

**Target:** `src/Features/Workspace/...` · namespace `Zaide.Features.Workspace.*`

**Authoritative production scope (7 paths — complete, no globs):**

- `src/Models/FileTreeNode.cs`
- `src/Models/Workspace.cs`
- `src/Services/FileTreeService.cs`
- `src/Services/IFileTreeService.cs`
- `src/ViewModels/FileTreeViewModel.cs`
- `src/Views/FileIconKeyResolver.cs`
- `src/Views/FileTreeView.cs`

**Authoritative test scope (4 paths — complete, no globs):**

- `tests/Zaide.Tests/Models/WorkspaceTests.cs`
- `tests/Zaide.Tests/Services/FileTreeServiceTests.cs`
- `tests/Zaide.Tests/ViewModels/FileTreeViewModelTests.cs`
- `tests/Zaide.Tests/Views/FileIconKeyResolverTests.cs`

**Notes / non-goals:** FileIconKeyResolver parked here (R62-D04).

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

**M3 completion record (2026-07-17):**

**Scope executed:** Mechanical rehome of Workspace only into
`src/Features/Workspace/{Domain,Contracts,Infrastructure,Presentation}/` and
matching `tests/Zaide.Tests/Features/Workspace/...`. Namespaces
`Zaide.Models` / `Zaide.Services` / `Zaide.ViewModels` / `Zaide.Views` →
`Zaide.Features.Workspace.*` for the 7 production paths:

| Pre-move path | Post-move path | Namespace |
|---------------|----------------|-----------|
| `src/Models/FileTreeNode.cs` | `.../Domain/FileTreeNode.cs` | `Zaide.Features.Workspace.Domain` |
| `src/Models/Workspace.cs` | `.../Domain/Workspace.cs` | `Zaide.Features.Workspace.Domain` |
| `src/Services/IFileTreeService.cs` | `.../Contracts/IFileTreeService.cs` | `Zaide.Features.Workspace.Contracts` |
| `src/Services/FileTreeService.cs` | `.../Infrastructure/FileTreeService.cs` | `Zaide.Features.Workspace.Infrastructure` |
| `src/ViewModels/FileTreeViewModel.cs` | `.../Presentation/FileTreeViewModel.cs` | `Zaide.Features.Workspace.Presentation` |
| `src/Views/FileIconKeyResolver.cs` | `.../Presentation/FileIconKeyResolver.cs` | `Zaide.Features.Workspace.Presentation` |
| `src/Views/FileTreeView.cs` | `.../Presentation/FileTreeView.cs` | `Zaide.Features.Workspace.Presentation` |

Matching tests rehomed (no durable renames required). `FileIconKeyResolver`
remains Workspace-owned (R62-D04). Public full-name baseline rewrote Workspace
type names (count still 348). Architecture admission admits
`src/Features/Workspace/` C# under top-level `Features` (alongside Settings).
Inventory tests updated for folder/namespace truth. CONVENTIONS + OVERVIEW
truthful current-tree notes. No DI, visibility, shell-composition, or behavior
changes. FindingId allowlist unchanged (9). No allowlist path rewrites required
for this slice. Under `Zaide.*.Features.*` namespaces, short name `Workspace`
binds to the sibling/parent namespace segment; affected test sites use
`global::Zaide.Features.Workspace.Domain.Workspace` (mechanical reference fix
only).

**Verification (2026-07-17):**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---------|--------|
| build | Succeeded; 0 errors; 1 existing CS0067 in ProjectDebugTargetResolverTests (pre-existing) |
| Architecture | 21 passed, 0 failed |
| full suite | 2,193 passed, 0 failed, 0 skipped |
| `git diff --check` | clean |

**FindingId allowlist:** unchanged (9 entries). **Public count:** 348.

**Next:** stop after M3; human review/commit of M3 only. Do not start M4 without
authorization.

### M4 — Editor

**Target:** `src/Features/Editor/...` · namespace `Zaide.Features.Editor.*`

**Authoritative production scope (24 paths — complete, no globs):**

- `src/Models/Document.cs`
- `src/Services/BraceFoldingStrategy.cs`
- `src/Services/FileService.cs`
- `src/Services/IFileService.cs`
- `src/Services/SupportedFileTypes.cs`
- `src/ViewModels/EditorLanguageInputViewModel.cs`
- `src/ViewModels/EditorSearchViewModel.cs`
- `src/ViewModels/EditorTabViewModel.cs`
- `src/ViewModels/EditorViewModel.cs`
- `src/ViewModels/IEditorLanguageOperations.cs`
- `src/ViewModels/IEditorTextOperations.cs`
- `src/ViewModels/IFoldingOperations.cs`
- `src/ViewModels/SearchEngine.cs`
- `src/Views/EditorCompletionPopup.cs`
- `src/Views/EditorHoverPopup.cs`
- `src/Views/EditorLanguagePickerPopup.cs`
- `src/Views/EditorTabBar.cs`
- `src/Views/EditorView.cs`
- `src/Views/FoldingOperations.cs`
- `src/Views/IndentGuideMetrics.cs`
- `src/Views/IndentGuideRenderer.cs`
- `src/Views/SearchBar.cs`
- `src/Views/UnsavedDialog.axaml`
- `src/Views/UnsavedDialog.axaml.cs`

**Authoritative test scope (25 paths — complete, no globs):**

- `tests/Zaide.Tests/Models/DocumentTests.cs`
- `tests/Zaide.Tests/Phase9M0EditorUxProofTests.cs`
- `tests/Zaide.Tests/Services/FormatDocumentCommandTests.cs`
- `tests/Zaide.Tests/Services/FormatOnSaveTests.cs`
- `tests/Zaide.Tests/Services/MockFileService.cs`
- `tests/Zaide.Tests/Services/Phase13M0EditorMeasurementSeam.cs`
- `tests/Zaide.Tests/Services/Phase13M0EditorMeasurementTests.cs`
- `tests/Zaide.Tests/Services/Phase13M4aCriticalPathEvidenceTests.cs`
- `tests/Zaide.Tests/Services/SupportedFileTypesTests.cs`
- `tests/Zaide.Tests/Services/TabCommandRegistrationTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorFoldingTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorLanguageInputRoutingTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorSearchIntegrationTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorSearchViewModelTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorTabBarLifecycleTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorTabReorderTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorTabViewModelTabLifecycleTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorTabViewModelTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorViewGrammarTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorViewModelTests.cs`
- `tests/Zaide.Tests/Views/EditorFormattingApplyTests.cs`
- `tests/Zaide.Tests/Views/EditorSelectionProjectionTests.cs`
- `tests/Zaide.Tests/Views/IndentGuideMetricsTests.cs`
- `tests/Zaide.Tests/Views/SearchBarViewTests.cs`
- `tests/Zaide.Tests/Views/UnsavedDialogTests.cs`

**Allowlist path rewrite(s):** R61-AL-LOC-EditorTabViewModel → new path of EditorTabViewModel.cs

**Notes / non-goals:** Editor breakpoint/IP types are Debugging (M7c), not Editor. FileService primary owner (R62-D01).

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

**M4 completion record (2026-07-17):**

**Scope executed:** Mechanical rehome of Editor only into
`src/Features/Editor/{Domain,Contracts,Infrastructure,Presentation}/` and
matching `tests/Zaide.Tests/Features/Editor/...`. Namespaces
`Zaide.Models` / `Zaide.Services` / `Zaide.ViewModels` / `Zaide.Views` →
`Zaide.Features.Editor.*` for the 24 production paths:

| Pre-move path | Post-move path | Namespace |
|---------------|----------------|-----------|
| `src/Models/Document.cs` | `.../Domain/Document.cs` | `Zaide.Features.Editor.Domain` |
| `src/Services/BraceFoldingStrategy.cs` | `.../Domain/BraceFoldingStrategy.cs` | `Zaide.Features.Editor.Domain` |
| `src/Services/SupportedFileTypes.cs` | `.../Domain/SupportedFileTypes.cs` | `Zaide.Features.Editor.Domain` |
| `src/ViewModels/SearchEngine.cs` | `.../Domain/SearchEngine.cs` | `Zaide.Features.Editor.Domain` |
| `src/Services/IFileService.cs` | `.../Contracts/IFileService.cs` | `Zaide.Features.Editor.Contracts` |
| `src/ViewModels/IEditorLanguageOperations.cs` | `.../Contracts/IEditorLanguageOperations.cs` | `Zaide.Features.Editor.Contracts` |
| `src/ViewModels/IEditorTextOperations.cs` | `.../Contracts/IEditorTextOperations.cs` | `Zaide.Features.Editor.Contracts` |
| `src/ViewModels/IFoldingOperations.cs` | `.../Contracts/IFoldingOperations.cs` | `Zaide.Features.Editor.Contracts` |
| `src/Services/FileService.cs` | `.../Infrastructure/FileService.cs` | `Zaide.Features.Editor.Infrastructure` |
| `src/ViewModels/EditorLanguageInputViewModel.cs` | `.../Presentation/EditorLanguageInputViewModel.cs` | `Zaide.Features.Editor.Presentation` |
| `src/ViewModels/EditorSearchViewModel.cs` | `.../Presentation/EditorSearchViewModel.cs` | `Zaide.Features.Editor.Presentation` |
| `src/ViewModels/EditorTabViewModel.cs` | `.../Presentation/EditorTabViewModel.cs` | `Zaide.Features.Editor.Presentation` |
| `src/ViewModels/EditorViewModel.cs` | `.../Presentation/EditorViewModel.cs` | `Zaide.Features.Editor.Presentation` |
| `src/Views/EditorCompletionPopup.cs` | `.../Presentation/EditorCompletionPopup.cs` | `Zaide.Features.Editor.Presentation` |
| `src/Views/EditorHoverPopup.cs` | `.../Presentation/EditorHoverPopup.cs` | `Zaide.Features.Editor.Presentation` |
| `src/Views/EditorLanguagePickerPopup.cs` | `.../Presentation/EditorLanguagePickerPopup.cs` | `Zaide.Features.Editor.Presentation` |
| `src/Views/EditorTabBar.cs` | `.../Presentation/EditorTabBar.cs` | `Zaide.Features.Editor.Presentation` |
| `src/Views/EditorView.cs` | `.../Presentation/EditorView.cs` | `Zaide.Features.Editor.Presentation` |
| `src/Views/FoldingOperations.cs` | `.../Presentation/FoldingOperations.cs` | `Zaide.Features.Editor.Presentation` |
| `src/Views/IndentGuideMetrics.cs` | `.../Presentation/IndentGuideMetrics.cs` | `Zaide.Features.Editor.Presentation` |
| `src/Views/IndentGuideRenderer.cs` | `.../Presentation/IndentGuideRenderer.cs` | `Zaide.Features.Editor.Presentation` |
| `src/Views/SearchBar.cs` | `.../Presentation/SearchBar.cs` | `Zaide.Features.Editor.Presentation` |
| `src/Views/UnsavedDialog.axaml` | `.../Presentation/UnsavedDialog.axaml` | (x:Class Presentation) |
| `src/Views/UnsavedDialog.axaml.cs` | `.../Presentation/UnsavedDialog.axaml.cs` | `Zaide.Features.Editor.Presentation` |

Matching tests rehomed (25). Phase/milestone-named tests renamed durably without
assertion changes:

| Pre-move test | Durable name |
|---------------|--------------|
| `Phase9M0EditorUxProofTests` | `EditorUxProofTests` |
| `Phase13M0EditorMeasurementSeam` | `EditorMeasurementSeam` |
| `Phase13M0EditorMeasurementTests` | `EditorMeasurementTests` |
| `Phase13M4aCriticalPathEvidenceTests` | `EditorCriticalPathEvidenceTests` |

`IFileService`/`FileService` remain Editor-owned (R62-D01). Editor breakpoint/IP
types remain in technical ViewModels (Debugging M7c). Allowlist path rewrite only:
`R61-AL-LOC-EditorTabViewModel` → `src/Features/Editor/Presentation/EditorTabViewModel.cs`
(FindingId set still 9). Public full-name baseline rewrote Editor type names
including nested `BraceRegion`/`SearchMatch` (count still 348). Architecture
admission admits `src/Features/Editor/` C# under top-level `Features` (alongside
Settings and Workspace). Inventory tests updated for folder/namespace truth.
CONVENTIONS + OVERVIEW truthful current-tree notes. No DI, visibility,
constructor signature, or behavior changes. Under `Zaide.*.Features.*`
namespaces, short name `Workspace` binds to the sibling/parent namespace
segment; affected sites use `global::Zaide.Features.Workspace.Domain.Workspace`
(mechanical reference fix only).

**Verification (2026-07-17):**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---------|--------|
| build | Succeeded; 0 errors; 1 existing CS0067 in ProjectDebugTargetResolverTests (pre-existing) |
| Architecture | 21 passed, 0 failed |
| full suite | 2,193 passed, 0 failed, 0 skipped |
| `git diff --check` | clean |

**FindingId allowlist:** unchanged (9 entries; EditorTabViewModel MatchKey path rewritten). **Public count:** 348.

**Next:** stop after M4; human review/commit of M4 only. Do not start M5+ without
authorization.

### M5a — ProjectSystem — discovery / context / operation gate / targets / project debug launch

**Target:** `src/Features/ProjectSystem/...` · namespace `Zaide.Features.ProjectSystem.*`

**Authoritative production scope (31 paths — complete, no globs):**

- `src/Services/FileSystemProjectFileSystem.cs`
- `src/Services/IProjectContextService.cs`
- `src/Services/IProjectDebugLaunchService.cs`
- `src/Services/IProjectDebugTargetResolver.cs`
- `src/Services/IProjectDiscovery.cs`
- `src/Services/IProjectFileSystem.cs`
- `src/Services/IProjectOperationGate.cs`
- `src/Services/IProjectOperationHandoffLease.cs`
- `src/Services/IProjectOperationLease.cs`
- `src/Services/ProjectCandidate.cs`
- `src/Services/ProjectContext.cs`
- `src/Services/ProjectContextService.cs`
- `src/Services/ProjectContextState.cs`
- `src/Services/ProjectDebugLaunchService.cs`
- `src/Services/ProjectDebugTargetResolution.cs`
- `src/Services/ProjectDebugTargetResolutionKind.cs`
- `src/Services/ProjectDebugTargetResolver.cs`
- `src/Services/ProjectDiscovery.cs`
- `src/Services/ProjectDiscoveryFailure.cs`
- `src/Services/ProjectDiscoveryResult.cs`
- `src/Services/ProjectExecutionProfile.cs`
- `src/Services/ProjectExecutionProfileResolver.cs`
- `src/Services/ProjectKind.cs`
- `src/Services/ProjectOperationAcquireResult.cs`
- `src/Services/ProjectOperationGate.cs`
- `src/Services/ProjectOperationGateMessages.cs`
- `src/Services/ProjectOperationKind.cs`
- `src/Services/ProjectOperationRejectionReason.cs`
- `src/Services/ProjectTargetResolution.cs`
- `src/Services/ProjectTargetResolver.cs`
- `src/Services/ResolvedProjectTarget.cs`

**Authoritative test scope (11 paths — complete, no globs):**

- `tests/Zaide.Tests/DI/Phase83M3DependencyInjectionTests.cs`
- `tests/Zaide.Tests/Services/Phase83M0DiscoveryProofTests.cs`
- `tests/Zaide.Tests/Services/Phase83M1ProjectDiscoveryTests.cs`
- `tests/Zaide.Tests/Services/Phase83M2ProjectContextServiceTests.cs`
- `tests/Zaide.Tests/Services/Phase83M3ProjectContextServiceIntegrationTests.cs`
- `tests/Zaide.Tests/Services/Phase8ProofOfConceptTests.cs`
- `tests/Zaide.Tests/Services/ProjectDebugLaunchServiceTests.cs`
- `tests/Zaide.Tests/Services/ProjectDebugTargetResolverTests.cs`
- `tests/Zaide.Tests/Services/ProjectOperationGateTests.cs`
- `tests/Zaide.Tests/Services/ProjectTargetResolutionTests.cs`
- `tests/Zaide.Tests/TestOperationGateFactory.cs`

**Notes / non-goals:** Mandatory independent milestone defined at M0. Do not merge with M5b/M5c.

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

#### M5a completion record

**Scope executed:** Mechanical rehome of ProjectSystem discovery / context /
operation gate / targets / project debug launch only into
`src/Features/ProjectSystem/{Domain,Contracts,Infrastructure}/` and matching
`tests/Zaide.Tests/Features/ProjectSystem/...`. Namespaces `Zaide.Services` →
`Zaide.Features.ProjectSystem.*` for the 31 production paths:

| Pre-move path | Post-move path | Namespace |
|---------------|----------------|-----------|
| `src/Services/IProject*.cs` (8 interfaces) + `ProjectOperationAcquireResult.cs` | `.../Contracts/` | `Zaide.Features.ProjectSystem.Contracts` |
| Pure models/enums/results (14 files) | `.../Domain/` | `Zaide.Features.ProjectSystem.Domain` |
| Implementations + resolvers (8 files) | `.../Infrastructure/` | `Zaide.Features.ProjectSystem.Infrastructure` |

Phase/milestone-named tests renamed durably without assertion changes:

| Pre-move test | Durable name |
|---------------|--------------|
| `Phase83M3DependencyInjectionTests` | `ProjectSystemDependencyInjectionTests` |
| `Phase83M0DiscoveryProofTests` | `ProjectDiscoveryProofTests` |
| `Phase83M1ProjectDiscoveryTests` | `ProjectDiscoveryTests` |
| `Phase83M2ProjectContextServiceTests` | `ProjectContextServiceTests` |
| `Phase83M3ProjectContextServiceIntegrationTests` | `ProjectContextServiceIntegrationTests` |
| `Phase8ProofOfConceptTests` | `ProjectSystemProofOfConceptTests` |

Public full-name baseline rewrote ProjectSystem type names (count still 348).
Architecture admission admits `src/Features/ProjectSystem/` C# under top-level
`Features` (alongside Settings, Workspace, and Editor). Inventory tests updated
for folder/namespace truth. CONVENTIONS + OVERVIEW truthful current-tree notes.
No DI registration/lifetime, visibility, constructor signature, or behavior
changes. FindingId allowlist unchanged (9). No allowlist path rewrites required
for this slice. Under `Zaide.*.Features.*` namespaces, short name `Workspace`
binds to the sibling/parent namespace segment; affected sites use
`global::Zaide.Features.Workspace.Domain.Workspace` (mechanical reference fix
only). Workflow, managed-process, output, diagnostics, test-results, and
Problems remain in technical layers for M5b/M5c.

**Verification (2026-07-17):**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---------|--------|
| build | Succeeded; 0 errors; 1 existing CS0067 in ProjectDebugTargetResolverTests when tests recompile (pre-existing) |
| Architecture | 21 passed, 0 failed |
| full suite | 2,193 passed, 0 failed, 0 skipped |
| `git diff --check` | clean |

**FindingId allowlist:** unchanged (9 entries). **Public count:** 348.

**Next:** stop after M5a; human review/commit of M5a only. Do not start M5b+ without
authorization.

### M5b — ProjectSystem — workflow / managed process / output

**Target:** `src/Features/ProjectSystem/...` · namespace `Zaide.Features.ProjectSystem.*`

**Authoritative production scope (20 paths — complete, no globs):**

- `src/Services/IManagedProcessRunner.cs`
- `src/Services/IProjectOutputService.cs`
- `src/Services/IProjectWorkflowService.cs`
- `src/Services/ManagedProcessOutputLine.cs`
- `src/Services/ManagedProcessRunResult.cs`
- `src/Services/ManagedProcessRunner.cs`
- `src/Services/ManagedProcessStartRequest.cs`
- `src/Services/ProcessStreamKind.cs`
- `src/Services/ProjectOutputService.cs`
- `src/Services/ProjectOutputSnapshot.cs`
- `src/Services/ProjectWorkflowOperation.cs`
- `src/Services/ProjectWorkflowOperationResult.cs`
- `src/Services/ProjectWorkflowOperationState.cs`
- `src/Services/ProjectWorkflowOutcomeKind.cs`
- `src/Services/ProjectWorkflowService.cs`
- `src/Services/ProjectWorkflowSnapshot.cs`
- `src/Services/ProjectWorkflowStatusPolicy.cs`
- `src/ViewModels/OutputLineViewModel.cs`
- `src/ViewModels/ProjectWorkflowViewModel.cs`
- `src/Views/OutputPanel.cs`

**Authoritative test scope (12 paths — complete, no globs):**

- `tests/Zaide.Tests/DI/ProjectWorkflowProjectionShutdownTests.cs`
- `tests/Zaide.Tests/DI/ProjectWorkflowServiceDiTests.cs`
- `tests/Zaide.Tests/Services/ManagedProcessRunnerTests.cs`
- `tests/Zaide.Tests/Services/ProjectBuildCommandTests.cs`
- `tests/Zaide.Tests/Services/ProjectOutputServiceTests.cs`
- `tests/Zaide.Tests/Services/ProjectRunCommandTests.cs`
- `tests/Zaide.Tests/Services/ProjectTestCommandTests.cs`
- `tests/Zaide.Tests/Services/ProjectWorkflowSaveBeforeStartTests.cs`
- `tests/Zaide.Tests/Services/ProjectWorkflowServiceTests.cs`
- `tests/Zaide.Tests/Services/ProjectWorkflowStatusPolicyTests.cs`
- `tests/Zaide.Tests/TestProjectWorkflowFactory.cs`
- `tests/Zaide.Tests/Views/OutputPanelScrollFollowTests.cs`

**Notes / non-goals:** ManagedProcess* stays here (R62-D02). Mandatory independent milestone defined at M0.

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

#### M5b completion record

**Scope executed:** Mechanical rehome of ProjectSystem workflow / managed process /
output only into `src/Features/ProjectSystem/{Domain,Contracts,Infrastructure,Presentation}/`
and matching `tests/Zaide.Tests/Features/ProjectSystem/...`. Namespaces
`Zaide.Services` / `Zaide.ViewModels` / `Zaide.Views` → `Zaide.Features.ProjectSystem.*`
for the 20 production paths:

| Pre-move path | Post-move path | Namespace |
|---------------|----------------|-----------|
| `IManagedProcessRunner`, `IProjectOutputService`, `IProjectWorkflowService` | `.../Contracts/` | `Zaide.Features.ProjectSystem.Contracts` |
| ManagedProcess models/enums, workflow models/enums/policy, output snapshot (11 files) | `.../Domain/` | `Zaide.Features.ProjectSystem.Domain` |
| `ManagedProcessRunner`, `ProjectOutputService`, `ProjectWorkflowService` | `.../Infrastructure/` | `Zaide.Features.ProjectSystem.Infrastructure` |
| `OutputLineViewModel`, `ProjectWorkflowViewModel`, `OutputPanel` | `.../Presentation/` | `Zaide.Features.ProjectSystem.Presentation` |

Matching 12 tests rehomed (no durable renames required). `ManagedProcess*` remains
ProjectSystem-owned (R62-D02). Public full-name baseline rewrote the 20 type names
(count still 348). Architecture inventory updated for folder/namespace truth.
CONVENTIONS + OVERVIEW truthful current-tree notes. No DI registration/lifetime,
visibility, constructor signature, or behavior changes. FindingId allowlist
unchanged (9). Diagnostics, test-results, Problems, and `BuildDiagnostic*` remain
in technical layers for M5c.

**Verification (2026-07-17):**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---------|--------|
| build | Succeeded; 0 errors; 1 existing CS0067 in ProjectDebugTargetResolverTests (pre-existing) |
| Architecture | 21 passed, 0 failed |
| full suite | 2,193 passed, 0 failed, 0 skipped |
| `git diff --check` | clean |

**FindingId allowlist:** unchanged (9 entries). **Public count:** 348.

**Next:** stop after M5b; human review/commit of M5b only. Do not start M5c+ without
authorization.

### M5c — ProjectSystem — build diagnostics / test results / problems projections

**Target:** `src/Features/ProjectSystem/...` · namespace `Zaide.Features.ProjectSystem.*`

**Authoritative production scope (20 paths — complete, no globs):**

- `src/Services/BuildDiagnostic.cs`
- `src/Services/BuildDiagnosticParser.cs`
- `src/Services/BuildDiagnosticSources.cs`
- `src/Services/BuildDiagnosticsService.cs`
- `src/Services/BuildDiagnosticsSnapshot.cs`
- `src/Services/IBuildDiagnosticsService.cs`
- `src/Services/ITestResultsService.cs`
- `src/Services/TestCaseOutcome.cs`
- `src/Services/TestCaseResult.cs`
- `src/Services/TestResultsParser.cs`
- `src/Services/TestResultsService.cs`
- `src/Services/TestResultsSnapshot.cs`
- `src/Services/TestResultsSummary.cs`
- `src/ViewModels/ProblemItemViewModel.cs`
- `src/ViewModels/ProblemKind.cs`
- `src/ViewModels/ProblemsViewModel.cs`
- `src/ViewModels/TestCaseItemViewModel.cs`
- `src/ViewModels/TestResultsViewModel.cs`
- `src/Views/ProblemsPanel.cs`
- `src/Views/TestResultsPanel.cs`

**Authoritative test scope (12 paths — complete, no globs):**

- `tests/Zaide.Tests/Services/BuildDiagnosticParserTests.cs`
- `tests/Zaide.Tests/Services/BuildDiagnosticsServiceTests.cs`
- `tests/Zaide.Tests/Services/TestResultsParserTests.cs`
- `tests/Zaide.Tests/Services/TestResultsServiceTests.cs`
- `tests/Zaide.Tests/TestProblemsFactory.cs`
- `tests/Zaide.Tests/TestTestResultsFactory.cs`
- `tests/Zaide.Tests/ViewModels/Phase83M4MainWindowViewModelProjectionTests.cs`
- `tests/Zaide.Tests/ViewModels/Phase83M4StatusBarViewModelProjectionTests.cs`
- `tests/Zaide.Tests/ViewModels/ProblemsBuildProjectionTests.cs`
- `tests/Zaide.Tests/ViewModels/ProblemsNavigationProjectionTests.cs`
- `tests/Zaide.Tests/ViewModels/ProblemsViewModelTests.cs`
- `tests/Zaide.Tests/ViewModels/TestResultsViewModelTests.cs`

**Notes / non-goals:** Mandatory independent milestone defined at M0. Phase83M4 projection tests assert project projections, not shell ownership.

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

#### M5c completion record

**Scope executed:** Mechanical rehome of ProjectSystem build diagnostics / test
results / Problems projections only into
`src/Features/ProjectSystem/{Domain,Contracts,Infrastructure,Presentation}/`
and matching `tests/Zaide.Tests/Features/ProjectSystem/...`. Namespaces
`Zaide.Services` / `Zaide.ViewModels` / `Zaide.Views` → `Zaide.Features.ProjectSystem.*`
for the 20 production paths:

| Pre-move path | Post-move path | Namespace |
|---------------|----------------|-----------|
| `IBuildDiagnosticsService`, `ITestResultsService` | `.../Contracts/` | `Zaide.Features.ProjectSystem.Contracts` |
| Build/test models, parsers, snapshots, sources (9 files) | `.../Domain/` | `Zaide.Features.ProjectSystem.Domain` |
| `BuildDiagnosticsService`, `TestResultsService` | `.../Infrastructure/` | `Zaide.Features.ProjectSystem.Infrastructure` |
| Problems + Test Results ViewModels/Views (7 files) | `.../Presentation/` | `Zaide.Features.ProjectSystem.Presentation` |

Matching 12 tests rehomed. Durable renames (projection-oriented, not shell ownership):

| Pre-move name | Post-move name |
|---------------|----------------|
| `Phase83M4MainWindowViewModelProjectionTests` | `ProjectSystemMainWindowViewModelProjectionTests` |
| `Phase83M4StatusBarViewModelProjectionTests` | `ProjectSystemStatusBarViewModelProjectionTests` |

Public full-name baseline rewrote the 20 type names (count still 348).
Architecture inventory updated for folder/namespace truth.
CONVENTIONS + OVERVIEW truthful current-tree notes. No DI registration/lifetime,
visibility, constructor signature, or behavior changes. FindingId allowlist
unchanged (9). Language and later features remain for M6+.

**Verification (2026-07-17):**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---------|--------|
| build | Succeeded; 0 errors; 1 existing CS0067 in ProjectDebugTargetResolverTests (pre-existing) |
| Architecture | 21 passed, 0 failed |
| full suite | 2,193 passed, 0 failed, 0 skipped |
| `git diff --check` | clean |

**FindingId allowlist:** unchanged (9 entries). **Public count:** 348.

**Next:** stop after M5c; human review/commit of M5c only. Do not start M6+ without
authorization.

### M6a — Language — application / contracts (non-Lsp)

**Target:** `src/Features/Language/{Contracts,Application}/` · namespace `Zaide.Features.Language.*`

**Authoritative production scope (53 paths — complete, no globs):**

- `src/Services/ILanguageCompletionService.cs`
- `src/Services/ILanguageDiagnosticsService.cs`
- `src/Services/ILanguageDocumentBridge.cs`
- `src/Services/ILanguageFormattingService.cs`
- `src/Services/ILanguageHoverService.cs`
- `src/Services/ILanguageNavigationService.cs`
- `src/Services/ILanguageSessionService.cs`
- `src/Services/ILanguageSymbolService.cs`
- `src/Services/LanguageActiveDocumentValidator.cs`
- `src/Services/LanguageCommandAvailability.cs`
- `src/Services/LanguageCompletionCommit.cs`
- `src/Services/LanguageCompletionItem.cs`
- `src/Services/LanguageCompletionItemMapper.cs`
- `src/Services/LanguageCompletionService.cs`
- `src/Services/LanguageCompletionSnapshot.cs`
- `src/Services/LanguageCompletionState.cs`
- `src/Services/LanguageCompletionTriggerPolicy.cs`
- `src/Services/LanguageDiagnostic.cs`
- `src/Services/LanguageDiagnosticSeverity.cs`
- `src/Services/LanguageDiagnosticsService.cs`
- `src/Services/LanguageDiagnosticsSnapshot.cs`
- `src/Services/LanguageDocumentBridge.cs`
- `src/Services/LanguageDocumentSyncPolicy.cs`
- `src/Services/LanguageDocumentUri.cs`
- `src/Services/LanguageFormattingEditApplier.cs`
- `src/Services/LanguageFormattingOutcome.cs`
- `src/Services/LanguageFormattingPolicy.cs`
- `src/Services/LanguageFormattingService.cs`
- `src/Services/LanguageFormattingSnapshot.cs`
- `src/Services/LanguageFormattingState.cs`
- `src/Services/LanguageHoverService.cs`
- `src/Services/LanguageHoverSnapshot.cs`
- `src/Services/LanguageHoverState.cs`
- `src/Services/LanguageHoverTriggerPolicy.cs`
- `src/Services/LanguageLocation.cs`
- `src/Services/LanguageLocationOrdering.cs`
- `src/Services/LanguageNavigationPolicy.cs`
- `src/Services/LanguageNavigationService.cs`
- `src/Services/LanguageNavigationSnapshot.cs`
- `src/Services/LanguageNavigationState.cs`
- `src/Services/LanguageSessionFailure.cs`
- `src/Services/LanguageSessionFailureKind.cs`
- `src/Services/LanguageSessionService.cs`
- `src/Services/LanguageSessionSnapshot.cs`
- `src/Services/LanguageSessionState.cs`
- `src/Services/LanguageSessionStatusPolicy.cs`
- `src/Services/LanguageSymbol.cs`
- `src/Services/LanguageSymbolPolicy.cs`
- `src/Services/LanguageSymbolService.cs`
- `src/Services/LanguageSymbolSnapshot.cs`
- `src/Services/LanguageSymbolState.cs`
- `src/Services/LanguageTextEdit.cs`
- `src/Services/LanguageTrackedDocumentInfo.cs`

**Authoritative test scope (12 paths — complete, no globs):**

- `tests/Zaide.Tests/DI/LanguageSessionServiceDiTests.cs`
- `tests/Zaide.Tests/Services/LanguageCompletionTests.cs`
- `tests/Zaide.Tests/Services/LanguageDiagnosticsServiceTests.cs`
- `tests/Zaide.Tests/Services/LanguageDocumentSyncTests.cs`
- `tests/Zaide.Tests/Services/LanguageFormattingTests.cs`
- `tests/Zaide.Tests/Services/LanguageHoverTests.cs`
- `tests/Zaide.Tests/Services/LanguageNavigationTests.cs`
- `tests/Zaide.Tests/Services/LanguageSessionServiceTests.cs`
- `tests/Zaide.Tests/Services/LanguageSessionStatusPolicyTests.cs`
- `tests/Zaide.Tests/Services/LanguageSymbolTests.cs`
- `tests/Zaide.Tests/Services/TestLanguageServerSession.cs`
- `tests/Zaide.Tests/ViewModels/LanguageCommandAvailabilityTests.cs`

**Notes / non-goals:** Authoritative explicit list only; no glob/except classification at implementation time. TestLanguageServerSession is the session-service test double and stays with M6a.

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

#### M6a completion record

**Scope executed:** Mechanical rehome of Language application/contracts only
into `src/Features/Language/{Contracts,Application}/` and matching
`tests/Zaide.Tests/Features/Language/...`. Namespaces `Zaide.Services` →
`Zaide.Features.Language.*` for the 53 production paths:

| Pre-move path | Post-move path | Namespace |
|---------------|----------------|-----------|
| `ILanguage*Service`, `ILanguageDocumentBridge` (8 files) | `.../Contracts/` | `Zaide.Features.Language.Contracts` |
| Non-LSP language application types, snapshots, policies, mappers, document bridge, session service/status types (45 files) | `.../Application/` | `Zaide.Features.Language.Application` |

Matching 12 tests rehomed (including `TestLanguageServerSession`). No
phase/milestone test renames required in the authoritative list. LSP
transport/parser/session types (`CsharpLsSession`, `LanguageServer*`,
`LspRange`, `LspUtf16PositionMapper`, etc.) remain under `src/Services/` for
M6b.

Public full-name baseline rewrote the 50 type names (count still 348).
Architecture inventory updated for folder/namespace truth; Features admission
includes `src/Features/Language/`. CONVENTIONS + OVERVIEW truthful current-tree
notes. No DI registration/lifetime, visibility, constructor signature, or
behavior changes. FindingId allowlist unchanged (9). M6b+ Language LSP and
later features remain.

**Verification (2026-07-17):**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---------|--------|
| build | Succeeded; 0 errors; 1 existing CS0067 in ProjectDebugTargetResolverTests (pre-existing) |
| Architecture | 21 passed, 0 failed |
| full suite | 2,193 passed, 0 failed, 0 skipped |
| `git diff --check` | clean |

**FindingId allowlist:** unchanged (9 entries). **Public count:** 348.

**Next:** stop after M6a; human review/commit of M6a only. Do not start M6b+ without
authorization.

### M6b — Language — Lsp infrastructure

**Target:** `src/Features/Language/Infrastructure/Lsp/` · namespace `Zaide.Features.Language.Infrastructure.Lsp`

**Authoritative production scope (22 paths — complete, no globs):**

- `src/Services/CsharpLsSession.cs`
- `src/Services/CsharpLsSessionFactory.cs`
- `src/Services/ILanguageServerBinaryLocator.cs`
- `src/Services/ILanguageServerSession.cs`
- `src/Services/ILanguageServerSessionFactory.cs`
- `src/Services/LanguageServerBinaryLocator.cs`
- `src/Services/LanguageServerCapabilities.cs`
- `src/Services/LanguageServerCapabilitiesParser.cs`
- `src/Services/LanguageServerCompletionParser.cs`
- `src/Services/LanguageServerCompletionResult.cs`
- `src/Services/LanguageServerDefinitionParser.cs`
- `src/Services/LanguageServerDefinitionResult.cs`
- `src/Services/LanguageServerFormattingParser.cs`
- `src/Services/LanguageServerFormattingResult.cs`
- `src/Services/LanguageServerHoverParser.cs`
- `src/Services/LanguageServerHoverResult.cs`
- `src/Services/LanguageServerPublishDiagnostics.cs`
- `src/Services/LanguageServerStartOptions.cs`
- `src/Services/LanguageServerSymbolParser.cs`
- `src/Services/LanguageServerSymbolResult.cs`
- `src/Services/LspRange.cs`
- `src/Services/LspUtf16PositionMapper.cs`

**Authoritative test scope (0 paths — complete, no globs):**

_None. This slice is production-only. Matching exclusive product tests do not exist; the suite gate is still full `dotnet test`._

**Notes / non-goals:** R61-V03 LSP portion. Exclusive product tests: none (production-only slice). Full suite remains the test gate.

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

#### M6b completion record

**Scope executed:** Mechanical rehome of Language LSP infrastructure only into
`src/Features/Language/Infrastructure/Lsp/`. Namespace `Zaide.Services` →
`Zaide.Features.Language.Infrastructure.Lsp` for the 22 production paths
(session, factory, binary locator, capabilities, parsers/results, diagnostics
payload, start options, `LspRange`, `LspUtf16PositionMapper`). No product tests
moved (production-only slice; exclusive matching product tests do not exist).

Public full-name baseline rewrote 17 type names (count still 348). Architecture
inventory folder counts: Services 94→72, Features 178→200. Features admission
already covered `src/Features/Language/` (M6a). CONVENTIONS + OVERVIEW truthful
current-tree notes. No DI registration/lifetime, visibility, constructor
signature, protocol/session behavior, or public API redesign. FindingId
allowlist unchanged (9). Completes the LSP portion of R61-V03 via rehome only.
M7+ Debugging and later features remain.

**Verification (2026-07-17):**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---------|--------|
| build | Succeeded; 0 errors; 1 existing CS0067 in ProjectDebugTargetResolverTests (pre-existing) |
| Architecture | 21 passed, 0 failed |
| full suite | 2,193 passed, 0 failed, 0 skipped |
| `git diff --check` | clean |

**FindingId allowlist:** unchanged (9 entries). **Public count:** 348.

**Next:** stop after M6b; human review/commit of M6b only. Do not start M7+ without
authorization.

### M7a — Debugging — application

**Target:** `src/Features/Debugging/{Contracts,Application}/` · namespace `Zaide.Features.Debugging.*`

**Authoritative production scope (18 paths — complete, no globs):**

- `src/Services/BreakpointOperationResult.cs`
- `src/Services/BreakpointOutcomeKind.cs`
- `src/Services/BreakpointService.cs`
- `src/Services/DebugBreakpointRequest.cs`
- `src/Services/DebugBreakpointVerification.cs`
- `src/Services/DebugBreakpointVerificationState.cs`
- `src/Services/DebugLaunchRequest.cs`
- `src/Services/DebugProjectionState.cs`
- `src/Services/DebugSessionFailure.cs`
- `src/Services/DebugSessionOperationResult.cs`
- `src/Services/DebugSessionOutcomeKind.cs`
- `src/Services/DebugSessionService.cs`
- `src/Services/DebugSessionSnapshot.cs`
- `src/Services/DebugSessionState.cs`
- `src/Services/DebugSessionTimeoutPolicy.cs`
- `src/Services/DebugSessionTimeouts.cs`
- `src/Services/IBreakpointService.cs`
- `src/Services/IDebugSessionService.cs`

**Authoritative test scope (12 paths — complete, no globs):**

- `tests/Zaide.Tests/DI/DebugSessionServiceDiTests.cs`
- `tests/Zaide.Tests/Services/BreakpointServiceTests.cs`
- `tests/Zaide.Tests/Services/DebugExecutionControlsCommandTests.cs`
- `tests/Zaide.Tests/Services/DebugSessionServiceTests.cs`
- `tests/Zaide.Tests/Services/DebugStartOrContinueCommandTests.cs`
- `tests/Zaide.Tests/Services/DebugToggleBreakpointCommandTests.cs`
- `tests/Zaide.Tests/Services/M3aDebugLaunchProofTests.cs`
- `tests/Zaide.Tests/Services/M3bDebugBreakpointProofTests.cs`
- `tests/Zaide.Tests/Services/M4DebugExecutionProofTests.cs`
- `tests/Zaide.Tests/Services/M5DebugStackProofTests.cs`
- `tests/Zaide.Tests/Services/M6DebugRecoveryProofTests.cs`
- `tests/Zaide.Tests/TestDebugSessionFactory.cs`

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

#### M7a completion record

**Scope executed:** Mechanical rehome of Debugging application/contracts only
into `src/Features/Debugging/{Contracts,Application}/` and matching
`tests/Zaide.Tests/Features/Debugging/...`. Namespaces `Zaide.Services` →
`Zaide.Features.Debugging.*` for the 18 production paths:

| Pre-move path | Post-move path | Namespace |
|---------------|----------------|-----------|
| `IBreakpointService`, `IDebugSessionService` (2 files) | `.../Contracts/` | `Zaide.Features.Debugging.Contracts` |
| Breakpoint/session application types, snapshots, timeouts, request/result records (16 files) | `.../Application/` | `Zaide.Features.Debugging.Application` |

Matching 12 tests rehomed (including DI and `TestDebugSessionFactory`). Durable
feature-oriented renames (assertions unchanged):

| Pre-move name | Post-move name |
|---------------|----------------|
| `M3aDebugLaunchProofTests` | `DebugLaunchProofTests` |
| `M3bDebugBreakpointProofTests` | `DebugBreakpointProofTests` |
| `M4DebugExecutionProofTests` | `DebugExecutionProofTests` |
| `M5DebugStackProofTests` | `DebugStackProofTests` |
| `M6DebugRecoveryProofTests` | `DebugRecoveryProofTests` |

DAP adapter/transport/parser types remain under `src/Services/` for M7b.
Debugger panels/margins/instruction-pointer presentation remain for M7c.

Public full-name baseline rewrote 18 type names (count still 348). Architecture
inventory updated for folder/namespace truth; Features admission includes
`src/Features/Debugging/`. CONVENTIONS + OVERVIEW truthful current-tree notes.
No DI registration/lifetime, visibility, constructor signature, or behavior
changes. FindingId allowlist unchanged (9). M7b+ Debugging Dap/presentation and
later features remain.

**Verification (2026-07-17):**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
```

| Command | Result |
|---------|--------|
| build | Succeeded; 0 errors; 1 existing CS0067 in ProjectDebugTargetResolverTests (pre-existing) |
| Architecture | 21 passed, 0 failed |
| full suite | 2,193 passed, 0 failed, 0 skipped |
| `git diff --check` | clean |

**FindingId allowlist:** unchanged (9 entries). **Public count:** 348.

**Next:** stop after M7a; human review/commit of M7a only. Do not start M7b+ without
authorization.

### M7b — Debugging — Dap infrastructure

**Target:** `src/Features/Debugging/Infrastructure/Dap/` · namespace `Zaide.Features.Debugging.Infrastructure.Dap`

**Authoritative production scope (19 paths — complete, no globs):**

- `src/Services/DapBreakpointVerificationParser.cs`
- `src/Services/DapContentLengthTransport.cs`
- `src/Services/DapContinuedEvent.cs`
- `src/Services/DapExitedEvent.cs`
- `src/Services/DapInspectionParser.cs`
- `src/Services/DapOutputEvent.cs`
- `src/Services/DapScopeInfo.cs`
- `src/Services/DapStackFrameInfo.cs`
- `src/Services/DapStoppedEvent.cs`
- `src/Services/DapStoppedInfo.cs`
- `src/Services/DapThreadInfo.cs`
- `src/Services/DapVariableInfo.cs`
- `src/Services/DebugAdapterLocator.cs`
- `src/Services/DebugAdapterSessionFactory.cs`
- `src/Services/DebugAdapterStartOptions.cs`
- `src/Services/IDebugAdapterLocator.cs`
- `src/Services/IDebugAdapterSession.cs`
- `src/Services/IDebugAdapterSessionFactory.cs`
- `src/Services/NetCoreDbgAdapterSession.cs`

**Authoritative test scope (6 paths — complete, no globs):**

- `tests/Zaide.Tests/Services/DapBreakpointVerificationParserTests.cs`
- `tests/Zaide.Tests/Services/DapContentLengthTransportTests.cs`
- `tests/Zaide.Tests/Services/DebugAdapterLocatorTests.cs`
- `tests/Zaide.Tests/Services/NetCoreDbgAdapterSessionDirectTests.cs`
- `tests/Zaide.Tests/Services/NetCoreDbgLifecycleProofTests.cs`
- `tests/Zaide.Tests/Services/TestDebugAdapterSession.cs`

**Notes / non-goals:** R61-V03 DAP portion.

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

### M7c — Debugging — presentation

**Target:** `src/Features/Debugging/Presentation/` · namespace `Zaide.Features.Debugging.Presentation`

**Authoritative production scope (18 paths — complete, no globs):**

- `src/ViewModels/DebugConsoleLineViewModel.cs`
- `src/ViewModels/DebugCurrentLocationViewModel.cs`
- `src/ViewModels/DebugPanelViewModel.cs`
- `src/ViewModels/DebugScopeViewModel.cs`
- `src/ViewModels/DebugSessionViewModel.cs`
- `src/ViewModels/DebugStackFrameViewModel.cs`
- `src/ViewModels/DebugStackProjectionViewModel.cs`
- `src/ViewModels/DebugThreadViewModel.cs`
- `src/ViewModels/DebugVariableViewModel.cs`
- `src/ViewModels/EditorBreakpointMarker.cs`
- `src/ViewModels/EditorBreakpointProjection.cs`
- `src/ViewModels/EditorBreakpointViewModel.cs`
- `src/ViewModels/EditorInstructionPointerMarker.cs`
- `src/Views/BreakpointMargin.cs`
- `src/Views/BreakpointOperations.cs`
- `src/Views/DebugPanel.cs`
- `src/Views/InstructionPointerMargin.cs`
- `src/Views/InstructionPointerOperations.cs`

**Authoritative test scope (9 paths — complete, no globs):**

- `tests/Zaide.Tests/TestDebugPanelFactory.cs`
- `tests/Zaide.Tests/TestEditorBreakpointFactory.cs`
- `tests/Zaide.Tests/ViewModels/DebugCurrentLocationViewModelTests.cs`
- `tests/Zaide.Tests/ViewModels/DebugPanelViewModelTests.cs`
- `tests/Zaide.Tests/ViewModels/DebugStackProjectionTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorBreakpointProjectionTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorBreakpointRegressionTests.cs`
- `tests/Zaide.Tests/ViewModels/EditorBreakpointViewModelTests.cs`
- `tests/Zaide.Tests/Views/DebugPanelSelectionTests.cs`

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

### M8 — SourceControl

**Target:** `src/Features/SourceControl/...` · namespace `Zaide.Features.SourceControl.*`

**Authoritative production scope (28 paths — complete, no globs):**

- `src/Models/FileChange.cs`
- `src/Models/FileChangeEvent.cs`
- `src/Models/GitBranch.cs`
- `src/Models/SourceControlPrimaryAction.cs`
- `src/Models/SourceControlState.cs`
- `src/Services/CommitResult.cs`
- `src/Services/FileDiffResult.cs`
- `src/Services/FileDiffService.cs`
- `src/Services/GitMutationService.cs`
- `src/Services/GitRepositoryService.cs`
- `src/Services/IFileDiffService.cs`
- `src/Services/IGitMutationService.cs`
- `src/Services/IGitRepositoryService.cs`
- `src/Services/ISourceControlDiffTabService.cs`
- `src/Services/ISourceControlSnapshotOrchestrator.cs`
- `src/Services/NullSourceControlDiffTabService.cs`
- `src/Services/PushResult.cs`
- `src/Services/RepositoryDiscoveryResult.cs`
- `src/Services/RepositoryStatusSnapshot.cs`
- `src/Services/SnapshotRefreshResult.cs`
- `src/Services/SourceControlActionDeriver.cs`
- `src/Services/SourceControlDiffContent.cs`
- `src/Services/SourceControlDiffTabKey.cs`
- `src/Services/SourceControlDiffTabService.cs`
- `src/Services/SourceControlSnapshotOrchestrator.cs`
- `src/Services/StageResult.cs`
- `src/ViewModels/SourceControlViewModel.cs`
- `src/Views/SourceControlPanel.cs`

**Authoritative test scope (13 paths — complete, no globs):**

- `tests/Zaide.Tests/Integration/SourceControlMutationFlowTests.cs`
- `tests/Zaide.Tests/Models/GitBranchTests.cs`
- `tests/Zaide.Tests/Services/FileDiffServiceTests.cs`
- `tests/Zaide.Tests/Services/GitMutationServiceTests.cs`
- `tests/Zaide.Tests/Services/GitRepositoryServiceTests.cs`
- `tests/Zaide.Tests/Services/LibGit2SharpDiffProofOfConceptTests.cs`
- `tests/Zaide.Tests/Services/LibGit2SharpMutationProofOfConceptTests.cs`
- `tests/Zaide.Tests/Services/SourceControlActionDeriverTests.cs`
- `tests/Zaide.Tests/Services/SourceControlDiffTabServiceTests.cs`
- `tests/Zaide.Tests/Services/SourceControlSnapshotOrchestratorTests.cs`
- `tests/Zaide.Tests/SourceControlTestFactory.cs`
- `tests/Zaide.Tests/ViewModels/SourceControlViewModelTests.cs`
- `tests/Zaide.Tests/Views/SourceControlPanelCommandWiringTests.cs`

**Allowlist path rewrite(s):** R61-AL-NS-SourceControlState; R61-AL-NS-SourceControlDiffTabService; R61-AL-LOC-SourceControlDiffTabService

**Notes / non-goals:** Do not invert SourceControlState→snapshot (6.3); do not replace diff-tab locator (6.3).

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

### M9 — Terminal

**Target:** `src/Features/Terminal/...` · namespace `Zaide.Features.Terminal.*`

**Authoritative production scope (24 paths — complete, no globs):**

- `src/Services/ITerminalService.cs`
- `src/Services/ITerminalSessionFactory.cs`
- `src/Services/LinuxPtyInterop.cs`
- `src/Services/LinuxTerminalService.cs`
- `src/Services/TerminalSessionFactory.cs`
- `src/ViewModels/AnsiParser.cs`
- `src/ViewModels/ITerminalHost.cs`
- `src/ViewModels/LogCategorizer.cs`
- `src/ViewModels/LogEntry.cs`
- `src/ViewModels/TerminalHost.cs`
- `src/ViewModels/TerminalScreen.cs`
- `src/ViewModels/TerminalSnapshot.cs`
- `src/ViewModels/TerminalSnapshotSearch.cs`
- `src/ViewModels/TerminalState.cs`
- `src/ViewModels/TerminalTabViewModel.cs`
- `src/ViewModels/TerminalViewModel.cs`
- `src/Views/TerminalGeometry.cs`
- `src/Views/TerminalKeyMapper.cs`
- `src/Views/TerminalPanel.cs`
- `src/Views/TerminalPanelSubscriptions.cs`
- `src/Views/TerminalRenderControl.cs`
- `src/Views/TerminalTabCloseBehavior.cs`
- `src/Views/TerminalTabHost.cs`
- `src/Views/TerminalTabStrip.cs`

**Authoritative test scope (14 paths — complete, no globs):**

- `tests/Zaide.Tests/Services/LinuxTerminalServiceTests.cs`
- `tests/Zaide.Tests/Services/TerminalSessionFactoryTests.cs`
- `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs`
- `tests/Zaide.Tests/ViewModels/LogCategorizerTests.cs`
- `tests/Zaide.Tests/ViewModels/TerminalHostTests.cs`
- `tests/Zaide.Tests/ViewModels/TerminalScreenTests.cs`
- `tests/Zaide.Tests/ViewModels/TerminalSnapshotSearchTests.cs`
- `tests/Zaide.Tests/ViewModels/TerminalSnapshotTests.cs`
- `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`
- `tests/Zaide.Tests/Views/TerminalGeometryTests.cs`
- `tests/Zaide.Tests/Views/TerminalKeyMapperTests.cs`
- `tests/Zaide.Tests/Views/TerminalPanelSubscriptionsTests.cs`
- `tests/Zaide.Tests/Views/TerminalRenderControlTests.cs`
- `tests/Zaide.Tests/Views/TerminalTabCloseBehaviorTests.cs`

**Allowlist path rewrite(s):** R61-AL-NS-ITerminalSessionFactory; R61-AL-NS-TerminalSessionFactory

**Notes / non-goals:** Do not break Services→ViewModels factory edge (6.3 / R61-V05).

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

### M10 — Townhall

**Target:** `src/Features/Townhall/...` · namespace `Zaide.Features.Townhall.*`

**Authoritative production scope (11 paths — complete, no globs):**

- `src/Models/Channel.cs`
- `src/Models/TownhallMessage.cs`
- `src/Models/TownhallState.cs`
- `src/Models/WorkspaceAgent.cs`
- `src/ViewModels/TownhallViewModel.cs`
- `src/Views/TownhallAvatarFactory.cs`
- `src/Views/TownhallChannelPanel.cs`
- `src/Views/TownhallChatPanel.cs`
- `src/Views/TownhallInputArea.cs`
- `src/Views/TownhallPeoplePanel.cs`
- `src/Views/TownhallView.cs`

**Authoritative test scope (5 paths — complete, no globs):**

- `tests/Zaide.Tests/Models/TownhallMessageTests.cs`
- `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs`
- `tests/Zaide.Tests/Views/TownhallChatPanelGroupingTests.cs`
- `tests/Zaide.Tests/Views/TownhallChatPanelKindTests.cs`
- `tests/Zaide.Tests/Views/TownhallInputAreaTests.cs`

**Notes / non-goals:** Preserve R61-V16; no conversation lifetime types (R61-LT01).

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

### M11 — Agents

**Target:** `src/Features/Agents/...` · namespace `Zaide.Features.Agents.*`

**Authoritative production scope (16 paths — complete, no globs):**

- `src/Models/AgentPanelState.cs`
- `src/Models/RouteRequest.cs`
- `src/Models/RouteResult.cs`
- `src/Services/AgentExecutionOptions.cs`
- `src/Services/AgentExecutionResult.cs`
- `src/Services/AgentExecutionService.cs`
- `src/Services/IAgentExecutionService.cs`
- `src/Services/MentionParser.cs`
- `src/ViewModels/AgentExecutionCoordinator.cs`
- `src/ViewModels/AgentPanelHost.cs`
- `src/ViewModels/AgentRouter.cs`
- `src/ViewModels/IAgentExecutionCoordinator.cs`
- `src/ViewModels/IAgentPanelHost.cs`
- `src/ViewModels/IAgentRouter.cs`
- `src/Views/AgentPanelHostView.cs`
- `src/Views/AgentPanelView.cs`

**Authoritative test scope (9 paths — complete, no globs):**

- `tests/Zaide.Tests/Models/AgentPanelStateTests.cs`
- `tests/Zaide.Tests/Services/AgentExecutionServiceTests.cs`
- `tests/Zaide.Tests/Services/LiveLlmConfigTests.cs`
- `tests/Zaide.Tests/Services/MentionParserTests.cs`
- `tests/Zaide.Tests/TestLlmFixture.cs`
- `tests/Zaide.Tests/ViewModels/AgentExecutionCoordinatorTests.cs`
- `tests/Zaide.Tests/ViewModels/AgentPanelHostTests.cs`
- `tests/Zaide.Tests/ViewModels/AgentRouterTests.cs`
- `tests/Zaide.Tests/Views/AgentPanelHostViewLifetimeTests.cs`

**Allowlist path rewrite(s):** R61-AL-NS-MentionParser

**Notes / non-goals:** Preserve R61-V06 until 6.3; no agent-session/run types (LT02/LT03).

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

### M12 — App Composition + Shell

**Target:** `src/App/Composition/, src/App/Shell/` · namespace `Zaide.App.Composition / Zaide.App.Shell`

**Authoritative production scope (22 paths — complete, no globs):**

- `src/App.axaml`
- `src/App.axaml.cs`
- `src/MainWindow.axaml`
- `src/MainWindow.axaml.cs`
- `src/Program.cs`
- `src/Services/CommandDescriptor.cs`
- `src/Services/CommandRegistry.cs`
- `src/Services/ICommandRegistry.cs`
- `src/Services/ResolvedKeyBinding.cs`
- `src/ViewModels/CommandPaletteViewModel.cs`
- `src/ViewModels/MainWindowViewModel.cs`
- `src/ViewModels/PaletteEntry.cs`
- `src/ViewModels/StatusBarViewModel.cs`
- `src/Views/Animations.cs`
- `src/Views/CommandPaletteOverlay.cs`
- `src/Views/FinalWindowCleanup.cs`
- `src/Views/GridLayoutResizeHelper.cs`
- `src/Views/HorizontalDirection.cs`
- `src/Views/IconFactory.cs`
- `src/Views/KeyBindingConverter.cs`
- `src/Views/NavBar.cs`
- `src/Views/StatusBar.cs`

**Authoritative test scope (11 paths — complete, no globs):**

- `tests/Zaide.Tests/CommandRegistryFactory.cs`
- `tests/Zaide.Tests/DI/Phase9M1DiIntegrationTests.cs`
- `tests/Zaide.Tests/MainWindowViewModelTests.cs`
- `tests/Zaide.Tests/Services/CanonicalCommandRegistrationTests.cs`
- `tests/Zaide.Tests/Services/CommandRegistryTests.cs`
- `tests/Zaide.Tests/Services/CommandResolutionAcceptanceTests.cs`
- `tests/Zaide.Tests/Services/Phase9CommandRegistrationTests.cs`
- `tests/Zaide.Tests/ViewModels/CommandPaletteViewModelTests.cs`
- `tests/Zaide.Tests/ViewModels/MainWindowViewModelBottomPanelModeTests.cs`
- `tests/Zaide.Tests/Views/AnimationsTests.cs`
- `tests/Zaide.Tests/Views/CommandPaletteViewTests.cs`

**Allowlist path rewrite(s):** R61-AL-LOC-Program; R61-AL-LOC-App

**Notes / non-goals:** No MainWindow extraction (V15); no App.Services removal (V09); no registration split (V10). Animations/IconFactory shell-owned (R62-D03). Empty technical folders cleanup allowed only when empty.

**Rollback gate:** one commit containing only this slice’s production moves, test moves/renames, required namespace/using/AXAML/resource/admission/allowlist-path updates, and this plan status if needed. Revert that single commit. Must pass the per-slice verification contract before commit.

---

### M13 — Optional root admission (not scheduled)

Only if human review approves after M12:

- Rehome `IFileService`/`FileService` to `Infrastructure/FileSystem` (R62-D01)
  with allowlist admission entries naming Editor + SourceControl consumers.
- Selective `UI/Shared` admissions (R62-D03/D04) with evidence.

Until then, **no** files under those roots.

---

## Classification completeness proof

| Bucket | Production paths | Product tests |
|--------|-----------------:|--------------:|
| M1 | 3 | 1 |
| M2 | 24 | 13 |
| M3 | 7 | 4 |
| M4 | 24 | 25 |
| M5a | 31 | 11 |
| M5b | 20 | 12 |
| M5c | 20 | 12 |
| M6a | 53 | 12 |
| M6b | 22 | 0 |
| M7a | 18 | 12 |
| M7b | 19 | 6 |
| M7c | 18 | 9 |
| M8 | 28 | 13 |
| M9 | 24 | 14 |
| M10 | 11 | 5 |
| M11 | 16 | 9 |
| M12 | 22 | 11 |
| **M1–M12 sum** | **360** | **169** |
| Architecture harness (R62-D05) | — | 17 |
| Test host plumbing (R62-D06) | — | 1 |
| **Tracked totals** | **360** (= 356 C# + 4 AXAML) | **187** |

Every production path is listed exactly once under exactly one migration milestone above. Every product test path is listed exactly once. No “complement of other slices” or “every `Language*.cs` except …” classification is permitted at implementation time.

## Dependencies

| Depends on | Why |
|------------|-----|
| Refactor 6.1 accepted + committed | Ratchets and dispositions |
| Human acceptance of this M0 | Gate before M1 |
| Each prior slice in order | Prefer stable namespaces for consumers; order is planning default—adjacent independent features may swap only with explicit plan amendment |
| Architecture test maintainability | Admission updates every slice |

Does **not** depend on Refactor 6.3/7/8 or V3.

---

## Non-goals (Refactor 6.2 overall)

- Dependency inversion, locator removal, DI split, lifetime remodel
- Visibility internalization / public-count reduction
- Agent conversation/session/run domain
- Townhall/shell visual extraction
- New features, behavior changes, package changes
- Assembly split
- Catch-all shared folders without admission

---

## Limitations

1. Mechanical moves leave all allowlisted edges and locator debt intact.
2. Root admission detectors are C#-only until a future decision (R62-D09).
3. Hybrid inventory is not a full semantic graph; slices must still compile.
4. Large features (ProjectSystem, Language) may need M5a/b/c-style splits at
   implementation time without changing ownership taxonomy.
5. Cross-feature `using` edges will still exist after path moves; 6.3 owns
   correction.
6. Shell remains large (V13/V15); movement is not extraction.
7. Optional M13 is not authorized by accepting M0 alone.

---

## Commit and rollback policy

- Prefer **one commit per milestone/slice** after verification.
- Commit message area prefix: `refactor-6.2: ...` or `arch: move <feature> ...`.
- Rollback = `git revert` of that single commit (or restore before commit).
- Do not combine a movement slice with 6.3 logic fixes.
- Do **not** commit or push from M0 unless a human explicitly asks for the doc
  commit.

---

## M0 exit gate (human)

Refactor 6.2 **M1 is blocked** until a human confirms:

1. Taxonomy and namespace rules are accepted.
2. Slice order and exact scopes are accepted (or amended in this plan first).
3. Deferrals R62-D01–D10 are accepted.
4. Exception non-growth and movement-only constraints are accepted.
5. Live verification results above are accepted as the baseline.

Until then: **stop after M0**.

---

## Future milestones summary

| Milestone | Authorized by |
|-----------|---------------|
| M0 | This plan (docs only) |
| M1–M12 (incl. M5a/M5b/M5c) | Explicit human acceptance of M0 + per-slice execution |
| M13 | Separate human admission review after M12 |
| 6.3 / 7 / 8 | Their own plans |

---

## Appendix A — Test assignment summary

| Slice | Test count | Notes |
|-------|----------:|-------|
| M1 | 1 | TextStyles |
| M2 | 13 | includes M9a/M9b, M5SettingsUi, Phase814 renames |
| M3 | 4 | |
| M4 | 25 | Phase13*, Phase9M0 editor renames |
| M5a | 11 | Phase83 discovery/context/gate/targets |
| M5b | 12 | workflow/process/output |
| M5c | 12 | diagnostics/problems/test results |
| M6a | 12 | includes TestLanguageServerSession |
| M6b | 0 | production-only |
| M7a | 12 | includes M3a–M6 debug proof renames |
| M7b | 6 | Dap/adapter |
| M7c | 9 | presentation |
| M8 | 13 | |
| M9 | 14 | |
| M10 | 5 | |
| M11 | 9 | includes AgentPanelHostViewLifetimeTests |
| M12 | 11 | Phase9 command/DI renames |
| DEFER harness | 17 | Architecture |
| DEFER host | 1 | XunitSettings |
| **Sum** | **187** | |

## Appendix B — Allowlist rewrite checklist (by slice)

| FindingId | Current MatchKey path fragment | Rewrite in |
|-----------|--------------------------------|------------|
| R61-AL-NS-SourceControlState | `src/Models/SourceControlState.cs` | M8 |
| R61-AL-NS-SourceControlDiffTabService | `src/Services/SourceControlDiffTabService.cs` | M8 |
| R61-AL-LOC-SourceControlDiffTabService | same | M8 |
| R61-AL-NS-ITerminalSessionFactory | `src/Services/ITerminalSessionFactory.cs` | M9 |
| R61-AL-NS-TerminalSessionFactory | `src/Services/TerminalSessionFactory.cs` | M9 |
| R61-AL-NS-MentionParser | `src/Services/MentionParser.cs` | M11 |
| R61-AL-LOC-EditorTabViewModel | `src/Features/Editor/Presentation/EditorTabViewModel.cs` (rewritten in M4) | M4 |
| R61-AL-LOC-Program | `src/Program.cs` | M12 |
| R61-AL-LOC-App | `src/App.axaml.cs` | M12 |

NamespaceDirection *folder* prefix in MatchKey (`Models`/`Services`) may need
harmonization with inventory after technical folders empty—update inventory
reader and ratchet together; **do not add FindingIds**.

---

*M0 evidence date: 2026-07-17. Amended same day for M5a/b/c and explicit
path lists. Stop after M0. Do not commit or push unless explicitly requested.*
