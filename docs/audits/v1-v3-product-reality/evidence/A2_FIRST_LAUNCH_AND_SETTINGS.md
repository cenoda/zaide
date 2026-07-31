# A2 Wiring Audit — `A2_FIRST_LAUNCH_AND_SETTINGS`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_FIRST_LAUNCH_AND_SETTINGS` (eighth A2 slice; prior:
`A2_AGENT_SEND`, `A2_MULTI_AGENT_ROUTING`,
`A2_TRACE_MEMORY_USAGE_TERMINATION`, `A2_RESTART_RECOVERY_AND_CONTEXT`,
`A2_TOOLS_PERMISSIONS`, `A2_AGENT_CREATION_AND_BACKEND_ONBOARDING`,
`A2_TOWNHALL_AND_CONVERSATIONS`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`a2fe46259ef5c0233ca8b2b11c79b1cef9418e65` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `a2fe46259ef5c0233ca8b2b11c79b1cef9418e65` |
| `git rev-parse origin/master` | `a2fe46259ef5c0233ca8b2b11c79b1cef9418e65` |
| Working tree at audit start | Clean (`git status --short` empty) |
| Seven published A2 evidence files | Present (Agent Send, Multi-Agent Routing, Trace/Memory/Usage/Termination, Restart/Recovery/Context, Tools/Permissions, Agent Creation/Backend Onboarding, Townhall/Conversations) |
| This slice evidence file before write | Absent |
| A1 acceptance authority | [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md) (2026-07-30) |
| Production code modified | No |
| Tests modified | No |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` edited | No |
| Earlier evidence edited | No |
| Issues / deferred findings edited | No |
| Real user profile / settings / secrets read or written | No |
| App launched | No |
| Build or tests run | No |
| A3 executed | No |
| Commit / push | No |

**Safety boundary:** this slice is A2 wiring inspection only. Production
source is verdict authority. Tests and historical phase closeout documents
are corroboration only. Runtime rendering, keyboard delivery on a live
desktop, font metrics, permission bits on a real profile, and clean-profile
restart behavior are not claimed from source alone. **No real credentials,
profile paths, or secret-file contents were accessed.**

**Verdict rows (this slice only):** `A1-FL-01` … `A1-FL-06`. No new verdicts
for AS, MR, TC, TP, AC, TH, or XX rows.

---

## 2. Sources inspected

### 2.1 Documentation

- [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§1 First Launch and Settings;
  §17.8 A2 progress)
- [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- Published A2 evidence:
  - [A2_AGENT_SEND.md](./A2_AGENT_SEND.md)
  - [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md)
  - [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./A2_TRACE_MEMORY_USAGE_TERMINATION.md)
  - [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md)
  - [A2_TOOLS_PERMISSIONS.md](./A2_TOOLS_PERMISSIONS.md)
  - [A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)
  - [A2_TOWNHALL_AND_CONVERSATIONS.md](./A2_TOWNHALL_AND_CONVERSATIONS.md)
- V1 Phase 0: [PHASES.md §"Phase 0: Foundation & Layout"](../../../roadmap/PHASES.md#phase-0-foundation--layout);
  [phase-0 plan](../../../phases/v1/phase-0/IMPLEMENTATION_PLAN.md)
- V2 Phase 8 / 8.1: [V2.md §"Phase 8"](../../../roadmap/V2.md#phase-8--core-platform-and-settings);
  [Phase 8 plan](../../../phases/v2/phase-8/IMPLEMENTATION_PLAN.md) (D1–D4);
  [Phase 8.1 plan](../../../phases/v2/phase-8/phase-8.1/IMPLEMENTATION_PLAN.md)
- V2 Phase 13: [V2.md §"Phase 13"](../../../roadmap/V2.md#phase-13--release-hardening);
  [M5_RELEASE_CLOSEOUT_EVIDENCE.md](../../../phases/v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md);
  [M0_RELEASE_BASELINE_PROOF.md](../../../phases/v2/phase-13/M0_RELEASE_BASELINE_PROOF.md)
- Design tokens: [DESIGN.md](../../../DESIGN.md) §7 palette table

### 2.2 Production source (minimum required + supporting)

**Shell layout / bottom panel / keybindings**

- [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs),
  [MainWindow.axaml](../../../../src/App/Shell/MainWindow.axaml),
  [MainWindowViewModel.cs](../../../../src/App/Shell/MainWindowViewModel.cs)
- [MainLayoutBuilder.cs](../../../../src/App/Shell/MainLayoutBuilder.cs),
  [BottomPanelHost.cs](../../../../src/App/Shell/BottomPanelHost.cs),
  [ShellPanelNavigation.cs](../../../../src/App/Shell/ShellPanelNavigation.cs),
  [RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs),
  [NavBar.cs](../../../../src/App/Shell/NavBar.cs),
  [StatusBar.cs](../../../../src/App/Shell/StatusBar.cs),
  [StatusBarViewModel.cs](../../../../src/App/Shell/StatusBarViewModel.cs)
- [KeyBindingConverter.cs](../../../../src/App/Shell/KeyBindingConverter.cs),
  [CommandRegistry.cs](../../../../src/App/Composition/CommandRegistry.cs),
  [ICommandRegistry.cs](../../../../src/App/Composition/ICommandRegistry.cs)

**Theme / palette**

- [App.axaml](../../../../src/App/Composition/App.axaml),
  [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs),
  [Program.cs](../../../../src/App/Composition/Program.cs)
- [PaletteTokens.cs](../../../../src/UI/DesignSystem/PaletteTokens.cs),
  [TextStyles.cs](../../../../src/UI/DesignSystem/TextStyles.cs),
  [LayoutTokens.cs](../../../../src/UI/DesignSystem/LayoutTokens.cs),
  [TypographyTokens.cs](../../../../src/UI/DesignSystem/TypographyTokens.cs)

**Settings / secrets**

- Domain: [SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs),
  [SettingsValidator.cs](../../../../src/Features/Settings/Domain/SettingsValidator.cs),
  [SettingsLoadResult.cs](../../../../src/Features/Settings/Domain/SettingsLoadResult.cs),
  [SettingsMutationResult.cs](../../../../src/Features/Settings/Domain/SettingsMutationResult.cs),
  [SettingsSaveResult.cs](../../../../src/Features/Settings/Domain/SettingsSaveResult.cs),
  [SettingsSaveError.cs](../../../../src/Features/Settings/Domain/SettingsSaveError.cs)
- Contracts: [ISettingsService.cs](../../../../src/Features/Settings/Contracts/ISettingsService.cs),
  [ISecretStore.cs](../../../../src/Features/Settings/Contracts/ISecretStore.cs)
- Infrastructure: [SettingsService.cs](../../../../src/Features/Settings/Infrastructure/SettingsService.cs),
  [SettingsPathResolver.cs](../../../../src/Features/Settings/Infrastructure/SettingsPathResolver.cs),
  [SettingsSerializer.cs](../../../../src/Features/Settings/Infrastructure/SettingsSerializer.cs),
  [FileSecretStore.cs](../../../../src/Features/Settings/Infrastructure/FileSecretStore.cs),
  [SettingsMigrator.cs](../../../../src/Features/Settings/Infrastructure/SettingsMigrator.cs),
  [SettingsMigrationV1ToV2.cs](../../../../src/Features/Settings/Infrastructure/SettingsMigrationV1ToV2.cs),
  [SettingsMigrationV2ToV3.cs](../../../../src/Features/Settings/Infrastructure/SettingsMigrationV2ToV3.cs)
- Presentation: [SettingsViewModel.cs](../../../../src/Features/Settings/Presentation/SettingsViewModel.cs),
  [SettingsPanelView.cs](../../../../src/Features/Settings/Presentation/SettingsPanelView.cs),
  [SettingsPanelFactory.cs](../../../../src/Features/Settings/Presentation/SettingsPanelFactory.cs),
  [SettingsBinding.cs](../../../../src/Features/Settings/Presentation/SettingsBinding.cs),
  [SettingsPanelAttachHost.cs](../../../../src/App/Shell/SettingsPanelAttachHost.cs)
- DI: [SettingsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/SettingsServiceCollectionExtensions.cs)

**Editor / terminal settings consumers**

- [EditorView.cs](../../../../src/Features/Editor/Presentation/EditorView.cs),
  [EditorViewModel.cs](../../../../src/Features/Editor/Presentation/EditorViewModel.cs),
  [TerminalPanel.cs](../../../../src/Features/Terminal/Presentation/TerminalPanel.cs)

**LLM secret / env resolution (production Native Harness path)**

- [AgentExecutionService.cs](../../../../src/Features/Agents/Infrastructure/AgentExecutionService.cs)
  (`BuildEffectiveOptions`),
  [NativeHarnessProviderOptionsSource.cs](../../../../src/Features/Agents/Infrastructure/NativeHarnessProviderOptionsSource.cs),
  [NativeHarnessAgentBackend.cs](../../../../src/Features/Agents/Infrastructure/NativeHarnessAgentBackend.cs),
  [AgentExecutionOptions.cs](../../../../src/Features/Agents/Application/AgentExecutionOptions.cs),
  [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs)

**Phase 13 measurement (tools, not product UI)**

- [tools/phase13-measure.py](../../../../tools/phase13-measure.py),
  [tools/phase13-generate-large-file.py](../../../../tools/phase13-generate-large-file.py)

### 2.3 Tests (corroboration only; not verdict authority)

- Settings core / migration / secret store tests under
  `tests/Zaide.Tests/Features/Settings/`
- `FormatOnSaveTests`, `AgentExecutionServiceTests` (env/secret precedence)
- Shell: `SettingsPanelAttachHostTests`, command-registry acceptance tests
  for `view.toggleBottomPanel` / `Ctrl+Oem3`

---

## 3. Six-row verdict table

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-FL-01` | **Wired-with-gap** | Production cold-start constructs `MainWindow` with a multi-column shell (nav \| left Explorer/SC \| Townhall center \| editor right \| status bar) and a multi-mode bottom panel (Terminal / Problems / Output / Test Results / Debug). Bottom panel visibility toggles via `view.toggleBottomPanel` default gestures `Ctrl+Oem3` (canonical backtick / tilde key) and `Ctrl+J`, registered on `MainWindowViewModel` and materialized into window `KeyBindings`. Window title, size, and min size are set; OS decorations provide min/max/close by Avalonia default. **Gaps vs Phase 0 wording:** layout is no longer a simple 3-panel “left / center / right agent area” grid; right column is the editor (Agent Panel chrome retired — [A2_TOWNHALL_AND_CONVERSATIONS](./A2_TOWNHALL_AND_CONVERSATIONS.md) `A1-TH-04`); bottom panel is a full multi-surface host, not a placeholder. Runtime keyboard delivery and visual layout are A3. |
| `A1-FL-02` | **Wired-with-gap** | `App.axaml` requests `Dark`, includes `FluentTheme`, `Semi.Avalonia` `SemiTheme.axaml`, and AvaloniaEdit Fluent styles. Application resources define the current Navy IDE palette (`PrimaryAccent` `#066ADB`, `SurfaceBase` `#0A0F19`, etc.). Code-built surfaces resolve brushes via resource keys with `PaletteTokens` fallbacks matching those hex values. **Gaps:** historical goal name “Ayaka Violet” is superseded (roadmap/Phase 0 language); product palette is the Navy set in `App.axaml` / `DESIGN.md` §7; theme/palette is not user-selectable in settings; pixel-perfect match and Semi.Avalonia dark appearance are runtime-unproven without launch. |
| `A1-FL-03` | **Wired-with-gap** | Versioned immutable `SettingsModel` (production schema **v3**), `SettingsService` with sync load, validate, atomic temp→rename save, LKG (`settings.json.lastknowngood`), v1→v2→v3 migrations, and queued generation-aware writer. Path resolver uses `$XDG_CONFIG_HOME/zaide` (absolute) else `$HOME/.config/zaide` on Linux. User-reachable settings surface: status-bar “Zaide” / settings control → `ShowSettings` → full-content `SettingsPanelView` (Editor / Terminal / LLM). Apply/Discard/Rebase/Close wired. **Gaps:** `LoadResult` and `WriteErrors` have **no production UI subscriber** (corrupt/LKG/unsupported outcomes are silent to the user); `SettingsViewModel.ApplyAsync` treats any `SettingsMutationResult.Applied` as success and does **not** inspect nested `SettingsSaveResult.Failed`; schema is v3 not the goal-matrix “schema v1 initial” wording (migrations exist). Restart persistence is source-proven for write path but runtime-unproven without A3. |
| `A1-FL-04` | **Wired-with-gap** | Ordinary settings model stores only `Llm.ApiKeySource` (default `"secret-store"`), never a key string; API key UI field is held on `SettingsViewModel.ApiKey` and on Apply is written/deleted via `ISecretStore` key `"llm.apiKey"`. `FileSecretStore` persists separate `secrets.json` with Linux `0600` create + repair-on-load. Native Harness options resolve through `AgentExecutionService.BuildEffectiveOptions`: `AGENT_API_KEY` → secret store → empty (plus `AGENT_API_URL` / `AGENT_MODEL` for endpoint/model). **Gaps:** `ApiKeySource` is not user-editable in the settings panel; missing env + empty secret → unconfigured provider (no successful Native Harness completion — consistent with prior agent slices requiring bind + configured options); OS permission bits and absence of key plaintext in a real `settings.json` are runtime-unproven; real secret values were never read. |
| `A1-FL-05` | **Wired** | Editor defaults (code/prose fonts, size, tab size, insert spaces, whitespace/tabs/spaces visibility, format-on-save) are in `EditorSettings`, editable on the settings surface, validated, and persisted through `SettingsService`. `EditorView` applies settings on construction and on `WhenChanged` via `SettingsBinding` → `ApplyEditorSettings`. Terminal font family/size apply through `TerminalPanel` the same way. Format-on-save is consulted on `EditorViewModel` save when formatting service is composed. Source path for configure → apply → persist is complete; live pixel metrics remain A3. |
| `A1-FL-06` | **Wired-with-gap** | **Settings recovery** is product-wired (missing → defaults; corrupt → LKG then defaults; unsupported schema → LKG/defaults without overwriting bad primary; atomic write + LKG refresh). **Performance “measurable / locked budgets”** is Phase 13 **harness + historical closeout** (`tools/phase13-measure.py`, [M5_RELEASE_CLOSEOUT_EVIDENCE.md](../../../phases/v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md)) — not a user-reachable product surface and not a runtime-enforced budget gate inside the app. LSP/process/DAP lifecycle code remains composed in production (Phases 10–12), but this slice does **not** re-audit those journeys end-to-end; user-visible recovery status for settings load/write remains absent (see FL-03). Do not treat M5 PASS as current product A3 proof. |

Verdict definitions: [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

---

## 4. End-to-end production-path maps

Legend: **T** = type/contract · **R** = production DI · **C** = production caller ·
**U** = user-reachable · **P** = user-visible result/failure · **A3** = runtime
unproven without clean-profile smoke.

### 4.1 `A1-FL-01` — shell layout and bottom panel toggle

```text
[cold start]
  Program.Main → BuildAvaloniaApp → App.OnFrameworkInitializationCompleted
  → resolve MainWindowViewModel + MainWindow(settings, secrets, registry, …)
  → MainLayoutBuilder.Build
       columns: Nav(40) | Left(260) | splitter | Townhall(*) | splitter | Editor(*)
       rows: content | bottom-splitter(0) | bottom(0) | status(24)
  → BottomPanelHost.AttachToLayoutGrid (Terminal/Problems/Output/Test/Debug)
  → RightColumnHost (editor tab bar + search + EditorView; no Agent Panel chrome)

[toggle bottom panel]
  user Ctrl+Oem3 / Ctrl+J  OR  shell mode buttons that force visible
  → CommandRegistry resolves view.toggleBottomPanel
  → MainWindow KeyBindings (MaterializeRegistryBindings)
  → ToggleBottomPanelCommand → ShellPanelNavigation
  → IsBottomPanelVisible flip
  → BottomPanelHost.ApplyBottomPanelVisibility (row heights 0 ↔ 4+250)
```

| Layer | Status | Evidence |
|-------|--------|----------|
| 1. Type / contract | Present | `BottomPanelMode`, `ToggleBottomPanelCommand`, layout hosts |
| 2. DI / composition | Present | `App.axaml.cs` constructs `MainWindow` with VM; layout built in window ctor |
| 3. Production caller | Present | Registry registration [MainWindowViewModel.cs](../../../../src/App/Shell/MainWindowViewModel.cs) L299–300; materialization [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs) L304–313, L373–398; visibility [BottomPanelHost.cs](../../../../src/App/Shell/BottomPanelHost.cs) L175–190 |
| 4. User reachability | **Yes (keyboard + chrome)** | Default gestures; bottom mode strip buttons force mode + visible ([ShellPanelNavigation.cs](../../../../src/App/Shell/ShellPanelNavigation.cs) L35–59; [BottomPanelHost.cs](../../../../src/App/Shell/BottomPanelHost.cs) L48–76) |
| 5. Result visibility | Source-proven row-height + `IsVisible` | Actual desktop paint A3 |
| 6. Window chrome | Title/size/min set L114–119 | System min/max/close via default Avalonia decorations; not custom title-bar code |

**Layout evolution note (not Missing):** Phase 0 promised left / center / right-agent.
Current product: left (Explorer/SC) / Townhall center / editor right, with bottom
multi-panel and full-height nav. Agent DM workflow is Townhall-only
([A2_TOWNHALL_AND_CONVERSATIONS](./A2_TOWNHALL_AND_CONVERSATIONS.md)).

### 4.2 `A1-FL-02` — theme and palette

```text
[cold start]
  App.axaml:
    RequestedThemeVariant="Dark"
    Styles: FluentTheme → SemiTheme.axaml → AvaloniaEdit Fluent
    Resources: Navy palette Color + SolidColorBrush keys
  code UI: Application.Current.Resources["…"] or PaletteTokens fallbacks
```

| Layer | Status | Evidence |
|-------|--------|----------|
| Theme composition | Present | [App.axaml](../../../../src/App/Composition/App.axaml) L5, L68–71 |
| Palette definition | Present (Navy) | L13–41 resource keys; [PaletteTokens.cs](../../../../src/UI/DesignSystem/PaletteTokens.cs) fallbacks |
| User theme switcher | **Absent** | [SettingsModel](../../../../src/Features/Settings/Domain/SettingsModel.cs) has Editor/Llm/Keybindings/Debug only |
| “Ayaka Violet” name | Historical only | Phase 0 / V1 roadmap language; live resources are Navy IDE |

### 4.3 `A1-FL-03` — settings schema, load/save, surface, recovery

```text
[DI]
  AddZaideSettings → ISettingsService=SettingsService, ISecretStore=FileSecretStore,
                     ISettingsPanelFactory

[construct SettingsService]
  resolve paths → TryLoadFrom(settings.json)
    Missing → Defaults
    Corrupt/IO → LKG then Defaults (LoadResult.Corrupt)
    UnsupportedVersion → LKG without overwriting result classification
    Loaded → migrate v1→v2→v3 → Current; refresh LKG
  start writer loop

[user open settings]
  StatusBar settings button → StatusBarViewModel.OpenSettingsCommand
  → MainWindowViewModel.ShowSettings Interaction
  → SettingsPanelAttachHost.ShowPanel
  → SettingsPanelFactory.Create(settings, secrets)
  → SettingsPanelView overlay (columns 0–5, rows 0–2)

[user Apply]
  SettingsViewModel.ApplyAsync
  → ISettingsService.ApplyAsync(base, candidate)  [validate + commit + queue write]
  → on Applied: secret Set/Delete for llm.apiKey
  → in-memory WhenChanged → EditorView / TerminalPanel / keybinding refresh
```

| Concern | Source-proven behavior | User-visible? |
|---------|------------------------|---------------|
| Schema version | Defaults `SchemaVersion: 3`; migrations v1→v2 (FormatOnSave), v2→v3 (Debug breakpoints) | No schema banner in UI |
| Path | [SettingsPathResolver.cs](../../../../src/Features/Settings/Infrastructure/SettingsPathResolver.cs) L17–57 | Not displayed |
| Atomic write | temp → `File.Move` overwrite; LKG updated ([SettingsService.cs](../../../../src/Features/Settings/Infrastructure/SettingsService.cs) L342–347) | No |
| Validation errors | `SettingsValidator` → panel `_errors` | **Yes** (panel) |
| Concurrent edit conflict | `ApplyAsync` Conflict → Rebase path | **Yes** (panel conflict text) |
| Disk write failure | `SettingsSaveResult.Failed` + `WriteErrors` | **No UI** — Apply still returns true on `Applied` regardless of `SaveResult` ([SettingsViewModel.cs](../../../../src/Features/Settings/Presentation/SettingsViewModel.cs) L111–132) |
| Corrupt load | LKG/defaults in memory | **No UI** for `LoadResult` |

### 4.4 `A1-FL-04` — secret boundary and env fallback

```text
[settings.json shape]
  Llm: { baseUrl, model, apiKeySource }   // no key value field

[secrets.json]
  FileSecretStore Get/Set/Delete  key "llm.apiKey"
  Linux: UnixCreateMode 0600 on temp; repair non-0600 on load

[effective Native Harness options — per call]
  AGENT_API_URL  ?? settings.Llm.BaseUrl
  AGENT_MODEL    ?? settings.Llm.Model
  AGENT_API_KEY  ?? secrets.Get("llm.apiKey") ?? ""
  → NativeHarnessProviderOptionsSource.ResolveOptions
  → IsConfigured requires non-empty ApiKey + BaseUrl + Model
```

| Claim | Source status |
|-------|---------------|
| Key not in settings model / serializer shape | **Proven** — no API-key property on `SettingsModel` / `LlmSettings` |
| Separate secret file | **Proven** — `GetSecretsPath()` → `secrets.json` |
| Restricted permissions (Linux) | **Source-proven intent** — create + repair 0600; runtime bit check A3 |
| Env fallback | **Proven** for `AGENT_API_KEY` / `AGENT_API_URL` / `AGENT_MODEL` |
| Settings UI stores key to secret store | **Proven** — Apply Set/Delete |
| User can configure credentials | **Yes** via Settings LLM API Key field (password char) |
| Missing env + no secret | Empty key → Native Harness unconfigured ([NativeHarnessAgentBackend.cs](../../../../src/Features/Agents/Infrastructure/NativeHarnessAgentBackend.cs) L208–211) |

**Prior A2 intersection:** [A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)
already recorded the same options chain as provider configuration, not as
actor↔backend binding. This slice confirms the secret boundary itself.

**Not inspected:** any real `~/.config/zaide/*`, environment values, or secret
JSON contents.

### 4.5 `A1-FL-05` — editor defaults configure + persist + apply

```text
SettingsPanelView Editor section
  → SettingsViewModel SetCodeFont* / SetTabSize / SetInsertSpaces / …
  → Apply → SettingsService.Current.Editor
  → WhenChanged
       → EditorView.ApplyEditorSettings (font, size, indent, whitespace)
       → TerminalPanel.ApplyTerminalSettings (terminal font/size)
  → on save: EditorViewModel.TryFormatOnSaveAsync if FormatOnSave
```

| Preference | Settings surface | Live consumer |
|------------|------------------|---------------|
| Code font family/size | Yes | `EditorView` |
| Prose font family | Yes | `EditorView` (`.md`) |
| Tab size / insert spaces | Yes | AvaloniaEdit options |
| Show whitespace/tabs/spaces | Yes | AvaloniaEdit options (tabs/spaces gated by show-whitespace) |
| Format on save | Yes | `EditorViewModel` save path |
| Terminal font family/size | Yes (Terminal section) | `TerminalPanel` |

### 4.6 `A1-FL-06` — performance budgets vs production recovery

| Dimension | What exists | Product-reachable? |
|-----------|-------------|--------------------|
| Measurement harness | `tools/phase13-measure.py` (explicitly “not application instrumentation”) | No — developer/opt-in tooling |
| Locked budgets + PASS | [M5_RELEASE_CLOSEOUT_EVIDENCE.md](../../../phases/v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md) historical samples | Documentation evidence, not live gate |
| Settings recovery | `SettingsService` LKG/defaults/atomic write | Silent recovery (no user status) |
| LSP / process / DAP recovery | Services remain in `src/Features/Language`, `ProjectSystem`, `Debugging` | Own journeys; not fully re-mapped here; Phase 13 M3 inventories were test/evidence-only |

**Attribution:** “locked budgets pass” is **closeout/harness evidence**, not a
source-proven continuous product monitor. A2 records that distinction rather
than re-running measurements.

---

## 5. User reachability matrix

| Goal | User entry (source) | Reachable without DI trivia? |
|------|---------------------|------------------------------|
| Layout | Launch app (main window is desktop `MainWindow`) | **Yes** |
| Bottom panel toggle | `Ctrl+Oem3` / `Ctrl+J` (if keyboard routing works); bottom mode strip | **Yes** (source); key delivery A3 |
| Theme | Launch only | **Yes** (fixed composition) |
| Settings open | Status bar settings / brand control | **Yes** |
| Change editor defaults | Settings panel Editor section + Apply | **Yes** |
| Change LLM endpoint/model | Settings panel LLM section + Apply | **Yes** |
| Store API key | Settings API Key field + Apply → secret store | **Yes** |
| Env-only credential | Process environment before launch (`AGENT_API_*`) | **Yes** (ops path; not UI) |
| Theme switcher | — | **No** |
| View load/write recovery status | — | **No** |
| Run performance budgets | — | **No** (tooling only) |

DI registration alone is not treated as reachability (per audit rule).

---

## 6. Source-proven vs runtime-unproven

| Claim class | Source-proven in this slice | Runtime-unproven (A3 / out of scope) |
|-------------|----------------------------|--------------------------------------|
| Layout tree composition | Yes | Visual proportions, splitter feel |
| Toggle command wiring | Yes | Actual `Ctrl+\`` key event on user desktop/layout |
| Semi + dark + palette resources | Yes | Pixel colors, Semi control chrome appearance |
| Settings load/save/migrate algorithms | Yes | File on disposable profile after restart |
| Secret not in settings model | Yes | Grep of real user `settings.json` |
| Linux 0600 secret file code | Yes | Actual mode after Apply on disk |
| Editor/terminal live binding | Yes | Fonts installed, visual whitespace |
| Env → secret precedence code | Yes | Live send success/failure ([A2_AGENT_SEND](./A2_AGENT_SEND.md) still applies) |
| Phase 13 budget numbers | Historical docs only | Not remeasured here |

---

## 7. Contradiction / reconciliation notes

1. **Phase 0 “3-panel + right agent area” vs current shell**
   Product truth is nav + left + Townhall + editor + bottom multi-panel. Agent
   Panel chrome retirement is consistent with [A2_TOWNHALL](./A2_TOWNHALL_AND_CONVERSATIONS.md)
   `A1-TH-04`. Verdict is **Wired-with-gap**, not Missing.

2. **“Ayaka Violet” vs Navy palette**
   Goal matrix retains Phase 0 name. Live resources and `DESIGN.md` §7 use the
   Navy IDE palette hex set. Audit judges against **live composition**.

3. **Goal “schema v1 initial” vs production schema v3**
   v1 was the initial ship; production defaults and migrations are at v3. The
   versioned schema + migration chain still satisfies the spirit of the promise;
   wording lag is noted, not treated as Missing.

4. **`LoadResult` / `WriteErrors` vs Phase 8.1 contract**
   Interface and service implement recovery and error observables; the settings
   panel does not surface load/write outcomes. Parallel pattern to conversation
   persistence silence noted in [A2_RESTART_RECOVERY_AND_CONTEXT](./A2_RESTART_RECOVERY_AND_CONTEXT.md)
   `A1-TC-04`.

5. **Apply success vs disk failure**
   In-memory commit can succeed while `SettingsSaveResult.Failed` returns nested
   under `Applied`. UI does not branch on `SaveResult` → user may believe
   persistence succeeded when only memory did.

6. **`DESIGN.md` vs `App.axaml` hex drift**
   Some `DESIGN.md` surface colors (e.g. `SurfacePanelBrush` table) differ from
   `App.axaml` resource values. **Runtime authority is `App.axaml` / code
   resource lookup.** Not a wiring absence.

7. **FL-06 vs Phase 13 M5 “GO”**
   M5 closed Release Hardening with honest environment limits. A2 does not
   re-certify budgets; it separates harness/closeout from product wiring and
   user-visible recovery.

---

## 8. A3 constraints only (not executed)

A3 for this journey **must** use a disposable isolated profile
(`XDG_CONFIG_HOME` absolute temp directory established **before** process
start). Never the real user profile, real `settings.json`, or real
`secrets.json`.

Suggested disposable-profile scenarios (description only):

1. **Cold launch layout:** confirm multi-column shell, status bar, bottom panel
   hidden initially; `Ctrl+\`` / `Ctrl+J` toggles bottom panel; mode strip
   switches Terminal/Problems/… .
2. **Theme:** visual dark Semi/Fluent chrome; sample palette swatches against
   `App.axaml` hex (not “Ayaka Violet” name).
3. **Settings persistence:** open status-bar settings; change code font size;
   Apply; kill app after write completes; restart with same disposable
   `XDG_CONFIG_HOME`; observe editor font size.
4. **Corrupt recovery:** with app stopped, corrupt `settings.json` while keeping
   a valid LKG (or both corrupt); relaunch; observe defaults/LKG **behavior**
   (expect silent recovery — no status UI).
5. **Secrets boundary:** enter a **throwaway** API key in Settings; Apply; assert
   disposable `settings.json` has no key plaintext; assert `secrets.json` mode
   `0600` on Linux; clear key; optional `AGENT_API_KEY` env-only path. Never
   print real credentials.
6. **Editor defaults:** toggle show whitespace / tab size; open file; confirm
   editor options; Format on Save only with disposable C# project if testing
   formatting.
7. **FL-06:** do **not** treat Phase 13 harness as A3 product smoke unless the
   A3 charter explicitly includes remeasurement; settings recovery is the
   product-reachable recovery path for this slice.

Production DI is allowed only when disposable config root is set first
(same isolation rule as Townhall A2 notes / ISSUE-009 class).

**A3 is not executed in this session.**

---

## 9. Next recommended A2 slice

**Next recommended A2 slice:** `A2_WORKSPACE_AND_PROJECT_OPENING`

| Item | Value |
|------|-------|
| Slice name | `A2_WORKSPACE_AND_PROJECT_OPENING` |
| Goal rows | `A1-WO-01` … `A1-WO-03` |
| Evidence file | `docs/audits/v1-v3-product-reality/evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md` |
| Status in this session | **Explicitly not started** — file not created; no WO verdicts assigned |

Rationale: matrix journey order after First Launch is Workspace / Project
Opening; natural dependency for later FN/LSP/debug slices.

---

## 10. Verification and working-tree closeout

### 10.1 Required content checklist

| Required section | Present |
|------------------|---------|
| 1. Audit identity, baseline, safety | Yes |
| 2. Sources inspected | Yes |
| 3. Six-row verdict table (each FL id once) | Yes |
| 4. Production-path maps | Yes |
| 5. User reachability | Yes |
| 6. Source-proven vs runtime-unproven | Yes |
| 7. Contradiction / reconciliation | Yes |
| 8. A3 constraints only | Yes |
| 9. Next slice explicitly not started | Yes |
| 10. Verification closeout | Yes |

### 10.2 Truth-constraint self-check

| Constraint | Honored? |
|------------|----------|
| DI registration ≠ user reachability | Yes |
| Tests / phase closeouts ≠ production wiring proof | Yes |
| Historical budget PASS ≠ live product gate | Yes |
| No real secrets/profile access | Yes |
| Prior-slice verdicts not reassigned | Yes |
| Each of `A1-FL-01`…`A1-FL-06` exactly once in primary table | Yes |
| No runtime claims from source alone | Yes |
| No production code / AUDIT_PLAN / GOAL_MATRIX edits | Yes |

### 10.3 Closeout verification commands (post-write)

Executed after writing this file only:

- Confirm exactly one untracked evidence file:
  `docs/audits/v1-v3-product-reality/evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md`
- Confirm no tracked modifications
- Whitespace check for the **untracked** file:

  ```bash
  git diff --no-index --check /dev/null \
    docs/audits/v1-v3-product-reality/evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md
  ```

  Exit status **1 is expected** because the files differ; there must be
  **no whitespace-diagnostic output**.
- Relative Markdown paths and fragment links resolve against this tree
- Primary verdicts: `A1-FL-01` Wired-with-gap; `A1-FL-02` Wired-with-gap;
  `A1-FL-03` Wired-with-gap; `A1-FL-04` Wired-with-gap; `A1-FL-05` **Wired**;
  `A1-FL-06` Wired-with-gap
- `A2_WORKSPACE_AND_PROJECT_OPENING` not created / not started

---

*End of `A2_FIRST_LAUNCH_AND_SETTINGS` evidence. Stop for re-audit. No commit or push.*
