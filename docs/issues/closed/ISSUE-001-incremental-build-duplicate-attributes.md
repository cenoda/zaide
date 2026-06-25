# ISSUE-001: Incremental build fails with duplicate assembly attributes

**Label:** BUG
**Status:** closed
**Priority:** critical
**Related:** Phase 0 preparation, .NET 10 upgrade

## Description

After a successful first build (`dotnet build`), the second consecutive build
always fails with CS0579 "Duplicate attribute" errors. The error targets
auto-generated files in `obj/Debug/net10.0/`:

- `Zaide.AssemblyInfo.cs` — generates Company, Configuration, FileVersion, etc.
- `.NETCoreApp,Version=v10.0.AssemblyAttributes.cs` — generates TargetFrameworkAttribute

Both files are valid and contain different attributes. The compiler treats them
as duplicate sources of the same attributes on incremental builds.

## Steps to Reproduce

1. `rm -rf bin/ obj/ tests/Zaide.Tests/bin/ tests/Zaide.Tests/obj/`
2. `dotnet build Zaide.sln` → **succeeds** ✅
3. `dotnet build Zaide.sln` → **fails** ❌ (8x CS0579 errors)

**Expected behavior:** Incremental build succeeds without errors.
**Actual behavior:** Second build always fails. `dotnet build` after that also fails.
Only `rm -rf obj/` + rebuild resolves it temporarily.

## Debug Log

### Attempt 1
- **Hypothesis:** Stale `.NET 9.0` obj artifacts from pre-upgrade builds causing conflict.
- **Action:** `rm -rf bin/ obj/`, then `dotnet clean`, then rebuild.
- **Result:** First build succeeds, second fails again.
- **Error / Output:** Same 8x CS0579 duplicate attribute errors.

### Attempt 2
- **Hypothesis:** `TargetFramework` in `Directory.Build.props` causes MSBuild to
  process the property twice (once from props, once from SDK defaults).
- **Action:** Moved `TargetFramework` from `Directory.Build.props` into each `.csproj` explicitly.
- **Result:** Build 1 succeeds, build 2 still fails.
- **Error / Output:** Same errors. `TargetFramework` placement was not the root cause.

### Attempt 3
- **Hypothesis:** `ManagePackageVersionsCentrally` duplicated between
  `Directory.Build.props` and `Directory.Packages.props` causes NuGet to
  re-process packages on incremental build, triggering attribute regeneration.
- **Action:** Removed `ManagePackageVersionsCentrally` from `Directory.Build.props`
  (kept only in `Directory.Packages.props` where it belongs per CPM convention).
- **Result:** Build 1 succeeds, build 2 still fails.
- **Error / Output:** Same errors. CPM duplication was not the root cause.

### Attempt 4
- **Hypothesis:** Explicit `<GenerateAssemblyInfo>true</GenerateAssemblyInfo>` and
  `<GenerateTargetFrameworkAttribute>true</GenerateTargetFrameworkAttribute>` in
  `Directory.Build.props` causes MSBuild to generate these files via TWO paths on
  incremental builds — the explicit setting AND the SDK default — producing
  duplicate compilation units.
- **Action:** Inspected `Directory.Build.props` — these settings were **not present**.
  Ran `dotnet build Zaide.sln -v:d` and read the actual `csc` invocation.
- **Result:** Disproven. Found the real cause (see Attempt 5).
- **Error / Output:** N/A

### Attempt 5 (root cause found)
- **Hypothesis:** The main project globs the test project's generated files.
- **Action:** Read the verbose `csc` command line for `Zaide.csproj`. It compiles:
  ```
  App.axaml.cs MainWindow.axaml.cs Program.cs
  tests/Zaide.Tests/obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
  tests/Zaide.Tests/obj/Debug/net10.0/Zaide.Tests.AssemblyInfo.cs
  obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.cs
  obj/Debug/net10.0/Zaide.AssemblyInfo.cs
  ```
  The main project is compiling **both** its own AND the test project's generated
  AssemblyInfo files.
- **Result:** Confirmed. `Zaide.csproj` lives at the repo root; the SDK's default
  `**/*.cs` glob recursively includes `tests/Zaide.Tests/obj/`. On a clean build the
  test `obj/` is empty so the glob misses it; on the second build the generated
  `AssemblyInfo.cs` exists and gets pulled in → duplicate attributes.
- **Error / Output:** 8x CS0579 (the same errors, now explained).

## Current State (resolved — structurally fixed)

The main project was moved from the repo root to `src/Zaide.csproj`. The
`DefaultItemExcludes` hack was removed — it is no longer needed since `tests/`
is now a sibling directory, not a child.

Layout:
```
Zaide/
├── src/Zaide.csproj      ← main project (moved from root)
├── tests/Zaide.Tests/
├── Zaide.slnx
└── Directory.Build.props
```

Three consecutive `dotnet build Zaide.slnx` runs succeed (0 errors).
`dotnet test Zaide.slnx` builds and runs cleanly.

## Resolution

- **Root cause:** `Zaide.csproj` sits at the repository root, so the .NET SDK's
  default `**/*.cs` compile glob recursively pulled in the test project's generated
  `obj/` files (`Zaide.Tests.AssemblyInfo.cs` and `.NETCoreApp,...AssemblyAttributes.cs`).
  This also meant test source files were being silently compiled into the main
  assembly. The earlier hypotheses (TFM placement, CPM duplication, GenerateAssemblyInfo)
  were all wrong — none of those settings were the cause.
- **Fix (initial):** Added `DefaultItemExcludes` to exclude `tests/**` (temporary hack).
- **Fix (structural):** Moved main project to `src/Zaide.csproj` so `tests/` is a
  sibling directory. The `.slnx` format was also adopted as the solution file format.
  The `DefaultItemExcludes` hack was removed.
- **Key rule:** Never place `.csproj` at repo root when there are subdirectory projects.
  Use `src/` layout from the start.
- **Closed date:** 2025-06-25
