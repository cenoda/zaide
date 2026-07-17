# Refactor 6.1: Architecture Rules and Module Map — Implementation Plan

## Status and authorization

**Current milestone:** M3 complete — legacy-violation allowlist and
architecture-rule ratchet for locator sites, namespace-direction edges, and
root-folder admissions (built on the M2 hybrid inventory). Review is required
before M4 (public/internal visibility and expanded root-admission baselines).

**Authorization boundary:** This plan,
[`M0_ARCHITECTURE_BASELINE.md`](M0_ARCHITECTURE_BASELINE.md), and the M1 doc
updates authorize no production structural changes. M2 authorizes only the
architecture-test harness and inventory reader inside the existing test
project. M3 authorizes only the legacy allowlist and ratchet tests inside
`tests/Zaide.Tests/Architecture/` plus truthful documentation updates. They do
not authorize source movement, namespace changes, dependency changes, DI or
visibility changes, production behavior changes, Refactor 6.2, Refactor 6.3,
or V3 feature implementation.

Refactor 6.1 defines the rules and future executable guardrails for structural
work. Refactor 6.2 will own approved mechanical movement. Refactor 6.3 will own
dependency inversion, composition, visibility, and lifetime correction.

## Goal

Establish a live-code-grounded feature/module map, dependency policy,
visibility policy, lifetime vocabulary, migration classification, and
legacy-violation ratchet that later executable architecture tests can enforce.
Do so without changing production structure or behavior in M0.

## Hard boundaries

- No production or test file is moved or renamed in Refactor 6.1 M0.
- No namespace, dependency, registration, visibility, lifetime, or behavior is
  changed in M0.
- No architecture-test code is created in M0.
- No Refactor 6.2 or Refactor 6.3 plan is created here.
- No future agent-orchestration implementation type or speculative scope is
  introduced.
- Refactor 6.1 does not redesign existing Editor, Townhall, agent-panel, LSP,
  DAP, workflow, or terminal behavior.
- Planning a future milestone is not authorization to execute it.

## M0 evidence summary

The reproducible inventory and evidence are in
[`M0_ARCHITECTURE_BASELINE.md`](M0_ARCHITECTURE_BASELINE.md). The decisive
findings are:

- `Zaide.slnx` contains one production project (`src/Zaide.csproj`) and one
  test project (`tests/Zaide.Tests/Zaide.Tests.csproj`).
- Production has 356 tracked C# files in flat technical namespaces and one
  compiled production assembly, `Zaide`.
- The compiled top-level production surface is 393 types: 348 public and 45
  internal under the documented counting rule.
- `Program.ConfigureServices` contains 64 explicit `AddSingleton` calls, one
  `AddTransient`, and no `AddScoped` call. Framework registrations added by
  `AddLogging` are outside those source-call counts.
- Current forbidden directions include `Services -> ViewModels` and one
  `Models -> Services` dependency. Four production types contain 44 provider
  resolution call expressions: 38 in composition (`Program` and `App`) and six
  in two forbidden non-composition owners. Static `App.Services` is an
  additional global-locator boundary.
- `MainWindowViewModel` is 608 lines with 18 constructor parameters;
  `MainWindow.axaml.cs` is 983 lines; `Program.cs` is 163 lines.
- The test project has 170 tracked C# files and 23 phase/milestone-named files.

## Approved feature/module taxonomy

The taxonomy follows current owners; it does not create empty layers or new
features.

```text
src/
  App/
    Composition/
    Shell/
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
        Lsp/
    Debugging/
      Infrastructure/
        Dap/
    Terminal/
  Infrastructure/
    FileSystem/
    Processes/
    Persistence/
  UI/
    DesignSystem/
    Shared/
```

Only folders required by moved, currently owned types may be created. A
feature may use a flat feature root or any subset of `Domain`, `Application`,
`Infrastructure`, `Presentation`, and `Contracts`; it must not create every
layer ceremonially.

The current `src/Styles` folder maps to `src/UI/DesignSystem`. Its two C# files
(`LayoutTokens` and `TextStyles`) and `Styles/Icons.axaml` are design-system
assets, not a feature module and not `UI/Shared` admissions.

### Current feature ownership

| Feature/module | Current owners that establish the boundary |
|----------------|---------------------------------------------|
| App / Shell | `Program`, `App`, `MainWindow`, global registration, startup, shutdown, shell layout and mode composition |
| Editor | `Document`, `EditorViewModel`, `EditorTabViewModel`, editor views, editor search/folding/language-input presentation, file editing contracts |
| Workspace | `Workspace`, `FileTreeNode`, `FileTreeService`, `FileTreeViewModel`, `FileTreeView`, folder/project selection bridge |
| Townhall | `Channel`, `TownhallMessage`, `TownhallState`, `WorkspaceAgent`, `TownhallViewModel`, Townhall views |
| Agents | `AgentPanelState`, route request/result, execution service/coordinator, mention parser/router, panel host and panel views |
| Settings | Settings records/results/validator, migrations, serializer/path/secret stores, settings service, ViewModel, and views |
| Source Control | File/branch/source-control state, Git read/mutation/diff/snapshot services, Source Control ViewModel and panel |
| Project System | Project discovery/context, operation gate, execution-profile and target resolution, workflow/process orchestration, output/build/test projections |
| Language | Language contracts, state/snapshots/policies/features, document bridge, and language presentation; LSP transport/session/parser types are owned by `Language/Infrastructure/Lsp` |
| Debugging | Breakpoints, debug launch/session/application projections and presentation; DAP transport/adapter/session/parser types are owned by `Debugging/Infrastructure/Dap` |
| Terminal | PTY/process service, terminal-session factory/host, parser/screen/snapshot/view state, and terminal views |
| Root shared infrastructure | Only proven multi-feature file-system, process, or persistence implementations with no truthful feature owner |
| Design system/shared UI | Current `src/Styles` maps to `UI/DesignSystem` for tokens, icons, and typography; only genuinely reused feature-neutral presentation primitives may enter `UI/Shared` |

Project workflow remains under Project System even when it consumes Workspace,
Editor save coordination, Problems, Output, or Test Results projections. LSP
infrastructure is not root infrastructure; DAP infrastructure is not root
infrastructure.

## Layer and dependency rules

### Meanings

- **Domain** owns feature truth, invariants, identities, and behavior that does
  not require UI, storage, transport, process, or DI frameworks.
- **Application** coordinates feature use cases and lifecycle-independent
  policies through explicit contracts.
- **Infrastructure** implements contracts using file systems, processes,
  protocols, persistence, operating-system APIs, or external libraries.
- **Presentation** owns Views, ViewModels, reactive commands, bindings,
  selection, focus, drafts, filters, and other UI state.
- **Contracts** contains the smallest interfaces and boundary values needed by
  another layer or feature. It is not a DTO dumping ground.
- **Snapshots** are immutable observations owned by the producing feature's
  contract or application boundary. A consumer may project them but must not
  become their source of truth.
- **View state** is mutable/reactive presentation state. Domain, Application,
  Infrastructure, and another feature's contracts must not consume it.

### Allowed directions

Within a feature:

```text
Presentation -> Application / Contracts / Domain
Infrastructure -> Application contracts / Contracts / Domain
Application -> Contracts / Domain
Contracts -> BCL and stable Domain value types only when necessary
Domain -> BCL and its own Domain only
```

Across features:

- A consumer may depend on the owning feature's `Contracts` or deliberately
  exposed Application façade.
- App composition may reference concrete Infrastructure and Presentation types
  only to register, construct, activate, and shut them down.
- Shared UI may depend on feature-neutral contracts, never feature
  Infrastructure.

### Forbidden directions

- Domain to Application, Infrastructure, Presentation, DI, Avalonia,
  ReactiveUI, process, protocol, or persistence implementation.
- Application to Presentation or concrete Infrastructure.
- Infrastructure to Presentation or ViewModels.
- Models/Domain/View state consuming another technical-layer implementation
  merely to reuse a snapshot or DTO.
- Any feature consuming another feature's Presentation or Infrastructure
  namespace.
- Views or ViewModels resolving dependencies through `IServiceProvider`.
- Production code outside App composition reading static `App.Services`.
- Root shared folders accepting feature-owned types.
- A dependency justified only by both types belonging to the same product
  “IDE layer” or “Agent layer”; those are product groups, not code layers.

## Root-folder admission rules

### `Infrastructure/`

A type is admitted only when all of the following are evidenced:

1. At least two current feature owners consume the implementation.
2. No one feature is its truthful owner.
3. The type implements a feature-neutral contract.
4. Its name and dependency set describe one infrastructure capability rather
   than generic helpers or services.
5. The architecture allowlist records the consumers and approving milestone.

Feature-specific LSP and DAP code fails this test and remains under Language
and Debugging respectively.

### `UI/Shared/`

A type is admitted only when at least two current presentation owners consume
it, it contains no feature workflow or state ownership, and it has no feature
Infrastructure dependency. `Common`, `Helpers`, `Utils`, and `Services` are not
acceptable ownership arguments. Design tokens and icons belong in
`UI/DesignSystem`, not `UI/Shared`.

Architecture tests must reject new files in either root unless a reviewed
ownership entry admits them.

## Visibility policy

- Default every production type to `internal`.
- `public` is reserved for a proven cross-module contract, a type required by
  Avalonia/XAML activation, or an externally consumed entry point.
- Cross-feature use alone does not automatically require `public` while all
  features remain in one assembly; `internal` plus explicit module contracts
  is preferred.
- Public interfaces and their boundary values must be minimal and owned by
  `Contracts` or an approved application façade.
- Infrastructure implementations, parsers, policies, mappers, ViewModels, and
  Views are internal unless a framework constraint is documented.
- Tests continue to use the existing `InternalsVisibleTo="Zaide.Tests"`
  boundary; visibility must not be widened for tests.
- The future ratchet starts at 348 public top-level production types. No new
  public type is allowed without an explicit allowlist entry; Refactor 6.3 must
  reduce the explicit list rather than merely remain below the count.

## Lifetime vocabulary

Only the following current vocabulary is approved by this refactor:

| Lifetime | Meaning | Current examples/evidence |
|----------|---------|---------------------------|
| Application | Exists for the root application container/window lifetime | settings, command registry, shell composition services |
| Workspace | State is reset or replaced when the open workspace changes | `Workspace`, project context, breakpoints and repository projections |
| Process | Owns one OS process or bounded process tree from start through termination/disposal | workflow runner operation, LSP server process, debug adapter/debuggee |
| Projection | Subscribes to a source and exists only while that projection is needed | Problems, Output, Test Results, debug/status projections |
| Editor session | One open editor/document presentation instance until its tab closes | `EditorViewModel` created for an opened `Document` |
| Terminal session | One PTY, shell process, screen, ViewModel, and tab until the tab closes | `LinuxTerminalService` plus `TerminalViewModel` created by `TerminalSessionFactory` |

DI registration lifetime and semantic lifetime are not synonyms. A singleton
may currently reset workspace state or own changing process state; Refactor
6.3 must make that ownership explicit without inventing future scopes.

### Future lifetime decisions explicitly deferred to Refactor 7

These terms are required by V3 but are not current Refactor 6.1 lifetimes and
must not be introduced as implementation types, scopes, factories, or DI
registrations here or during mechanical Refactor 6.2 movement:

| Decision ID | Future term | Provisional meaning to verify | Refactor 7 gate |
|-------------|-------------|-------------------------------|-----------------|
| R61-LT01 | Conversation | Lifetime of one ordered communication owner from creation/restoration through archive or removal; current channel-selected collections do not establish this owner. | Refactor 7 M0 must define the concrete owner and boundary or record a named later deferral. |
| R61-LT02 | Agent session | Lifetime of one bounded live or resumable agent execution context associated with stable identity; current panel identity and in-flight send do not establish it. | Refactor 7 M0 must prove a concrete consumer and ownership rule before any type/scope is introduced. |
| R61-LT03 | Run | Lifetime of one bounded execution attempt from admission through a terminal outcome; current panel send has no run owner or correlation identity. | Refactor 7 M0 must define the minimum existing-behavior representation or record a named later deferral. |

The IDs make these omissions explicit without expanding the approved current
lifetime vocabulary.

## Assembly decision for Refactor 6.2

**Decision: keep Refactor 6.2 in the existing `Zaide` assembly.**

Evidence does not justify an assembly split:

- there is one production project today and no independent deployment unit;
- the known violations require movement or dependency correction before an
  assembly boundary would be truthful;
- an early split would force visibility, project-reference, DI, and resource
  changes into a refactor intended to be mechanical;
- `InternalsVisibleTo` already permits tests to verify an internalized surface;
- feature-specific LSP and DAP protocols have real owners but do not need
  separate shipping assemblies for Refactor 6.2.

Any later assembly proposal requires its own evidence and approval; it must not
be smuggled into a Refactor 6.2 movement slice.

## Migration order guidance for Refactor 6.2 planning

Refactor 6.2 owns its formal M0 and may revise this order when it rechecks the
live graph. Its default planning order should be:

1. `Styles` to `UI/DesignSystem`, moving C#, AXAML resources, and matching tests
   together.
2. Settings, then Workspace, because their ownership is already cohesive and
   they establish contracts consumed by later slices.
3. Editor, then Project System, so document/editor-session and project-context
   owners have stable feature paths before downstream language/debugging work.
4. Language (including `Infrastructure/Lsp`), then Debugging (including
   `Infrastructure/Dap`).
5. Source Control, carrying R61-V02 and R61-V07 unchanged on the exact legacy
   allowlist until Refactor 6.3 corrects them.
6. Terminal, carrying R61-V05 unchanged and preserving terminal-session
   ownership tests.
7. Townhall, then Agents, preserving current string/status/channel behavior for
   Refactor 7 rather than redesigning it during movement.
8. App composition and Shell last, after feature namespaces are stable.

Every production file moves with its matching tests and AXAML/resource
references in one rollback unit. Root `Infrastructure` and `UI/Shared` are not
early migration destinations; candidates enter only after the admission rules
prove multiple current consumers. Dependency corrections never ride along with
a Refactor 6.2 movement slice.

## Legacy allowlist and ratchet strategy

The later executable baseline will store exact legacy entries with a stable ID,
source type/file, forbidden target, rule, owner, disposition, and removal
milestone. Broad wildcard exceptions are forbidden except temporary namespace
prefix entries required during a named Refactor 6.2 slice.

The first baseline must enforce:

1. Exact legacy dependency edges may remain; no new forbidden edge may appear.
2. Every Refactor 6.2/6.3 slice must remove or preserve the allowlist count; it
   may never increase it without review and a named deferred rationale.
3. Removed entries are deleted in the same slice and cannot be reintroduced.
4. Public types are checked by explicit full name plus a public-count ceiling
   of 348; count-only compliance is insufficient.
5. Service-locator sites are exact-file allowlist entries; no new View or
   ViewModel site is permitted.
6. Root shared-folder admission is deny-by-default.
7. Test organization mirrors the approved feature owner; temporary legacy test
   paths are allowlisted by exact file during migration.
8. Architecture-test updates and the structural change they permit land in the
   same review/rollback unit.

M0 records the policy only. It does not create the allowlist or tests.

## Complete violation disposition table

| ID | Verified finding | Disposition | Rationale / removal boundary |
|----|------------------|-------------|------------------------------|
| R61-V01 | Production is grouped under `Models`, `Services`, `ViewModels`, `Views`, and `Styles`; 224 of 356 C# files are in `Services`. | Movement-only work for Refactor 6.2 | Rehome by current feature owner with namespace/resource/test updates; map `Styles` specifically to `UI/DesignSystem`; no logic change. |
| R61-V02 | `SourceControlState` in `Models` consumes `RepositoryStatusSnapshot` from `Services`. | Dependency inversion/lifetime work for Refactor 6.3 | Movement alone preserves an invalid technical-layer edge. Refactor 6.3 must make the snapshot an explicit Source Control contract/application output and classify `SourceControlState` as presentation/view state or remove the redundant state bag. |
| R61-V03 | LSP and DAP protocol/process types are mixed into root `Services`. | Movement-only work for Refactor 6.2 | Move to `Language/Infrastructure/Lsp` and `Debugging/Infrastructure/Dap`; retain behavior and existing assembly. |
| R61-V04 | Tests mirror technical layers; 23 of 170 tracked test C# files carry phase/milestone names. | Movement-only work for Refactor 6.2 | Rehome and durably rename tests with their feature slice; assertions and behavior stay unchanged. |
| R61-V05 | `ITerminalSessionFactory` and `TerminalSessionFactory` in Services expose/create `TerminalViewModel`. | Dependency inversion/lifetime work for Refactor 6.3 | Define a presentation-owned terminal-session composition boundary and preserve terminal-session disposal ownership. |
| R61-V06 | `MentionParser` in Services depends on `IAgentPanelHost` from ViewModels. | Dependency inversion/lifetime work for Refactor 6.3 | Parser input must be an explicit agent lookup contract/value, not mutable presentation state. |
| R61-V07 | `SourceControlDiffTabService` depends on editor ViewModels and resolves three editor dependencies through `IServiceProvider`. | Dependency inversion/lifetime work for Refactor 6.3 | Replace with an explicit editor-session/open-diff contract or factory owned at the application/presentation boundary. |
| R61-V08 | `EditorTabViewModel` stores `IServiceProvider` and resolves editor dependencies when opening a tab. | Dependency inversion/lifetime work for Refactor 6.3 | Inject an explicit editor-session factory and make tab-close ownership testable. |
| R61-V09 | Static mutable `App.Services` is assigned in `Program` and read throughout startup/shutdown. | Dependency inversion/lifetime work for Refactor 6.3 | Establish an owned composition root and remove hidden global resolution. |
| R61-V10 | `Program.ConfigureServices` has 64 explicit singleton registrations, one transient, no scopes, and registrations for all features in one method. | Dependency inversion/lifetime work for Refactor 6.3 | Split registration by feature and assign semantic lifetimes without changing behavior. |
| R61-V11 | Application singletons currently carry workspace, process, projection, and session semantics; terminal sessions and editor instances are manually constructed outside container tracking. | Dependency inversion/lifetime work for Refactor 6.3 | Align explicit owners/factories with the approved current lifetime vocabulary. |
| R61-V12 | `App.DisposeServicesOnExit` resolves and synchronously disposes a fixed subset in manual order; the root provider is not the stated owner and async-capable language/debug resources are shut down synchronously. | Dependency inversion/lifetime work for Refactor 6.3 | Introduce truthful ordered shutdown ownership and prove process-tree termination. |
| R61-V13 | `MainWindowViewModel` is a cross-feature composition surface: 608 lines, 18 constructor parameters, feature activation/subscriptions, save handoffs, and agent/Townhall projection. | Dependency inversion/lifetime work for Refactor 6.3 | Reduce shell composition pressure through explicit feature coordinators/contracts; do not redesign product behavior. |
| R61-V14 | The compiled top-level surface is 348 public versus 45 internal types; most Services types are public. | Dependency inversion/lifetime work for Refactor 6.3 | Internalize implementations after approved module contracts exist; do not mix visibility churn into mechanical moves. |
| R61-V15 | `MainWindow.axaml.cs` is a 983-line imperative shell/view composition surface. | Explicitly deferred exception | Refactor 8 owns view extraction and UI-foundation work; Refactor 6.3 may narrow injected composition only and must not perform the visual extraction. |
| R61-V16 | Existing agent-panel/Townhall flow interprets output/status strings and targets active channel state. | Explicitly deferred exception | Refactor 7 owns the behavior/domain correction; structural movement must preserve current behavior and its tests. |
| R61-V17 | `MainWindow` manually creates and disposes `SettingsViewModel`/`SettingsPanelView` and constructs most feature views in shell code. | Dependency inversion/lifetime work for Refactor 6.3 | Clarify application/projection ownership and shell factories; visible UI extraction remains deferred to Refactor 8. |
| R61-V18 | No authoritative conversation lifetime owner exists; current Townhall state is selected-channel presentation/state-bag behavior. | Explicitly deferred exception | Tracked as R61-LT01. Refactor 7 M0 owns the concrete definition or named later deferral; no Refactor 6 type is authorized. |
| R61-V19 | No agent-session lifetime owner exists; panel identity and one in-flight send are not a session contract. | Explicitly deferred exception | Tracked as R61-LT02. Refactor 7 M0 must prove the consumer/owner before implementation. |
| R61-V20 | No run lifetime owner/correlation exists for one agent execution attempt. | Explicitly deferred exception | Tracked as R61-LT03. Refactor 7 M0 must define the minimum existing-behavior representation or defer it explicitly. |

## Future milestones

No milestone after M1 is authorized by this plan until review explicitly
allows it.

| Milestone | Session-sized scope | Verification and rollback boundary |
|-----------|---------------------|------------------------------------|
| M0 | Create this plan and the read-only live architecture baseline. | Only the two `refactor-6.1` docs may change. Run full build, full test, `git diff --check`, and status. Roll back by deleting the two uncommitted docs or reverting the documentation-only milestone commit. **Complete** (`94c734a`). |
| M1 | Codify the accepted taxonomy, dependency, admission, visibility, lifetime, and assembly rules in `docs/CONVENTIONS.md` and `docs/architecture/OVERVIEW.md`; create no production movement. | Docs-only diff. Full build/test plus `git diff --check`. Revert the single M1 docs commit without touching M0 evidence. **Complete** (see M1 record below). |
| M2 | Add the minimal architecture-test harness in the existing test project and a deterministic inventory reader; do not change production code. | Full build, focused `dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture`, full test, and diff check. Revert only architecture-test files and test-project changes from M2. **Complete** (see M2 record below). |
| M3 | Materialize the exact legacy dependency/service-locator allowlist and no-new-violation ratchet, seeded only from M0 IDs. | Focused architecture tests must pass with every allowlist entry exercised; full build/test and diff check. Revert the M3 allowlist/tests as one unit. **Complete** (see M3 record below). |
| M4 | Add executable public/internal visibility and root-folder admission ratchets, including the explicit public full-name baseline and 348 ceiling. | Focused architecture tests, exact type-count output, full build/test, and diff check. Revert M4 tests/baselines only. |
| M5 | Reconcile docs with executable rules, prove all M0 findings are represented, and close Refactor 6.1 without moving production files. | Full build/test, all architecture tests, diff check, clean milestone status, and human acceptance. Revert M5 documentation only; earlier executable milestones remain separately revertible. |

Prefer one commit per authorized milestone. If M2–M4 evidence makes a milestone
too large, split it into `M2a`/`M2b`-style slices before implementation rather
than creating another refactor-family member.

## Verification contract

Every future milestone must run sequentially:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
git status --short --branch
```

Architecture-test milestones additionally run their focused test filter after
the full build and before the full suite. A milestone is not complete if it
adds a new allowlist entry, leaves an allowlist entry unexercised, changes
production behavior, or crosses another refactor's boundary.

## M0 exit conditions

- [x] Live projects, folders, namespaces, assembly, dependencies,
      registrations, lifetimes, shutdown ownership, type visibility, large
      composition surfaces, tests, and feature owners are inventoried.
- [x] Feature/module taxonomy and LSP/DAP ownership are explicit.
- [x] Allowed/forbidden dependency and layer rules are explicit without
      requiring every feature to use every layer.
- [x] Root shared-folder admission rules are explicit.
- [x] Visibility and current lifetime policies are explicit.
- [x] Future conversation, agent-session, and run lifetime decisions are
      explicitly deferred to Refactor 7 under R61-LT01 through R61-LT03.
- [x] Every verified violation has exactly one Refactor 6.2, Refactor 6.3, or
      deferred disposition.
- [x] Existing-assembly migration is selected for Refactor 6.2.
- [x] Future Refactor 6.1 milestones, architecture-test baseline, ratchet,
      rollback, and verification boundaries are planned but not started.
- [x] Human review accepts M0 and authorizes the next milestone.

## M1 completion record

**Scope executed:** Architecture rule codification only. No production/test
files moved, renamed, created, or deleted. No architecture-test code, allowlist
files, packages, DI changes, visibility changes, or behavior changes.

### Documentation placement decision (M1 decision #1 — resolved)

**Decision: do not create a separate architecture-rules document.**

| Surface | Role after M1 |
|---------|----------------|
| [`docs/CONVENTIONS.md`](../../CONVENTIONS.md) | Canonical **detailed** enforceability rules (taxonomy, layers, dependencies, admission, visibility, lifetimes, deferrals) |
| [`docs/architecture/OVERVIEW.md`](../../architecture/OVERVIEW.md) | Concise architecture summary that distinguishes approved target rules from the still-current technical-layer tree; links here |
| This plan + [`M0_ARCHITECTURE_BASELINE.md`](M0_ARCHITECTURE_BASELINE.md) | Evidence, violation dispositions, migration order, and milestone record |

Rules are non-duplicative: CONVENTIONS holds the enforceable detail; OVERVIEW
holds the short target-vs-current summary; the refactor docs hold evidence and
history.

### Exact docs changed in M1

| File | Change |
|------|--------|
| `docs/CONVENTIONS.md` | Added Architecture rules section with accepted M0 target rules; clarified namespaces current-vs-target |
| `docs/architecture/OVERVIEW.md` | Added Source architecture (target vs current); updated V3/Refactor 6.1 status; link to this plan and baseline |
| `docs/refactor/refactor-6.1/IMPLEMENTATION_PLAN.md` | This M1 completion record, exit conditions, and residual decisions |

No other files may change for M1.

### M1 exit conditions

- [x] Accepted taxonomy, optional layers, snapshot/view-state ownership,
      dependency directions, root admission, LSP/DAP ownership, visibility,
      current lifetime vocabulary, and R61-LT01–LT03 deferrals are codified in
      `docs/CONVENTIONS.md`.
- [x] `docs/architecture/OVERVIEW.md` states a concise truthful target-vs-current
      summary and links to this plan.
- [x] M1 decision #1 is resolved: no separate architecture-rules document.
- [x] Diff is documentation-only (the three files above).
- [x] Verification contract commands pass (recorded below).
- [x] M2 is not started.

### M1 verification results

Run sequentially after the final documentation edit:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
git status --short --branch
```

Recorded results:

- `dotnet build Zaide.slnx --no-restore` — succeeded with 0 errors. Emitted 1
  existing `CS0067` warning at
  `tests/Zaide.Tests/Services/ProjectDebugTargetResolverTests.cs:34`
  (`FakeManagedProcessRunner.ProcessStarted` unused); not introduced by M1.
- `dotnet test Zaide.slnx --no-build` — 2,172 passed, 0 failed, 0 skipped;
  total 2,172.
- `git diff --check` — clean.
- `git status --short --branch` — `master...origin/master` with only the three
  M1 documentation files modified:
  `docs/CONVENTIONS.md`, `docs/architecture/OVERVIEW.md`,
  `docs/refactor/refactor-6.1/IMPLEMENTATION_PLAN.md`.

### M1 rollback boundary

M1 changes only the three documentation files listed above. Before commit,
rollback is `git checkout --` / restore of those files. After an explicitly
authorized M1 commit, rollback is one revert of that documentation-only
commit; it must not touch production/test files, M0 evidence content beyond
this plan's status fields, or any later milestone.

**M1 commit hash:** `201e85c8c492603ad845b2fea5292710642be719`

Stop after M1 and request review. Do not begin M2, M3, M4, M5, Refactor 6.2,
Refactor 6.3, Refactor 7, or Refactor 8.

## Unresolved decisions requiring review (M2 gate and later)

1. ~~Whether M1 should also create a separate architecture rules document or
   keep the accepted rules only in `CONVENTIONS.md`, `OVERVIEW.md`, and this
   plan.~~ **Resolved in M1:** no separate document; CONVENTIONS is detailed
   rules, OVERVIEW is summary, this plan/baseline are evidence and migration
   record.
2. ~~Whether the future executable inventory should be source-based, compiled-
   metadata-based, or hybrid.~~ **Resolved in M2:** hybrid inventory (see M2
   completion record).
3. ~~Whether architecture tests may use only existing xUnit/BCL capabilities or
   should propose a library through the normal dependency checkpoint.~~
   **Resolved in M2:** no package added; xUnit + BCL only (see M2 library
   evaluation).
4. The exact sequence of Refactor 6.2 feature slices after Refactor 6.1 closes;
   Refactor 6.1 supplies default migration-order guidance but does not create
   or authorize the Refactor 6.2 plan. **Post–Refactor 6.1 / Refactor 6.2 M0.**

## M2 completion record

**Scope executed:** Architecture-test harness and deterministic hybrid
inventory reader only. No production code, AXAML, resources, namespaces, DI,
visibility, lifetimes, or behavior changed. No M3 allowlist/ratchet, no M4
visibility/admission enforcement, no M5 closeout, and no Refactor 6.2/6.3/7/8
work. No NuGet package, `Directory.Packages.props`, `.csproj`, or
`docs/LIBRARIES.md` change.

### M2 gate decision #2 — hybrid inventory (resolved)

**Decision: use a hybrid inventory.**

| Channel | What it reads | Why |
|---------|---------------|-----|
| Source scans | Tracked production paths (`git ls-files`), technical-folder and declared-namespace placement, `IServiceProvider` / `App.Services` / resolution-call sites, technical-namespace dependency locations (`Services -> ViewModels`, `Models -> Services`), root-folder admission evidence | File organization, locator debt, and admission evidence are source-truth concerns |
| Compiled metadata | Non-nested, non-compiler-generated top-level `Zaide*` types from the loaded `Zaide` assembly; `IsPublic` / `IsNotPublic` | Visibility must match C# semantics (partials, records, default accessibility), same rule as M0 |

Stable `ArchitectureFinding` entries (sorted by `StableKey`) are produced so
M3 can attach exact allowlists without re-deriving keys.

### M2 gate decision #3 — architecture library evaluation (resolved)

Candidates evaluated at the start of M2 against the current .NET 10 / xUnit
2.9.3 environment:

| Candidate | Latest considered | License | .NET fit | M2 coverage |
|-----------|-------------------|---------|----------|-------------|
| `TngTech.ArchUnitNET` + `TngTech.ArchUnitNET.xUnit` | Core draft line through 2.1.0-draft; xUnit adapter **0.13.3** | Apache-2.0 | netstandard2.0 / usable on net10.0 with xUnit 2 | Compiled type/dependency fluent rules only |
| `NetArchTest.Rules` | **1.3.2** (last release 2021; effectively unmaintained) | Project MIT-style (see upstream) | netstandard2.0 | Compiled type/dependency fluent rules only |
| `NetArchTest.eNhancedEdition` | **1.4.5** | MIT | netstandard2.0 | Same surface as NetArchTest with fixes; still assembly-only |

**Decision: no candidate qualifies for addition in M2.**

Reasons (concise):

1. M2’s required work is a **hybrid inventory reader** and determinism proof of
   the M0 baseline, not fluent layer-rule assertions.
2. All candidates operate on compiled assemblies (typically Mono.Cecil). They
   do **not** replace source scans for tracked paths, folder/namespace
   placement, provider/service-locator sites, or root-folder admission
   evidence.
3. Remaining source-scan work after adopting any candidate would still be
   essentially the full M2 inventory. Benefits over xUnit/BCL for M2 are not
   material.
4. Per gate rules, even a later-useful M3 dependency library must not be added
   without explicit approval and `docs/LIBRARIES.md` update. None was required
   to complete M2.

**Outcome:** complete M2 with existing **xUnit + BCL** only
(`System.Reflection`, `System.IO`, `System.Text.RegularExpressions`,
`System.Diagnostics.Process` for `git ls-files`). No package pending approval.

### Exact files changed in M2

| File | Change |
|------|--------|
| `tests/Zaide.Tests/Architecture/ArchitectureInventoryReader.cs` | Hybrid inventory reader |
| `tests/Zaide.Tests/Architecture/ArchitectureInventory.cs` | Aggregate inventory model |
| `tests/Zaide.Tests/Architecture/ArchitectureFinding.cs` | Stable finding/entry for later allowlists |
| `tests/Zaide.Tests/Architecture/ProductionSourceFileEntry.cs` | Source placement entry |
| `tests/Zaide.Tests/Architecture/ProductionTypeEntry.cs` | Compiled visibility entry |
| `tests/Zaide.Tests/Architecture/ProviderEvidenceEntry.cs` | Provider/locator evidence entry |
| `tests/Zaide.Tests/Architecture/NamespaceDependencyEvidenceEntry.cs` | Technical-namespace dependency evidence |
| `tests/Zaide.Tests/Architecture/RootFolderAdmissionEvidenceEntry.cs` | Root-folder admission evidence |
| `tests/Zaide.Tests/Architecture/ArchitectureInventoryTests.cs` | Determinism + M0 baseline tests (no violation failures) |
| `docs/refactor/refactor-6.1/IMPLEMENTATION_PLAN.md` | This M2 completion record |

### Inventory type counts reproduced

| Metric | M0 baseline | M2 reader |
|--------|------------:|----------:|
| Total top-level types | 393 | 393 |
| Public | 348 | 348 |
| Internal | 45 | 45 |

Tracked production source files remain 356 (3 root / 22 Models / 224 Services /
2 Styles / 53 ViewModels / 52 Views).

### M2 exit conditions

- [x] Architecture tests live under a clearly separated
      `tests/Zaide.Tests/Architecture/` location.
- [x] Hybrid inventory strategy is locked and implemented.
- [x] Library candidates evaluated; none sufficient for M2; xUnit/BCL only; no
      unapproved dependency added.
- [x] Reader deterministically reproduces M0 visibility baseline 393/348/45.
- [x] Reader produces source placement, provider evidence, dependency-location
      evidence, root-admission evidence, and stable findings for M3.
- [x] Tests do not fail on existing known violations and do not implement M3
      allowlists/ratchets or M4 enforcement.
- [x] Production code unchanged; no `.csproj` / packages / LIBRARIES.md change.
- [x] Verification contract commands pass (recorded below).
- [x] M3 is not started.

### M2 verification results

Run sequentially after the final M2 edit:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
git status --short --branch
```

Recorded results:

- `dotnet build Zaide.slnx --no-restore` — succeeded with 0 warnings and 0
  errors on the final incremental verification run. An earlier compiling run in
  this M2 session emitted 1 existing `CS0067` warning at
  `tests/Zaide.Tests/Services/ProjectDebugTargetResolverTests.cs:34`
  (`FakeManagedProcessRunner.ProcessStarted` unused); not introduced by M2.
- Focused Architecture filter — 6 passed, 0 failed, 0 skipped.
- `dotnet test Zaide.slnx --no-build` — 2,178 passed, 0 failed, 0 skipped;
  total 2,178 (M1 baseline 2,172 + 6 new Architecture tests).
- `git diff --check` — clean.
- `git status --short --branch` — `master...origin/master` with
  `docs/refactor/refactor-6.1/IMPLEMENTATION_PLAN.md` modified and
  `tests/Zaide.Tests/Architecture/` untracked.

### M2 rollback boundary

M2 changes only the Architecture test files and this plan’s M2 record. Before
commit, rollback is deletion/restore of those files. After an explicitly
authorized M2 commit, rollback is one revert of that commit; it must not touch
production files, M0 baseline content beyond this plan’s status fields, or any
later milestone.

Stop after M2 and request review. Do not begin M3, M4, M5, Refactor 6.2,
Refactor 6.3, Refactor 7, or Refactor 8.

## M3 completion record

**Scope executed:** Legacy-violation allowlist and no-new-violation ratchet for
three M2-supported categories only. No production code, AXAML, resources,
namespaces, DI, visibility, lifetimes, or behavior changed. No M4 public-type
full-name baseline or target feature-layout enforcement. No M5 closeout. No
Refactor 6.2/6.3/7/8 work. No NuGet package, `.csproj`, or `docs/LIBRARIES.md`
change.

### Allowlist mutation rule (exact)

1. **Add** only when: (a) the entry maps to an existing M0 `R61-V##` or a
   plan-documented deferred exception; (b) the hybrid inventory already shows
   live evidence for the exact `MatchKey`; (c) the same review/rollback unit
   updates `LegacyArchitectureAllowlist`, the frozen `ApprovedFindingIds`
   baseline in tests, and this plan’s rationale; (d) the addition is not used
   to hide newly introduced debt without human review.
2. **Remove** only in the same change that eliminates the live inventory
   evidence for that `MatchKey`. Removed keys must not reappear without a new
   reviewed Add.
3. **Change** `MatchKey`, category, or M0 ID only as an explicit remove+add
   pair. Rationale/owner/disposition text may be clarified without changing
   `MatchKey` when the debt site is unchanged.
4. Broad wildcards are forbidden. Locator and root-admission keys are exact
   files; namespace edges are technical-folder direction plus exact source path.
5. Allowlist growth without updating the frozen FindingId set fails the
   ratchet tests.

### Allowlisted finding categories and entries

| Category | Count | Rule |
|----------|------:|------|
| **NamespaceDirection** | 5 | Exact-file `Services → ViewModels` / `Models → Services` edges |
| **LocatorSite** | 4 | Exact production files with provider/locator inventory evidence |
| **RootFolderAdmission** | 0 | Deny-by-default `src/Infrastructure/` and `src/UI/Shared/` |

| FindingId | Category | MatchKey | M0 ID | Disposition / removal |
|-----------|----------|----------|-------|------------------------|
| R61-AL-NS-SourceControlState | NamespaceDirection | `namespace:Models->Zaide.Services:src/Models/SourceControlState.cs` | R61-V02 | DependencyInversion / 6.3 |
| R61-AL-NS-ITerminalSessionFactory | NamespaceDirection | `namespace:Services->Zaide.ViewModels:src/Services/ITerminalSessionFactory.cs` | R61-V05 | DependencyInversion / 6.3 |
| R61-AL-NS-TerminalSessionFactory | NamespaceDirection | `namespace:Services->Zaide.ViewModels:src/Services/TerminalSessionFactory.cs` | R61-V05 | DependencyInversion / 6.3 |
| R61-AL-NS-MentionParser | NamespaceDirection | `namespace:Services->Zaide.ViewModels:src/Services/MentionParser.cs` | R61-V06 | DependencyInversion / 6.3 |
| R61-AL-NS-SourceControlDiffTabService | NamespaceDirection | `namespace:Services->Zaide.ViewModels:src/Services/SourceControlDiffTabService.cs` | R61-V07 | DependencyInversion / 6.3 |
| R61-AL-LOC-Program | LocatorSite | `locator:src/Program.cs` | R61-V09 | DependencyInversion / 6.3 |
| R61-AL-LOC-App | LocatorSite | `locator:src/App.axaml.cs` | R61-V09 | DependencyInversion / 6.3 |
| R61-AL-LOC-SourceControlDiffTabService | LocatorSite | `locator:src/Services/SourceControlDiffTabService.cs` | R61-V07 | DependencyInversion / 6.3 |
| R61-AL-LOC-EditorTabViewModel | LocatorSite | `locator:src/ViewModels/EditorTabViewModel.cs` | R61-V08 | DependencyInversion / 6.3 |

**Not allowlisted in M3 (by design):** public-type full names and the 348
ceiling (M4); lifetime / composition size / deferred domain findings without
M2 inventory edges (R61-V10–V20 / R61-LT01–03 remain documented only).

### Failure mode distinction

| Prefix | Meaning |
|--------|---------|
| `NEW_VIOLATION` | Live inventory evidence outside the allowlist |
| `STALE_ALLOWLIST` | Allowlist entry with no live evidence (must remove when debt is cleared) |
| `INVENTORY_FAILURE` | Hybrid reader/tooling could not produce inventory |
| `ALLOWLIST_INTEGRITY` | Ill-formed / non-deterministic allowlist data |

Known accepted legacy debt is asserted present and does not fail the suite.

### Exact files changed in M3

| File | Change |
|------|--------|
| `tests/Zaide.Tests/Architecture/ArchitectureAllowlistEntry.cs` | Allowlist entry model |
| `tests/Zaide.Tests/Architecture/ArchitectureViolation.cs` | Live ratcheted violation model |
| `tests/Zaide.Tests/Architecture/ArchitectureRatchet.cs` | Violation detection and allowlist comparison |
| `tests/Zaide.Tests/Architecture/LegacyArchitectureAllowlist.cs` | Frozen allowlist + mutation rule |
| `tests/Zaide.Tests/Architecture/ArchitectureRatchetTests.cs` | Ratchet / integrity tests |
| `docs/refactor/refactor-6.1/IMPLEMENTATION_PLAN.md` | This M3 completion record |
| `docs/CONVENTIONS.md` | Executable M3 ratchet summary |
| `docs/architecture/OVERVIEW.md` | Truthful M3 baseline status |

### M3 exit conditions

- [x] Exact legacy namespace-direction and locator-site allowlist seeded only
      from M0 IDs with inventory support.
- [x] Root-folder admission deny-by-default ratchet with empty approved set.
- [x] Stable FindingIds, rationale, owner, disposition, and removal boundary on
      every entry.
- [x] New violations outside the allowlist fail; allowlist growth without frozen
      FindingId update fails; every entry is exercised by live inventory.
- [x] Failure modes distinguish known debt, new violations, and inventory
      failures.
- [x] No M4 public-type / target-layout enforcement added.
- [x] Production code unchanged; no package or `.csproj` change.
- [x] Verification contract commands pass (recorded below).
- [x] M4 is not started.

### M3 verification results

Run sequentially after the final M3 edit:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter FullyQualifiedName~Architecture
dotnet test Zaide.slnx --no-build
git diff --check
git status --short --branch
```

Recorded results:

- `dotnet build Zaide.slnx --no-restore` — succeeded with 0 errors and 1
  existing `CS0067` warning at
  `tests/Zaide.Tests/Services/ProjectDebugTargetResolverTests.cs:34`
  (`FakeManagedProcessRunner.ProcessStarted` unused); not introduced by M3.
- Focused Architecture filter — 14 passed, 0 failed, 0 skipped
  (6 M2 inventory + 8 M3 ratchet/allowlist tests).
- `dotnet test Zaide.slnx --no-build` — 2,186 passed, 0 failed, 0 skipped;
  total 2,186 (M2 baseline 2,178 + 8 new M3 tests).
- `git diff --check` — clean.
- `git status --short --branch` — `master...origin/master` with three modified
  docs (`CONVENTIONS.md`, `architecture/OVERVIEW.md`, this plan) and five new
  untracked Architecture test files (allowlist entry, violation, ratchet,
  allowlist, ratchet tests).

### M3 rollback boundary

M3 changes only the Architecture allowlist/ratchet test files listed above and
the three documentation files. Before commit, rollback is deletion/restore of
those files. After an explicitly authorized M3 commit, rollback is one revert
of that commit; it must not touch production files, M0 baseline content beyond
this plan’s status fields, M2 inventory harness files except as needed for
compilation of new types, or any later milestone.

Stop after M3 and request review. Do not begin M4, M5, Refactor 6.2,
Refactor 6.3, Refactor 7, or Refactor 8.

## Limitations

- Source scans establish current file-level namespace edges but do not replace
  a semantic dependency graph; M3 executable allowlists close that gap for the
  two technical-namespace forbidden directions using M2 inventory evidence.
- The visibility baseline counts compiled non-nested top-level types only; it
  intentionally excludes nested and compiler-generated types. Public-type
  ratchets remain M4.
- Line and constructor-parameter counts identify composition pressure, not an
  automatic requirement to split every large class.
- Effective lifetime findings are based on registrations, construction, and
  disposal paths in source; no runtime lifetime tracing was added in M0.
- The migration order above is guidance for Refactor 6.2 M0, not authorization
  or a substitute for its independent live-code plan.
- M3 does not resolve allowlisted debt; it only freezes and ratchets it.

## Rollback

M0 changes only:

- `docs/refactor/refactor-6.1/IMPLEMENTATION_PLAN.md`
- `docs/refactor/refactor-6.1/M0_ARCHITECTURE_BASELINE.md`

Before commit, rollback is deletion of those two untracked files and the empty
directory. After an explicitly authorized M0 commit, rollback is one revert of
that documentation-only commit; it must not touch production/test files or any
later milestone.

**M0 commit hash:** `94c734a745ce081d1e831d0dbc735f630586be7b`

Stop after M0 and request review. Do not begin M1 or any structural migration.
