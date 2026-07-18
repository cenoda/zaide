# Refactor 6.3: Composition and Lifetime Cleanup — Implementation Plan

## Status and authorization

**M0 acceptance status:** **GO — accepted (2026-07-18).** Human accepted the
eighth-revision planning gate.

**Progress (truth-sync):** **M1 complete** at `e590a79`
(`refactor-6.3: M1 editor session factory`). **M2 complete** at `d9799ad`
(`refactor-6.3: M2 editor read-only tab gateway`). **M3 complete** at
`22b869e` (`refactor-6.3: M3 terminal service factory` / V05) — automated
verification green; manual terminal smoke **not run**. **M4 complete** at
`698b094` (`refactor-6.3: M4 mention parser purity` / V06) — automated
verification green; manual agent-panel routing smoke **not run**. **M5
complete** at `273cc56` (`refactor-6.3: M5 delete unused source control state`
/ V02) — automated verification green (build, focused Architecture+SourceControl
150/150, Architecture 21/21, full suite 2204/2204, `git diff --check`);
manual verification **not required** (deleted type had no production consumer).
**M6a complete** at `c59ad7b` (`refactor-6.3: M6a AppCore DI module`) —
first M6 registration slice: internal
`AppCoreServiceCollectionExtensions.AddZaideAppCore` owns the six AppCore
singletons; `Program.ConfigureServices` calls it once; public baseline **346**
unchanged; internal **51 → 52**; total top-level **397 → 398**; production C#
**359 → 360**; App C# **20 → 21**. Automated verification green (build,
focused DI+Architecture 47/47, Architecture 21/21, full suite 2209/2209,
`git diff --check`); manual verification **not required**. **M6b complete**
at `43b8e85` (`refactor-6.3: M6b Settings DI module`) — second completed M6
slice: internal
`SettingsServiceCollectionExtensions.AddZaideSettings` owns the two Settings
singletons (`ISettingsService` → `SettingsService`; `ISecretStore` factory →
`FileSecretStore(SettingsPathResolver.GetSecretsPath())`);
`Program.ConfigureServices` calls `AddZaideSettings()` exactly once after
`AddZaideAppCore()`; public baseline **346** unchanged; internal **52 → 53**;
total top-level **398 → 399**; production C# **360 → 361**; App C# **21 → 22**.
Automated verification green (build, focused DI+Architecture 53/53,
Architecture 21/21, full suite 2215/2215, `git diff --check`);
manual verification **not required**. **M6c complete** at `1ad3625` (`refactor-6.3: M6c Workspace DI module`) —
third completed M6 slice: internal
`WorkspaceServiceCollectionExtensions.AddZaideWorkspace` owns the two
Workspace/file-tree singletons (`IFileTreeService` → `FileTreeService`;
`FileTreeViewModel` self-registration); `Program.ConfigureServices` calls
`AddZaideWorkspace()` exactly once after `AddZaideSettings()`; Domain
`Workspace` remains in AppCore (M6a); public baseline **346** unchanged;
internal **53 → 54**; total top-level **399 → 400**; production C#
**361 → 362**; App C# **22 → 23**. Automated verification green (build,
focused DI+Architecture 58/58, Architecture 21/21, full suite 2220/2220,
`git diff --check`); manual verification **not required**. **M6d complete**
at `234a38f` (`refactor-6.3: M6d Editor DI module`) — fourth completed M6 slice:
internal `EditorServiceCollectionExtensions.AddZaideEditor` owns the six
Editor singletons (`IFileService` → `FileService`; `IEditorSessionFactory` →
`EditorSessionFactory`; `IEditorReadOnlyTabService` → `EditorReadOnlyTabService`;
`EditorSearchViewModel`; `EditorTabViewModel`; `EditorLanguageInputViewModel`);
`Program.ConfigureServices` calls `AddZaideEditor()` exactly once after
`AddZaideWorkspace()`; module order is `AddZaideAppCore` → `AddZaideSettings` →
`AddZaideWorkspace` → `AddZaideEditor`; `AddLogging` remains in `Program`;
`EditorViewModel` remains unregistered (factory-created); public baseline
**346** unchanged; internal **54 → 55**; total top-level **400 → 401**;
production C# **362 → 363**; App C# **23 → 24**; internal
Composition.Registration modules **4**. Automated verification green (build,
focused DI+Architecture 64/64, Architecture 21/21, full suite 2226/2226,
`git diff --check`); manual verification **not required**. **M6e complete at
`8ab50c0`** (`refactor-6.3: M6e Terminal DI module`) — fifth
M6 registration slice: internal
`TerminalServiceCollectionExtensions.AddZaideTerminal` owns the two Terminal
singletons (`ITerminalServiceFactory` → `LinuxTerminalServiceFactory`;
`ITerminalHost` → `TerminalHost`); `Program.ConfigureServices` calls
`AddZaideTerminal()` exactly once after `AddZaideEditor()`; module order is
`AddZaideAppCore` → `AddZaideSettings` → `AddZaideWorkspace` →
`AddZaideEditor` → `AddZaideTerminal`; `AddLogging` remains in `Program`;
public baseline **346** unchanged; internal **55 → 56**; total top-level
**401 → 402**; production C# **363 → 364**; App C# **24 → 25**; internal
Composition.Registration modules **5**. Automated verification green (build,
focused registration+DI+Architecture 51/51, Architecture 21/21, full suite
2231/2231, `git diff --check`); manual verification **not required**.
**M6f complete at `cd809d2`** (`refactor-6.3: M6f Agents DI module`) — sixth
M6 registration slice: internal
`AgentsServiceCollectionExtensions.AddZaideAgents` owns the six Agents
singletons (`IAgentPanelHost` → `AgentPanelHost`;
`IAgentExecutionService` → `AgentExecutionService`;
`IAgentExecutionCoordinator` → `AgentExecutionCoordinator`; `MentionParser`
self-registration; `IAgentRouter` → `AgentRouter`; `HttpClient` factory with
120s timeout); `Program.ConfigureServices` calls `AddZaideAgents()` exactly
once after `AddZaideTerminal()`; module order is `AddZaideAppCore` →
`AddZaideSettings` → `AddZaideWorkspace` → `AddZaideEditor` →
`AddZaideTerminal` → `AddZaideAgents`; `AddLogging` remains in `Program`;
public baseline **346** unchanged; internal **56 → 57**; total top-level
**402 → 403**; production C# **364 → 365**; App C# **25 → 26**; internal
Composition.Registration modules **6**. Automated verification green (build,
focused registration+DI+Architecture 56/56, Architecture 21/21, full suite
2236/2236, `git diff --check`); manual verification **not required**.
**M6g complete at `1f18e49`** (`refactor-6.3: M6g Townhall DI module`) —
seventh M6 registration slice: internal
`TownhallServiceCollectionExtensions.AddZaideTownhall` owns the two Townhall
singleton self-registrations (`TownhallState`; `TownhallViewModel`);
`Program.ConfigureServices` calls `AddZaideTownhall()` exactly once after
`AddZaideAgents()`; module order is `AddZaideAppCore` → `AddZaideSettings` →
`AddZaideWorkspace` → `AddZaideEditor` → `AddZaideTerminal` →
`AddZaideAgents` → `AddZaideTownhall`; `AddLogging` remains in `Program`;
M6h–M6k registrations remain direct in `Program`; public baseline **346**
unchanged; internal **57 → 58**; total top-level **403 → 404**; production C#
**365 → 366**; App C# **26 → 27**; internal Composition.Registration modules
**7**. Automated verification green (build clean 0 warnings / 0 errors, focused
registration+DI+Architecture 61/61, Architecture 21/21, full suite 2241/2241,
`git diff --check` clean; `git diff --cached --check` clean before the
implementation commit). Manual verification **not required**.
**M6h implemented and staged pending review** (`refactor-6.3: M6h SourceControl
DI module`) — eighth M6 registration slice: internal
`SourceControlServiceCollectionExtensions.AddZaideSourceControl` owns the six
SourceControl singleton registrations (`SourceControlViewModel` self-registration;
`IGitRepositoryService` → `GitRepositoryService`;
`ISourceControlSnapshotOrchestrator` → `SourceControlSnapshotOrchestrator`;
`IFileDiffService` → `FileDiffService`;
`ISourceControlDiffTabService` → `SourceControlDiffTabService`;
`IGitMutationService` → `GitMutationService`);
`Program.ConfigureServices` calls `AddZaideSourceControl()` exactly once after
`AddZaideTownhall()`; module order is `AddZaideAppCore` → `AddZaideSettings` →
`AddZaideWorkspace` → `AddZaideEditor` → `AddZaideTerminal` →
`AddZaideAgents` → `AddZaideTownhall` → `AddZaideSourceControl`; `AddLogging`
remains in `Program`; M6i–M6k registrations remain direct in `Program`; public
baseline **346** unchanged; internal **58 → 59**; total top-level **404 → 405**;
production C# **366 → 367**; App C# **27 → 28**; internal
Composition.Registration modules **8**. Automated verification green (build
clean 0 warnings / 0 errors, focused registration+DI+Architecture
66/66, Architecture 21/21, full suite 2246/2246, `git diff --check` clean;
`git diff --cached --check` clean while staged pending review). Manual
verification **not required**. **M6i** (ProjectSystem) is next eligible and
requires separate authorization; M6i–M6k remain unauthorized. M6a–M6h are
individually completed slices; the whole M6 series is not complete.

**Authorization boundary (M0 docs only):** the only files M0 may create or
edit are:

| Path | Role |
|------|------|
| `docs/refactor/refactor-6.3/IMPLEMENTATION_PLAN.md` | This plan |
| `docs/refactor/refactor-6.3/*` | Optional read-only evidence notes under this directory |
| `docs/roadmap/V3.md` | Status / next-step pointer only |
| `docs/phases/README.md` | Status / next-step pointer only |
| `docs/architecture/OVERVIEW.md` | Status / next-step pointer only |

M0 does **not** authorize:

- production or product-test source edits
- DI registration shape changes, visibility changes, or allowlist removals
- dependency inversion, factory introductions, or shutdown rewrites
- Refactor 7 agent/conversation domain work
- Refactor 8 shell view extraction or visual redesign
- Phase 14 / V3 feature work
- creation of `Infrastructure/` or `UI/Shared` roots (M13 declined in 6.2)
- edits to any path outside the table above

**Prerequisite (re-verified 2026-07-18):**

| Check | Result |
|-------|--------|
| Branch | `master` |
| `HEAD` | `5d06958` (`docs: record refactor 6.2 closeout decision`) |
| vs `origin/master` | up to date (before M0 doc work) |
| Refactor 6.1 | closed (`9a0a83f`) — rules, ratchets, dispositions |
| Refactor 6.2 | accepted closed M1–M12 (`72102da` + closeout docs); optional M13 declined |

Accepting M0 authorizes **no** production milestone. Each implementation
milestone requires a separate explicit start after M0 acceptance.

---

## Goal

Correct composition, dependency direction, service-locator, lifetime ownership,
shutdown, shell-composition pressure, and public-surface debt that Refactor 6.1
classified as **Refactor 6.3 dependency/lifetime ownership** and that Refactor
6.2 deliberately preserved during mechanical feature-first migration.

Preserve user-visible behavior. Prefer explicit factories and feature
registration modules over speculative DI scopes. Do not invent Conversation,
Agent session, or Run lifetimes (R61-LT01–LT03).

---

## Hard boundaries (entire Refactor 6.3)

1. **Assembly:** keep the single production assembly (`src/Zaide.csproj` →
   `Zaide`). No project split.
2. **Behavior preservation:** no intentional product/UX change. Existing
   automated regression and composition/shutdown tests remain the gate. Named
   correctness exceptions require an explicit plan amendment and tests.
3. **No future lifetimes:** do not introduce `ConversationScope`,
   `AgentSessionScope`, run IDs, backend capability types, or registrations for
   types not already in production.
4. **No Refactor 7/8 scope:** do not redesign agent/Townhall protocols, fix
   active-channel attribution as a product change, extract
   `MainWindow.axaml.cs` into visual components, or redesign Townhall UI.
5. **Allowlist (authoritative — see § Allowlist mutation policy):** the frozen
   **FindingId set of nine** may only shrink. No new FindingId without a plan
   amendment + human review. MatchKey re-key of an *existing* FindingId is
   allowed only as remove+add in one unit when the same residual debt moves
   path/namespace intentionally.
6. **Public baseline ratchet:** may only shrink or stay. No new public
   production type without intentional baseline update in the same change;
   prefer `internal` implementations.
7. **Root admission:** do not create `src/Infrastructure/` or `src/UI/Shared/`
   unless a later separate admission decision reopens 6.2 M13 with evidence.
8. **Session-sized milestones:** if a milestone is too large, split as
   `MNa`/`MNb` with exact inventories (this plan already slices M6, M9, M11).
9. **Locked ownership:** M1–M5 contract and file ownership decisions in
   § Locked design decisions are binding. Do not reopen them during
   implementation without a plan amendment.

---

## Allowlist mutation policy (single authoritative rule)

This section supersedes any shorter paraphrase elsewhere in this document.

| Action | Allowed? | Conditions |
|--------|----------|------------|
| **Remove** a FindingId | Yes | Live inventory evidence for its MatchKey is gone in the **same** change unit; architecture tests updated; this plan’s debt matrix updated. |
| **Add a new FindingId** | **No** (default) | Requires plan amendment + human review. This is **new-debt admission**, not a re-key. Frozen set size must not grow above nine without that amendment. |
| **Re-key MatchKey** for an **existing** FindingId | Yes | Same FindingId, same M0 finding (R61-V##), same residual debt. Implemented as remove+add of that FindingId’s entry in one review unit when the debt site’s path/namespace is intentionally relocated. Rationale may be clarified without MatchKey change when the site is unchanged. |
| **Change Category / M0FindingId** | Only as re-key | Treated like MatchKey re-key: remove+add pair, same unit. |

**FindingId set (frozen at M0; must only shrink as debt clears):**

```
R61-AL-LOC-App
R61-AL-LOC-Program
R61-AL-NS-MentionParser
R61-AL-NS-SourceControlState
```

**Live set after M5 (2 FindingIds):**
```
R61-AL-LOC-App
R61-AL-LOC-Program
```
`R61-AL-NS-SourceControlState` was **removed** in M5 (V02 debt cleared), not
re-keyed. No NamespaceDirection residual remains. Earlier removals remain
cleared: M1 `R61-AL-LOC-EditorTabViewModel`; M2 both V07 FindingIds; M3
Terminal NS FindingIds `R61-AL-NS-ITerminalSessionFactory` and
`R61-AL-NS-TerminalSessionFactory`; M4 `R61-AL-NS-MentionParser`.

---

## M0 live re-verification (post-6.2)

### Evidence date

2026-07-18.

### Production tree

Feature-first layout is live. No residual technical roots
`Models` / `Services` / `ViewModels` / `Views` / `Styles`.

| Metric | Live count |
|--------|----------:|
| Production C# (excl. obj/bin) | **356** |
| Production AXAML | **4** |
| Test C# (excl. obj/bin) | **191** |
| Architecture tests | **21** passed |

### DI registration shape (R61-V10)

`Program.ConfigureServices` — one method, all features:

| Registration | Count |
|--------------|------:|
| `AddSingleton` | **64** |
| `AddTransient` | **1** (`EditorViewModel`) |
| `AddScoped` | **0** |
| **Total explicit calls** | **65** |

Full membership is listed under **M6** slices below (exact service list).

### Service-locator sites (44 resolve call expressions)

| File | Approx. calls | FindingId | Finding |
|------|--------------:|-----------|---------|
| `src/App/Composition/App.axaml.cs` | 35 | `R61-AL-LOC-App` | V09 / V12 |
| `src/App/Composition/Program.cs` | 3 | `R61-AL-LOC-Program` | V09 |
| `src/Features/SourceControl/Application/SourceControlDiffTabService.cs` | 3 | `R61-AL-LOC-SourceControlDiffTabService` | V07 |
| `src/Features/Editor/Presentation/EditorTabViewModel.cs` | 3 | `R61-AL-LOC-EditorTabViewModel` | V08 |

### `EditorViewModel` construction sites (exactly three)

1. `src/App/Composition/Program.cs` — `AddTransient<EditorViewModel>`
2. `src/Features/Editor/Presentation/EditorTabViewModel.cs` — open-file path
3. `src/Features/SourceControl/Application/SourceControlDiffTabService.cs` — open-diff path

### Shell metrics

| Surface | Live metric |
|---------|-------------|
| `MainWindowViewModel.cs` | **628** lines; **18** ctor parameters (16 required + 2 optional) |
| `MainWindow.axaml.cs` | **993** lines — **Refactor 8**; 6.3 does not extract this file |
| Settings panel | `new SettingsViewModel` / `new SettingsPanelView` in shell (~L879) |

### SourceControlState (V02)

**Cleared in M5** at `273cc56`. `src/Features/SourceControl/Domain/SourceControlState.cs`
is deleted. No production `SourceControlState` reference remains.
`RepositoryStatusSnapshot` remains under SourceControl Application.
`R61-AL-NS-SourceControlState` was removed without replacement; NamespaceDirection
residuals are empty.

### Architecture gate

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --filter FullyQualifiedName~Architecture
```

**Result (2026-07-18):** 21 passed, 0 failed.

### Shared sequential gate (every implementation milestone)

```bash
dotnet build Zaide.slnx
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
git status --short --branch
```

Pass criteria: build 0 errors; Architecture 21+ passed 0 failed (count may grow
if tests added); full suite 0 failed; `git diff --check` clean; status shows only
intended milestone files.

---

## Owned debt inventory

| ID | Live site | FindingId(s) | Clearing milestone |
|----|-----------|--------------|--------------------|
| V02 | `SourceControlState` Domain → Application | `R61-AL-NS-SourceControlState` | **M5** (cleared) |
| V05 | Terminal factory Contracts/Application → Presentation | `R61-AL-NS-ITerminalSessionFactory`, `R61-AL-NS-TerminalSessionFactory` | **M3** (cleared) |
| V06 | `MentionParser` → `IAgentPanelHost` | `R61-AL-NS-MentionParser` | **M4** (cleared) |
| V07 | Diff tab service → Editor Presentation + provider | `R61-AL-NS-SourceControlDiffTabService`, `R61-AL-LOC-SourceControlDiffTabService` | **M2** |
| V08 | `EditorTabViewModel` + `IServiceProvider` | `R61-AL-LOC-EditorTabViewModel` | **M1** |
| V09 | Public static `App.Services` + composition locators | `R61-AL-LOC-Program`, `R61-AL-LOC-App` | **M7** removes public `App.Services` and centralizes the store on `CompositionRoot.Services`. **Deliberate residual** (not full V09 clearance): static mutable composition-root provider + the two composition LOC FindingIds **remain** with updated rationale — see M7. M13 permits only these composition residuals. |
| V10 | Monolithic 65 registrations | documented | **M6a–M6k** |
| V11 | Semantic lifetime vs DI singleton | documented | **M12** |
| V12 | Manual sync shutdown | partial LOC-App | **M8** |
| V13 | `MainWindowViewModel` hub | documented | **M9a–M9c** |
| V14 | 348 public baseline | executable baseline | **M11a–M11d** |
| V17 | Shell `new Settings*` | documented | **M10** |

### Explicitly out of scope

| ID | Owner |
|----|-------|
| V15 | Refactor 8 (`MainWindow.axaml.cs` extraction) |
| V16, V18–V20, LT01–LT03 | Refactor 7 |

**Residual note (not allowlisted today):** `AgentRouter` and
`AgentExecutionCoordinator` (Application) still reference `IAgentPanelHost`
(Presentation). M4 cleared the allowlisted `MentionParser` edge only
(`R61-AL-NS-MentionParser` removed). A later plan amendment may invert
AgentRouter; do not expand that residual without amendment.

---

## Locked design decisions (M1–M5)

These decisions are **binding** for implementation. Types may be renamed only
with a plan amendment.

### D0 — Public DI injection accessibility (applies to M1–M3)

A **public** constructor cannot take a less-accessible parameter type, and
Microsoft DI must activate public consumers through ordinary public ctors.

**Locked strategy (chosen for M1–M3):** keep each **injected interface public**
so public consumers (`EditorTabViewModel`, `SourceControlDiffTabService`,
`TerminalHost`) stay public with public ctors. Implementations of new factories
may be `internal`. **Offset every new public full name** in the same milestone
by internalizing an **exact** existing public implementation listed below so
the public baseline does **not grow** (M3 nets shrink via deletions).

Do **not** internalize the public consumers in M1–M3 (would cascade through
public shell properties and break DI activation shapes).

### D1 — Editor session factory (M1 / V08)

| Item | Decision |
|------|----------|
| Interface | `IEditorSessionFactory` |
| Implementation | `EditorSessionFactory` |
| Namespace / folder | `Zaide.Features.Editor.Presentation` / `src/Features/Editor/Presentation/` |
| Visibility | Interface **`public`** (injected into public `EditorTabViewModel`); implementation **`internal`** |
| Why not Contracts | Factory returns `EditorViewModel` (Presentation). Contracts → Presentation is forbidden. |
| Constructor deps | `IFileService`, `ISettingsService?`, `ILanguageFormattingService?` — **no** `IServiceProvider` |
| API | `EditorViewModel Create(Document document);` |
| Consumers after M1 | `EditorTabViewModel` only (diff path still uses provider until M2) |
| DI | `services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();` and **remove** `AddTransient<EditorViewModel>` entirely — only the session factory constructs `EditorViewModel` (plus M2 gateway via the same factory) |
| `EditorTabViewModel` | Remains **public**; remove `_services` / `IServiceProvider`; inject public `IEditorSessionFactory` |
| Baseline offset (exact) | Add public `IEditorSessionFactory` (+1). Same milestone make **`internal`**: `Zaide.Features.Editor.Infrastructure.FileService` in `src/Features/Editor/Infrastructure/FileService.cs` (−1). **Net 0**. Update `PublicProductionTypeBaseline.txt`: add interface full name; remove `Zaide.Features.Editor.Infrastructure.FileService`. |

**Files (exact inventory for M1):**

| Path | Action |
|------|--------|
| `src/Features/Editor/Presentation/IEditorSessionFactory.cs` | **Create** (`public`) |
| `src/Features/Editor/Presentation/EditorSessionFactory.cs` | **Create** (`internal`) |
| `src/Features/Editor/Presentation/EditorTabViewModel.cs` | **Edit** — drop provider; use factory |
| `src/Features/Editor/Infrastructure/FileService.cs` | **Edit** — `public` → `internal` (baseline offset) |
| `src/App/Composition/Program.cs` | **Edit** — register factory; align transient |
| `tests/Zaide.Tests/Architecture/PublicProductionTypeBaseline.txt` | **Edit** — +`IEditorSessionFactory`, −`FileService` |
| `tests/Zaide.Tests/Architecture/LegacyArchitectureAllowlist.cs` | **Edit** — remove `R61-AL-LOC-EditorTabViewModel` when evidence gone |

**Test files that construct `EditorTabViewModel` with `IServiceProvider` (exact
inventory — every path must compile after M1; no wildcard):**

| Path | Action |
|------|--------|
| `tests/Zaide.Tests/Features/Editor/Presentation/EditorTabViewModelTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Editor/Presentation/EditorTabViewModelTabLifecycleTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Editor/Presentation/EditorTabBarLifecycleTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Editor/Presentation/EditorTabReorderTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Editor/Presentation/EditorFoldingTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Editor/Presentation/EditorUxProofTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Editor/Presentation/EditorLanguageInputRoutingTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Editor/Presentation/UnsavedDialogTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Editor/Infrastructure/TabCommandRegistrationTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Editor/Infrastructure/EditorMeasurementSeam.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Editor/Infrastructure/FormatDocumentCommandTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/SourceControl/SourceControlTestFactory.cs` | **Edit** |
| `tests/Zaide.Tests/App/Shell/MainWindowViewModelTests.cs` | **Edit** |
| `tests/Zaide.Tests/App/Shell/MainWindowViewModelBottomPanelModeTests.cs` | **Edit** |
| `tests/Zaide.Tests/App/Composition/CommandRegistrationTests.cs` | **Edit** |
| `tests/Zaide.Tests/App/Composition/CommandResolutionAcceptanceTests.cs` | **Edit** |
| `tests/Zaide.Tests/App/Composition/CanonicalCommandRegistrationTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/ProjectSystem/TestProblemsFactory.cs` | **Edit** |
| `tests/Zaide.Tests/Features/ProjectSystem/TestTestResultsFactory.cs` | **Edit** |
| `tests/Zaide.Tests/Features/ProjectSystem/Presentation/ProblemsViewModelTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/ProjectSystem/Presentation/ProblemsNavigationProjectionTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/ProjectSystem/Presentation/ProblemsBuildProjectionTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/ProjectSystem/Presentation/ProjectSystemMainWindowViewModelProjectionTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/ProjectSystem/Presentation/TestResultsViewModelTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/ProjectSystem/Presentation/ProjectSystemStatusBarViewModelProjectionTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Settings/Presentation/SettingsUiTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Settings/Infrastructure/SettingsDrivenKeyBindingRefreshTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Settings/Infrastructure/KeyBindingMaterializationTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Debugging/Presentation/EditorBreakpointRegressionTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Debugging/Presentation/EditorBreakpointViewModelTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Debugging/Presentation/DebugCurrentLocationViewModelTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Debugging/Application/DebugToggleBreakpointCommandTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Debugging/Application/DebugExecutionControlsCommandTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Language/Application/LanguageNavigationTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Language/Application/LanguageSymbolTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Language/Application/LanguageCommandAvailabilityTests.cs` | **Edit** |

**Not in M1:** `SourceControlDiffTabService` (M2).

### D2 — Editor diff-tab gateway (M2 / V07)

| Item | Decision |
|------|----------|
| Contract | `IEditorReadOnlyTabService` in **`Zaide.Features.Editor.Contracts`** |
| Contract file | `src/Features/Editor/Contracts/IEditorReadOnlyTabService.cs` |
| Request type (exact location) | `public sealed record EditorReadOnlyTabRequest(string ReuseKey, string VirtualPath, string Content, string ComparisonStateLabel)` declared **in the same file** `IEditorReadOnlyTabService.cs` (no separate request file) |
| Visibility (locked) | `IEditorReadOnlyTabService` and `EditorReadOnlyTabRequest` are **`public`**; `EditorReadOnlyTabService` implementation is **`internal`** |
| Implementation | `EditorReadOnlyTabService` in **`Zaide.Features.Editor.Presentation`** |
| Impl deps | `EditorTabViewModel`, `IEditorSessionFactory`, `Workspace` |
| Behavior | Open-or-update read-only tab; set `IsReadOnly`, `IsSourceControlDiff`, `SourceControlDiffKey`, `SourceControlComparisonState`; activate tab; sync workspace active document — **same observable behavior as current** `SourceControlDiffTabService.OpenOrUpdateDiff` / `RefreshOpenDiff` tab mutation |
| SourceControl | `SourceControlDiffTabService` remains **public**; depends on public `IEditorReadOnlyTabService` + existing SC services only; **delete** `IServiceProvider` field; **delete** `using` of `Zaide.Features.Editor.Presentation`; keep diff text formatting in SourceControl Application (`SourceControlDiffContent`, keys) |
| DI (exact site) | M2 runs **before** M6. Register **only** in `src/App/Composition/Program.cs` inside `ConfigureServices`: `services.AddSingleton<IEditorReadOnlyTabService, EditorReadOnlyTabService>();`. Do not create an M6 module in M2. |
| Baseline offset (exact) | Add public `IEditorReadOnlyTabService` and `EditorReadOnlyTabRequest` (+2). Same milestone make **`internal`**: (1) `Zaide.Features.SourceControl.Infrastructure.GitRepositoryService`; (2) `Zaide.Features.SourceControl.Infrastructure.FileDiffService` (−2). **Net 0**. |

**Files (exact inventory for M2 — every path always edited/created; no conditionals):**

| Path | Action |
|------|--------|
| `src/Features/Editor/Contracts/IEditorReadOnlyTabService.cs` | **Create** — public interface + public `EditorReadOnlyTabRequest` record in this file |
| `src/Features/Editor/Presentation/EditorReadOnlyTabService.cs` | **Create** (`internal`) |
| `src/Features/SourceControl/Application/SourceControlDiffTabService.cs` | **Edit** — inject `IEditorReadOnlyTabService`; remove provider + Editor.Presentation usings |
| `src/Features/SourceControl/Infrastructure/GitRepositoryService.cs` | **Edit** — `public` → `internal` |
| `src/Features/SourceControl/Infrastructure/FileDiffService.cs` | **Edit** — `public` → `internal` |
| `src/App/Composition/Program.cs` | **Edit** — register `IEditorReadOnlyTabService` → `EditorReadOnlyTabService` |
| `tests/Zaide.Tests/Architecture/PublicProductionTypeBaseline.txt` | **Edit** — +2 contracts, −2 infra types |
| `tests/Zaide.Tests/Architecture/LegacyArchitectureAllowlist.cs` | **Edit** — remove both V07 FindingIds |
| `tests/Zaide.Tests/Architecture/ArchitectureInventoryReader.cs` | **Edit** — remove V07 special-case path/regex |
| `tests/Zaide.Tests/Architecture/ArchitectureRatchetTests.cs` | **Edit** — remove residual-path asserts for V07 |
| `tests/Zaide.Tests/Architecture/ArchitectureInventoryTests.cs` | **Edit** — remove residual-path asserts for V07 |
| `tests/Zaide.Tests/Features/SourceControl/SourceControlTestFactory.cs` | **Edit** — construct `SourceControlDiffTabService` with gateway (no `IServiceProvider`) |
| `tests/Zaide.Tests/Features/SourceControl/Application/SourceControlDiffTabServiceTests.cs` | **Edit** — cover gateway-backed open/refresh behavior (extend existing class) |
| `tests/Zaide.Tests/Features/SourceControl/Integration/SourceControlMutationFlowTests.cs` | **Edit** — via factory construction updates |
| `tests/Zaide.Tests/Features/SourceControl/Presentation/SourceControlViewModelTests.cs` | **Edit** — via `SourceControlTestFactory.CreateWithDiffTabs` construction |

**Public baseline:** **must not grow** (net 0 via exact offsets above).

### D3 — Terminal process factory (M3 / V05)

| Item | Decision |
|------|----------|
| Remove | `ITerminalSessionFactory` (`Contracts`) and `TerminalSessionFactory` (`Application`) |
| Add contract | `ITerminalServiceFactory` in **`Zaide.Features.Terminal.Contracts`** |
| Contract API | `ITerminalService Create();` — **returns Contracts type only** |
| Visibility (locked) | `ITerminalServiceFactory` is **`public`** (injected into public `TerminalHost`); `LinuxTerminalServiceFactory` is **`internal`**; `TerminalHost` remains **public** |
| Implementation | `LinuxTerminalServiceFactory` in **`Zaide.Features.Terminal.Infrastructure`** — `return new LinuxTerminalService();` |
| Presentation composition | `TerminalHost` injects `ITerminalServiceFactory`; on new tab: `new TerminalViewModel(_serviceFactory.Create())` |
| Why | Contracts must not reference Presentation; Application must not construct Presentation. Presentation owns ViewModel pairing. |
| DI | `AddSingleton<ITerminalServiceFactory, LinuxTerminalServiceFactory>()`; remove old session factory registration |
| Public baseline | Remove `ITerminalSessionFactory` and `TerminalSessionFactory` (−2); add `ITerminalServiceFactory` (+1); impl internal. **Net −1**. No separate offset internalization required. |
| Allowlist | **Remove** `R61-AL-NS-ITerminalSessionFactory` and `R61-AL-NS-TerminalSessionFactory` (debt cleared, not re-keyed) |

**Files (exact inventory for M3):**

| Path | Action |
|------|--------|
| `src/Features/Terminal/Contracts/ITerminalSessionFactory.cs` | **Delete** |
| `src/Features/Terminal/Application/TerminalSessionFactory.cs` | **Delete** |
| `src/Features/Terminal/Contracts/ITerminalServiceFactory.cs` | **Create** (`public`) |
| `src/Features/Terminal/Infrastructure/LinuxTerminalServiceFactory.cs` | **Create** (`internal`) |
| `src/Features/Terminal/Presentation/TerminalHost.cs` | **Edit** — inject process factory; create VM |
| `src/App/Composition/Program.cs` | **Edit** registration |
| `tests/Zaide.Tests/Features/Terminal/Application/TerminalSessionFactoryTests.cs` | **Delete** |
| `tests/Zaide.Tests/Features/Terminal/Infrastructure/LinuxTerminalServiceFactoryTests.cs` | **Create** (replace coverage) |
| `tests/Zaide.Tests/Features/Terminal/Presentation/TerminalHostTests.cs` | **Edit** construction |
| `tests/Zaide.Tests/Architecture/LegacyArchitectureAllowlist.cs` | **Remove** both Terminal NS entries |
| `tests/Zaide.Tests/Architecture/ArchitectureInventoryReader.cs` | **Remove** Terminal factory path constants/regexes |
| `tests/Zaide.Tests/Architecture/PublicProductionTypeBaseline.txt` | **Edit** — remove two deleted names; add `ITerminalServiceFactory` |

### D4 — Mention parser purity (M4 / V06)

| Item | Decision |
|------|----------|
| Type | `MentionParser` remains in Application; **no constructor dependency** |
| API | `RouteResult Parse(string sourcePanelId, string rawInput, IReadOnlyList<string> visibleAgentNames)` |
| Matching rules | Unchanged (case-insensitive exact name match; zero/one `@mention`; failure reasons unchanged) |
| Call site (exact) | In `AgentRouter.RouteAndExecuteAsync`, immediately before `Parse`: ```csharp IReadOnlyList<string> visibleAgentNames = _panelHost.Panels .Select(static p => p.AgentName) .ToList(); var result = _parser.Parse(sourcePanelId, rawInput, visibleAgentNames); ``` No other expression, projection, or helper method for name collection. `using System.Linq` remains. |
| Not in M4 | Inverting `AgentRouter` / `AgentExecutionCoordinator` off `IAgentPanelHost` |

**Files (exact inventory for M4):**

| Path | Action |
|------|--------|
| `src/Features/Agents/Application/MentionParser.cs` | **Edit** |
| `src/Features/Agents/Application/AgentRouter.cs` | **Edit** — pass names |
| `tests/Zaide.Tests/Features/Agents/Application/MentionParserTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Agents/Application/AgentRouterTests.cs` | **Edit** — pass visible names into parser call path |
| `tests/Zaide.Tests/Architecture/LegacyArchitectureAllowlist.cs` | **Remove** `R61-AL-NS-MentionParser` |
| `tests/Zaide.Tests/Architecture/ArchitectureInventoryReader.cs` | **Remove** MentionParser special-case |

### D5 — SourceControlState deletion (M5 / V02)

| Item | Decision |
|------|----------|
| Action | **Delete** `src/Features/SourceControl/Domain/SourceControlState.cs` |
| Not chosen | Reclassification / move snapshot — unnecessary while type is unused |
| Snapshot | `RepositoryStatusSnapshot` remains under Application (live producers/consumers) |
| Gate before edit | Re-run production reference search; if any production consumer appears, **stop** and amend plan |
| Public baseline | Remove `Zaide.Features.SourceControl.Domain.SourceControlState` (present in baseline line inventory) |
| Inventory | Remove Domain→Application special-case for this path |

**Files (exact inventory for M5):**

| Path | Action |
|------|--------|
| `src/Features/SourceControl/Domain/SourceControlState.cs` | **Delete** |
| `tests/Zaide.Tests/Architecture/LegacyArchitectureAllowlist.cs` | **Edit** — remove `R61-AL-NS-SourceControlState` entry and FindingId set member |
| `tests/Zaide.Tests/Architecture/ArchitectureInventoryReader.cs` | **Edit** — remove SourceControlState Domain→Application special-case path/regex |
| `tests/Zaide.Tests/Architecture/ArchitectureRatchetTests.cs` | **Edit** — remove residual-path asserts that require `SourceControlState.cs` live evidence |
| `tests/Zaide.Tests/Architecture/ArchitectureInventoryTests.cs` | **Edit** — remove residual-path asserts that require `SourceControlState.cs` live evidence |
| `tests/Zaide.Tests/Architecture/PublicProductionTypeBaseline.txt` | **Edit** — remove line `Zaide.Features.SourceControl.Domain.SourceControlState` (unconditional) |

---

## Milestones (executable)

Dependency order:

```text
M0 (docs)
 → M1 (editor factory)
 → M2 (diff gateway)     [requires M1]
 → M3 (terminal factory) [independent of M1–M2 after M0]
 → M4 (mention parser)   [independent]
 → M5 (delete SC state)  [independent]
 → M6a…M6k (DI modules)  [prefer after M1–M5 so modules register final types]
 → M7 (composition root)
 → M8 (shutdown)
 → M9a…M9c (shell VM)
 → M10 (settings factory)
 → M11a…M11d (visibility)
 → M12 (lifetime map)
 → M13 (closeout)
```

M3–M5 may run in any order after M0 once M1 is not required; **M2 requires M1**.

---

### M0 — Planning gate (this document)

| | |
|--|--|
| **Scope** | Docs only under `docs/refactor/refactor-6.3/` plus truthful status pointers |
| **Completion** | Human accepts M0 after re-audit; no production diffs |
| **Commands** | Architecture filter (baseline green); `git diff --check` on docs |

---

### M1 — Editor session factory (V08)

| | |
|--|--|
| **Status** | **Complete** — commit `e590a79` (`refactor-6.3: M1 editor session factory`) |
| **Design** | § D1 + § D0 |
| **Completion condition** | (1) `EditorTabViewModel` has zero `IServiceProvider` / `GetRequiredService` / `GetService` usages; (2) FindingId `R61-AL-LOC-EditorTabViewModel` removed; (3) inventory shows **3** locator files max (Program, App, DiffTab until M2); (4) public baseline net **0** (`IEditorSessionFactory` added, `FileService` removed); (5) `FileService` is `internal`; (6) shared sequential gate green — **all met** |
| **Live counts after M1** | 395 total / 348 public / 47 internal; FindingIds **8**; locator sites **3** |

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~EditorTabViewModelTests\
|FullyQualifiedName~EditorTabViewModelTabLifecycleTests\
|FullyQualifiedName~EditorViewModelTests\
|FullyQualifiedName~Architecture"
```

**Manual (Linux desktop, optional but recommended):** Open a folder → open two
files → edit → save (Ctrl+S) → close dirty tab (save/discard/cancel). No crash;
dirty UX unchanged.

**Rollback:** single commit revert including allowlist.

---

### M2 — Editor read-only tab gateway (V07)

| | |
|--|--|
| **Status** | **Complete** — commit `d9799ad` (`refactor-6.3: M2 editor read-only tab gateway`); verification green |
| **Design** | § D2 + § D0; requires M1 complete |
| **Completion condition** | (1) `SourceControlDiffTabService` has no `IServiceProvider` and no `using Zaide.Features.Editor.Presentation`; (2) both V07 FindingIds removed; (3) inventory special-case for that path removed; (4) public baseline net **0** (+`IEditorReadOnlyTabService`, +`EditorReadOnlyTabRequest`, −`GitRepositoryService`, −`FileDiffService`); (5) both offset types `internal`; (6) shared gate green — **all met** (re-verified on closeout) |
| **Live counts after M2** | 398 total / 348 public / 50 internal; FindingIds **6**; locator sites **2** (Program, App); production C# **360** / Features **338** |

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~SourceControlDiffTabServiceTests\
|FullyQualifiedName~SourceControlMutationFlowTests\
|FullyQualifiedName~SourceControlViewModelTests\
|FullyQualifiedName~EditorTabViewModelTests\
|FullyQualifiedName~Architecture"
```

**Manual:** Open a git workspace with a modified file → Source Control → open
diff tab → confirm read-only diff content → refresh after stage if applicable.

**Rollback:** single commit.

---

### M3 — Terminal service factory (V05)

| | |
|--|--|
| **Status** | **Complete** — commit `22b869e` (`refactor-6.3: M3 terminal service factory`); `ITerminalServiceFactory` + `LinuxTerminalServiceFactory`; session factory deleted |
| **Design** | § D3 + § D0 |
| **Completion condition** | (1) `ITerminalSessionFactory` / `TerminalSessionFactory` deleted; (2) both Terminal NS FindingIds removed; (3) Contracts has no `using` of Terminal.Presentation; (4) public `ITerminalServiceFactory` + public `TerminalHost` ctor; `LinuxTerminalServiceFactory` `internal`; (5) public baseline **net −1**; (6) shared gate green — **all met** |
| **Live counts after M3** | 398 total / 347 public / 51 internal; FindingIds **4** (6 → 4); locator sites **2** (Program, App); public production types **348 → 347**; public baseline net **−1** |
| **Verification** | build 0 errors; focused terminal+Architecture **119** passed; Architecture **21/21** passed; full suite **2201/2201** passed; `git diff --check` clean; manual terminal smoke **not run** |

**Focused tests (final names after M3 file renames):**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~TerminalHostTests\
|FullyQualifiedName~LinuxTerminalServiceFactoryTests\
|FullyQualifiedName~TerminalTabCloseBehaviorTests\
|FullyQualifiedName~TerminalViewModelTests\
|FullyQualifiedName~Architecture"
```

**Manual:** Toggle terminal (Ctrl+`) → new tab → type `echo ok` → close tab.
Shell process must not outlive tab close / app exit. **Not exercised** in the
M3 verification environment (no interactive desktop session claimed).

**Rollback:** single commit.

---

### M4 — Mention parser purity (V06)

| | |
|--|--|
| **Status** | **Complete** — commit `698b094` (`refactor-6.3: M4 mention parser purity`); `MentionParser` pure Application with caller-supplied names; `R61-AL-NS-MentionParser` removed |
| **Design** | § D4 |
| **Completion condition** | (1) `MentionParser.cs` has no `using`/`field` of Presentation types; (2) `R61-AL-NS-MentionParser` removed; (3) parser tests updated; (4) shared gate green — **all met** |
| **Live counts after M4** | FindingIds **3** (4 → 3: `R61-AL-NS-MentionParser` removed, not re-keyed); NS live violations **1** (SourceControlState only); locator sites **2** (Program, App); Architecture **21/21** |
| **Verification** | build 0 errors; focused MentionParser+AgentRouter+Architecture **39** passed; Architecture **21/21** passed; full suite **2204/2204** passed (prior 2201 + 3 strengthened M4 cases); `git diff --check` clean; manual agent-panel routing smoke **not run** |

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~MentionParserTests\
|FullyQualifiedName~AgentRouterTests\
|FullyQualifiedName~Architecture"
```

**Manual:** Two agent panels → `@Beta hello` from Alpha → routed send + Townhall
mirror behavior unchanged; unknown `@Nope` still surfaces routing failure.
**Not exercised** in this verification environment (no interactive app session claimed).

**Rollback:** single commit.

---

### M5 — Delete SourceControlState (V02)

| | |
|--|--|
| **Status** | **Complete** — commit `273cc56` (`refactor-6.3: M5 delete unused source control state`); `SourceControlState` deleted; `R61-AL-NS-SourceControlState` removed without replacement |
| **Design** | § D5 |
| **Preflight** | `rg -n SourceControlState src --glob '*.cs'` showed only `src/Features/SourceControl/Domain/SourceControlState.cs` (no other production hits) |
| **Completion condition** | (1) file deleted; (2) `R61-AL-NS-SourceControlState` removed; (3) inventory special-case removed; (4) shared gate green — **all met** |
| **Live counts after M5** | FindingIds **2** (3 → 2: exactly `R61-AL-LOC-App`, `R61-AL-LOC-Program`); NS live violations **0** (no NamespaceDirection residual); locator sites **2** (Program, App); public production types **347 → 346**; total top-level types **398 → 397**; internal production types **51** (unchanged); production C# files **360 → 359**; Features C# files **338 → 337**; Architecture **21/21** |
| **Verification** | build 0 errors; focused Architecture+SourceControl **150/150** passed; Architecture **21/21** passed; full suite **2204/2204** passed; `git diff --check` clean; manual verification **none required** (deleted type had no production consumer) |

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~Architecture\
|FullyQualifiedName~SourceControl"
```

**Manual:** none required (dead type). Not invented or claimed.

**Rollback:** single commit.

---

### M6 — Feature DI registration modules (V10)

**Location (locked):** `src/App/Composition/Registration/`

Each slice adds **one** file
`{Feature}ServiceCollectionExtensions.cs` with an
**`internal static`** class and **`internal static`** extension method
`IServiceCollection AddZaide{Feature}(this IServiceCollection services)`.
Visibility is locked **`internal`** so M6 does **not** grow the public
baseline (extension classes/methods are not added to
`PublicProductionTypeBaseline.txt`).

`Program.ConfigureServices` becomes ordered calls only:

```csharp
services.AddZaideAppCore();
services.AddZaideSettings();
// …exact order locked below…
```

**Registration order (locked — preserve current relative dependency safety):**

1. `AddZaideAppCore`
2. `AddZaideSettings`
3. `AddZaideWorkspace`
4. `AddZaideEditor` (includes factory types from M1/M2)
5. `AddZaideTerminal`
6. `AddZaideAgents` (includes `HttpClient`)
7. `AddZaideTownhall`
8. `AddZaideSourceControl`
9. `AddZaideProjectSystem`
10. `AddZaideLanguage`
11. `AddZaideDebugging`

**No lifetime changes** in M6 — only move registration lines. Exact membership:

#### M6a — AppCore (6 registrations)

| Registration |
|--------------|
| `Workspace` |
| `ICommandRegistry` → `CommandRegistry` |
| `StatusBarViewModel` |
| `IScheduler` → `AvaloniaScheduler.Instance` |
| `MainWindowViewModel` |
| `CommandPaletteViewModel` |

File: `src/App/Composition/Registration/AppCoreServiceCollectionExtensions.cs`

**Status:** **complete** at `c59ad7b` (`refactor-6.3: M6a AppCore DI module`).
Production: `Program.ConfigureServices` calls `services.AddZaideAppCore()`
exactly once; the six registrations live only in the internal module (all
`AddSingleton`; scheduler factory remains
`_ => ReactiveUI.Avalonia.AvaloniaScheduler.Instance`). `AddLogging` remains
in `Program`. Public production types **346** (unchanged); internal **52**
(+1 extension class); total top-level **398**; production C# files **360**;
App C# files **21**. Tests: `AppCoreRegistrationModuleTests` plus existing
composition/DI suite — automated verification green (focused 47/47,
Architecture 21/21, full suite 2209/2209); manual verification **not
required**. M6b–M6k registrations remain direct in `Program` (no other
registration modules yet).

#### M6b — Settings (2 at M6 time; M10 adds a third)

| Registration |
|--------------|
| `ISettingsService` → `SettingsService` |
| `ISecretStore` → `FileSecretStore` |

File: `src/App/Composition/Registration/SettingsServiceCollectionExtensions.cs`  
(`AddZaideSettings`). **M10** later adds `ISettingsPanelFactory` →
`SettingsPanelFactory` to this same method only.

**Status:** **complete** at `43b8e85` (`refactor-6.3: M6b Settings DI module`).
Production: `Program.ConfigureServices` calls `services.AddZaideSettings()`
exactly once immediately after `AddZaideAppCore()`; the two registrations live
only in the internal module (both `AddSingleton`; secret-store factory remains
`_ => new FileSecretStore(SettingsPathResolver.GetSecretsPath())`).
`AddLogging` remains in `Program`. No `ISettingsPanelFactory` /
`SettingsPanelFactory` (M10 reserved). Public production types **346**
(unchanged); internal **53** (+1 extension class); total top-level **399**;
production C# files **361**; App C# files **22**. Tests:
`SettingsRegistrationModuleTests` plus M6a ratchet advancement and existing
composition/DI suite — automated verification green (build, focused 53/53,
Architecture 21/21, full suite 2215/2215, `git diff --check`); manual
verification **not required**. M6c–M6k registrations remain direct in
`Program` (no later M6 modules yet).

#### M6c — Workspace (2)

| Registration |
|--------------|
| `IFileTreeService` → `FileTreeService` |
| `FileTreeViewModel` |

File: `src/App/Composition/Registration/WorkspaceServiceCollectionExtensions.cs`
(`AddZaideWorkspace`). Domain `Workspace` stays in M6a AppCore.

**Status:** **complete** at `1ad3625` (`refactor-6.3: M6c Workspace DI module`).
Production: `Program.ConfigureServices` calls `services.AddZaideWorkspace()`
exactly once immediately after `AddZaideSettings()`; the two registrations live
only in the internal module (both `AddSingleton`; `IFileTreeService` →
`FileTreeService`; `FileTreeViewModel` self-registration). `AddLogging` remains
in `Program`. Domain `Workspace` remains in `AddZaideAppCore` only. Public
production types **346** (unchanged); internal **54** (+1 extension class);
total top-level **400**; production C# files **362**; App C# files **23**.
Composition.Registration contains three internal modules (AppCore, Settings,
Workspace). Tests: `WorkspaceRegistrationModuleTests` plus M6a/M6b ratchet
advancement and existing composition/DI suite — automated verification green
(build, focused 58/58, Architecture 21/21, full suite 2220/2220,
`git diff --check`); manual verification **not required**. M6d–M6k
registrations remain direct in `Program` (no later M6 modules yet).

#### M6d — Editor (6 — post-M1/M2; no `EditorViewModel` registration)

| Registration |
|--------------|
| `IFileService` → `FileService` |
| `IEditorSessionFactory` → `EditorSessionFactory` |
| `IEditorReadOnlyTabService` → `EditorReadOnlyTabService` |
| `EditorSearchViewModel` |
| `EditorTabViewModel` |
| `EditorLanguageInputViewModel` |

File: `src/App/Composition/Registration/EditorServiceCollectionExtensions.cs`
(`AddZaideEditor`). **No** `EditorViewModel` registration (M1/M2 factory seams).

**Status:** **complete** at `234a38f` (`refactor-6.3: M6d Editor DI module`).
Production: `Program.ConfigureServices` calls `services.AddZaideEditor()`
exactly once immediately after `AddZaideWorkspace()`; module order is
`AddZaideAppCore` → `AddZaideSettings` → `AddZaideWorkspace` →
`AddZaideEditor`; the six registrations live only in the internal module (all
`AddSingleton`; three interface mappings and three ViewModel self-registrations).
`AddLogging` remains in `Program`. `EditorViewModel` is absent from the service
collection (intentionally unregistered; factory-created). Public production
types **346** (unchanged); internal **55** (+1 extension class); total
top-level **401**; production C# files **363**; App C# files **24**.
Composition.Registration contains four internal modules (AppCore, Settings,
Workspace, Editor). Tests: `EditorRegistrationModuleTests` plus M6a–M6c
ratchet advancement and existing composition/DI suite — automated verification
green (build, focused DI+Architecture 64/64, Architecture 21/21, full suite
2226/2226, `git diff --check`); manual verification **not required**. M6f–M6k
registrations remain direct in `Program` (no later M6 modules yet).

#### M6e — Terminal (2 — post-M3)

| Registration |
|--------------|
| `ITerminalServiceFactory` → `LinuxTerminalServiceFactory` |
| `ITerminalHost` → `TerminalHost` |

File: `src/App/Composition/Registration/TerminalServiceCollectionExtensions.cs`
(`AddZaideTerminal`).

**Status:** **complete** at `8ab50c0` (`refactor-6.3: M6e Terminal DI module`;
closeout docs `d85a83b`). Production: `Program.ConfigureServices` calls
`services.AddZaideTerminal()` exactly once immediately after `AddZaideEditor()`;
module order is `AddZaideAppCore` → `AddZaideSettings` → `AddZaideWorkspace` →
`AddZaideEditor` → `AddZaideTerminal`; the two registrations live only in the
internal module (both `AddSingleton`; `ITerminalServiceFactory` →
`LinuxTerminalServiceFactory`; `ITerminalHost` → `TerminalHost`).
`AddLogging` remains in `Program`. Public production types **346** (unchanged);
internal **56** (+1 extension class); total top-level **402**; production C#
files **364**; App C# files **25**. Composition.Registration contains five
internal modules (AppCore, Settings, Workspace, Editor, Terminal) after M6e.
Tests: `TerminalRegistrationModuleTests` plus M6a–M6d ratchet advancement and
existing composition/DI suite — automated verification green (build, focused
registration+DI+Architecture 51/51, Architecture 21/21, full suite 2231/2231,
`git diff --check`); manual verification **not required**.

#### M6f — Agents (6)

| Registration |
|--------------|
| `IAgentPanelHost` → `AgentPanelHost` |
| `IAgentExecutionService` → `AgentExecutionService` |
| `IAgentExecutionCoordinator` → `AgentExecutionCoordinator` |
| `MentionParser` |
| `IAgentRouter` → `AgentRouter` |
| `HttpClient` (120s timeout factory) |

File: `src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs`
(`AddZaideAgents`).

**Status:** **complete** at `cd809d2` (`refactor-6.3: M6f Agents DI module`).
Production: `Program.ConfigureServices` calls
`services.AddZaideAgents()` exactly once immediately after `AddZaideTerminal()`;
module order is `AddZaideAppCore` → `AddZaideSettings` → `AddZaideWorkspace` →
`AddZaideEditor` → `AddZaideTerminal` → `AddZaideAgents`; the six registrations
live only in the internal module (all `AddSingleton`; five type/self mappings
plus `HttpClient` factory with `TimeSpan.FromSeconds(120)`; factory creates a
new `HttpClient` only — no network activity during registration). Lifetimes and
service-to-implementation mappings are unchanged from the pre-M6f
`Program` registrations. `AddLogging` remains in `Program`. At M6f completion, M6g–M6k
registrations remained direct in `Program` (no later M6 modules yet). Public
production types **346** (unchanged); internal **57** (+1 extension class);
total top-level **403**; production C# files **365**; App C# files **26**.
Composition.Registration contains six internal modules (AppCore, Settings,
Workspace, Editor, Terminal, Agents). Tests: `AgentsRegistrationModuleTests`
plus M6a–M6e ratchet advancement (M6fPlus → M6gPlus; Agents markers removed;
allow one `AddZaideAgents`) and existing composition/DI suite — automated
verification green (build, focused registration+DI+Architecture 56/56,
Architecture 21/21, full suite 2236/2236, `git diff --check` clean;
`git diff --cached --check` was clean before the implementation commit);
manual verification **not required**. Completing M6f did **not** authorize later
M6 slices; **M6g** required and received separate explicit authorization and is
now complete (see below).

#### M6g — Townhall (2)

| Registration |
|--------------|
| `TownhallState` |
| `TownhallViewModel` |

File: `src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs`
Method: `AddZaideTownhall`.

**Status:** **complete** at `1f18e49` (`refactor-6.3: M6g Townhall DI module`).

Both registrations remain **Singleton self-registrations**
(`AddSingleton<TownhallState>()`; `AddSingleton<TownhallViewModel>()`).
`TownhallViewModel` continues to depend on the registered `TownhallState`
singleton (live ctor: `TownhallViewModel(TownhallState state)`). No lifetime,
type, constructor, or dependency changes.

Production: `Program.ConfigureServices` calls `services.AddZaideTownhall()`
exactly once immediately after `AddZaideAgents()`; module order is
`AddZaideAppCore` → `AddZaideSettings` → `AddZaideWorkspace` →
`AddZaideEditor` → `AddZaideTerminal` → `AddZaideAgents` →
`AddZaideTownhall`; the two registrations live only in the internal module;
`AddLogging` remains in `Program`; M6h–M6k registrations remain direct in
`Program` (no `AddZaideSourceControl` / `AddZaideProjectSystem` /
`AddZaideLanguage` / `AddZaideDebugging` calls).

Inventory after M6g: public **346** unchanged; internal **57 → 58**; total
top-level **403 → 404**; production C# **365 → 366**; App C# **26 → 27**;
internal Composition.Registration modules **7**.

Tests: `TownhallRegistrationModuleTests` plus M6a–M6f ratchet advancement
(M6gPlus → M6hPlus; Townhall markers removed from later-direct sets; allow one
`AddZaideTownhall`) and existing composition/DI suite — automated verification
green (build clean 0 warnings / 0 errors, focused registration+DI+Architecture
61/61, Architecture 21/21, full suite 2241/2241, `git diff --check` clean;
`git diff --cached --check` was clean before the implementation commit);
manual verification **not required**. Architecture bookkeeping only for the
new internal type/file (`ArchitectureInventoryReader`,
`ArchitectureInventoryTests`, `ArchitectureVisibilityTests`,
`PublicProductionTypeBaseline.cs` constants); public baseline text and public
type count unchanged; FindingIds and architecture allowlists unchanged.
**M6h** (SourceControl) is next eligible and requires separate authorization.
Completing M6g does **not** authorize later M6 slices.

#### M6h — SourceControl (6)

| Registration |
|--------------|
| `SourceControlViewModel` |
| `IGitRepositoryService` → `GitRepositoryService` |
| `ISourceControlSnapshotOrchestrator` → `SourceControlSnapshotOrchestrator` |
| `IFileDiffService` → `FileDiffService` |
| `ISourceControlDiffTabService` → `SourceControlDiffTabService` |
| `IGitMutationService` → `GitMutationService` |

File: `src/App/Composition/Registration/SourceControlServiceCollectionExtensions.cs`
Method: `AddZaideSourceControl`.

**Status:** **implemented and staged pending review** (proposed message
`refactor-6.3: M6h SourceControl DI module`). Not committed.

All six registrations remain **Singleton**. `SourceControlViewModel` remains a
self-registration (`AddSingleton<SourceControlViewModel>()`). The five
interface-to-implementation mappings are unchanged:
`IGitRepositoryService` → `GitRepositoryService`;
`ISourceControlSnapshotOrchestrator` → `SourceControlSnapshotOrchestrator`;
`IFileDiffService` → `FileDiffService`;
`ISourceControlDiffTabService` → `SourceControlDiffTabService`;
`IGitMutationService` → `GitMutationService`. Milestone comments preserved in
the module. No lifetime, type, constructor, or dependency changes.

Production: `Program.ConfigureServices` calls `services.AddZaideSourceControl()`
exactly once immediately after `AddZaideTownhall()`; module order is
`AddZaideAppCore` → `AddZaideSettings` → `AddZaideWorkspace` →
`AddZaideEditor` → `AddZaideTerminal` → `AddZaideAgents` →
`AddZaideTownhall` → `AddZaideSourceControl`; the six registrations live only
in the internal module; `AddLogging` remains in `Program`; M6i–M6k
registrations remain direct in `Program` (no `AddZaideProjectSystem` /
`AddZaideLanguage` / `AddZaideDebugging` calls).

Inventory after M6h: public **346** unchanged; internal **58 → 59**; total
top-level **404 → 405**; production C# **366 → 367**; App C# **27 → 28**;
internal Composition.Registration modules **8**.

Tests: `SourceControlRegistrationModuleTests` plus M6a–M6g ratchet advancement
(M6hPlus → M6iPlus; SourceControl markers removed from later-direct sets; allow
one `AddZaideSourceControl`) and existing composition/DI suite — automated
verification green (build clean 0 warnings / 0 errors, focused
registration+DI+Architecture **66/66**, Architecture **21/21**, full suite
**2246/2246**, `git diff --check` clean; `git diff --cached --check` clean
before proposed commit); manual verification **not required**. Architecture
bookkeeping only for the new internal type/file (`ArchitectureInventoryReader`,
`ArchitectureInventoryTests`, `ArchitectureVisibilityTests`,
`PublicProductionTypeBaseline.cs` constants); public baseline text and public
type count unchanged; FindingIds and architecture allowlists unchanged.
**M6i** (ProjectSystem) is next eligible and requires separate authorization.
Completing M6h does **not** authorize later M6 slices.

#### M6i — ProjectSystem (14)

| Registration |
|--------------|
| `IProjectFileSystem` → `FileSystemProjectFileSystem` |
| `IProjectDiscovery` → `ProjectDiscovery` |
| `IProjectContextService` → `ProjectContextService` |
| `IProjectOperationGate` → `ProjectOperationGate` |
| `IProjectDebugTargetResolver` → `ProjectDebugTargetResolver` |
| `IProjectDebugLaunchService` → `ProjectDebugLaunchService` |
| `IManagedProcessRunner` → `ManagedProcessRunner` |
| `IProjectWorkflowService` → `ProjectWorkflowService` |
| `IProjectOutputService` → `ProjectOutputService` |
| `ProjectWorkflowViewModel` |
| `IBuildDiagnosticsService` → `BuildDiagnosticsService` |
| `ITestResultsService` → `TestResultsService` |
| `TestResultsViewModel` |
| `ProblemsViewModel` |

File: `…/ProjectSystemServiceCollectionExtensions.cs`

#### M6j — Language (10)

| Registration |
|--------------|
| `ILanguageServerBinaryLocator` → `LanguageServerBinaryLocator` |
| `ILanguageServerSessionFactory` → `CsharpLsSessionFactory` |
| `ILanguageSessionService` → `LanguageSessionService` |
| `ILanguageDocumentBridge` → `LanguageDocumentBridge` |
| `ILanguageDiagnosticsService` → `LanguageDiagnosticsService` |
| `ILanguageCompletionService` → `LanguageCompletionService` |
| `ILanguageHoverService` → `LanguageHoverService` |
| `ILanguageNavigationService` → `LanguageNavigationService` |
| `ILanguageSymbolService` → `LanguageSymbolService` |
| `ILanguageFormattingService` → `LanguageFormattingService` |

File: `…/LanguageServiceCollectionExtensions.cs`

#### M6k — Debugging (10)

| Registration |
|--------------|
| `IDebugAdapterLocator` → `DebugAdapterLocator` |
| `IDebugAdapterSessionFactory` → `DebugAdapterSessionFactory` |
| `DebugSessionTimeoutPolicy` |
| `IDebugSessionService` → `DebugSessionService` |
| `IBreakpointService` → `BreakpointService` |
| `DebugSessionViewModel` |
| `DebugStackProjectionViewModel` |
| `DebugCurrentLocationViewModel` |
| `DebugPanelViewModel` |
| `EditorBreakpointViewModel` |

File: `…/DebuggingServiceCollectionExtensions.cs`

**Checksum at M6 time (after M1–M5, before M10):**  
AppCore 6 + Settings 2 + Workspace 2 + Editor 6 + Terminal 2 + Agents 6 +
Townhall 2 + SC 6 + ProjectSystem 14 + Language 10 + Debugging 10 = **66**.  
(M10 adds settings factory → **67** post-M10, matching M12 inventory.)

**Per-slice completion:** (1) listed registrations exist only in that extension
file + `Program` calls the extension; (2) no registration lifetime/type change;
(3) shared gate; (4) DI tests still resolve via `Program.ConfigureServices`.

**Focused tests each M6 slice:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~CompositionDiIntegrationTests\
|FullyQualifiedName~ProjectSystemDependencyInjectionTests\
|FullyQualifiedName~ProjectWorkflowServiceDiTests\
|FullyQualifiedName~LanguageSessionServiceDiTests\
|FullyQualifiedName~DebugSessionServiceDiTests\
|FullyQualifiedName~Architecture"
```

**Commit boundary (locked):** exactly **one commit per slice** `M6a` through
`M6k` (eleven commits). Do **not** batch slices. Each commit message names
its slice id (e.g. `refactor-6.3: M6b settings DI module`).

**Manual:** none if DI tests green.

---

### M7 — Composition root store / remove public `App.Services` (V09 partial)

| | |
|--|--|
| **Scope** | Remove public static `App.Services`. Introduce a single internal composition-root **store** for the ReactiveUI bootstrap provider assignment. |
| **Locked approach (no alternatives)** | Create `internal static class CompositionRoot` in `src/App/Composition/CompositionRoot.cs` with **only** `internal static IServiceProvider Services { get; set; }` (no `Start` method, **no** `GetRequiredService` / `GetService` calls inside `CompositionRoot`). `Program.BuildAvaloniaApp` `withResolver` assigns `CompositionRoot.Services = sp` (not `App.Services`). **Delete** public static `App.Services`. All existing eager resolves and `DisposeServicesOnExit` **remain** in `App.axaml.cs`, rewritten to use `CompositionRoot.Services` wherever `App.Services` was read (including `OnFrameworkInitializationCompleted` and Exit). |
| **Why not move resolves into CompositionRoot in M7** | Moving `GetRequiredService` into a new file would either grow the FindingId set or require MatchKey games mid-refactor. Keeping provider **call sites** on the already-allowlisted `App.axaml.cs` / `Program.cs` files preserves the frozen allowlist set size. |
| **V09 residual (deliberate limitation — not full clearance)** | Static mutable composition-root provider **remains** as `CompositionRoot.Services`. FindingIds **`R61-AL-LOC-Program`** and **`R61-AL-LOC-App` remain** after M7. MatchKeys stay exactly: `src/App/Composition/Program.cs` and `src/App/Composition/App.axaml.cs`. Rationale text on both entries is updated to: “composition-boundary residual after public `App.Services` removal; ReactiveUI `UseReactiveUIWithMicrosoftDependencyResolver` requires a root store; non-composition locator debt cleared in M1–M2.” M13 may only retain these two LOC residuals among composition files. Full removal of static provider storage requires a **future plan amendment**, not silent M7 expansion. |

**Completion condition:**

1. Zero occurrences of identifier `App.Services` in production (tests may only mention it in comments if any remain — prefer zero).
2. `CompositionRoot.Services` is the sole static `IServiceProvider` store.
3. `CompositionRoot.cs` contains **no** `GetRequiredService` / `GetService` / `IServiceProvider` field storage other than the static `Services` property.
4. Locator production files with provider **call** evidence remain exactly `{Program.cs, App.axaml.cs}`; FindingId set size still 9 or less only by removals from other milestones — M7 does **not** remove LOC-Program/LOC-App.
5. Shared gate green.

**Files (exact inventory for M7):**

| Path | Action |
|------|--------|
| `src/App/Composition/CompositionRoot.cs` | **Create** — static `Services` property only |
| `src/App/Composition/Program.cs` | **Edit** — assign `CompositionRoot.Services = sp` |
| `src/App/Composition/App.axaml.cs` | **Edit** — delete `App.Services`; all former `Services` / `App.Services` reads become `CompositionRoot.Services` |
| `tests/Zaide.Tests/Architecture/LegacyArchitectureAllowlist.cs` | **Edit** — update rationale text for `R61-AL-LOC-Program` and `R61-AL-LOC-App` only; **MatchKeys unchanged** |

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~CompositionDiIntegrationTests\
|FullyQualifiedName~ProjectWorkflowProjectionShutdownTests\
|FullyQualifiedName~Architecture"
```

**Manual:** cold start app → main window appears → Command Palette Ctrl+Shift+P
registers → open folder.

---

### M8 — Ordered shutdown owner (V12)

| | |
|--|--|
| **Type (locked)** | `internal static class ApplicationShutdown` in `src/App/Composition/ApplicationShutdown.cs` |
| **API (locked)** | `internal static void Run(IServiceProvider services)` — **not** registered in DI; **not** an instance service |
| **Call sites** | Desktop Exit handler invokes `ApplicationShutdown.Run(...)`; `App.DisposeServicesOnExit` becomes a **one-line** forwarder `ApplicationShutdown.Run(services)` so existing tests that call `App.DisposeServicesOnExit` keep compiling without a mass rename |
| **Order (locked, from live App.axaml.cs)** | (1) resolve Output, BuildDiagnostics, TestResults; (2) dispose `IDebugSessionService`; (3) dispose debug projection VMs (Panel, CurrentLocation, EditorBreakpoint, Session); (4) dispose `IProjectWorkflowService`; (5) dispose Output/BuildDiagnostics/TestResults; (6) dispose language services (Formatting→Navigation→Symbol→Completion→Hover→Diagnostics→DocumentBridge→Session); (7) dispose `IProjectContextService`; (8) dispose `IFileTreeService?`; (9) dispose `ITerminalHost?` |
| **Dispose selection (locked — exactly once per owner)** | For each resolved owner, in the order above, call **one** teardown path only: ```text if (owner is IAsyncDisposable asyncDisposable) → asyncDisposable.DisposeAsync().AsTask().Wait(ShutdownAsyncTimeout); else if (owner is IDisposable disposable) → disposable.Dispose(); // never both ``` Constant `ShutdownAsyncTimeout = TimeSpan.FromSeconds(5)`. Do **not** call `Dispose()` and then `DisposeAsync()` on the same instance. Do not introduce a fire-and-forget async Exit handler. |
| **Exactly-once proof** | Extend `tests/Zaide.Tests/Features/ProjectSystem/DI/ProjectWorkflowProjectionShutdownTests.cs` so each ordered owner is observed disposed **exactly once**. Existing order assertions remain. |
| **Completion** | Body of shutdown lives only in `ApplicationShutdown.Run`; `DisposeServicesOnExit` is ≤ 3 lines; order + exactly-once tests pass; shared gate green |

**Files (exact inventory for M8):**

| Path | Action |
|------|--------|
| `src/App/Composition/ApplicationShutdown.cs` | **Create** |
| `src/App/Composition/App.axaml.cs` | **Edit** — Exit + thin `DisposeServicesOnExit` |
| `tests/Zaide.Tests/Features/ProjectSystem/DI/ProjectWorkflowProjectionShutdownTests.cs` | **Edit** — keep `App.DisposeServicesOnExit` entry; add exactly-once coverage |

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~ProjectWorkflowProjectionShutdownTests\
|FullyQualifiedName~Architecture"
```

**Manual:** start debug or build if tools available → quit app → no orphan
`dotnet`/`netcoredbg`/`csharp-ls` processes (`pgrep -a` before/after).

**Rollback:** single commit.

---

### M9 — Shell ViewModel composition reduction (V13)

Measurable baseline (live): **628** lines; **18** ctor parameters (16 required +
2 optional).

**Accessibility rule (locked for all M9 slices):** new helper types are
`internal` and are **constructed inside** `MainWindowViewModel` (or held as
private fields). They are **never** exposed as public properties/parameters on
`MainWindowViewModel`. Public shell/view API on MWVM remains the forwarder
surface (`SendAgentMessageAsync`, panel mode properties/commands, `Activate`).

#### M9a — Agent send / Townhall mirror extraction

| Item | Locked decision |
|------|-----------------|
| Type | `internal sealed class AgentTownhallMirrorCoordinator` |
| Path | `src/App/Shell/AgentTownhallMirrorCoordinator.cs` |
| Construction | Created **inside** `MainWindowViewModel` ctor: `new AgentTownhallMirrorCoordinator(agentRouter, agentPanelHost, townhallViewModel)` — **not** DI-registered; **not** a MWVM ctor parameter type |
| API on coordinator | `Task SendAsync(string panelId, string userMessage, CancellationToken ct)` containing today’s `SendAgentMessageAsync` body (L512–586) — may be `async Task` internally |
| Public MWVM API (exact; compiles) | **Not** `async` + `return Task`. Locked forwarding shape: ```csharp public Task SendAgentMessageAsync( string panelId, string userMessage, CancellationToken ct = default) => _agentTownhallMirror.SendAsync(panelId, userMessage, ct); ``` Expression-bodied `Task` return; **no** `async` keyword on MWVM. View continues calling `ViewModel.SendAgentMessageAsync` (`MainWindow.axaml.cs` ~L189). |
| Ctor params (locked change) | **Remove** required parameter `IAgentExecutionCoordinator agentExecutionCoordinator` and **remove** public property `AgentExecutionCoordinator` (live: only stored; never read by shell or tests beyond construction). **Keep** `IAgentRouter`, `IAgentPanelHost`, `TownhallViewModel` as ctor params (host/townhall still public shell properties). **Remove** public property `AgentRouter` (shell does not read it; coordinator holds the router reference privately). Live baseline: **16 required + 2 optional = 18 total**. After M9a: **15 required + 2 optional = 17 total**. |
| Measurable | MWVM line count **≤ 560**; `SendAgentMessageAsync` is the expression-bodied forwarder above (≤ 3 lines); ctor parameter counts **15 required / 17 total** |

**Production files (exact):**

| Path | Action |
|------|--------|
| `src/App/Shell/AgentTownhallMirrorCoordinator.cs` | **Create** |
| `src/App/Shell/MainWindowViewModel.cs` | **Edit** |
| `src/App/Composition/Program.cs` | **No change** (DI still registers `IAgentExecutionCoordinator` for `AgentRouter`; MWVM ctor no longer requests it) |

**Test files (exact — all 13 live files with `new MainWindowViewModel(...)`; every site must drop the coordinator argument):**

| Path | Action |
|------|--------|
| `tests/Zaide.Tests/App/Shell/MainWindowViewModelTests.cs` | **Edit** |
| `tests/Zaide.Tests/App/Shell/MainWindowViewModelBottomPanelModeTests.cs` | **Edit** |
| `tests/Zaide.Tests/App/Composition/CanonicalCommandRegistrationTests.cs` | **Edit** |
| `tests/Zaide.Tests/App/Composition/CommandRegistrationTests.cs` | **Edit** |
| `tests/Zaide.Tests/App/Composition/CommandResolutionAcceptanceTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Debugging/Application/DebugExecutionControlsCommandTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Editor/Presentation/EditorUxProofTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/ProjectSystem/Presentation/ProjectSystemMainWindowViewModelProjectionTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/ProjectSystem/Presentation/ProjectSystemStatusBarViewModelProjectionTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/ProjectSystem/Presentation/TestResultsViewModelTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Settings/Infrastructure/KeyBindingMaterializationTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Settings/Infrastructure/SettingsDrivenKeyBindingRefreshTests.cs` | **Edit** |
| `tests/Zaide.Tests/Features/Settings/Presentation/SettingsUiTests.cs` | **Edit** |

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~MainWindowViewModelTests\
|FullyQualifiedName~MainWindowViewModelBottomPanelModeTests\
|FullyQualifiedName~CanonicalCommandRegistrationTests\
|FullyQualifiedName~CommandRegistrationTests\
|FullyQualifiedName~CommandResolutionAcceptanceTests\
|FullyQualifiedName~DebugExecutionControlsCommandTests\
|FullyQualifiedName~EditorUxProofTests\
|FullyQualifiedName~ProjectSystemMainWindowViewModelProjectionTests\
|FullyQualifiedName~ProjectSystemStatusBarViewModelProjectionTests\
|FullyQualifiedName~TestResultsViewModelTests\
|FullyQualifiedName~KeyBindingMaterializationTests\
|FullyQualifiedName~SettingsDrivenKeyBindingRefreshTests\
|FullyQualifiedName~SettingsUiTests\
|FullyQualifiedName~AgentRouterTests\
|FullyQualifiedName~MentionParserTests\
|FullyQualifiedName~Architecture"
```

**Manual:** send agent message from panel → Townhall user + response/error
mirror unchanged.

#### M9b — Panel navigation extraction

| Item | Locked decision |
|------|-----------------|
| Type | `internal sealed class ShellPanelNavigation` |
| Path | `src/App/Shell/ShellPanelNavigation.cs` |
| Notification model (locked — no alternatives) | **MWVM retains all `RaiseAndSetIfChanged` ownership** for shell-observed properties. Live `MainWindow.axaml.cs` subscribes to `ViewModel.LeftPanelMode`, `ViewModel.BottomPanelMode`, and `ViewModel.IsBottomPanelVisible` (~L286–331). Those properties **must** continue to raise change notifications **on `MainWindowViewModel`**. Do **not** store mode state only on `ShellPanelNavigation` with simple MWVM getters (that would break animations/visibility). |
| Owns on MWVM (unchanged storage + notify) | Backing fields and public setters for `LeftPanelMode`, `BottomPanelMode`, `IsBottomPanelVisible`, and the derived flags (`IsExplorerMode`, `IsSourceControlMode`, `IsTerminalBottomMode`, …) using today’s `RaiseAndSetIfChanged` cascade (same semantics as live L87–164). |
| Owns on `ShellPanelNavigation` | **Command construction and decision actions only**: `SwitchToExplorerCommand`, `SwitchToSourceControlCommand`, `SwitchToTerminalBottomCommand`, `SwitchToProblemsBottomCommand`, `SwitchToOutputBottomCommand`, `SwitchToTestResultsBottomCommand`, `SwitchToDebugBottomCommand`, `ToggleBottomPanelCommand`, `HideBottomPanelCommand`. Commands mutate **only** by calling injected delegates that set MWVM properties (e.g. `setLeft(LeftPanelMode.Explorer)`, `setBottomVisible(!getBottomVisible())`), so notifications fire on MWVM. |
| Construction | Inside MWVM ctor: ```csharp _panelNavigation = new ShellPanelNavigation( setLeft: mode => LeftPanelMode = mode, setBottom: mode => BottomPanelMode = mode, setBottomVisible: v => IsBottomPanelVisible = v, getBottomVisible: () => IsBottomPanelVisible); SwitchToExplorerCommand = _panelNavigation.SwitchToExplorerCommand; // …assign every public command property from _panelNavigation ``` |
| Public MWVM API | Same property and command **names** as today for shell/tests. Property bodies stay on MWVM with `RaiseAndSetIfChanged`. Command properties are get-only and assigned from `_panelNavigation` in the ctor. |
| DI | No new registration |
| Measurable | After M9a+M9b, MWVM **≤ 500** lines; no new public types; panel mode tests still pass without API renames; `WhenAnyValue` on MWVM mode properties still observes command-driven changes |

**Production files (exact):**

| Path | Action |
|------|--------|
| `src/App/Shell/ShellPanelNavigation.cs` | **Create** — commands + delegates only; no authoritative mode fields |
| `src/App/Shell/MainWindowViewModel.cs` | **Edit** — keep property notify ownership; wire navigation delegates/commands |
| `src/App/Shell/MainWindow.axaml.cs` | **No change** (subscriptions stay on MWVM properties) |

**Test files (exact):**

| Path | Action |
|------|--------|
| `tests/Zaide.Tests/App/Shell/MainWindowViewModelTests.cs` | **No change** (public panel API names unchanged) |
| `tests/Zaide.Tests/App/Shell/MainWindowViewModelBottomPanelModeTests.cs` | **No change** |

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~MainWindowViewModelTests\
|FullyQualifiedName~MainWindowViewModelBottomPanelModeTests\
|FullyQualifiedName~Architecture"
```

**Manual:** Explorer ↔ Source Control; bottom Terminal/Problems/Output/Test/Debug;
Ctrl+J toggle.

#### M9c — Activation host extraction

| Item | Locked decision |
|------|-----------------|
| Type | `internal sealed class MainWindowActivationHost` |
| Path | `src/App/Shell/MainWindowActivationHost.cs` |
| DI | **Not** registered — constructed only inside `MainWindowViewModel` ctor |
| Exact constructor | ```csharp public MainWindowActivationHost( ProblemsViewModel problemsViewModel, ProjectWorkflowViewModel projectWorkflowViewModel, DebugSessionViewModel debugSessionViewModel, DebugPanelViewModel debugPanelViewModel, EditorBreakpointViewModel editorBreakpointViewModel, DebugCurrentLocationViewModel? debugCurrentLocationViewModel, TestResultsViewModel testResultsViewModel, FileTreeViewModel fileTreeViewModel, SourceControlViewModel sourceControlViewModel, EditorTabViewModel editorTabs, ITerminalHost terminalHost, Workspace workspace, IProjectContextService projectContextService, IScheduler projectContextScheduler, ReactiveCommand<Unit, Unit> closeFolderCommand, Action<BottomPanelMode> setBottomPanelMode, Action<bool> setIsBottomPanelVisible, Action<string?> setStatusText, Action<ProjectContext> setCurrentProjectContext, Action<string?> setWorkspaceProjectName) ``` |
| Nullability | `debugCurrentLocationViewModel` is **`DebugCurrentLocationViewModel?`** (matches live optional MWVM property). All other constructor parameters are **non-null**; host ctor throws `ArgumentNullException` for every non-nullable parameter that is null. |
| MWVM construction site (exact) | In `MainWindowViewModel` ctor after commands that `Activate` needs exist (`CloseFolderCommand` initialized): ```csharp _activationHost = new MainWindowActivationHost( ProblemsViewModel, ProjectWorkflowViewModel, DebugSessionViewModel, DebugPanelViewModel, EditorBreakpointViewModel, DebugCurrentLocationViewModel, TestResultsViewModel, FileTreeViewModel, SourceControlViewModel, EditorTabs, TerminalHost, workspace, projectContextService, ProjectContextScheduler, CloseFolderCommand, mode => BottomPanelMode = mode, visible => IsBottomPanelVisible = visible, text => StatusText = text, ctx => CurrentProjectContext = ctx, name => WorkspaceProjectName = name); ``` |
| Owns | Entire current `Activate()` body (live L367–503): feature `Activate()` calls, show-panel subscriptions, RootPath→workspace/SC sync, CloseFolderRequested handler, project-context `WhenChanged`, status text routing, OpenFileRequested handling — using constructor fields/delegates only (no capture of outer MWVM except via the five `Action`s and injected services/VMs) |
| Public method on host | `public void Activate(CompositeDisposable disposables)` |
| Public MWVM `Activate()` | Exact body only: ```csharp if (_disposables is not null) return; _disposables = new CompositeDisposable(); _activationHost.Activate(_disposables); ``` |
| Measurable | MWVM **≤ 420** lines after M9a–M9c; `Activate()` ≤ 6 lines |

**Production files (exact):**

| Path | Action |
|------|--------|
| `src/App/Shell/MainWindowActivationHost.cs` | **Create** |
| `src/App/Shell/MainWindowViewModel.cs` | **Edit** |
| `src/App/Shell/MainWindow.axaml.cs` | **No change** (still calls `ViewModel!.Activate()`) |

**Test files (exact):**

| Path | Action |
|------|--------|
| `tests/Zaide.Tests/App/Shell/MainWindowViewModelTests.cs` | **No change** (`Activate()` public entrypoint unchanged) |
| `tests/Zaide.Tests/App/Shell/MainWindowViewModelBottomPanelModeTests.cs` | **No change** |

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~MainWindowViewModelTests\
|FullyQualifiedName~MainWindowViewModelBottomPanelModeTests\
|FullyQualifiedName~ProjectSystemMainWindowViewModelProjectionTests\
|FullyQualifiedName~Architecture"
```

**M9 series completion:** all measurables met; shared sequential gate green; no
UX change for agent send, panel modes, or activation side-effects.

---

### M10 — Settings panel factory (V17)

**Prerequisite state (mandatory):** M6b and M7 are complete. Registrations live
under `src/App/Composition/Registration/`. Bootstrap reads
`CompositionRoot.Services` from `App.axaml.cs` (no `App.Services`).

| Item | Locked decision |
|------|-----------------|
| Interface | `public interface ISettingsPanelFactory` in `src/Features/Settings/Presentation/ISettingsPanelFactory.cs` |
| Implementation | `internal sealed class SettingsPanelFactory` in `src/Features/Settings/Presentation/SettingsPanelFactory.cs` |
| API (exact) | ```csharp (SettingsViewModel ViewModel, SettingsPanelView View) Create( ISettingsService settings, ISecretStore secrets); ``` Implementation: `var viewModel = new SettingsViewModel(settings, secrets); var panel = new SettingsPanelView(viewModel); return (viewModel, panel);` |
| Call site | `ShowSettingsPanel`: `var (viewModel, panel) = _settingsPanelFactory.Create(_settings, _secrets);` |
| Dispose | Unchanged close path in `CloseSettingsPanel` (no factory Dispose API) |
| Baseline offset | +`ISettingsPanelFactory` (+1); **`internal`** `Zaide.Features.Settings.Infrastructure.SettingsMigrator` (−1); **net 0** |
| DI registration (exact site after M6) | **Only** `src/App/Composition/Registration/SettingsServiceCollectionExtensions.cs` inside `AddZaideSettings`: `services.AddSingleton<ISettingsPanelFactory, SettingsPanelFactory>();`. Do **not** add a second registration in `Program.ConfigureServices` body. |
| Bootstrap resolve (exact site after M7) | In `App.axaml.cs` desktop bootstrap (same method that currently builds `new MainWindow(...)`): `var settingsPanelFactory = CompositionRoot.Services.GetRequiredService<ISettingsPanelFactory>();` pass into `new MainWindow(..., settingsPanelFactory)`. |
| MainWindow | Ctor gains `ISettingsPanelFactory settingsPanelFactory`; field `_settingsPanelFactory`; `ShowSettingsPanel` uses factory only |

**Files (exact inventory for M10):**

| Path | Action |
|------|--------|
| `src/Features/Settings/Presentation/ISettingsPanelFactory.cs` | **Create** (`public`) |
| `src/Features/Settings/Presentation/SettingsPanelFactory.cs` | **Create** (`internal`) |
| `src/Features/Settings/Infrastructure/SettingsMigrator.cs` | **Edit** — `public` → `internal` |
| `src/App/Composition/Registration/SettingsServiceCollectionExtensions.cs` | **Edit** — register factory in `AddZaideSettings` |
| `src/App/Composition/App.axaml.cs` | **Edit** — resolve via `CompositionRoot.Services`; pass into `MainWindow` |
| `src/App/Shell/MainWindow.axaml.cs` | **Edit** — ctor + factory usage; zero `new SettingsViewModel` / `new SettingsPanelView` |
| `tests/Zaide.Tests/Architecture/PublicProductionTypeBaseline.txt` | **Edit** — +`ISettingsPanelFactory`, −`SettingsMigrator` |
| `tests/Zaide.Tests/Features/Settings/Infrastructure/SettingsCoreTests.cs` | **Edit** — `SettingsMigrator` is internal; keep compiling via `InternalsVisibleTo` (same assembly access already) |

**Test files with no production-surface change required (do not list conditionals):**

| Path | Action |
|------|--------|
| `tests/Zaide.Tests/Features/Settings/Presentation/SettingsViewModelTests.cs` | **No change** |
| `tests/Zaide.Tests/Features/Settings/Presentation/SettingsPanelViewTests.cs` | **No change** |
| `tests/Zaide.Tests/Features/Settings/Presentation/SettingsUiTests.cs` | **No change** |
| `tests/Zaide.Tests/Features/Settings/Presentation/SettingsPersistenceUiTests.cs` | **No change** |

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~SettingsViewModelTests\
|FullyQualifiedName~SettingsPanelViewTests\
|FullyQualifiedName~SettingsUiTests\
|FullyQualifiedName~SettingsPersistenceUiTests\
|FullyQualifiedName~SettingsCoreTests\
|FullyQualifiedName~Architecture"
```

**Manual:** open Settings → change non-secret editor setting → save/discard →
close → reopen shows expected persistence.

**Completion:** (1) zero `new SettingsViewModel` / `new SettingsPanelView` in
`MainWindow.axaml.cs`; (2) factory registered only in `AddZaideSettings`;
(3) factory resolved only via `CompositionRoot.Services` in `App.axaml.cs`;
(4) baseline net 0; (5) shared gate green.

**Not in M10:** layout extraction (Refactor 8).

---

### M11 — Visibility internalization (V14)

**Baseline at start of M11 series:** public full-name count after prior
milestones (starts from 348; M3 already removed 2; M5 removed
`SourceControlState` if still public). **Rule:** never grow within a slice.
Each slice internalizes **exactly the types listed** — no substitution list,
no implementation-time “pick another type.”

If a listed type cannot be made `internal` (unexpected framework/XAML
constraint discovered live), **stop the milestone**, amend this plan with a
replacement type, and re-authorize — do not silently swap.

Keep **Contracts interfaces** and **XAML-activated Views** public unless listed.

Each slice updates `PublicProductionTypeBaseline.txt` in the **same** commit
(remove the exact full names internalized).

#### M11a — Language implementations (exactly 10 types → all 10 internalized)

| Full name to make `internal` | Source file |
|------------------------------|-------------|
| `Zaide.Features.Language.Application.LanguageSessionService` | `src/Features/Language/Application/LanguageSessionService.cs` |
| `Zaide.Features.Language.Application.LanguageDocumentBridge` | `src/Features/Language/Application/LanguageDocumentBridge.cs` |
| `Zaide.Features.Language.Application.LanguageDiagnosticsService` | `src/Features/Language/Application/LanguageDiagnosticsService.cs` |
| `Zaide.Features.Language.Application.LanguageCompletionService` | `src/Features/Language/Application/LanguageCompletionService.cs` |
| `Zaide.Features.Language.Application.LanguageHoverService` | `src/Features/Language/Application/LanguageHoverService.cs` |
| `Zaide.Features.Language.Application.LanguageNavigationService` | `src/Features/Language/Application/LanguageNavigationService.cs` |
| `Zaide.Features.Language.Application.LanguageSymbolService` | `src/Features/Language/Application/LanguageSymbolService.cs` |
| `Zaide.Features.Language.Application.LanguageFormattingService` | `src/Features/Language/Application/LanguageFormattingService.cs` |
| `Zaide.Features.Language.Infrastructure.Lsp.LanguageServerBinaryLocator` | `src/Features/Language/Infrastructure/Lsp/LanguageServerBinaryLocator.cs` |
| `Zaide.Features.Language.Infrastructure.Lsp.CsharpLsSessionFactory` | `src/Features/Language/Infrastructure/Lsp/CsharpLsSessionFactory.cs` |

**Shrink:** exactly **−10** from baseline (all ten listed).

**Also edit:** `tests/Zaide.Tests/Architecture/PublicProductionTypeBaseline.txt`
(remove the ten full names).

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~Language\
|FullyQualifiedName~ArchitectureVisibility\
|FullyQualifiedName~Architecture"
```

#### M11b — Debugging + ProjectSystem implementations (exactly 8 types)

| Full name to make `internal` | Source file |
|------------------------------|-------------|
| `Zaide.Features.Debugging.Infrastructure.Dap.DebugAdapterLocator` | `src/Features/Debugging/Infrastructure/Dap/DebugAdapterLocator.cs` |
| `Zaide.Features.Debugging.Infrastructure.Dap.DebugAdapterSessionFactory` | `src/Features/Debugging/Infrastructure/Dap/DebugAdapterSessionFactory.cs` |
| `Zaide.Features.Debugging.Application.DebugSessionService` | `src/Features/Debugging/Application/DebugSessionService.cs` |
| `Zaide.Features.Debugging.Application.BreakpointService` | `src/Features/Debugging/Application/BreakpointService.cs` |
| `Zaide.Features.ProjectSystem.Infrastructure.ProjectWorkflowService` | `src/Features/ProjectSystem/Infrastructure/ProjectWorkflowService.cs` |
| `Zaide.Features.ProjectSystem.Infrastructure.ManagedProcessRunner` | `src/Features/ProjectSystem/Infrastructure/ManagedProcessRunner.cs` |
| `Zaide.Features.ProjectSystem.Infrastructure.ProjectContextService` | `src/Features/ProjectSystem/Infrastructure/ProjectContextService.cs` |
| `Zaide.Features.ProjectSystem.Infrastructure.BuildDiagnosticsService` | `src/Features/ProjectSystem/Infrastructure/BuildDiagnosticsService.cs` |

**Shrink:** exactly **−8**.

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~Debugging\
|FullyQualifiedName~ProjectWorkflow\
|FullyQualifiedName~ProjectSystem\
|FullyQualifiedName~ArchitectureVisibility\
|FullyQualifiedName~Architecture"
```

#### M11c — SourceControl + Terminal application/infra (exactly 5 types)

> **Note:** `FileService`, `GitRepositoryService`, and `FileDiffService` are
> **not** listed here — they are baseline-offset internalizations in **M1** and
> **M2** and must already be `internal` before M11c starts.

| Full name to make `internal` | Source file |
|------------------------------|-------------|
| `Zaide.Features.SourceControl.Infrastructure.GitMutationService` | `src/Features/SourceControl/Infrastructure/GitMutationService.cs` |
| `Zaide.Features.SourceControl.Application.SourceControlSnapshotOrchestrator` | `src/Features/SourceControl/Application/SourceControlSnapshotOrchestrator.cs` |
| `Zaide.Features.SourceControl.Application.SourceControlActionDeriver` | `src/Features/SourceControl/Application/SourceControlActionDeriver.cs` |
| `Zaide.Features.SourceControl.Application.SourceControlDiffTabService` | `src/Features/SourceControl/Application/SourceControlDiffTabService.cs` |
| `Zaide.Features.Terminal.Infrastructure.LinuxTerminalService` | `src/Features/Terminal/Infrastructure/LinuxTerminalService.cs` |

**Shrink:** exactly **−5**.

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~SourceControl\
|FullyQualifiedName~LinuxTerminalService\
|FullyQualifiedName~Terminal\
|FullyQualifiedName~ArchitectureVisibility\
|FullyQualifiedName~Architecture"
```

#### M11d — Agents + Settings infrastructure (exactly 3 types)

| Full name to make `internal` | Source file |
|------------------------------|-------------|
| `Zaide.Features.Agents.Infrastructure.AgentExecutionService` | `src/Features/Agents/Infrastructure/AgentExecutionService.cs` |
| `Zaide.Features.Settings.Infrastructure.SettingsService` | `src/Features/Settings/Infrastructure/SettingsService.cs` |
| `Zaide.Features.Settings.Infrastructure.FileSecretStore` | `src/Features/Settings/Infrastructure/FileSecretStore.cs` |

**Shrink:** exactly **−3**.

**Focused tests:**

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~AgentExecution\
|FullyQualifiedName~AgentRouter\
|FullyQualifiedName~SettingsService\
|FullyQualifiedName~SettingsCore\
|FullyQualifiedName~FileSecretStore\
|FullyQualifiedName~SecretStore\
|FullyQualifiedName~ArchitectureVisibility\
|FullyQualifiedName~Architecture"
```

**M11 series completion:** exactly **−26** public types from the ten+eight+five+three
lists (relative to the baseline at the start of each slice’s commit). Cumulative
target after M11d: prior count minus 26 for these lists. No discretionary swaps.

---

### M12 — Lifetime ownership map (V11)

| Item | Locked decision |
|------|-----------------|
| Artifact path | **`docs/refactor/refactor-6.3/LIFETIME_MAP.md` only** |
| Scope | Document **exactly the 67** production DI registrations in the post-M10 inventory below (no optional rows, no “if present”) |
| Code changes | **Docs only** — no production/test source edits. Code mismatch → **stop** and plan amendment; no pre-authorized follow-up slice |
| Vocabulary | Application, Workspace, Process, Projection, Editor session, Terminal session only |

**Derivation (fixed):** live pre-6.3 count = 65. M1 **removes** `EditorViewModel` Transient (−1). M1 **adds** `IEditorSessionFactory` (+1). M2 **adds** `IEditorReadOnlyTabService` (+1). M3 **replaces** session factory registration with `ITerminalServiceFactory` (same slot count). M10 **adds** `ISettingsPanelFactory` (+1). Result: **65 − 1 + 1 + 1 + 1 = 67**.

**Post-M10 registration inventory (exactly 67; fill every row in `LIFETIME_MAP.md`):**

| # | Registration (service key) | DI lifetime | Semantic lifetime | Owner / dispose trigger |
|--:|----------------------------|-------------|-------------------|-------------------------|
| 1 | `Workspace` | Singleton | | |
| 2 | `ICommandRegistry` → `CommandRegistry` | Singleton | | |
| 3 | `ISettingsService` → `SettingsService` | Singleton | | |
| 4 | `ISettingsPanelFactory` → `SettingsPanelFactory` | Singleton | | |
| 5 | `ISecretStore` → `FileSecretStore` | Singleton | | |
| 6 | `StatusBarViewModel` | Singleton | | |
| 7 | `IFileService` → `FileService` | Singleton | | |
| 8 | `IEditorSessionFactory` → `EditorSessionFactory` | Singleton | | |
| 9 | `IEditorReadOnlyTabService` → `EditorReadOnlyTabService` | Singleton | | |
| 10 | `EditorSearchViewModel` | Singleton | | |
| 11 | `EditorTabViewModel` | Singleton | | |
| 12 | `EditorLanguageInputViewModel` | Singleton | | |
| 13 | `ITerminalServiceFactory` → `LinuxTerminalServiceFactory` | Singleton | | |
| 14 | `ITerminalHost` → `TerminalHost` | Singleton | | |
| 15 | `IAgentPanelHost` → `AgentPanelHost` | Singleton | | |
| 16 | `IAgentExecutionService` → `AgentExecutionService` | Singleton | | |
| 17 | `IAgentExecutionCoordinator` → `AgentExecutionCoordinator` | Singleton | | |
| 18 | `MentionParser` | Singleton | | |
| 19 | `IAgentRouter` → `AgentRouter` | Singleton | | |
| 20 | `HttpClient` | Singleton | | |
| 21 | `IFileTreeService` → `FileTreeService` | Singleton | | |
| 22 | `IScheduler` → AvaloniaScheduler | Singleton | | |
| 23 | `FileTreeViewModel` | Singleton | | |
| 24 | `MainWindowViewModel` | Singleton | | |
| 25 | `CommandPaletteViewModel` | Singleton | | |
| 26 | `TownhallState` | Singleton | | |
| 27 | `TownhallViewModel` | Singleton | | |
| 28 | `SourceControlViewModel` | Singleton | | |
| 29 | `IGitRepositoryService` → `GitRepositoryService` | Singleton | | |
| 30 | `ISourceControlSnapshotOrchestrator` → orchestrator | Singleton | | |
| 31 | `IFileDiffService` → `FileDiffService` | Singleton | | |
| 32 | `ISourceControlDiffTabService` → `SourceControlDiffTabService` | Singleton | | |
| 33 | `IGitMutationService` → `GitMutationService` | Singleton | | |
| 34 | `IProjectFileSystem` → `FileSystemProjectFileSystem` | Singleton | | |
| 35 | `IProjectDiscovery` → `ProjectDiscovery` | Singleton | | |
| 36 | `IProjectContextService` → `ProjectContextService` | Singleton | | |
| 37 | `IProjectOperationGate` → `ProjectOperationGate` | Singleton | | |
| 38 | `IProjectDebugTargetResolver` → `ProjectDebugTargetResolver` | Singleton | | |
| 39 | `IProjectDebugLaunchService` → `ProjectDebugLaunchService` | Singleton | | |
| 40 | `IManagedProcessRunner` → `ManagedProcessRunner` | Singleton | | |
| 41 | `IProjectWorkflowService` → `ProjectWorkflowService` | Singleton | | |
| 42 | `IProjectOutputService` → `ProjectOutputService` | Singleton | | |
| 43 | `ProjectWorkflowViewModel` | Singleton | | |
| 44 | `IBuildDiagnosticsService` → `BuildDiagnosticsService` | Singleton | | |
| 45 | `ITestResultsService` → `TestResultsService` | Singleton | | |
| 46 | `TestResultsViewModel` | Singleton | | |
| 47 | `ProblemsViewModel` | Singleton | | |
| 48 | `ILanguageServerBinaryLocator` → `LanguageServerBinaryLocator` | Singleton | | |
| 49 | `ILanguageServerSessionFactory` → `CsharpLsSessionFactory` | Singleton | | |
| 50 | `ILanguageSessionService` → `LanguageSessionService` | Singleton | | |
| 51 | `ILanguageDocumentBridge` → `LanguageDocumentBridge` | Singleton | | |
| 52 | `ILanguageDiagnosticsService` → `LanguageDiagnosticsService` | Singleton | | |
| 53 | `ILanguageCompletionService` → `LanguageCompletionService` | Singleton | | |
| 54 | `ILanguageHoverService` → `LanguageHoverService` | Singleton | | |
| 55 | `ILanguageNavigationService` → `LanguageNavigationService` | Singleton | | |
| 56 | `ILanguageSymbolService` → `LanguageSymbolService` | Singleton | | |
| 57 | `ILanguageFormattingService` → `LanguageFormattingService` | Singleton | | |
| 58 | `IDebugAdapterLocator` → `DebugAdapterLocator` | Singleton | | |
| 59 | `IDebugAdapterSessionFactory` → `DebugAdapterSessionFactory` | Singleton | | |
| 60 | `DebugSessionTimeoutPolicy` | Singleton | | |
| 61 | `IDebugSessionService` → `DebugSessionService` | Singleton | | |
| 62 | `IBreakpointService` → `BreakpointService` | Singleton | | |
| 63 | `DebugSessionViewModel` | Singleton | | |
| 64 | `DebugStackProjectionViewModel` | Singleton | | |
| 65 | `DebugCurrentLocationViewModel` | Singleton | | |
| 66 | `DebugPanelViewModel` | Singleton | | |
| 67 | `EditorBreakpointViewModel` | Singleton | | |

**Not registered after M1:** `EditorViewModel` (constructed only via `IEditorSessionFactory` / editor gateway).

**Files (exact inventory for M12):**

| Path | Action |
|------|--------|
| `docs/refactor/refactor-6.3/LIFETIME_MAP.md` | **Create** — 67 rows fully filled |
| `docs/refactor/refactor-6.3/IMPLEMENTATION_PLAN.md` | **Edit** — mark V11 / M12 complete in status only |

**Completion:** all 67 rows filled; production tree registration count matches 67;
no production diffs; run:

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter "FullyQualifiedName~Architecture"
dotnet test Zaide.slnx --no-build
```

---

### M13 — Refactor 6.3 closeout

| | |
|--|--|
| **Proof** | Every owned V0x row is cleared **or** listed as deliberate residual with FindingId |
| **Allowlist residual (exact)** | Only `R61-AL-LOC-Program` and `R61-AL-LOC-App` may remain (composition-boundary residual from M7). All NS FindingIds and non-composition LOC FindingIds must be gone |
| **Docs** | Update `CONVENTIONS.md` / `OVERVIEW.md` / `V3.md` next-step; this plan status → accepted closed |
| **Commands** | Full shared sequential gate; record pass counts in plan closeout section |

**Does not authorize** Refactor 7/8 or Phase 14.

---

## M0 exit conditions

- [x] Live post-6.2 metrics recorded and re-verified against commands in this
      document.
- [x] Every R61 finding owned by 6.3 is mapped to a **locked** design decision
      and every implementation milestone M1–M13 has an **exact** production/test
      file inventory with **no** alternative-path / “if needed” / “or module”
      wording (including M2 request location, M7 residual FindingIds, M10
      post-M6/M7 call sites, M12 fixed 67-row inventory).
- [x] V09 residual after M7 is explicitly the composition-boundary limitation
      (`CompositionRoot.Services` + `R61-AL-LOC-Program` + `R61-AL-LOC-App`), not
      described as full clearance.
- [x] Deferred V15–V20 / LT01–LT03 remain explicit.
- [x] Allowlist mutation policy is single and non-contradictory.
- [x] Every implementation milestone has exact focused test **filters**, manual
      steps where applicable, and completion conditions.
- [x] M12 artifact path is fixed (`LIFETIME_MAP.md`) with exactly **67** rows;
      no pre-authorized unnamed follow-up slices.
- [x] Architecture tests green (21 passed at draft time).
- [x] Human accepts this M0 plan after re-audit (2026-07-18).
- [ ] Human authorizes the first implementation milestone (**M1** only).

*M0 planning gate accepted. Do not start M1 without a separate explicit start.*

---

## Non-goals

- Product features, harness/ACP, Phase 14
- Refactor 7 domain; Refactor 8 visual extraction
- Multi-assembly split; `Infrastructure/` / `UI/Shared` roots
- Conversation / AgentSession / Run scopes
- Silent Townhall attribution “fixes”
- Unrelated package upgrades

---

## Dependencies

| Depends on | Why |
|------------|-----|
| Refactor 6.1 closed | Rules, allowlist IDs, lifetime vocabulary |
| Refactor 6.2 closed | Feature-first paths |
| Human M0 acceptance | Gate before M1 |
| M1 before M2 | Shared editor factory |

---

## Rollback plan

| Scope | Rollback |
|-------|----------|
| M0 docs | Delete/revert docs under `docs/refactor/refactor-6.3/` and status pointer edits |
| Each implementation milestone | One commit preferred; revert commit; allowlist/baseline/production together |
| M6 / M9 / M11 slices | Revert reverse-order within the series before the next series |

---

## Limitations (by design)

- ReactiveUI `UseReactiveUIWithMicrosoftDependencyResolver` may force a residual
  composition-root provider assignment (M7).
- Desktop Exit may force bounded sync-over-async in M8.
- `MainWindow.axaml.cs` stays large until Refactor 8.
- AgentRouter Application→Presentation remains until a future amendment.

---

## Exact next step

1. **M1–M5 complete** as previously recorded. **M6a complete** at `c59ad7b`
   (AppCore DI registration module / `AddZaideAppCore`). **M6b complete** at
   `43b8e85` (Settings DI registration module / `AddZaideSettings`). **M6c
   complete** at `1ad3625` (Workspace DI registration module /
   `AddZaideWorkspace`). **M6d complete** at `234a38f` (Editor DI registration
   module / `AddZaideEditor`). **M6e complete at `8ab50c0`** (Terminal DI
   registration module / `AddZaideTerminal`; closeout `d85a83b`). **M6f
   complete at `cd809d2`** (Agents DI registration module / `AddZaideAgents`;
   closeout `8144aba`). **M6g complete at `1f18e49`** (Townhall DI registration
   module / `AddZaideTownhall`; closeout `8624ab2`). **M6h implemented and
   staged pending review** (SourceControl DI registration module /
   `AddZaideSourceControl`; proposed message
   `refactor-6.3: M6h SourceControl DI module`). M6a–M6h are individually
   completed slices; the whole M6 series is **not** complete.
2. **Next eligible slice:** authorize **M6i only** (§ M6i — ProjectSystem
   registration module: `ProjectSystemServiceCollectionExtensions.cs` /
   `AddZaideProjectSystem`) when ready. M6i production implementation has
   **not** started and requires a separate explicit authorization.
3. Do not start M6i–M6k, M7+, Refactor 7/8, or Phase 14 without separate
   authorization. Completing M6h does **not** authorize the rest of M6.
4. **M6i–M6k** remain unauthorized. Completing M6h does not authorize later M6
   slices; each slice requires its own explicit authorization.

---

*Last updated: 2026-07-18 (M1–M5 and M6a–M6g complete; M6h SourceControl implemented and staged pending review; automated verification green: build clean 0 warnings / 0 errors, focused 66/66, Architecture 21/21, full suite 2246/2246, git diff checks clean; manual verification not required; public 346 / internal 59 / total 405 / prod C# 367 / App C# 28; eight internal Registration modules; M6i ProjectSystem next eligible and awaiting separate authorization; M6i–M6k unauthorized; whole M6 series not complete)*
