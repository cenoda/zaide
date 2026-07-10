# Phase 7.1 — M0: Seam Decision & Compatibility Proof

**Status: Locked (2026-07-09).**

M0 locks the Phase 7.1 seam before any implementation. No later-milestone
work (UI rewiring, diff rendering, stage/unstage, commit) has started. This
document is the source of truth for the M0 acceptance criteria.

---

## 1. Git-service interface shape (locked)

A single narrow **read-only** service seam, named `IGitRepositoryService`,
lives in `src/Services/`. It answers the questions later phases need without
owning UI or mutation logic.

```csharp
namespace Zaide.Services;

public interface IGitRepositoryService
{
    // Discover the repository root from a starting path.
    // Returns null when the path is not inside a git repository.
    RepositoryDiscoveryResult Discover(string startingPath);

    // Read branch/HEAD + working-tree status for an already-discovered repo.
    // Throws only on unexpected IO/permissions failure, NOT on "not a repo".
    RepositoryStatusSnapshot ReadStatus(string repositoryRoot);
}
```

Decision notes:
- One interface, two methods. Discovery is separated from reading so a
  non-repo result never forces a status read.
- Methods take an explicit path; the service is stateless and DI-registered
  as `Singleton`. It does not reach into `Workspace` directly.
- Read-only by contract for all of Phase 7.1. Stage/unstage/commit are
  explicitly out of scope and are not on this interface.

## 2. Repository-root discovery rule (locked)

- Entry point is `Repository.Discover(startingPath)` (LibGit2Sharp). This walks
  **upward** from `startingPath` until it finds a `.git` directory/file.
- The service returns the discovered root (normalized, ending in `.git/`) in
  `RepositoryDiscoveryResult.RepositoryRoot`.
- If `Repository.Discover` returns `null`/empty, the service returns a
  `NotFound` result. No exception is thrown for this case.
- POC confirmed real behavior: `Repository.Discover("/home/cenoda/zaide")`
  returned `/home/cenoda/zaide/.git/`.

## 3. How the app exposes the real workspace path (locked)

`Workspace` currently exposes only `ProjectName` (derived from a folder name);
it does **not** retain the full opened folder path. For repository discovery to
work truthfully, the real path must be available.

Decision:
- Add a `WorkspacePath` property (`string?`, nullable) to `src/Models/Workspace`
  alongside the existing `ProjectName`. `SetProjectFromPath(string?)` will store
  the full path (not just the derived name).
- The source control refresh path obtains the starting path from
  `Workspace.WorkspacePath` (falling back to the process working directory if
  unset). This keeps the git service ignorant of `Workspace` internals — the
  caller passes the path in.
- This is the minimal seam; no file-tree coupling or watcher is introduced in
  M0.

## 4. "Not a repo" result shape (locked)

```csharp
namespace Zaide.Services;

public sealed class RepositoryDiscoveryResult
{
    public bool IsRepository { get; init; }
    public string? RepositoryRoot { get; init; }   // null when IsRepository == false
    public string StartingPath { get; init; }

    public static RepositoryDiscoveryResult NotFound(string startingPath) =>
        new() { IsRepository = false, StartingPath = startingPath };

    public static RepositoryDiscoveryResult Found(string startingPath, string root) =>
        new() { IsRepository = true, StartingPath = startingPath, RepositoryRoot = root };
}
```

Consumers (ViewModel layer in M3) treat `IsRepository == false` as the truthful
non-repo state: an empty/disabled Source Control surface, no seeded branches or
changes. The existing `SourceControlState` demo data is **not** used as the
source of truth for the read path.

## 5. Minimal branch / status model direction (locked)

Reuse and extend the existing `src/Models` types rather than creating a
parallel set:

- `GitBranch` (exists): keep `Name` + `IsCurrent`. The service populates
  `IsCurrent` from `Branch.IsCurrentRepositoryHead`. For detached HEAD, the
  consumer surfaces the commit SHA instead of a branch name; `GitBranch` may
  later carry an optional `IsDetached`/`DetachedSha`, but M0 only locks the
  direction — no model edit is required yet.
- `FileChange` (exists) + `GitChangeType` (exists): the service maps
  `FileStatus` (`Added`/`Modified`/`Deleted`) onto `GitChangeType`. Staged vs
  unstaged split (later phases) maps to `FileChange.IsStaged`. M0 locks only
  the mapping direction; the service is not added until M1.
- A new snapshot container, `RepositoryStatusSnapshot`, is the return shape of
  `ReadStatus` and will hold: `CurrentBranchName` (or detached SHA),
  `IsDetachedHead`, `IReadOnlyList<GitBranch> Branches`, and
  `IReadOnlyList<FileChange> Changes`. This is the passive container later
  phases consume — it replaces the role of `SourceControlState` as source of
  truth, though `SourceControlState` may remain temporarily as a holder until
  M2 narrows it.

## 6. Library / API compatibility proof (recorded)

**Verified 2026-07-09 against the live target framework.**

| Item | Value |
|------|-------|
| Target framework | `net10.0` |
| SDK | `10.0.108` (from `global.json`) |
| Chosen library | **LibGit2Sharp** 0.30.0 |
| Proof location | Temporary POC project (not committed to repo) |
| Result | **Viable** — built and ran on net10.0; all required reads succeeded |

POC exercised (against the real `/home/cenoda/zaide` repo):
- `Repository.Discover` → `/home/cenoda/zaide/.git/`
- `repo.Head.FriendlyName` → `master`; `repo.Info.IsHeadDetached` → `false`
- `repo.Branches` enumeration with `IsCurrentRepositoryHead`
- `repo.RetrieveStatus()` → per-file `FileStatus` + `FilePath`

`DiffPlex` remains the documented choice for diff rendering (Phase 7.x later);
it is **not** needed for the read seam and is out of scope for 7.1.

### Install notes for M1
- Add `<PackageVersion Include="LibGit2Sharp" Version="0.30.0" />` to
  `Directory.Packages.props`.
- Add `<PackageReference Include="LibGit2Sharp" />` to `src/Zaide.csproj`.
- POC used a throwaway `/tmp` project; the real project is **not** modified in
  M0 (no later-milestone work started).

---

## M0 acceptance — verification

- [x] Seam design locked (`IGitRepositoryService` + discovery/status split)
- [x] Repository-root discovery rule locked (`Repository.Discover` upward walk)
- [x] Real workspace-path exposure locked (`Workspace.WorkspacePath`)
- [x] "Not a repo" result shape locked (`RepositoryDiscoveryResult.NotFound`)
- [x] Minimal branch/status model direction locked (reuse `GitBranch`/`FileChange`, new `RepositoryStatusSnapshot`)
- [x] Compatibility check recorded (LibGit2Sharp 0.30.0 on net10.0 — viable)
- [x] No later-milestone work started (UI rewiring, diff, stage/commit untouched)

## Re-checked live files (M0 pre-implementation verification)

- `src/ViewModels/SourceControlViewModel.cs` — still consumes seeded
  `SourceControlState` demo data; no git seam. Confirmed unchanged.
- `src/Models/SourceControlState.cs` — still seeds fake branches/changes.
  Confirmed unchanged; becomes passive snapshot in M2.
- `src/Models/FileChange.cs` — `GitChangeType` (Added/Modified/Deleted) +
  `IsStaged` exist and are sufficient for the status mapping direction.
- `src/Models/GitBranch.cs` — `Name` + `IsCurrent` exist; sufficient for M0.
- `src/Program.cs` — DI registration present; no git service registered yet.
- `src/Models/Workspace.cs` — only `ProjectName`; `WorkspacePath` to be added
  in M1 (decision locked here).
- No real git service seam exists in live code (grep for `LibGit2`,
  `Repository`, `IGit`, `GitService`, `DiffPlex` across `src/**/*.cs` → 0
  results). Confirmed.