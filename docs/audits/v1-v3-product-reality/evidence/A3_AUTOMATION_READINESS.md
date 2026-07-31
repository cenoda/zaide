# A3-H0 Automation Readiness — V1–V3 Product Reality Audit

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3-H0 only** (automation readiness design and
compatibility proof). This note does **not** start A3 acceptance, does **not**
claim A3 passed, and does **not** authorize A4, stabilization, or V4 planning.
**Evidence date:** 2026-07-31
**Method:** read-only repository inspection, NuGet version resolution for
`Avalonia.Headless` / `Avalonia.Headless.XUnit` against the locked Avalonia
version, and an out-of-tree disposable restore/build of those packages. No
desktop UI launch, no xdtools, no screenshots, no pointer automation, no
production-code or test edits, no package added to the repository, no commit
or push.

---

## 0. Charter and safety boundary

| Constraint | Status for this note |
|------------|----------------------|
| A3 acceptance / clean-profile smoke execution | **Not started** |
| Real desktop UI launch | **Not done** |
| xdtools / screenshots / pointer automation / manual desktop | **Out of scope** |
| Production code modified | **No** |
| Existing tests modified | **No** |
| Package or tracked harness implementation added | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` status rewritten to “A3 complete” | **No** |
| A4 / stabilization / V4 planning begun | **No** |
| Commit / push | **No** |
| Real user profile / settings / secrets / conversation store | **Not read or written** |

**Authority documents:**

- [AGENTS.md](../../../../AGENTS.md)
- [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md) (A3 disposable-profile rules; A2 complete)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- A2 evidence under [evidence/](./) (especially isolation notes in
  `A2_FIRST_LAUNCH_AND_SETTINGS`, `A2_AGENT_SEND`, `A2_RESTART_RECOVERY_AND_CONTEXT`,
  `A2_DEBUGGING_AND_OUTPUT`, `A2_TERMINAL`, workspace/file/search slices)
- Production composition: [Program.cs](../../../../src/App/Composition/Program.cs),
  [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs)
- Production path resolvers: [SettingsPathResolver.cs](../../../../src/Features/Settings/Infrastructure/SettingsPathResolver.cs),
  [ConversationStorePathResolver.cs](../../../../src/Features/Conversations/Infrastructure/ConversationStorePathResolver.cs),
  [AgentDurableRecordPathResolver.cs](../../../../src/Features/Agents/Infrastructure/Transparency/Storage/AgentDurableRecordPathResolver.cs)
- ISSUE class: [ISSUE-009](../../../issues/open/ISSUE-009-production-di-test-contaminates-conversation-store.md)

---

## 1. Investigation summary

### 1.1 Avalonia version in this repository

| Pin | Location | Value |
|-----|----------|-------|
| Avalonia core | [Directory.Packages.props](../../../../Directory.Packages.props) | **12.0.5** |
| Avalonia.Desktop / Themes / Fonts / Diagnostics | same | **12.0.5** |
| App TFM | [src/Zaide.csproj](../../../../src/Zaide.csproj) | **net10.0** (`OutputType` WinExe) |
| Tests TFM | [tests/Zaide.Tests/Zaide.Tests.csproj](../../../../tests/Zaide.Tests/Zaide.Tests.csproj) | **net10.0** |
| `Avalonia.Headless` in repo today | packages / csproj | **Not referenced** |
| Existing test Avalonia bootstrap | [ReactiveUiTestBootstrap.cs](../../../../tests/Zaide.Tests/Infrastructure/ReactiveUiTestBootstrap.cs) | ReactiveUI + `new App()` without headless platform |

Prior phase notes already anticipated headless cost without adopting it (e.g.
[phase-3.9.1 TOFIX](../../../phases/v1/phase-3.9.1/TOFIX.md) TOFIX-002;
[phase-3.6 plan](../../../phases/v1/phase-3.6/IMPLEMENTATION_PLAN.md) “Do not
add Avalonia.Headless unless approved”).

### 1.2 Avalonia.Headless compatibility verdict

**Verdict: compatible and available** for the repository’s Avalonia / TFM pair.

| Check | Result |
|-------|--------|
| NuGet `Avalonia.Headless` versions include `12.0.5` | Yes (also `12.0.0`–`12.0.4`, `12.1.x`) |
| NuGet `Avalonia.Headless.XUnit` versions include `12.0.5` | Yes |
| Package dependency on `Avalonia` | **12.0.5** (exact match to central pin) |
| Package TFMs | **net8.0** and **net10.0** (matches app/tests `net10.0`) |
| Out-of-tree restore+build with Avalonia 12.0.5 + Headless 12.0.5 + Headless.XUnit 12.0.5 on net10.0 | **Succeeded** (0 warnings / 0 errors; disposable project outside the repo; nothing tracked) |

Public surface relevant to A3 (from package XML, not implemented here):

- `AppBuilder.UseHeadless(AvaloniaHeadlessPlatformOptions?)`
- `AvaloniaHeadlessPlatformOptions.UseHeadlessDrawing` (default headless drawing; disable if using Skia)
- `HeadlessUnitTestSession.StartNew(Type entryPointType)` — entry type must expose
  `BuildAvaloniaApp() → AppBuilder` **or** inherit `Application`
- `HeadlessUnitTestSession.Dispatch(...)` — UI-thread execution
- `IHeadlessWindow` — synthetic `KeyPress` / `KeyRelease` / `TextInput` /
  mouse / wheel / drag-drop (automation input without OS desktop pointer tools)
- `Avalonia.Headless.XUnit`: `[AvaloniaFact]` / `[AvaloniaTheory]` /
  `[AvaloniaTestFramework]`

**Stop condition for this note:** the Avalonia headless option is **not**
incompatible. No alternative harness is required as a replacement. Alternatives
below are documented only as fallbacks for rows where headless cannot observe
the claim, or if a later A3 implementation session discovers a runtime blocker
not visible from package compatibility alone.

### 1.3 Production composition under an isolated process

Production entry points today:

```text
Main(args)
  → BuildAvaloniaApp()
       .UsePlatformDetect()
       .UseReactiveUIWithMicrosoftDependencyResolver(ConfigureServices, CompositionRoot.Services = sp)
       .WithInterFont() / LogToTrace / optional DeveloperTools
  → StartWithClassicDesktopLifetime(args)

App.OnFrameworkInitializationCompleted
  → only when ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
  → resolve MainWindowViewModel, settings, secrets, registry, palette, LSP, debug VMs, …
  → construct MainWindow
  → permission dialog owner + continuity reconcile
  → desktop.Exit → ApplicationShutdown.Run(CompositionRoot.Services)
```

**Important seams for automation (already public/internal without new packages):**

| Seam | Role for A3 |
|------|-------------|
| `Program.ConfigureServices(IServiceCollection)` | Full production DI registration graph (all `AddZaide*` modules + console logging). Used by many composition tests. |
| `Program.BuildAvaloniaApp()` | Production `AppBuilder` with **desktop** platform detect — **not** headless by itself. |
| `CompositionRoot.Services` | Set only by the ReactiveUI DI bootstrap path inside `BuildAvaloniaApp`. |
| `App.OnFrameworkInitializationCompleted` | Full shell construction requires a **classic desktop-style lifetime**, even under headless. |
| `ApplicationShutdown.Run` | Ordered dispose of debug/workflow/language/terminal/townhall/agent owners. A2 notes that conversation flush is **not** fully owned here; draft flush timing must be documented per scenario. |

**Existing production-DI integration tests** (corroboration only; not A3 evidence):

- Pattern: `new ServiceCollection()` → `Program.ConfigureServices` → substitute
  `IScheduler` with `CurrentThreadScheduler` → `BuildServiceProvider()`.
- Examples: [CompositionDiIntegrationTests.cs](../../../../tests/Zaide.Tests/App/Composition/CompositionDiIntegrationTests.cs),
  `*RegistrationModuleTests` under `tests/Zaide.Tests/App/Composition/`.
- These resolve services **without** a UI host and **without** setting disposable
  `XDG_CONFIG_HOME`. That is exactly the ISSUE-009 contamination class when
  Townhall draft state is mutated under production paths.

**Implication for A3 process design:**

1. Full product-runtime evidence must start a **dedicated process** with
   disposable XDG/`HOME` set **before** any production path resolver runs.
2. DI-only product-runtime scenarios may call `Program.ConfigureServices` inside
   that process (no desktop) when the success condition is observable on
   ViewModels, services, or filesystem artifacts under the disposable root.
3. Shell / keyboard / control-tree scenarios need a headless `AppBuilder` that
   still reaches `IClassicDesktopStyleApplicationLifetime` so
   `OnFrameworkInitializationCompleted` builds `MainWindow`. That builder is
   **design-only** in this note; it is not implemented and must not patch
   production `UsePlatformDetect` until an authorized A3 harness session.

### 1.4 Profile path surface (what isolation must cover)

On Linux, production durable state converges on one config directory:

| Artifact | Resolver | Path under isolated config |
|----------|----------|----------------------------|
| Settings | `SettingsPathResolver` | `$XDG_CONFIG_HOME/zaide/settings.json` (+ LKG, tmp) |
| Secrets | same | `$XDG_CONFIG_HOME/zaide/secrets.json` (+ tmp; Linux 0600 create/repair) |
| Conversations | `ConversationStorePathResolver` | `$XDG_CONFIG_HOME/zaide/conversations/conversations.json` (+ LKG, tmp) |
| Agents durable (Phase 21) | `AgentDurableRecordPathResolver` | `$XDG_CONFIG_HOME/zaide/agents-durable/...` |

`SettingsPathResolver` rules (Linux):

1. If `XDG_CONFIG_HOME` is non-empty **and** absolute → `$XDG_CONFIG_HOME/zaide`.
2. Else if `HOME` set → `$HOME/.config/zaide`.
3. Else fallback `/tmp`-based `.config/zaide`.

**Zaide does not currently read `XDG_DATA_HOME` / `XDG_STATE_HOME` /
`XDG_CACHE_HOME` for product state.** Those variables are still part of the
isolation protocol to prevent accidental leakage via third-party tooling,
dotfiles, or future path growth, and to keep scenario environments hermetic.

**Read-only host tool discovery (not product state, but HOME-sensitive):**

- `LanguageServerBinaryLocator` falls back to
  `Environment.SpecialFolder.UserProfile` + `/.dotnet/tools/csharp-ls`.
- Debug adapter: `ZAIDE_NETCOREDBG_PATH` then `PATH` (`netcoredbg`).
- Isolating `HOME` without preserving host `PATH` (or without an explicit
  tool path) can flip LSP/DAP scenarios from positive-path to
  `AdapterUnavailable` / no-server — valid negative evidence only when intended.

---

## 2. Compatible harness option (design only)

### 2.1 Selected option

**Name:** `A3 Headless Product Runtime` (planned; not implemented)

**Stack (to be added only in a later authorized A3 implementation session):**

1. **Process isolation** — one OS process per scenario, disposable profile env.
2. **Avalonia.Headless 12.0.5** (+ optional **Avalonia.Headless.XUnit 12.0.5**) —
   version-locked to Avalonia 12.0.5 / net10.0.
3. **Production DI** — `Program.ConfigureServices` (or an audit-only
   `AppBuilder` that calls the same method).
4. **Observation layers** — ViewModel/service state, control property tree
   where headless inspection is possible, filesystem under the disposable root,
   child process / PTY output, exit codes, restart-second-process comparison.

**Out of scope for the selected option (explicit):**

- xdtools, OS screenshots, real pointer automation, manual desktop interaction
- Launching the real user desktop session of Zaide
- Mutating production code or existing product tests as part of readiness

### 2.2 Scenario process topology

```text
orchestrator (docs/audit host; no product DI)
  │
  ├─ create PROFILE_ROOT (mktemp)
  ├─ write scenario input (workspace fixtures, optional settings seed)
  ├─ spawn scenario process with env isolation
  │     env: HOME, XDG_*, optional ZAIDE_NETCOREDBG_PATH, PATH (tools)
  │     args: --scenario <id> --profile <PROFILE_ROOT> --evidence <path>
  │
  ├─ scenario process
  │     1. assert profile env absolute and under PROFILE_ROOT
  │     2. build headless AppBuilder OR DI-only provider
  │     3. run command/input sequence
  │     4. emit machine-readable evidence record
  │     5. ApplicationShutdown / provider dispose
  │     6. exit non-zero on assertion failure
  │
  ├─ optional second process (restart scenarios) with SAME profile root
  │
  └─ delete PROFILE_ROOT (even on failure, after evidence capture)
```

**Restart scenarios** are the only allowed multi-process use of one profile:
process A writes durable state and exits cleanly; process B starts with the
**same** disposable env and observes restoration. No concurrent processes share
one profile.

### 2.3 AppBuilder strategy (design)

Prefer an **audit-only entry type** (not a production edit) of the form:

```csharp
// DESIGN ONLY — not present in the tree
public static class A3HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                // Prefer headless drawing unless Skia visual sampling is required
                UseHeadlessDrawing = true,
            })
            .UseReactiveUIWithMicrosoftDependencyResolver(
                containerConfig: Program.ConfigureServices,
                withResolver: sp => CompositionRoot.Services = sp!)
            .WithInterFont()
            .LogToTrace();
}
```

Rationale:

- Production `Program.BuildAvaloniaApp()` calls `UsePlatformDetect()` for real
  desktop backends. Reusing it unchanged would either fail headless or risk
  attaching a real platform backend — both violate the non-desktop charter.
- `HeadlessUnitTestSession.StartNew` is built for types that expose
  `BuildAvaloniaApp`; an audit-only type satisfies that without editing
  production `Program`.
- Shell startup still needs classic desktop lifetime semantics so
  `App.OnFrameworkInitializationCompleted` constructs `MainWindow`. The
  implementation session must prove the exact headless lifetime API used
  (`StartWithClassicDesktopLifetime` under headless platform vs session
  `Dispatch` + explicit `MainWindow.Show`) and record the proven path in A3
  evidence. **Not proven in H0 beyond package API availability.**

### 2.4 Input sequence model (non-desktop)

Prefer, in order:

1. **Command/registry invocation** — `ICommandRegistry.GetById(...).Command.Execute`
   or ViewModel `ReactiveCommand` execution (deterministic, product entry points).
2. **Direct ViewModel mutation** only when it mirrors a documented user entry
   (e.g. set draft text then send) and is labeled as such.
3. **Headless keyboard injection** via `IHeadlessWindow.KeyPress` /
   `TextInput` for gesture-delivery claims (`Ctrl+Oem3`, `Ctrl+Shift+P`, etc.).
4. **Never** OS-level pointer tools or screenshot OCR as primary evidence.

Folder open that requires a native storage picker is a **known gap**: production
`PickFolder` is OS-dialog based. Deterministic A3 should inject the folder path
through the same post-picker production seam (`FileTreeViewModel` /
`OpenFolderCommand` / `SetRootPath`) and classify native-picker UX itself as
visual/OS-dialog UNVERIFIED unless a headless storage provider stub is
explicitly authorized later.

### 2.5 Fallback harness (only if headless later fails)

If a later session discovers a **runtime** blocker (lifetime mismatch,
`CompositionRoot` not set, STA/dispatcher deadlock, Semi.Avalonia resource
failure under headless drawing), stop implementing headless and use the
**smallest deterministic alternative:**

| Fallback | What it is | What it loses |
|----------|------------|---------------|
| **DI product-runtime process** | Isolated process + `Program.ConfigureServices` + command/ViewModel drives + filesystem/PTY observation | Control tree / keyboard routing / layout property inspection |
| **Existing test corroboration only** | Cite green product tests | Not product-runtime A3 evidence |

Do **not** fall back to xdtools/manual desktop for A3 product-runtime claims
under this audit charter.

---

## 3. Isolation protocol (scenario-per-process)

### 3.1 Mandatory environment

Before starting any scenario process:

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

# Optional tool paths (host binaries; not product state)
# export PATH="/usr/bin:...:$PATH"
# export ZAIDE_NETCOREDBG_PATH="/absolute/path/to/netcoredbg"
```

### 3.2 Hard rules

1. **Absolute `XDG_CONFIG_HOME` required** — relative values fall through to
   `$HOME/.config/zaide` and can miss isolation intent.
2. **Env must be set before process start** — not after DI construction
   (path resolvers read environment at call time; services cache paths at
   construction).
3. **One scenario → one process → one profile** (except ordered restart pair).
4. **Never** point at the real user `~/.config/zaide` or an existing developer
   profile.
5. **Full production DI is allowed** only under the disposable root (A2 agent-send
   isolation note; ISSUE-009 lesson).
6. **Cleanup** — capture evidence, then delete `PROFILE_ROOT`. Failure to clean
   up is an orchestrator bug, not an excuse to reuse the root.
7. **Secrets / credentials** — use synthetic keys only; never copy real secrets
   into the disposable profile.
8. **Workspace fixtures** — use disposable project/git trees under
   `PROFILE_ROOT/workspaces/...`, not the live repo working tree, when the
   scenario mutates files, runs builds, or stages git.

### 3.3 Preflight assertions (scenario process)

Before executing steps, the process must assert:

- `Path.IsPathRooted(XDG_CONFIG_HOME)`
- `SettingsPathResolver.GetSettingsDirectory()` starts with `XDG_CONFIG_HOME`
- settings/conversations/agents-durable paths are under the disposable tree
- real user config path is not equal to the resolved settings directory

### 3.4 Why HOME is still required

Even when `XDG_CONFIG_HOME` is set, production and host tools may consult
`HOME` / `UserProfile` for non-config discovery. Setting `HOME` under
`PROFILE_ROOT` prevents accidental writes to the developer home if any code
path falls back. Tool discovery must then rely on `PATH` or explicit env
overrides (`ZAIDE_NETCOREDBG_PATH`, pre-seeded tool symlinks under the
disposable home if required).

---

## 4. Scenario runner design (design only)

### 4.1 Responsibilities

| Component | Responsibility |
|-----------|----------------|
| **Orchestrator** | Enumerate scenario IDs, create/destroy profiles, spawn processes, collect evidence files, produce journey rollups |
| **Scenario process** | Isolated product runtime, execute steps, assert observables, write one evidence record, exit code |
| **Fixture library** | Disposable C# projects, multi-sln trees, git repos, corrupt settings seeds — under profile root |
| **Evidence writer** | Machine-readable JSON (primary) + human markdown excerpt (optional) |

### 4.2 Scenario definition shape

```yaml
# DESIGN ONLY
id: A1-FL-03
journey: first-launch
mode: di-or-headless   # di | headless | di+restart
requires:
  tools: []            # e.g. [dotnet, csharp-ls, netcoredbg]
  headless: false
steps:
  - action: settings.set
    path: Editor.FontSize
    value: 18
  - action: settings.apply
  - action: process.exit
  - action: process.restart_same_profile
  - action: assert.settings
    path: Editor.FontSize
    equals: 18
observations:
  - class: product-runtime
    kinds: [filesystem, viewmodel]
```

### 4.3 Modes

| Mode | When to use |
|------|-------------|
| `di` | Success condition is fully observable via production services/ViewModels + filesystem under disposable root; no control tree required |
| `headless` | Needs MainWindow, keybinding materialization, control `IsVisible` / layout properties, permission dialog owner attachment |
| `di+restart` / `headless+restart` | Persistence across process boundary (FL-03, TC-04, TC-05) |
| `negative-path` | Expected product failure or Missing entry-point proof (AS-02 unbound, AC-02 no bind UI, etc.) |

### 4.4 Exit codes (proposed)

| Code | Meaning |
|------|---------|
| 0 | All assertions passed |
| 2 | Product assertion failed (valid A3 fail evidence) |
| 3 | Isolation preflight failed (do not trust observations) |
| 4 | Missing external tool required for positive path |
| 5 | Harness internal error |

---

## 5. Machine-readable evidence schema

Primary artifact per scenario process: one JSON document (path under audit
evidence, e.g. `evidence/a3-runs/<run-id>/<scenario-id>.json`). Schema versioned
so A4 can aggregate without re-parsing free text.

```json
{
  "schema_version": "a3-evidence-1",
  "audit": "v1-v3-product-reality",
  "phase": "A3",
  "scenario_id": "A1-FL-03",
  "a1_row_ids": ["A1-FL-03"],
  "started_at_utc": "2026-07-31T00:00:00Z",
  "finished_at_utc": "2026-07-31T00:00:10Z",
  "host": {
    "os": "linux",
    "rid": "linux-x64",
    "repo_head": "<git-sha>",
    "harness": "a3-headless-product-runtime",
    "harness_version": "not-implemented"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-XXXXXXXX",
    "home": ".../home",
    "xdg_config_home": ".../config",
    "xdg_data_home": ".../data",
    "xdg_state_home": ".../state",
    "xdg_cache_home": ".../cache",
    "resolved_settings_dir": ".../config/zaide",
    "preflight_ok": true
  },
  "command_input_sequence": [
    {
      "i": 1,
      "kind": "command|viewmodel|key|service|process|filesystem_seed",
      "name": "settings.apply",
      "payload": {},
      "timestamp_utc": "..."
    }
  ],
  "observed_events": [
    {
      "source": "ViewModel|Service|Control|Filesystem|Process|Pty",
      "name": "SettingsService.Saved",
      "data": {},
      "timestamp_utc": "..."
    }
  ],
  "control_state": [
    {
      "path": "MainWindow.BottomPanel.IsVisible",
      "property": "IsVisible",
      "value": true,
      "inspectable": true
    }
  ],
  "filesystem_artifacts": [
    {
      "path": ".../config/zaide/settings.json",
      "exists": true,
      "sha256": "...",
      "unix_mode": "0600",
      "notes": "optional redacted excerpt refs"
    }
  ],
  "process_pty": [
    {
      "role": "scenario|child|pty",
      "argv": ["dotnet", "build", "..."],
      "exit_code": 0,
      "stdout_ref": "artifacts/build.stdout.txt",
      "stderr_ref": "artifacts/build.stderr.txt"
    }
  ],
  "exit_and_restart": {
    "first_exit_code": 0,
    "restart_performed": true,
    "second_exit_code": 0,
    "durable_state_compared": ["settings.json", "conversations.json"]
  },
  "assertions": [
    {
      "id": "font-size-persisted",
      "result": "pass|fail|skipped",
      "evidence_class": "product-runtime",
      "detail": ""
    }
  ],
  "evidence_classes_used": [
    "product-runtime",
    "corroborating-existing-tests",
    "test-only-fake-backend",
    "unverified-visual-only"
  ],
  "limitations": [],
  "verdict_hint": "pass|fail|blocked|unverified"
}
```

### 5.1 Evidence classes (mandatory separation)

| Class | Meaning | A3 use |
|-------|---------|--------|
| **product-runtime** | Observed in an isolated process running production composition / production code paths | Primary A3 proof |
| **corroborating-existing-tests** | Existing unit/integration tests in `tests/` that support the wiring claim | Secondary only; never substitutes product-runtime for A3 success |
| **test-only/fake-backend** | Fakes, test doubles, in-memory backends, non-production bindings | Must not be labeled product-runtime success for backend-bound journeys |
| **unverified-visual-only** | Color fidelity, pixel layout, glyph raster, OS chrome, true pointer-hover, screenshot comparison | Record as UNVERIFIED under non-desktop A3; do not invent pass |

Every assertion in a scenario record must name exactly one primary class.
Mixing is allowed at the scenario level only via the `evidence_classes_used`
array, not by silently upgrading a fake-backend pass.

---

## 6. Automatable A1 rows vs UNVERIFIED without visual acceptance

Legend for **planned A3 automation** (not executed):

| Tag | Meaning |
|-----|---------|
| **AUTO-DI** | Automatable via isolated DI product-runtime (no headless required) |
| **AUTO-H** | Automatable via headless product-runtime (control/key/shell) |
| **AUTO-FS** | Filesystem / permissions / persistence artifacts under disposable profile |
| **AUTO-PROC** | Child process / build / test / PTY output |
| **AUTO-RESTART** | Multi-process same-profile restart |
| **NEG** | Negative-path or Missing-entry proof is the honest A3 result |
| **BLOCKED** | Cannot complete positive path without missing product UI or external provision |
| **UNVERIFIED-VIS** | Requires visual/desktop acceptance; non-desktop A3 cannot pass the visual claim |
| **OUT** | Explicitly out of A3 scope per matrix/A2 |

`A1-XX-*` rows are not user-goal smoke targets; listed only where they constrain
automation.

### 6.1 First launch and settings

| id | A2 (summary) | Automation | Notes |
|----|--------------|------------|-------|
| A1-FL-01 | Wired-with-gap | **AUTO-H** partial; **UNVERIFIED-VIS** for true 3-panel paint | Bottom panel toggle via command/key + `IsBottomPanelVisible`; layout chrome pixels UNVERIFIED |
| A1-FL-02 | Wired-with-gap | **UNVERIFIED-VIS** primary | Theme/palette color fidelity needs visual acceptance; resource presence can be AUTO-H weak corroboration only |
| A1-FL-03 | Wired-with-gap | **AUTO-DI + AUTO-FS + AUTO-RESTART** | Settings write/restart/load; corrupt→LKG can be FS-seeded; silent load failures = observe absence of UI surface (NEG for recovery UX) |
| A1-FL-04 | Wired-with-gap | **AUTO-DI + AUTO-FS** | Store synthetic secret; assert `settings.json` lacks key; assert `secrets.json` mode 0600 |
| A1-FL-05 | Wired | **AUTO-DI + AUTO-H** partial | Settings apply + ViewModel/editor property; pixel font metrics UNVERIFIED-VIS |
| A1-FL-06 | Wired-with-gap | **OUT** / recovery subset **AUTO-FS** | Performance budgets out of A3 product smoke per matrix; settings recovery FS paths OK |

### 6.2 Workspace / project opening

| id | A2 | Automation | Notes |
|----|----|------------|-------|
| A1-WO-01 | Wired-with-gap | **AUTO-DI + AUTO-FS** | Inject folder path post-picker; ignore list + hidden toggle via commands/VM; native picker UX UNVERIFIED-VIS |
| A1-WO-02 | Wired-with-gap | **AUTO-DI + AUTO-FS** | No-project / ambiguous fixtures; status strings; missing picker UI → NEG for multi-sln selection success |
| A1-WO-03 | Wired-with-gap | **AUTO-DI** | Open/close folder; observe project context + SC refresh coupling |

### 6.3 File navigation and editing

| id | A2 | Automation | Notes |
|----|----|------------|-------|
| A1-FN-01 | Wired | **AUTO-DI + AUTO-H** partial | Open/edit/save/dirty via VM; TextMate colors UNVERIFIED-VIS |
| A1-FN-02 | Wired-with-gap | **AUTO-DI** for copy-path; splitter range **AUTO-H** weak / **UNVERIFIED-VIS** | 180–320 vs 180–500 is measurable as constraint if control min/max exposed; live drag feel UNVERIFIED |
| A1-FN-03–06 | Wired | **AUTO-DI + AUTO-H** | Search/replace/fold/tabs/status via commands/VM |
| A1-FN-08 | Wired-with-gap | **AUTO-DI + AUTO-PROC** if `csharp-ls` on PATH | Else BLOCKED positive path; NEG for no-server |
| A1-FN-09–14 | Wired | **AUTO-DI + AUTO-PROC** with `csharp-ls` | Completion/hover/definition/symbols/format; caret-dwell hover not pointer hover (FN-10) |
| A1-FN-15 | Wired-with-gap | **AUTO-DI + AUTO-FS** | Format-on-save setting + save path |

### 6.4 Search and command discovery

| id | A2 | Automation | Notes |
|----|----|------------|-------|
| A1-SC-01 | Wired-with-gap | **AUTO-DI + AUTO-FS** | Keybinding overrides in settings; no editor UI → NEG for user keybindings editor |
| A1-SC-02 | Wired-with-gap | **AUTO-H** | Palette open/filter/execute/focus; pointer row reselect gap remains |
| A1-SC-03 | Wired | **AUTO-DI** | Registry descriptors resolve by id |

### 6.5 Build / run / test

| id | A2 | Automation | Notes |
|----|----|------------|-------|
| A1-BR-01–04 | Wired | **AUTO-DI + AUTO-PROC + AUTO-H** (panel mode) | Disposable project + `dotnet`; Output vs terminal separation via mode + sinks |

### 6.6 Debugging and output

| id | A2 | Automation | Notes |
|----|----|------------|-------|
| A1-DB-01 | Wired-with-gap | **AUTO-DI + AUTO-PROC** if NetCoreDbg provisioned; else **NEG** only | Positive path BLOCKED without `ZAIDE_NETCOREDBG_PATH`/`PATH`; gutter paint / three-column proportions **UNVERIFIED-VIS** |
| A1-XX-04 | disposition | constrains DB automation | Not a user-goal smoke row |

### 6.7 Terminal

| id | A2 | Automation | Notes |
|----|----|------------|-------|
| A1-TR-01 | Wired-with-gap | **AUTO-DI + AUTO-PROC** partial | PTY I/O, restart lifecycle; alt-screen/TUI visual **UNVERIFIED-VIS**; selection paint UNVERIFIED |
| A1-TR-02 | Wired-with-gap | **AUTO-DI + AUTO-H** partial | Multi-tab session ownership; static titles gap; runtime isolation of PTYs via process list |

### 6.8 Git

| id | A2 | Automation | Notes |
|----|----|------------|-------|
| A1-GT-01–04 | Wired | **AUTO-DI + AUTO-FS + AUTO-PROC** | Disposable git repo; status/diff/stage/commit/branch via SC VM + libgit2 |

### 6.9 Townhall / conversations

| id | A2 | Automation | Notes |
|----|----|------------|-------|
| A1-TH-01 | Wired-with-gap | **AUTO-DI + AUTO-H** | Channels/filters; limited kinds |
| A1-TH-02 | Wired | **AUTO-DI + AUTO-FS** | DM find-or-create; privacy by store partition |
| A1-TH-04 | Wired | **AUTO-H** | Assert no agent-panel chrome in shell tree |
| A1-TH-05 | Wired-with-gap | **AUTO-DI** / **NEG** | Routed flow projection gaps are valid A3 observations |

### 6.10 Agent creation / send / tools / multi-agent / trace

| id | A2 | Automation | Notes |
|----|----|------------|-------|
| A1-AC-01 | Missing | **NEG** | Historical panel retired; prove absence of create workflow |
| A1-AC-02 | Wired-with-gap | **NEG** / **BLOCKED** positive bind | No user bind/configure UI; infrastructure-only paths are not product success |
| A1-AS-01 | Missing | **NEG** | Panel send path retired |
| A1-AS-02 | Wired-with-gap | **NEG** unbound path **AUTO-DI**; positive send **BLOCKED** without bind UI or authorized hook | Do not use fake backends as product-runtime pass |
| A1-TP-01–03 | Wired-with-gap | **BLOCKED** positive multi-file/permission UX without bind; partial **AUTO-DI** if harness forces backend action | Permission dialog needs owner window → **AUTO-H** when reachable |
| A1-MR-01 | Missing | **NEG** | Panel-bound routing retired |
| A1-MR-03 | Wired-with-gap | **AUTO-DI** partial / **NEG** | Catalog routing without panels; empty catalog failure |
| A1-TC-01 | Wired-with-gap | **AUTO-DI** partial | Context selector in-memory; no settings default |
| A1-TC-02,03,08,09 | Missing | **NEG** | No user surfaces for inspect/manage/usage/end |
| A1-TC-04 | Wired-with-gap | **AUTO-DI + AUTO-FS + AUTO-RESTART** | Drafts/selection restore; document flush-on-shutdown gap |
| A1-TC-05 | Wired-with-gap | **AUTO-RESTART** partial | Reconcile vs resume; no auto re-invoke; projection gaps NEG |

### 6.11 Roll-up counts (planning only)

Approximate planning split of the **57** user-goal rows (not A3 verdicts):

| Bucket | Approx. rows | Comment |
|--------|--------------|---------|
| Automatable product-runtime (DI and/or headless and/or FS/PROC) without visual pass required | ~35–40 | Includes many Wired / Wired-with-gap journeys |
| Automatable only as **NEG/Missing/blocked** honest outcomes | ~10–12 | AC-01/02, AS-01, MR-01, TC-02/03/08/09, positive AS-02/TP without bind UI |
| **UNVERIFIED-VIS** for some success conditions even if other observables pass | ~8–12 claims | FL-02 theme, layout paint, TextMate colors, debug gutter, TUI alt-screen, true hover, native dialogs |
| Explicit **OUT** of A3 product smoke | 1+ | FL-06 performance budgets |

Exact A3 scenario selection remains a later A3 implementation task; H0 only
classifies feasibility.

---

## 7. Limitations

1. **Headless package not integrated** — compatibility is version/TFM/API proven
   out-of-tree; in-repo lifetime + Semi.Avalonia + ReactiveUI DI bootstrap under
   headless is **not** runtime-proven in this note.
2. **`BuildAvaloniaApp` is desktop-oriented** — production uses
   `UsePlatformDetect` + `StartWithClassicDesktopLifetime`. Audit harness needs
   an audit-only builder (design §2.3), not a silent reuse of `Main`.
3. **Native folder/file dialogs** — not deterministic under headless without a
   storage-provider test double; post-picker seams are the product-runtime path.
4. **External tools** — `dotnet`, `csharp-ls`, `netcoredbg`, optional TUI
   binaries are host dependencies. Absence is BLOCKED for positive paths, not a
   harness bug.
5. **Backend bind UI missing** — positive agent/tools/multi-agent success paths
   remain BLOCKED; fake backends are test-only class, not product-runtime.
6. **Conversation flush on shutdown** — A2 restart slice: explicit shutdown may
   not dispose/flush `ConversationPersistenceService`; restart scenarios must
   document flush triggers (provider dispose vs host Exit).
7. **Visual claims** — non-desktop A3 cannot accept pixel/theme/TUI visual
   success; those stay UNVERIFIED-VIS.
8. **Existing composition tests contaminate** when run without disposable
   XDG (ISSUE-009). A3 must not “just run the test suite” as clean-profile
   proof.
9. **No package added in H0** — implementation will require an authorized
   session to add `Avalonia.Headless` (and optionally `.XUnit`) to a **new**
   audit harness project or test project, update `docs/LIBRARIES.md`, and keep
   production `src/Zaide.csproj` free of test-only packages unless deliberately
   chosen otherwise.
10. **Parallel scenario processes** — safe only with distinct profile roots;
    do not parallelize restarts that share a profile.

---

## 8. Blockers for A3 implementation (not H0 failures)

These block **full** A3 coverage or positive-path acceptance; they do **not**
make Avalonia.Headless incompatible.

| Blocker | Impact | Mitigation for A3 |
|---------|--------|-------------------|
| No user backend bind/configure UI | Positive AS-02, AC-02, TP-*, MR success paths | Record NEG/BLOCKED; do not fake product success |
| NetCoreDbg not provisioned | Positive DB-01 | Provision disposable host tool or accept NEG-only |
| csharp-ls not on PATH | Positive FN-08–14 | Install global tool or skip as BLOCKED |
| Native picker / visual-only claims | FL-02, some FL-01, DB gutter, TUI visuals | UNVERIFIED-VIS; separate visual acceptance if ever authorized |
| ConversationPersistence flush gap | TC-04 draft restore flaky if exit path wrong | Document and assert actual flush path |
| Headless lifetime not yet proven in-tree | Shell scenarios | First A3 implementation spike: single FL-01/SC-02 smoke under isolation |
| ISSUE-009 class in existing tests | Confusion if tests used as A3 | Keep A3 in dedicated process+profile; never rely on suite pollution |

**H0 non-blocker:** Avalonia.Headless **12.0.5** is available, TFM-compatible,
and dependency-compatible with the repo’s Avalonia pin.

---

## 9. Recommended next step (explicitly not started)

When an authorized session begins A3 (not this note):

1. **Spike S0** — new untracked or audit-only harness project; add
   `Avalonia.Headless` 12.0.5; prove one process with disposable XDG starts
   headless lifetime, constructs `MainWindow`, toggles bottom panel via
   command, writes evidence JSON, exits clean, deletes profile.
2. **Spike S1** — DI-only FL-03/FL-04 settings+secrets restart without headless.
3. Only after S0/S1, schedule journey scenario packs; keep Missing/NEG rows as
   first-class evidence.

**Do not** mark A3 complete, edit A2 verdicts, or open A4 from this note.

---

## 10. Working-tree impact of this H0 session

| Path | Action |
|------|--------|
| `docs/audits/v1-v3-product-reality/evidence/A3_AUTOMATION_READINESS.md` | **Created** (this file) |
| Production / tests / packages / other audit status docs | **Unchanged by H0 design intent** |

---

## 11. Status line

**A3-H0 Automation Readiness: complete as a docs-only design + compatibility
report.**

**A3 Clean-profile smoke: not started.**

**A3 acceptance: not claimed.**

**A4 / V4: not authorized.**

---

*Recorded 2026-07-31. Read-only readiness investigation; Avalonia.Headless
12.0.5 compatibility verified out-of-tree; no production edits, no harness
implementation, no commits or pushes.*
