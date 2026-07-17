# Refactor 6.1 M0 Architecture Baseline

## Status

**Evidence date:** 2026-07-16

**Scope:** Read-only inspection of the live checkout plus the two requested
Refactor 6.1 documentation files. No production/test structure, namespace,
dependency, DI registration, visibility, lifetime, or behavior changed.

This is evidence for review of
[`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md), not authorization for M1,
architecture-test implementation, Refactor 6.2, or Refactor 6.3.

## Evidence method

The inventory used tracked source files where file organization mattered,
source scans where dependency/resolution sites mattered, compiled metadata
where C# visibility semantics mattered, and direct reads of composition and
shutdown owners.

Key reproducible commands:

```bash
git ls-files 'src/*.cs' 'src/**/*.cs'
git ls-files 'tests/Zaide.Tests/*.cs' 'tests/Zaide.Tests/**/*.cs'
rg -n --glob '*.cs' 'using Zaide\.ViewModels|Zaide\.ViewModels\.' src/Services
rg -n --glob '*.cs' 'using Zaide\.Services|Zaide\.Services\.' src/Models
rg -n --glob '!**/bin/**' --glob '!**/obj/**' \
  '\bIServiceProvider\b|App\.Services|GetRequiredService<|GetService<' src
rg -n 'Add(Singleton|Transient|Scoped)' src/Program.cs
wc -l src/Program.cs src/App.axaml.cs src/MainWindow.axaml.cs \
  src/ViewModels/MainWindowViewModel.cs
```

## Project, assembly, folder, and namespace structure

`Zaide.slnx` contains exactly:

| Project | Role | Output/reference |
|---------|------|------------------|
| `src/Zaide.csproj` | Production Avalonia executable | One `net10.0` `WinExe` assembly named `Zaide` |
| `tests/Zaide.Tests/Zaide.Tests.csproj` | xUnit test project | References `src/Zaide.csproj`; production grants `InternalsVisibleTo="Zaide.Tests"` |

Workflow fixtures and Phase 10 proof/smoke tools exist elsewhere in the
repository but are not projects in `Zaide.slnx` and are not production modules.

### Production source

| Current folder | Tracked C# files | Current namespace declarations |
|----------------|-----------------:|--------------------------------|
| `src/` root | 3 | 3 `Zaide` |
| `src/Models` | 22 | 22 `Zaide.Models` |
| `src/Services` | 224 | 224 `Zaide.Services` |
| `src/Styles` | 2 | 2 `Zaide.Styles` |
| `src/ViewModels` | 53 | 53 `Zaide.ViewModels` |
| `src/Views` | 52 | 52 `Zaide.Views` |
| **Total** | **356** | **356 matching declarations** |

Tracked AXAML is `App.axaml`, `MainWindow.axaml`, `Styles/Icons.axaml`, and
`Views/UnsavedDialog.axaml`. Current folder and namespace organization match
the technical-layer convention, but not the target feature-first ownership
map.

The file counts above are not type counts. In particular, `src/Models` has 22
tracked C# files but compiles to 31 non-nested top-level `Zaide.Models` types
because several files contain related records/enums in addition to their main
type. The separate compiled-metadata table below is authoritative for type
visibility counts.

The approved target maps current `src/Styles/LayoutTokens.cs`,
`src/Styles/TextStyles.cs`, and `src/Styles/Icons.axaml` to
`src/UI/DesignSystem`; they are not `UI/Shared` content.

No separate Domain, Application, Infrastructure, Presentation, or feature
assembly exists. The product-level IDE/Agent language in the architecture doc
is not enforced as a code dependency boundary.

### Test source

| Current folder | Tracked C# files | Current namespace declarations |
|----------------|-----------------:|--------------------------------|
| `tests/Zaide.Tests/` root | 14 | 13 `Zaide.Tests`; one file contains no namespace declaration |
| `DI` | 6 | 6 `Zaide.Tests.DI` |
| `Integration` | 1 | 1 `Zaide.Tests.Integration` |
| `Models` | 5 | 5 `Zaide.Tests.Models` |
| `Services` | 79 | 79 `Zaide.Tests.Services` |
| `ViewModels` | 39 | 39 `Zaide.Tests.ViewModels` |
| `Views` | 26 | 26 `Zaide.Tests.Views` |
| **Total** | **170** | **169 namespace declarations** |

The test project mirrors technical layers rather than feature ownership.

## Production public/internal type count

### Counting rule

The count is compiled-metadata-based so partial declarations, records, and C#
default visibility are classified semantically rather than by a fragile text
regex.

1. Build `src/bin/Debug/net10.0/Zaide.dll` from the current checkout.
2. Load that assembly.
3. Include only non-nested types whose namespace is `Zaide` or begins
   `Zaide.`.
4. Exclude types marked `CompilerGeneratedAttribute`.
5. Count `Type.IsPublic` as public and `Type.IsNotPublic` as internal.
6. Nested helper types are intentionally excluded; this baseline governs the
   top-level module surface.

Run the following from the repository root using the installed
`dotnet-script` tool. `Path.GetFullPath` keeps the command independent of the
checkout's absolute location:

```bash
dotnet script eval 'using System; using System.IO; using System.Linq; using System.Reflection;
var a = Assembly.LoadFrom(Path.GetFullPath("src/bin/Debug/net10.0/Zaide.dll"));
var ts = a.GetTypes().Where(t => !t.IsNested && t.Namespace != null &&
    (t.Namespace == "Zaide" || t.Namespace.StartsWith("Zaide.")) &&
    t.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() == null)
    .ToArray();
Console.WriteLine($"total={ts.Length} public={ts.Count(t => t.IsPublic)} internal={ts.Count(t => t.IsNotPublic)}");
foreach (var g in ts.GroupBy(t => t.Namespace).OrderBy(g => g.Key))
    Console.WriteLine($"{g.Key}: total={g.Count()} public={g.Count(t => t.IsPublic)} internal={g.Count(t => t.IsNotPublic)}");'
```

Current result:

| Namespace | Total | Public | Internal |
|-----------|------:|-------:|---------:|
| `Zaide` | 3 | 2 | 1 |
| `Zaide.Models` | 31 | 31 | 0 |
| `Zaide.Services` | 231 | 214 | 17 |
| `Zaide.Styles` | 2 | 2 | 0 |
| `Zaide.ViewModels` | 72 | 58 | 14 |
| `Zaide.Views` | 54 | 41 | 13 |
| **Total** | **393** | **348** | **45** |

This is a baseline, not an endorsed public API. The test assembly can already
consume internal types through `InternalsVisibleTo`.

## Actual dependency directions

### Technical namespace scan

| Source | Current direct Zaide namespace dependencies |
|--------|---------------------------------------------|
| `Models` | `Services` |
| `Services` | `Models`, `ViewModels` |
| `ViewModels` | `Models`, `Services`, same-namespace ViewModels |
| `Views` | `Models`, `Services`, `Styles`, `ViewModels` |
| `Styles` | none |
| root App/Shell | Models, Services, Styles, ViewModels, Views plus framework composition APIs |

### Verified forbidden edges

| Source | Target/evidence | Why it is a real edge |
|--------|-----------------|-----------------------|
| `Services/ITerminalSessionFactory.cs` | `TerminalViewModel` | Public factory contract returns a presentation type. |
| `Services/TerminalSessionFactory.cs` | `LinuxTerminalService`, `TerminalViewModel` | Service factory creates the process owner and ViewModel as one unit. |
| `Services/MentionParser.cs` | `IAgentPanelHost` | Parsing reads mutable panel ViewModel state to resolve visible names. |
| `Services/SourceControlDiffTabService.cs` | `EditorTabViewModel`, `EditorViewModel` | Source Control application service directly opens and updates editor presentation state. |
| `Models/SourceControlState.cs` | `RepositoryStatusSnapshot` | A Models state bag consumes a Services snapshot type. |

No `Services -> Views` or `ViewModels -> Views` `using`/qualified reference was
found. Those current absences should become executable no-new-violation rules.

### Service locator and hidden resolution

Counting rule: a **resolution call expression** is one source occurrence of
`.GetRequiredService<...>`, `.GetService<...>`, or non-generic
`.GetService(...)`. A **provider-using type** is a production type containing
at least one such expression. Static `App.Services` assignment is reported
separately because assignment is global-locator debt but not a resolution call.

| Provider-using type | Call expressions | Boundary |
|---------------------|-----------------:|----------|
| `Program` | 3 | Allowed composition-root transient factory |
| `App` | 35 | App composition/startup/shutdown, but reached through static `App.Services` |
| `SourceControlDiffTabService` | 3 | Forbidden non-composition service locator |
| `EditorTabViewModel` | 3 | Forbidden ViewModel service locator |
| **Total** | **44** | **38 composition calls; 6 forbidden non-composition calls** |

The source therefore has four provider-using production types, two forbidden
non-composition locator owners, and one additional static global provider
property/assignment boundary. The owner count and call-expression count are
reported separately to avoid conflating them.

| Site | Resolution behavior | Classification |
|------|---------------------|----------------|
| `Program.BuildAvaloniaApp` | assigns the root provider to static mutable `App.Services` | Global composition debt |
| `App.OnFrameworkInitializationCompleted` | resolves shell, settings, command, debug, and language singletons through `App.Services` | Composition/service-locator debt |
| `App.DisposeServicesOnExit` | resolves shutdown participants through an `IServiceProvider` | Manual shutdown/service-locator debt |
| `Services/SourceControlDiffTabService` | stores `IServiceProvider`; resolves `IFileService`, optional settings, and optional formatting per diff tab | Forbidden application-service resolution |
| `ViewModels/EditorTabViewModel` | stores `IServiceProvider`; resolves the same editor dependencies per opened tab | Forbidden ViewModel resolution |
| `Program` transient editor factory | uses the provider inside the composition root | Allowed composition-root factory, but its consumers mostly bypass it |

No file under `src/Views` resolves through `IServiceProvider`. Exactly one
ViewModel type (`EditorTabViewModel`) does. No production code outside `App`
reads `App.Services`; the static property is nevertheless the startup and
shutdown global.

Reproduction command:

```bash
for file in \
  src/Program.cs \
  src/App.axaml.cs \
  src/Services/SourceControlDiffTabService.cs \
  src/ViewModels/EditorTabViewModel.cs
do
  printf '%s ' "$file"
  rg -o '\.GetRequiredService(<|\()|\.GetService(<|\()' "$file" | wc -l
done
```

## DI registrations and effective lifetimes

`Program.ConfigureServices` is 163 lines and contains:

- 64 explicit `AddSingleton` calls;
- one explicit `AddTransient` call for `EditorViewModel`;
- zero `AddScoped` calls;
- one `AddLogging` call, whose framework-added descriptors are deliberately
  not counted as Zaide's explicit registrations.

### Effective lifetime observations

| Current registration/construction | Effective ownership |
|-----------------------------------|---------------------|
| All 64 explicit singletons | Root application-container instance; no child scope exists. |
| `Workspace`, project context, breakpoints, repository/source-control projections | Registered for application lifetime while semantically resetting or switching with workspace state. |
| Workflow, managed process runner, language session, and debug session services | Application singleton owners/coordinators of changing process state. |
| Problems, Output, Test Results, debug, status, and other ViewModels | Mostly application singletons even when their semantic role is a projection. |
| `EditorViewModel` DI descriptor | Transient, but `EditorTabViewModel` and `SourceControlDiffTabService` construct instances directly with `new`; the container does not track those instances. |
| `TerminalSessionFactory` | Application singleton factory; each call manually creates one `LinuxTerminalService` and one `TerminalViewModel`. |
| `TerminalHost` | Application singleton and manual owner of all terminal-session ViewModels; tab close and host dispose call session dispose. |
| `MainWindow` | Constructed manually in `App`, not registered. |
| `SettingsViewModel` / `SettingsPanelView` | Constructed and disposed manually by `MainWindow` for the settings overlay. |

This proves that registration lifetime and semantic lifetime are currently
mixed. M0 does not choose new scopes or modify registrations.

## Shutdown and disposal ownership

`App.DisposeServicesOnExit` performs a synchronous fixed-order shutdown:

1. Resolve Output, Build Diagnostics, and Test Results projections early.
2. Dispose the debug session service.
3. Dispose Debug Panel, Current Location, Editor Breakpoint, and Debug Session
   ViewModel projections if they were constructed.
4. Dispose Project Workflow, then Output, Build Diagnostics, and Test Results.
5. Dispose Language Formatting, Navigation, Symbols, Completion, Hover,
   Diagnostics, Document Bridge, and Language Session.
6. Dispose Project Context, optional File Tree, and optional Terminal Host.

Additional manual owners exist:

- `TerminalHost.CloseTab` and `TerminalHost.Dispose` dispose terminal sessions;
- `TerminalViewModel.Dispose` disposes its `ITerminalService` process owner;
- `ProjectWorkflowService` disposes its process runner;
- language and debug session implementations kill/dispose their owned
  processes/sessions;
- `MainWindow` uses `FinalWindowCleanup` for Editor view and Terminal view-host
  cleanup and separately owns settings overlay disposal;
- `MainWindowViewModel.Activate` adds some projection ViewModels to a composite,
  while comments reserve other debug projection disposal for `App`.

The source contains no explicit root-provider disposal in `App`. Some
registered `IDisposable` services, including Settings, are not named in the
manual exit list. M0 does not infer whether the ReactiveUI integration performs
additional implicit provider disposal; the current explicit ownership is
therefore incomplete/ambiguous and belongs to Refactor 6.3 evidence.

## Large composition and orchestration surfaces

| Surface | Measured size | Verified pressure |
|---------|--------------:|-------------------|
| `Program.cs` | 163 lines | One 64-singleton/one-transient registration method for every feature. |
| `App.axaml.cs` | 135 lines | Static provider, eager feature resolution, manual window construction, ordered shutdown. |
| `MainWindowViewModel.cs` | 608 lines | 18 constructor parameters; 15 exposed feature owners/projections; cross-feature activation, subscriptions, save handoffs, panel modes, and agent/Townhall mirroring. |
| `MainWindow.axaml.cs` | 983 lines | Nine-service/ViewModel composition constructor; constructs the feature views, layout, bindings, settings overlay, focus, animation, and cleanup. |

Other large files exist (`DebugSessionService` 1,354 lines,
`TerminalRenderControl` 1,199, `EditorView` 1,138), but line count alone is not
an architecture violation. They stay with their current feature owners unless
a later bounded plan proves a responsibility split.

## Test organization and phase-named debt

Twenty-three tracked test C# files use phase/milestone names rather than only
durable behavior names:

```text
DI/Phase83M3DependencyInjectionTests.cs
DI/Phase9M1DiIntegrationTests.cs
Phase9M0EditorUxProofTests.cs
Services/M3aDebugLaunchProofTests.cs
Services/M3bDebugBreakpointProofTests.cs
Services/M4DebugExecutionProofTests.cs
Services/M5DebugStackProofTests.cs
Services/M6DebugRecoveryProofTests.cs
Services/M9aKeyBindingMaterializationTests.cs
Services/M9bSettingsDrivenRefreshTests.cs
Services/Phase13M0EditorMeasurementSeam.cs
Services/Phase13M0EditorMeasurementTests.cs
Services/Phase13M4aCriticalPathEvidenceTests.cs
Services/Phase83M0DiscoveryProofTests.cs
Services/Phase83M1ProjectDiscoveryTests.cs
Services/Phase83M2ProjectContextServiceTests.cs
Services/Phase83M3ProjectContextServiceIntegrationTests.cs
Services/Phase8ProofOfConceptTests.cs
Services/Phase9CommandRegistrationTests.cs
ViewModels/Phase83M4MainWindowViewModelProjectionTests.cs
ViewModels/Phase83M4StatusBarViewModelProjectionTests.cs
Views/M5SettingsUiTests.cs
Views/Phase814SettingsTests.cs
```

The test suite has no architecture-test folder or executable module-boundary
baseline today. Renaming/rehome work belongs to matching Refactor 6.2 slices;
M0 does not rename historical evidence.

## Existing feature ownership evidence

| Feature | Domain/state owners | Application/infrastructure owners | Presentation owners |
|---------|---------------------|-----------------------------------|---------------------|
| Editor | `Document`; editor/tab state | file service; formatting consumer seams | editor/tab/search/language-input/folding ViewModels and editor views |
| Workspace | `Workspace`, `FileTreeNode` | file-tree service and workspace/project-path coordination | file-tree and shell projection |
| Townhall | `Channel`, `TownhallMessage`, `TownhallState`, `WorkspaceAgent` | current behavior is largely in ViewModel/state rather than a separate application owner | `TownhallViewModel` and Townhall component views |
| Agents | `AgentPanelState`, route request/result | execution service, coordinator, parser, router | panel host/View and shell mirroring |
| Settings | settings records, validation and mutation results | settings service, migrations, serialization, paths, secret store | settings ViewModel/panel/font picker/bindings |
| Source Control | file change, branch, state, primary action | Git repository/mutation/diff/snapshot orchestration | Source Control ViewModel/panel and editor diff-tab projection |
| Project System | project context/candidate/profile/targets/workflow results | discovery, context, operation gate, workflow, managed process, output/build/test services | workflow, Problems, Output, and Test Results projections |
| Language/LSP | language state, snapshots, locations, edits, diagnostics, capabilities | feature services and document bridge; `csharp-ls` locator/session/transport/parsers are Language-owned LSP infrastructure | editor completion/hover/language picker/input and Problems/status projection |
| Debugging/DAP | breakpoint/debug state, snapshots, stack/scope/variable values | breakpoint and debug launch/session services; netcoredbg locator/session/transport/parsers are Debugging-owned DAP infrastructure | debug session/panel/stack/current-location/breakpoint ViewModels and views/margins |
| Terminal | screen, snapshot, parser and tab/session state | Linux PTY service and terminal-session factory | terminal host/ViewModel, tab host/strip/panel/render control |

The ownership map is intentionally based on current code. It introduces no
future orchestration implementation type.

Current `Styles` ownership is the target `UI/DesignSystem` module. The target
does not admit these files to `UI/Shared`.

### Future lifetime decisions

The current code proves no authoritative conversation, agent-session, or run
lifetime owner. The plan records these as R61-LT01, R61-LT02, and R61-LT03 and
explicitly defers their concrete definition or named later deferral to
Refactor 7 M0. This M0 introduces no implementation type, scope, factory, or
registration for them.

## Violation classification

Every verified violation is assigned in the complete table in
`IMPLEMENTATION_PLAN.md`:

- movement-only: R61-V01, R61-V03, and R61-V04;
- dependency inversion/lifetime: R61-V02, R61-V05 through R61-V14, and
  R61-V17;
- explicitly deferred: R61-V15, R61-V16, and R61-V18 through R61-V20.

There is no unclassified M0 finding. The existing-assembly decision applies to
all movement-only Refactor 6.2 work.

## Required M0 verification

Run sequentially after the final documentation edit:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
git status --short --branch
```

Recorded results:

- `dotnet build Zaide.slnx --no-restore` — final incremental verification
  succeeded with 0 warnings and 0 errors. The first compiling run in this M0
  session also succeeded with 0 errors and emitted 1 existing `CS0067` warning
  at `tests/Zaide.Tests/Services/ProjectDebugTargetResolverTests.cs:34`
  because `FakeManagedProcessRunner.ProcessStarted` is never used; the later
  incremental run did not re-emit it.
- `dotnet test Zaide.slnx --no-build` — 2,172 passed, 0 failed, 0 skipped;
  total 2,172.
- `git diff --check` — clean.
- `git status --short --branch` — `master...origin/master` with only the new
  untracked `docs/refactor/refactor-6.1/` directory.

## M0 conclusion

The current assembly is suitable for a mechanical feature-first Refactor 6.2,
but the migration must not start until executable Refactor 6.1 guardrails are
accepted and completed. The live dependency, composition, lifetime, and
visibility debt is real; it must not be hidden inside file moves. Human review
is the next gate.

## Limitations

- Source scans do not semantically resolve every possible dependency; later
  executable architecture tests must use an accepted source/metadata strategy.
- Compiled visibility counts cover non-nested top-level production types only.
- Lifetime findings are source-grounded; M0 added no runtime tracing.
- Migration order is guidance only and remains subject to Refactor 6.2's
  independent M0.
- `git diff --check` does not inspect untracked files; trailing-whitespace
  checks were also run directly over both new Markdown files.

## Rollback and commit record

Only `IMPLEMENTATION_PLAN.md` and this baseline are changed. Before commit,
rollback is deletion of the two untracked files and their empty directory.
After an explicitly authorized commit, revert that single documentation-only
commit.

**M0 commit hash:** Not recorded — no commit was requested or created. Add the
hash only after explicit user authorization to commit M0.
