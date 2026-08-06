# F5 — Agent / transparency config in Settings

One-page plan for moving durable agent and transparency configuration out of
Townhall inspection chrome into the Settings window. **No open-flag changes
(F1), no empty-chrome rework (F3), no F7/F8.**

## Decision: Agents section after LLM

| Surface | Choice | Why |
|---------|--------|-----|
| **Section name** | **Agents** | Covers capture defaults, ACP non-secret identity, and context policy without splitting LLM endpoint (already in LLM). |
| **Placement** | After **LLM**, before button bar | Matches existing vertical stack in `SettingsPanelView`; left-aligned 520px column (F13). |
| **Page size** | User-configurable in Settings | Replaces hardcoded `DefaultPageSize` / `MaxPageSize` on the management VM; trace paging caption dropped from Townhall when not in full chrome. |

## Schema v4 + migration

| Item | Approach |
|------|----------|
| **Version** | `SettingsModel.SchemaVersion` **3 → 4** |
| **New block** | `AgentsSettings` on root: `traceCaptureEnabled`, `usageCaptureEnabled`, `tracePageSize`, `traceMaxPageSize`, `acpExecutablePath`, `acpArguments`, `acpExpectedAgentName`, `acpExpectedAgentVersion`, `defaultContextPolicyLevel` |
| **Migration** | `SettingsMigrationV3ToV4`: preserve Editor / LLM / Keybindings / Debug; add `AgentsSettings.Default` |
| **Defaults** | Trace/usage capture **off**; page size **64** / max **256**; ACP strings empty; context policy **Standard** |
| **Secrets** | No secret slots; `SettingsValidator` rejects secret-shaped `acpArguments` (bearer, api-key, token=, etc.) |
| **Proof test** | `Phase23SettingsAgentsTests.Migration_V3ToV4_PreservesExistingAndFillsAgentDefaults` loads representative v3 JSON via `SettingsService`, asserts v4 shape + every default |

## Inventory (TOFIX §F5)

### Move to Settings

| Item | Source today | Settings home |
|------|--------------|---------------|
| Trace capture default | Trace panel toggle | Agents → Enable trace capture |
| Usage capture default | Usage panel toggle | Agents → Enable usage capture |
| Trace page size / max | VM constants + paging caption | Agents → Trace page size / max |
| ACP executable, args, expected name/version | `AgentBackendBindingPanel` text boxes | Agents → ACP fields |
| Application-default context policy | `AgentContextApplicationDefault.Standard` | Agents → Default context policy |
| LLM model / URL / API key | Already Settings | Unchanged (LLM section) |

### Keep in Townhall

| Item | Home |
|------|------|
| Open / list / Refresh / Retry / Close | Evidence surfaces |
| Memory CRUD + draft | Memory surface |
| Bind / Unbind / End session / Probe / Auth / Logout | DM backend strip (session) |
| Session context override + clear | DM policy selector |
| Empty evidence policy copy (once) | Status / summary (F2) |

## Deep-link strategy

| Affordance | Path |
|------------|------|
| Status bar Settings segment | `StatusBarViewModel.OpenSettingsCommand` → `MainWindowViewModel.ShowSettings` → `SettingsPanelAttachHost.HandleShowSettings` |
| Trace / Usage capture-off empty state | `Open Settings` button → `OpenSettingsRequested` → `TownhallView` → `MainWindow.axaml.cs` same `ShowSettings.Handle` |
| Backend binding ACP summary | `Open Settings` on binding panel → same `OpenSettingsRequested` chain |

**One route only** — no second navigation service or command id.

## Runtime wiring

- `AgentTransparencySettingsSync` (singleton): on construct + `ISettingsService.WhenChanged`, applies capture flags to trace/usage sinks and refreshes availability projections.
- `AgentTransparencyManagementViewModel`: reads page sizes from `ISettingsService.Current.Agents`; removes panel capture toggle commands.
- `AgentSessionService`: application-default context policy from settings, not hardcoded static.
- `TownhallViewModel`: ACP bind reads durable fields from settings; binding panel shows read-only prefilled values.

## Per-file changes (four commits)

### (a) Schema + migration + service

| File | Change |
|------|--------|
| `SettingsModel.cs` | Add `AgentsSettings`; bump defaults to v4 |
| `SettingsMigrationV3ToV4.cs` | New migration |
| `SettingsSerializer.cs` | Accept schema ≤ 4 |
| `SettingsValidator.cs` | Agents field validation + secret-shaped args guard |
| `SettingsService.cs` | Register v3→v4 in production chain |

### (b) Settings UI + DI sync

| File | Change |
|------|--------|
| `SettingsViewModel.cs` | Agents setters + merge |
| `SettingsPanelView.cs` | Agents section controls |
| `AgentTransparencySettingsSync.cs` | New sync service |
| `AgentsServiceCollectionExtensions.cs` | Register sync; session service gets `ISettingsService` |
| `AgentSessionService.cs` | Default policy from settings |
| `AgentTransparencyManagementViewModel.cs` | Page size from settings |

### (c) Townhall chrome removal + deep-link

| File | Change |
|------|--------|
| `AgentTracePanel.cs` / `AgentUsagePanel.cs` | Remove capture toggles; capture-off status copy |
| `AgentTraceAvailabilityState.cs` / `AgentUsageAvailabilityState.cs` | Capture-off status text |
| `AgentBackendBindingPanel.cs` | Read-only ACP display + Open Settings |
| `TownhallView.cs` | Drop panel→VM ACP edits; wire binding Open Settings |
| `TownhallViewModel.cs` | `ISettingsService` for ACP bind + draft sync |
| `TownhallServiceCollectionExtensions.cs` | Pass `ISettingsService` |

### (d) Tests + docs

| File | Change |
|------|--------|
| `Phase23SettingsAgentsTests.cs` | Migration, round-trip, secrets guard |
| `Phase23F5TownhallConfigTests.cs` | Chrome removal, deep-link, binding read-only |
| `Phase22*Tests.cs` | Capture via settings instead of panel toggles |
| `TOFIX.md` | Mark F5 done |
| `DF-006-more-settings-options.md` | Close with commit hash |

## Non-goals

- F7 bottom-panel IA, F8 image preview, F3 follow-up chrome changes.
- New `ISecretStore` slots for ACP.
- Second `ShowSettings` entry point or section deep-link targets.
- Schema changes beyond v3→v4 migration.
- Product-readiness claims.
