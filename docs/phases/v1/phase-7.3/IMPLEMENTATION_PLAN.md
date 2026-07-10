# Phase 7.3: Basic Diff View — Implementation Plan

## Pre-Implementation Verification

- [x] Phase 7.2 live Source Control wiring is in place: `SourceControlViewModel` loads
      live snapshots from `ISourceControlSnapshotOrchestrator`, `MainWindowViewModel.OpenFolderCommand`
      invokes `RefreshCommand` on workspace-open, and `SourceControlPanel` exposes a
      header refresh button bound to `RefreshCommand`. M1/M2 are complete; M3 manual
      walkthrough is deferred.
- [x] Confirm the existing `SourceControlPanel` change rows (`UnstagedChanges` / `StagedChanges`)
      are `ItemsControl` + `FuncDataTemplate<FileChange>` with no selection model, each row exposes
      a stage/unstage button but no row-click or diff trigger (lines 108, 239 of
      `SourceControlPanel.cs`).
- [x] Confirm `SourceControlViewModel.ApplyResult` clears and recreates both
      `ObservableCollection<FileChange>` on every refresh (line 166), discarding all
      object identity. No `SelectedFileChange` or `SelectedFilePath` property exists today.
- [x] Confirm `IGitRepositoryService` is intentionally status-only / read-only with
      no diff method. `LibGit2Sharp` is already in the project; `DiffPlex` is documented
      in `LIBRARIES.md` but **not yet added** to `.csproj`.
- [x] Confirm LibGit2Sharp's `Repository.Diff.Compare<Patch>()` API is available and
      can produce unified-diff text for a single file against HEAD vs index vs workdir.
      *(Proof-of-concept: committed at `tests/Zaide.Tests/Services/LibGit2SharpDiffProofOfConceptTests.cs`.
      Full results recorded in the Proof-of-Concept Results section below.)*

## Proof-of-Concept Results

**File:** `tests/Zaide.Tests/Services/LibGit2SharpDiffProofOfConceptTests.cs` (9 tests)

**Verdict:** LibGit2Sharp 0.30.0 is fully sufficient for Phase 7.3 diff production.
No DiffPlex dependency is needed at this stage.

### API Surface Findings

| Scenario | API Call | Result |
|----------|----------|--------|
| Unstaged modified file | `repo.Diff.Compare<Patch>(HEAD, DiffTargets.WorkingDirectory, [path])` | Unified diff with `diff --git` header, `--- a/` / `+++ b/`, and `@@` hunks. `Patch.Content` is the complete unified diff string. |
| Unstaged deleted file | Same as above | Correct deletion diff: `--- a/path` / `+++ /dev/null` with `-` lines. |
| Unstaged untracked file | Same as above | Full-file addition diff: `--- /dev/null` / `+++ b/path` with `+` lines. Works when an explicit path filter is passed. |
| Staged modified file | `repo.Diff.Compare<Patch>(HEAD, DiffTargets.Index, [path])` | Unified diff identical in structure to unstaged. |
| Staged new file | Same as above | Full-file addition diff (as above). |
| Staged deleted file | Same as above | Full deletion diff (as above). |
| No diff (unmodified) | Same as above | `Patch.Content` is empty string. |
| Multiple files | `repo.Diff.Compare<Patch>(HEAD, DiffTargets.WorkingDirectory)` (no path filter) | Combined unified diff with all modified files. |

### Caveats Discovered

1. **`PatchEntryChanges.Hunks` is NOT available in LibGit2Sharp 0.30.0.**
   The `PatchEntryChanges` type does not expose a `Hunks` enumerable.
   - `LinesAdded` / `LinesDeleted` work fine (int totals).
   - Per-hunk iteration is not needed for M1–M3 (we only need `Patch.Content`).
   - If per-hunk metadata is needed later, a `string.Split` on `@@` markers in the
     unified diff string is a straightforward workaround.

2. **Untracked files DO produce a diff when a path filter is provided.**
   This is actually convenient: the same API call works for both tracked and
   untracked files without special-casing.

3. **Binary detection is NOT directly on `Patch`.**
   The `Patch` type does not expose `IsBinaryComparison` at the top level.
   - `TreeChanges` (from `repo.Diff.Compare<TreeChanges>(...)`) can determine
     binary vs text per file.
   - For M1, the implementation should first check `TreeChanges` for the file's
     status, then request `Patch` only for text files. This avoids attempting
     to produce a binary diff.

## Planning Status

**Revised 2026-07-09 — audit tightened before implementation begins.**

All locked decisions (see below) are confirmed viable by the proof-of-concept.

The `IFileDiffService` seam design is verified against LibGit2Sharp's actual API.

This sub-phase adds the first diff surface after repository truth and live Source Control
status are already stable. The following decisions are **locked in this plan** and must
not be re-opened during implementation.

### 🎯 Locked Decisions (M0)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Selection trigger** | Single-click on a change row selects it and shows its diff. No separate button, no keyboard-combo, no right-click. The row highlights visually. | Simplest path; matches VS Code / GitHub Desktop mental model. |
| **Selection widget** | Switch `ItemsControl` → `ListBox`. `ListBox` has built-in `SelectedItem` / selection visual, two-way bindable via `SelectedItem="{Binding SelectedUnstagedChange}"` or similar. | Avoids reinventing selection highlighting, keyboard nav, and aria support. |
| **Selection survives refresh** | Yes. Track `SelectedFilePath` (string) in VM. After `ApplyResult` clears collections, walk the new items and re-select any file whose path matches. If the file no longer exists, clear the diff surface. | Prevents diff from disappearing on spurious refresh while keeping coherence. |
| **Diff seam owner** | New `IFileDiffService` in `src/Services/`. Not an extension of `IGitRepositoryService` (which stays read-only status), and not an extension of `ISourceControlSnapshotOrchestrator` (which stays refresh-only orchestration). | Single responsibility. Diff has different inputs (staged vs unstaged, binary handling) than refresh. |
| **Diff engine** | **LibGit2Sharp `Diff.Compare<Patch>()` only.** No DiffPlex in Phase 7.3. LibGit2Sharp already in the project and produces unified diff text directly from the repo objects. DiffPlex is deferred to a later phase when unsaved-editor diff or word-level highlighting is needed. | Avoids adding a dependency that is not yet needed. The proof-of-concept proves LibGit2Sharp is sufficient for this phase. |
| **Diff surface location** | Inline below the Source Control change lists, inside the existing scroll panel. Not a new shell region, not a flyout, not a modal. A collapsible/expandable section between the unstaged list and the staged list, or at the bottom of the scroll content. | Keeps the change list and its detail co-located. No shell layout changes. |
| **Binary / unsupported files** | `IFileDiffService.GetDiff` returns a `FileDiffResult` with `IsBinary = true` and `DiffText = null`. The view renders a "Binary file — diff not available" notice inline. | Honest to the user, no silent failure. |

## Goal

Allow the user to click a changed file in the Source Control list and see its unified
diff inline, without leaving the Source Control panel.

## Boundaries

Phase 7.3 adds minimal diff retrieval and rendering only. It does **not** add inline
hunk actions, history compare, side-by-side editor integration, or broad review tooling.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| **M0** ✅ | Lock all decisions above. Complete the LibGit2Sharp diff proof-of-concept (9 tests: init a repo, stage a file, modify/delete/create, call `Diff.Compare<Patch>()` for HEAD:index and HEAD:workdir, verify unified diff string). Record the result. | Proof-of-concept tests all pass (9/9) at `tests/Zaide.Tests/Services/LibGit2SharpDiffProofOfConceptTests.cs`. |
| **M1** ✅ | Add `IFileDiffService` seam + implementation. `GetDiff(repoRoot, FileChange)` returns `FileDiffResult` with unified diff text (or binary/unsupported marker). Staged files diff against HEAD:index; unstaged against HEAD:workdir. | Unit tests at `tests/Zaide.Tests/Services/FileDiffServiceTests.cs`: modified file returns diff text, new file returns full content as diff, deleted file returns deletion diff, binary file returns `IsBinary=true`, unknown file path returns null. |
| **M2** ✅ | Add `SelectedFileChange` / `SelectedFilePath` / `SelectedDiff` to `SourceControlViewModel`. Wire file-click → diff load. Persist selection by path across `ApplyResult`. Clear diff when file no longer exists. | ViewModel tests at `tests/Zaide.Tests/ViewModels/SourceControlViewModelTests.cs`: clicking a file loads a diff, refresh with same path reselects it, refresh with removed path clears it, binary file populates `IsBinary` state. |
| **M3** ✅ | Convert change lists from `ItemsControl` to `ListBox` in `SourceControlPanel`. Style selected row. Add diff rendering surface (a `TextBlock` / `ScrollViewer` showing the unified diff in monospace, or a "Binary file" fallback). Bind everything. | Build + tests; focused manual verification for selection, diff display, binary fallback, and refresh coherence. |

## Seam Design (Pre-Approved)

### `IFileDiffService` (new, `src/Services/`)

```csharp
public interface IFileDiffService
{
    /// <summary>
    /// Returns a unified diff for <paramref name="change"/> against the
    /// appropriate git tree (HEAD:index for staged, HEAD:workdir for unstaged).
    /// Returns null when the file path is not valid in the repository.
    /// Returns a result with <see cref="FileDiffResult.IsBinary"/> = true for
    /// binary files (no diff text).
    /// </summary>
    FileDiffResult? GetDiff(string repositoryRoot, FileChange change);
}

public sealed class FileDiffResult
{
    public string FilePath { get; init; } = string.Empty;
    public bool IsBinary { get; init; }
    public string? DiffText { get; init; } // null when IsBinary
    public int AddedLines { get; init; }
    public int DeletedLines { get; init; }
}
```

Implementation uses `LibGit2Sharp.Repository.Diff.Compare<Patch>()` with the
appropriate `TreeComparisonHandle` (HEAD vs index or HEAD vs workdir) filtered
to the single file path. Renders `Patch` content into unified diff string format.

### Changes to `SourceControlViewModel`

- New property: `FileChange? SelectedFileChange` (two-way for `ListBox.SelectedItem`)
- New property: `string? SelectedFilePath` (persisted across refresh)
- New property: `FileDiffResult? CurrentDiff` (reactive, consumed by view)
- New command: `ReactiveCommand<FileChange, Unit> SelectFileCommand`
- DI: inject `IFileDiffService`
- In `ApplyResult`, after rebuilding collections:
  1. Read `_selectedFilePath`
  2. Walk new `UnstagedChanges` + `StagedChanges` for a match
  3. If found, set `SelectedFileChange` and load diff via `IFileDiffService`
  4. If not found, clear `SelectedFileChange` and `CurrentDiff`

### Changes to `SourceControlPanel`

- `_unstagedList` / `_stagedList`: `ItemsControl` → `ListBox`
- Set `SelectionMode="Single"`, bind `SelectedItem` to `SelectedFileChange`
- Use existing `CreateChangeItemTemplate` as `ItemTemplate`
- Add diff surface: a `ScrollViewer` containing a monospace `TextBlock` bound to `CurrentDiff.DiffText` (visible when `CurrentDiff` is not null). Replace content with `"Binary file — diff not available"` when `CurrentDiff.IsBinary`.
- Placement: between unstaged list and staged header, or at the very bottom of the scroll content. Evaluate during implementation; prefer after staged list to avoid visual displacement during rapid file switching.

## Out of Scope

- Side-by-side compare editor
- Inline hunk staging
- Commit history / previous revisions
- Syntax-aware or semantic diffing (no syntax highlighting in the diff text)
- Review comments / annotations
- DiffPlex integration (deferred to later phase)

## Limitations (by design)

- Text files only for full diff content; binary files show an inline notice
- Large files may be slow (no streaming, but LibGit2Sharp handles this in native code)
- Diff loading is on-demand per file selection, not precomputed
- Unified diff is plain monospace text with no colorization (Phase 7.3); coloring the `+`/`-`/` ` lines can be added later without changing the seam

## Exit Conditions

- [x] A selected changed file surfaces a basic unified diff below the change lists
- [x] Binary / unsupported files show an inline "not available" message
- [x] Selection survives refresh (same path → diff stays; removed path → diff clears)
- [x] Empty / unknown file paths degrade gracefully (null → diff area hidden)
- [x] Diff generation is behind `IFileDiffService`, not in the view or ViewModel
- [x] `IGitRepositoryService` and `ISourceControlSnapshotOrchestrator` are unchanged
- [x] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [x] Tests pass: `dotnet test Zaide.slnx --no-build`

## Automated Verification (Phase 7.3 Closeout — 2026-07-09)

The following automated verification was performed as part of this closeout pass:

- `dotnet build Zaide.slnx --no-restore` — **passed with 0 warnings and 0 errors**
- `dotnet test Zaide.slnx --no-build` — **passed 777/777 (0 failed, 0 skipped)**

### Manual UI Verification

**Not performed in this closeout pass.** The diff surface (inline unified diff
rendering, selection highlighting, binary-file fallback, refresh coherence) was
exercised during M3 implementation via focused manual verification per the M3
milestone. No additional manual UI smoke test was repeated for this docs-only
closeout pass. A manual smoke pass for the full Phase 7.3 diff surface should be
performed before the Phase 7 overall exit condition is declared complete.

## Exact Next Step

After 7.3 is complete, move to `docs/phases/v1/phase-7.4/IMPLEMENTATION_PLAN.md`
for stage/unstage and local commit flow. DiffPlex evaluation is deferred until
unsaved-editor diffing or word-level highlighting is scoped.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
