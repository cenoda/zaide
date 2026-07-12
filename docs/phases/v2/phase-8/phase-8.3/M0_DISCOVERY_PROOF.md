# Phase 8.3 M0 — Discovery Proof

**Date:** 2026-07-12
**Agent session:** Phase 8.3 M0 entry gate

---

## 1. Target Framework

Confirmed via `src/Zaide.csproj` and `tests/Zaide.Tests/Zaide.Tests.csproj`:

```
<TargetFramework>net10.0</TargetFramework>
```

---

## 2. Exact Planned Production Files

The following files will be created in M1 during production implementation:

| # | File | Purpose |
|---|---|---|
| 1 | `src/Services/IProjectFileSystem.cs` | Seam wrapping `Directory.EnumerateFiles` |
| 2 | `src/Services/FileSystemProjectFileSystem.cs` | Production implementation calling `Directory.EnumerateFiles(root, "*", TopDirectoryOnly)` |
| 3 | `src/Services/IProjectDiscovery.cs` | `Task<ProjectDiscoveryResult> DiscoverAsync(string, CancellationToken)` |
| 4 | `src/Services/ProjectDiscovery.cs` | Extension classification, ordinal sorting, failure catching |
| 5 | `src/Services/ProjectDiscoveryResult.cs` | `sealed record` with `SupportedCandidates`, `UnsupportedFiles`, `Failure?` |
| 6 | `src/Services/ProjectDiscoveryFailure.cs` | `sealed record` with `Kind` and `Message` |
| 7 | `src/Services/ProjectCandidate.cs` | `sealed record` with `FilePath`, `DisplayName`, `ProjectKind` |
| 8 | `src/Services/ProjectKind.cs` | `enum { Solution, SolutionX, CSharpProject }` |
| 9 | `src/Services/ProjectContext.cs` | `sealed record` with state, root, candidates, selection, unsupported, error |
| 10 | `src/Services/ProjectContextState.cs` | `enum` with `Unloaded`, `Loading`, `NoProject`, `Unsupported`, `SingleProject`, `Ambiguous`, `Selected`, `Failed` |
| 11 | `src/Services/IProjectContextService.cs` | `interface` with `Current`, `WhenChanged`, `LoadAsync`, `ReloadAsync`, `UnloadAsync`, `SelectProject` |
| 12 | `src/Services/ProjectContextService.cs` | Singleton lifecycle implementation |

### Planned test file

| # | File | Purpose |
|---|---|---|
| 13 | `tests/Zaide.Tests/Services/Phase83M0DiscoveryProofTests.cs` | M0 proof — created in this milestone |

---

## 3. Concrete Adapter Approach

### 3.1 `IProjectFileSystem` seam

```csharp
// src/Services/IProjectFileSystem.cs  (planned M1)
public interface IProjectFileSystem
{
    string[] EnumerateFiles(string directory);
}
```

Production implementation:

```csharp
// src/Services/FileSystemProjectFileSystem.cs  (planned M1)
public sealed class FileSystemProjectFileSystem : IProjectFileSystem
{
    public string[] EnumerateFiles(string directory)
        => Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                    .ToArray();
}
```

The interface exists solely to allow test injection of a deterministic fake. The
production implementation calls the real file system with `TopDirectoryOnly` —
it does not recurse into subdirectories because project files always live at
workspace root.

### 3.2 `ProjectDiscovery` algorithm

```csharp
// src/Services/ProjectDiscovery.cs  (planned M1)
public sealed class ProjectDiscovery : IProjectDiscovery
{
    private readonly IProjectFileSystem _fileSystem;

    public async Task<ProjectDiscoveryResult> DiscoverAsync(
        string workspaceRoot, CancellationToken cancellationToken)
    {
        string[] files;
        try
        {
            files = _fileSystem.EnumerateFiles(workspaceRoot);
        }
        catch (DirectoryNotFoundException ex)
        {
            return new ProjectDiscoveryResult([], [], new ProjectDiscoveryFailure(
                ProjectDiscoveryFailureKind.NotFound, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ProjectDiscoveryResult([], [], new ProjectDiscoveryFailure(
                ProjectDiscoveryFailureKind.Unauthorized, ex.Message));
        }
        catch (IOException ex)
        {
            return new ProjectDiscoveryResult([], [], new ProjectDiscoveryFailure(
                ProjectDiscoveryFailureKind.Io, ex.Message));
        }

        // Normalise to absolute, sort by ordinal full path
        var sorted = files.Select(Path.GetFullPath)
                          .OrderBy(f => f, StringComparer.Ordinal)
                          .ToArray();

        var supported = new List<ProjectCandidate>();
        var unsupported = new List<string>();

        foreach (var path in sorted)
        {
            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext))
                continue;

            var extLower = ext.ToLowerInvariant();

            if (extLower == ".sln")
                supported.Add(new ProjectCandidate(path, Path.GetFileNameWithoutExtension(path), ProjectKind.Solution));
            else if (extLower == ".slnx")
                supported.Add(new ProjectCandidate(path, Path.GetFileNameWithoutExtension(path), ProjectKind.SolutionX));
            else if (extLower == ".csproj")
                supported.Add(new ProjectCandidate(path, Path.GetFileNameWithoutExtension(path), ProjectKind.CSharpProject));
            else if (KnownUnsupported.Contains(extLower))
                unsupported.Add(path);
            // else: unknown → ignored
        }

        return new ProjectDiscoveryResult(supported, unsupported, null);
    }

    private static readonly HashSet<string> KnownUnsupported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vbproj", ".fsproj", ".vcxproj", ".pyproj", ".dbproj", ".wixproj", ".shproj"
    };
}
```

### 3.3 Discovery result → service state mapping

When `ProjectDiscoveryResult.Failure` is null:

| Supported | Unsupported | Mapped state |
|-----------|-------------|--------------|
| empty | empty | `NoProject` |
| empty | non-empty | `Unsupported` |
| 1 candidate | any | `SingleProject` (auto-selected) |
| 2+ candidates | any | `Ambiguous` (no selection) |

When `Failure` is non-null, both collections must be empty. Maps to `Failed`
with `Failure.Message` as `ErrorMessage`.

`OperationCanceledException` is never caught by `ProjectDiscovery` — it
propagates to `ProjectContextService`, which handles it per the transition
contract (no `Failed` snapshot emitted).

---

## 4. Supported Extensions

| Extension | `ProjectKind` | Notes |
|-----------|---------------|-------|
| `.sln` | `Solution` | Case-insensitive match |
| `.slnx` | `SolutionX` | Case-insensitive match |
| `.csproj` | `CSharpProject` | Case-insensitive match |

## 5. Known Unsupported Extensions

Seven extensions recognised as known project-file formats (not supported by
Zaide in V2):

| Extension | Notes |
|-----------|-------|
| `.vbproj` | Visual Basic |
| `.fsproj` | F# |
| `.vcxproj` | C++ |
| `.pyproj` | Python |
| `.dbproj` | Database |
| `.wixproj` | WiX Toolset |
| `.shproj` | Shared project |

All matched case-insensitively. Unknown extensions and extensionless files are
silently ignored.

---

## 6. Test Coverage (Phase83M0DiscoveryProofTests)

13 tests, all passing:

| Test | What it proves |
|------|---------------|
| `MixedCaseSupportedExtensions_AreClassified` | `.SLN`, `.Sln`, `.sln`, `.SLNX`, `.slnx`, `.CSPROJ`, `.CsProj`, `.csproj` all classified |
| `AllSevenUnsupportedExtensions_AreClassified` | All seven known unsupported extensions produce `UnsupportedFiles` |
| `MixedSupportedAndUnsupported_ReturnsBoth` | Supported + unsupported in same root |
| `UnknownExtensions_AreIgnored` | `.md`, `.txt`, `.json`, `.png`, `.py`, extensionless, `.hidden` — all ignored |
| `OrdinalPathOrdering_IsDeterministic` | `A.csproj < B.csproj < a.csproj < c.csproj` (ordinal) |
| `EmptyRoot_MapsToNoProject` | No files → empty supported + empty unsupported |
| `UnsupportedOnly_MapsToUnsupported` | Only `.vbproj` → empty supported, non-empty unsupported |
| `MissingRoot_ReturnsNotFoundFailure` | Non-existent directory → `NotFound` failure |
| `Cancellation_RemainsDistinctFromFailure` | `OperationCanceledException` is not converted to failure |
| `SupportedCandidates_AreNormalisedAbsolutePaths` | `Path.IsPathFullyQualified` and match `Path.GetFullPath` |
| `MixedCaseUnsupportedExtensions_AreClassified` | `.VBPROJ`, `.FSPROJ`, `.VCXPROJ` classified |
| `SupportedCandidate_HasCorrectDisplayName` | `DisplayName` is `Path.GetFileNameWithoutExtension` |
| `SupportedCandidate_KindIsDerivedFromExtension` | `.csproj` → `CSharpProject`, `.sln` → `Solution`, `.slnx` → `SolutionX` |

---

## 7. Verification Commands

### 7.1 Filtered test command

```
$ dotnet test Zaide.slnx --filter FullyQualifiedName~Phase83M0DiscoveryProofTests

Restore complete (0.4s)
  Zaide net10.0 succeeded (0.1s) → src/bin/Debug/net10.0/Zaide.dll
  Zaide.Tests net10.0 succeeded (0.2s) → tests/Zaide.Tests/bin/Debug/net10.0/Zaide.Tests.dll
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v3.0.2+dd36e86129 (64-bit .NET 10.0.9)
[xUnit.net 00:00:00.05]   Discovering: Zaide.Tests
[xUnit.net 00:00:00.11]   Discovered:  Zaide.Tests
[xUnit.net 00:00:00.12]   Starting:    Zaide.Tests
[xUnit.net 00:00:00.16]   Finished:    Zaide.Tests
  Zaide.Tests test net10.0 succeeded (0.5s)

Test summary: total: 13, failed: 0, succeeded: 13, skipped: 0, duration: 0.5s
Build succeeded in 1.4s
```

**Result: 13 passed, 0 failed.** The `dotnet test Zaide.slnx` acceptance
condition defined in the milestone table requires `1 passed` at minimum. We
exceed that with 13 passing tests.

### 7.2 `git diff --check`

```
$ git diff --check
(no output)
```

No whitespace errors.

### 7.3 Production code untouched

```
$ git diff --stat
(no output — no tracked files modified)

$ git status
On branch master
Untracked files:
  tests/Zaide.Tests/Services/Phase83M0DiscoveryProofTests.cs
```

Only the test-side proof file is new. No production files were created or
modified. No DI wiring, no status-bar integration, no production abstractions.

---

## 8. Acceptance Decision

| Criterion | Status |
|-----------|--------|
| Target framework confirmed `net10.0` | ✅ |
| Planned production files listed (12 files) | ✅ |
| Planned test file listed | ✅ |
| Concrete adapter approach documented | ✅ |
| Supported/unsupported/unknown classification proven | ✅ |
| Ordinal path ordering proven | ✅ |
| Empty/unsupported collection mapping proven | ✅ |
| Expected filesystem failure representation proven | ✅ |
| Cancellation distinct from failure proven | ✅ |
| Filtered test command output recorded (13/13 passed) | ✅ |
| `git diff --check` has no output | ✅ |
| Production code untouched | ✅ |

**M0 is accepted.**

---

## 9. Blocker Assessment for M1

**No blockers.** The proof confirms:

1. The target framework, discovery algorithm, and contract boundaries are
   consistent with the live codebase.
2. The `InternalsVisibleTo` on `src/Zaide.csproj` already covers the test
   assembly.
3. All 13 contract-equivalent tests pass without any production infrastructure.

**M1 can proceed** with creating the 12 production files listed in §2 above,
implementing `IProjectFileSystem`, `FileSystemProjectFileSystem`,
`IProjectDiscovery`, `ProjectDiscovery`, `ProjectDiscoveryResult`,
`ProjectDiscoveryFailure`, `ProjectCandidate`, `ProjectKind`,
`ProjectContext`, `ProjectContextState`, `IProjectContextService`, and
`ProjectContextService`, and wiring discovery + DI.
