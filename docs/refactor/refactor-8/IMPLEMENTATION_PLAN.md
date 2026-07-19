# Refactor 8: Townhall and Conversation UI Foundation — Implementation Plan

## Status and authorization

**Refactor 8 status:** **M6 accepted (2026-07-19).** M0 planning gate accepted;
M1 token baseline implemented; M2 bottom-panel host extracted and accepted;
M3 right-column host extracted and accepted at `73fc66c` (status docs `19bb674`);
M4 main layout builder extracted and accepted at `b3c8684` (status docs `09ccde9`);
M5 settings attach and overlay focus wiring extracted and accepted at `d947efa`;
M6 Townhall presentation maintainability cleanup accepted at `1ca1e08` (smoke docs `3b40af8`).
M7+ unauthorized until explicit authorization.

**Production and test code must not change under M0.** M0 is documentation-
only. **M1 and later milestones are unauthorized** until a human explicitly
accepts this plan and authorizes the next named milestone only.

**Do not start Phase 14.** Phase 14 remains a separate feature phase with its
own later M0. Refactor 8 must not implement unified DM navigation, Agent Panel
retirement, persistence, privacy UX, or any Phase 14 product behavior.

**Dependency status:**

| Predecessor | Status | Evidence |
|-------------|--------|----------|
| Refactor 6.1–6.3 | Accepted and closed | Feature-first tree, composition modules, lifetime map, shell factories |
| Refactor 7 | Complete and closed at `a7d2887` (docs closeout `945f0e7`) | Typed Actor/Conversation domain, run correlation, projection cutover, attribution fix, ownership ratchets |
| Phase 14 | Unauthorized | V3 §18 candidate only; not this plan |

**Roadmap source:** V3 §16.4 (Townhall and Conversation UI Foundation),
R61-V15 / R61-V17 deferred shell extraction, DF-001 revisit notes (seams only).

---

## M0 live-code audit (post–Refactor 7)

Audit baseline: **`945f0e7`** (`master`, clean working tree; Refactor 7 M7
accepted and refactor closed). Domain/orchestration after Refactor 7 is treated
as a frozen behavioral surface for this UI refactor.

### Current ownership and UI composition path

| Concern | Live owner / path | M0 finding |
|---------|-------------------|------------|
| Shell window construction | `MainWindow.axaml.cs` (**995** lines); `MainWindow.axaml` is a 7-line stub | **R61-V15 residual.** Layout, bottom mode strip, editor/agent column, settings overlay attach, keybinding materialization, and a large `WhenActivated` block still live in one imperative file. Refactor 6.3 narrowed DI/composition pressure but did not extract visual construction. |
| Shell layout grid | `MainWindow.BuildLayout()` (~lines 510–854) | Builds nav / left panel / Townhall / editor+agent / bottom panel / status bar with inline splitters, magic widths (40/260/4/star), and feature view construction. |
| Shell activation wiring | `MainWindow` `WhenActivated` (~lines 163–415) plus `MainWindowActivationHost` (ViewModel-side, 242 lines) and `ShellPanelNavigation` (75 lines) | 6.3 already extracted **ViewModel** activation and panel-mode commands. **View-side** host wiring (set ViewModels, editor tabs, bottom visibility, palette/search focus, panel send event) remains in `MainWindow`. |
| Settings panel lifecycle | `MainWindow` (`ShowSettingsPanel` / attach / detach / focus restore) via `ISettingsPanelFactory` | Factory ownership is 6.3-correct; view attach/detach still shell-imperative. Extraction may move attach helpers; do not redesign Settings UX. |
| Townhall composite | `TownhallView` (323) + `TownhallChannelPanel` (146) + `TownhallPeoplePanel` (168) + `TownhallChatPanel` (208) + `TownhallInputArea` (247) + `TownhallAvatarFactory` (124) | Already componentized into four sub-panels. Residual debt is token/hardcode consistency, filter chrome, and lack of Phase-14-ready internal seams (not navigation). |
| Townhall presentation model | `TownhallViewModel` (408), `TownhallState`, `TownhallMessage`, `TownhallEntryProjection` (internal) | Refactor 7 made typed entries authoritative and kept compatibility projections. **R8 must not re-open domain ownership, mirror attribution, or string protocol deletion.** |
| Agent panel surface | `AgentPanelHostView` (424), `AgentPanelView` (191), `AgentPanelHost` (178) | Dedicated panel remains visible. Host view owns tab strip + content swap. Presentation already projects typed conversation output (R7 M5b). R8 may extract UI structure only; must not retire the panel or add Townhall DM nav (DF-001 / Phase 14). |
| Design tokens | `src/UI/DesignSystem/{LayoutTokens,TextStyles,Icons.axaml}` + palette in `App.axaml` | Tokens exist and are partially adopted. Live violations remain: `FontSize =` literals in shell/bottom strip, Agent panel, Command palette, etc.; ad-hoc `Color.FromArgb` / `Color.Parse` fallbacks in Townhall and shell chrome. |
| Shared UI root | `src/UI/Shared/` | **Deny-by-default** (architecture `RootFolderAdmission`). Empty and unadmitted. R8 may only admit types that meet CONVENTIONS multi-consumer rules with an explicit architecture entry in the same milestone. Prefer shell-local or DesignSystem homes first. |
| Shell chrome already extracted | `NavBar`, `StatusBar`, `CommandPaletteOverlay`, `Animations`, `IconFactory`, `GridLayoutResizeHelper`, `FinalWindowCleanup` | Prior refactors moved some chrome. Do not re-home `Animations`/`IconFactory` (R62-D03 shell-owned) unless a later accepted decision says otherwise. |
| Public surface baseline | Architecture visibility ratchet | **339** public / **104** internal / **443** total top-level production types after R7 M7. R8 defaults new extractees to `internal`. |
| Orchestration (out of R8 behavior scope) | `AgentTownhallMirrorCoordinator`, Agents application/domain, Conversations store | Closed by Refactor 7. R8 may touch only if a pure move requires an import/path update; **no behavior change**. |

### Live structural measurements (audit-time)

| Surface | Path | LOC (approx.) |
|---------|------|---------------|
| Main shell view | `src/App/Shell/MainWindow.axaml.cs` | 995 |
| Main shell VM | `src/App/Shell/MainWindowViewModel.cs` | 393 |
| Shell activation host (VM-side) | `src/App/Shell/MainWindowActivationHost.cs` | 242 |
| Townhall view tree | `src/Features/Townhall/Presentation/*.cs` | ~1,752 total |
| Agent panel presentation | `src/Features/Agents/Presentation/*.cs` | ~847 total |
| Design system | `src/UI/DesignSystem/*` | 175 total |

### Current behavior that must remain observable

These are behavior-preservation boundaries for every R8 milestone:

1. **Shell layout geometry** — column order and roles remain:
   nav (fixed ~40) | left panel (~260, min 180, max 320) | Townhall (dominant star) | editor+agent (secondary star) | status bar (24px). Splitter interaction and star-column normalization (`GridLayoutResizeHelper`) keep current resize behavior.
2. **Left panel modes** — Explorer vs Source Control visibility and slide animation remain; SC refresh-on-switch remains.
3. **Bottom panel** — Terminal / Problems / Output / Test Results / Debug mode strip, show/hide row heights, default heights, and focus/start routing for Terminal remain.
4. **Townhall** — channel list, people roster, chat grouping/headers/timestamps, filter All/Chat/Activity, input Enter/newline, send affordance, seeded channels/agents, and exact projected message content/prefixes remain.
5. **Agent Panel** — multi-tab host, create/close/activate, draft, busy/idle/error presentation, send keybindings, and projected `OutputHistory` / typed entry rendering remain. Closing a panel still does not cancel in-flight work (existing contract).
6. **Public mirror** — implicit public Townhall mirror of agent sends remains visible (Phase 14 privacy change only). Attribution stays admission-captured (R7 M6).
7. **Settings overlay** — open/close, attach/detach, return focus, and factory construction path remain.
8. **Command palette / search / keybindings** — registry-driven bindings, palette open/dismiss focus restore, and search bar focus behavior remain.
9. **Accessibility / input** — keyboard paths that currently work (panel send, Townhall send, palette, settings, bottom modes) must not regress. No redesign of focus rings or semantics beyond preserving existing controls.
10. **Visual identity** — Discord-like Townhall composition and current palette remain. No “Zaide final design” restyle, no glass redesign, no new chrome language.

### Explicit non-behaviors (must not appear)

- Visible direct-conversation / DM navigation in Townhall.
- Agent Panel retirement or hiding behind a flag.
- Removal or conditional disabling of the public agent→Townhall mirror.
- New conversation kinds, unread badges, draft migration UI, or persistence.
- Domain type redesign (`Conversation`, `Actor`, run model) or new DI scopes
  (`ConversationScope`, `AgentSessionScope`).
- Speculative Phase 14 contracts “for later” without a live R8 consumer.
- Assembly split, new NuGet packages, or root `Infrastructure/` admission.

### M0 UI foundation decisions

| Decision | Locked Refactor 8 rule |
|----------|------------------------|
| Refactor class | Behavior-preserving structural UI refactor. Observable product behavior stays at the R7 closeout surface except for documented incidental fixes that are **not** authorized by default. Prefer documenting residual visual debt over “while we are here” polish. |
| Primary debt owner | **R61-V15** — extract `MainWindow.axaml.cs` imperative construction into maintainable shell components. |
| Secondary debt | Townhall/Agent presentation consistency with DesignSystem tokens; clarify token/primitive usage without shipping a redesign. |
| Extraction home | Shell layout extractees stay under `src/App/Shell/` unless a type is proven feature-neutral and multi-consumer, in which case DesignSystem (tokens) or a **reviewed** `UI/Shared` admission applies. Townhall extractees stay under `Features/Townhall/Presentation`. Agent extractees stay under `Features/Agents/Presentation`. |
| XAML policy | DESIGN.md §1 remains: C# construction is default. Do not convert Townhall/shell to large XAML views. Tier-1/2 resource dictionaries only. |
| Token policy | Prefer `LayoutTokens`, `TextStyles`, and `App.axaml` resource keys. Replacing hardcoded hex/`FontSize` with **existing** tokens is in scope when pixel-identical. Introducing new token keys is allowed only when they capture an already-rendered value (no visual change) and are documented in the milestone. |
| Visibility | New production types default to `internal`. Public baseline stays at 339/104/443 unless a framework activation requirement forces a same-milestone baseline update with rationale. |
| Architecture ratchets | RootFolderAdmission, NamespaceDirection, LocatorSite, Refactor7 ownership ratchets, and public visibility baseline must stay green every milestone. |
| DF-001 | R8 prepares **internal presentation seams** only (e.g. clearer host boundaries, composable conversation surfaces). Visible unification and panel retirement remain Phase 14. |
| R61-LT02 | Agent Session remains deferred. Do not invent session UI or rename panel host to session. |
| Phase 14 seams | Internal-only. Seams must be justified by current extraction needs or by making an existing dependency edge explicit. No empty folders, no unused navigation ViewModels, no “DM list” stubs. |
| Tests first | Prefer moving/keeping coverage at shell, Townhall ViewModel, Agent panel host, Architecture, and DesignSystem tests. Add focused construction/wiring tests when extraction risks silent layout/binding breakage. Manual smoke is required after visual-structure milestones. |
| Line-count goal | Not a hard ratchet. Success is maintainable ownership and green gates, not a magic LOC number. Expect material reduction of `MainWindow.axaml.cs` by closeout; do not chase renames to fake reduction. |

### Presentation seam policy (Phase 14 preparation without Phase 14)

Allowed internal seams:

- Shell layout host that exposes already-wired child views (Townhall, editor
  column, agent host, bottom panel) without new product API.
- Clearer construction boundaries for Townhall sub-panels and Agent panel host
  chrome so Phase 14 can later compose navigation around them.
- Shared brush/spacing helpers that already duplicate across Townhall/shell.

Forbidden seams:

- `IConversationNavigator`, DM sidebar ViewModels, unread services, or
  persistence ports introduced “for Phase 14.”
- Cross-feature Presentation dependencies (Townhall Presentation → Agents
  Presentation or reverse) beyond what composition already does in App.Shell.
- Moving domain/application types into Presentation.

### Type placement and composition policy

| Concept | Locked home | Notes |
|---------|-------------|-------|
| Shell layout grid / column hosts | `App/Shell` | Prefer `internal` types; constructed by `MainWindow`, not DI, unless a milestone proves a factory consumer already exists. |
| Bottom panel mode strip + content host | `App/Shell` | Extract from `BuildLayout` bottom section; keep mode switching driven by existing `MainWindowViewModel` commands. |
| Editor + agent right column | `App/Shell` | Structural host only; does not own editor or agent domain. |
| Settings attach helpers | `App/Shell` | May extract methods/types; keep `ISettingsPanelFactory` contract. |
| Design tokens / typography / icons | `UI/DesignSystem` | Extend only for existing rendered values. |
| Feature-neutral multi-consumer chrome | `UI/Shared` only after admission proof | Deny-by-default; architecture evidence required. |
| Townhall panels / avatar / input | `Features/Townhall/Presentation` | Keep ViewModel projection contracts stable. |
| Agent panel host/view | `Features/Agents/Presentation` | Keep `IAgentPanelHost` / send event contract stable. |
| DI registration modules | Unchanged by default | R8 should not need new modules. If a factory registration is required, document consumer proof in that milestone; do not invent scopes. |

---

## Scope

**Goal:** Make the Townhall/conversation shell UI maintainable and
token-consistent by extracting large imperative views and clarifying design
primitives, while preserving every currently observable interaction and
rendered value, and preparing only internal seams Phase 14 may later use.

**In scope:**

- extract `MainWindow` layout construction into focused shell components;
- reduce view-side `WhenActivated` / settings attach pressure without changing
  product flows;
- keep Townhall and Agent Panel presentation structure maintainable;
- route remaining shell/Townhall/Agent presentation literals through existing
  DesignSystem tokens where pixel-identical;
- add focused tests and manual smoke evidence for extraction safety;
- update architecture baselines only when a real type/home change requires it;
- keep docs (`IMPLEMENTATION_PLAN`, CONVENTIONS/architecture status as needed)
  truthful at milestone closeout.

**Out of scope:**

- Phase 14 unified conversation workspace / DM navigation / panel retirement;
- any Agent/Conversation domain change (Refactor 7 closed surface);
- mirror/privacy/product behavior changes;
- visual redesign, new palette, new icon set, glass overhaul;
- persistence, recovery, unread, drafts migration, deletion policy;
- Agent Session / Runtime / harness / ACP / streaming / tools;
- DF-002 Korean IBUS and unrelated Settings scrolling/alignment findings
  (DF-003/DF-004) unless a pure move touches the same file and must preserve
  behavior;
- broad Editor/Terminal/Debug/SourceControl redesign (only shell-host wiring
  that already constructs those views may move);
- package/assembly splits.

---

## Behavior-preservation boundaries (hard)

Every milestone must treat the following as regression blockers:

| ID | Boundary | How verified |
|----|----------|--------------|
| BP-01 | No change to typed conversation admission, mirror target capture, or Townhall entry projection strings | R7 ownership/mirror tests + full suite |
| BP-02 | Shell column roles, default sizes, splitter behavior unchanged | Manual layout smoke + existing shell tests |
| BP-03 | Townhall send/filter/channel select/people roster unchanged | Townhall ViewModel/integration tests + manual |
| BP-04 | Agent panel tab lifecycle and send/output presentation unchanged | Agent panel host/view tests + manual |
| BP-05 | Bottom panel modes and Terminal focus/start path unchanged | `MainWindowViewModelBottomPanelModeTests` + manual |
| BP-06 | Settings open/close/focus restore unchanged | Settings UI tests + manual |
| BP-07 | Command palette and search focus restore unchanged | Existing shell/settings/editor tests + manual |
| BP-08 | Public type ceiling and root-folder admission unchanged unless intentional same-milestone update | Architecture tests |
| BP-09 | No new visible navigation affordance for DMs/direct conversations | Manual + code review stop rule |
| BP-10 | Pixel-class visual identity preserved (palette, Discord-like Townhall chrome) | Manual default/min window smoke; optional screenshot notes |

**Incidental correctness changes are not pre-authorized.** If extraction
uncovers a bug, stop, document it (issue or deferred finding), and finish the
structural milestone without fixing product behavior unless the user
explicitly expands scope.

---

## File inventory

### Production files likely touched (by area)

**Shell (primary extraction target)**

| File | Role in R8 |
|------|------------|
| `src/App/Shell/MainWindow.axaml.cs` | Extract layout/wiring; shrink |
| `src/App/Shell/MainWindow.axaml` | Leave as minimal partial stub unless framework requires more |
| `src/App/Shell/MainWindowViewModel.cs` | Prefer no logic change; touch only if wiring signatures require it |
| `src/App/Shell/MainWindowActivationHost.cs` | Prefer leave; VM-side already extracted in 6.3 |
| `src/App/Shell/ShellPanelNavigation.cs` | Prefer leave |
| `src/App/Shell/NavBar.cs` | Token consistency only if milestone includes chrome tokens |
| `src/App/Shell/StatusBar.cs` | Token consistency only if needed |
| `src/App/Shell/CommandPaletteOverlay.cs` | Token consistency optional; not primary |
| `src/App/Shell/GridLayoutResizeHelper.cs` | Keep behavior; may be consumed by extracted hosts |
| `src/App/Shell/Animations.cs` | Shell-owned; reuse, do not redesign |
| `src/App/Shell/IconFactory.cs` | Shell-owned; reuse |
| `src/App/Shell/FinalWindowCleanup.cs` | Keep |
| **New (expected)** `src/App/Shell/*` host/builder types | e.g. layout root builder, bottom panel host, right column host, settings attach helper — exact names chosen in implementing milestones |

**Townhall presentation**

| File | Role in R8 |
|------|------------|
| `src/Features/Townhall/Presentation/TownhallView.cs` | Composite maintainability / token cleanup |
| `TownhallChannelPanel.cs` | Hardcoded selection/hover colors → tokens if identical |
| `TownhallPeoplePanel.cs` | Hover colors → tokens if identical |
| `TownhallChatPanel.cs` | Preserve rendering; structural only if needed |
| `TownhallInputArea.cs` | Token fallbacks / FontSize policy |
| `TownhallAvatarFactory.cs` | Fallback hex → resource keys |
| `TownhallViewModel.cs` | **Prefer no change** (R7 surface) |
| `TownhallEntryProjection.cs` | **Do not change** (R7 M7 ownership) |

**Townhall domain (read-only / avoid)**

| File | Role in R8 |
|------|------------|
| `Channel.cs`, `TownhallMessage.cs`, `TownhallState.cs`, `WorkspaceAgent.cs` | Out of scope unless a pure rename/move is impossible without them (should not be required) |

**Agent presentation**

| File | Role in R8 |
|------|------------|
| `AgentPanelHostView.cs` | Structure/token cleanup; preserve host API |
| `AgentPanelView.cs` | Token/`TextStyles` consistency; preserve send UX |
| `AgentPanelHost.cs` / `IAgentPanelHost.cs` | **Prefer no change** |

**Design system / app resources**

| File | Role in R8 |
|------|------------|
| `src/UI/DesignSystem/LayoutTokens.cs` | Extend only for existing values |
| `src/UI/DesignSystem/TextStyles.cs` | Extend only if a missing size/weight is already rendered |
| `src/UI/DesignSystem/Icons.axaml` | Prefer leave |
| `src/App/Composition/App.axaml` | Token resources only when pixel-identical additions are required |
| `src/UI/Shared/**` | Empty; admit only with proof + architecture update |

**Explicitly out of inventory for implementation**

- `src/Features/Conversations/**` domain/application (R7)
- `src/Features/Agents/Application/**`, `Domain/**`, `Infrastructure/**`
- `src/App/Shell/AgentTownhallMirrorCoordinator.cs` (behavior freeze)
- `docs/phases/**` Phase 14 plans (do not create/start)

### Test files likely used or extended

| File / area | Role |
|-------------|------|
| `tests/Zaide.Tests/App/Shell/*` | Shell VM, bottom panel, activation, palette; M5 wiring/lifecycle when settings attach moves |
| `tests/Zaide.Tests/Features/Townhall/Presentation/*` | Townhall VM + typed entry integration |
| `tests/Zaide.Tests/Features/Agents/Presentation/*` | Host lifetime |
| `tests/Zaide.Tests/Features/Settings/Presentation/*` | Required M5 filter (`Settings.Presentation`); open/close/focus paths |
| `tests/Zaide.Tests/Features/Editor/Presentation/*` | Required M5 filter (`Editor.Presentation`); search/editor surface wiring |
| `tests/Zaide.Tests/UI/DesignSystem/TextStylesTests.cs` | Token helpers |
| `tests/Zaide.Tests/Architecture/*` | Root folders, visibility baseline, R7 ownership |
| **New focused tests** | Construction/wiring of extracted hosts as milestones need them; **M5 must add** a shell wiring/lifecycle test if settings attach/detach or focus-restoration logic moves beyond existing coverage |

### Docs files for R8

| File | Role |
|------|------|
| `docs/refactor/refactor-8/IMPLEMENTATION_PLAN.md` | This plan (milestone checklist) |
| `docs/refactor/refactor-8/TOFIX.md` | Create when first review finding appears |
| `docs/CONVENTIONS.md` / `docs/architecture/OVERVIEW.md` / `docs/roadmap/V3.md` | Truth-sync at authorized closeout milestones only |
| `docs/DESIGN.md` | Update only if token inventory clarification is accepted as doc truth (no redesign) |

---

## Dependencies and ordering

1. Refactor 7 remains closed; do not reopen domain milestones.
2. Design-token clarification for surfaces about to move should precede or
   accompany the first extraction that would otherwise copy hardcodes.
3. Extract bottom panel host and right column before/with the full layout
   builder so `MainWindow` shrinks in reviewable slices.
4. View-side activation/settings helper extraction happens only after layout
   hosts exist (or in a dedicated slice that does not mix layout geometry).
5. Townhall/Agent presentation cleanup may proceed in parallel **only** if it
   does not conflict with shell layout file churn; prefer serial milestones.
6. Architecture baseline updates ride with the milestone that introduces types
   or homes — never as a silent follow-up.
7. Closeout docs/status updates only after automated + required manual evidence.

No milestone may begin before the prior milestone or named slice is accepted.
If a milestone exceeds one agent session, split into `MNa`/`MNb` without
expanding concern.

---

## Milestones (incremental)

| Milestone | Description | Verification gate |
|-----------|-------------|-------------------|
| **M0** | Planning gate: live audit, scope, BP boundaries, inventory, slices, commands, rollback, stop rules. **Docs only.** | Plan exists; no production/test diffs required; human acceptance required before M1 |
| **M1** | **Token baseline for R8-owned surfaces:** apply only the pre-mapped, pixel-identical typography and palette substitutions in the M1 literal inventory below. No layout extraction, hover/selection-overlay cleanup, Agent-panel work, or broader literal sweep. | Build; focused DesignSystem + Townhall + shell tests; Architecture; full suite; manual Townhall/shell glance smoke | **[x] 2026-07-19** |
| **M2** | **Extract bottom panel host** from `MainWindow.BuildLayout` (mode strip buttons, content grid, border chrome) into an `App/Shell` type. Preserve commands, visibility, heights, Terminal focus path. | Build; bottom-panel shell tests; Architecture; full suite; manual bottom-panel mode smoke | **[x] 2026-07-19** |
| **M3** | **Extract right column host** (editor tab bar + search + editor/welcome + vertical splitter + `AgentPanelHostView`) into an `App/Shell` type. Preserve splitter and agent host wiring points. | Build; shell + agent host tests; Architecture; full suite; manual editor/agent resize smoke | **[x] 2026-07-19** |
| **M4** | **Extract main layout builder** (column definitions, nav/left/townhall placement, splitters, status bar attach) so `MainWindow` composes hosts rather than inlining geometry. Preserve `GridLayoutResizeHelper` behavior. | Build; shell tests; Architecture; full suite; manual default + min window layout smoke | **[x] 2026-07-19** |
| **M5** | **Extract view-side settings attach + reduce `WhenActivated` pressure** into shell helper type(s) without changing interactions. Keep palette/search/editor wiring behavior identical. **If** settings attach/detach or focus-restoration logic moves beyond what existing tests cover, M5 must add a focused shell wiring/lifecycle test in the same milestone (plan-required; missing test = incomplete). | Build; shell tests; Settings Presentation + Editor Presentation focused filters (see Verification commands); new shell wiring/lifecycle test when extraction moves settings attach/detach or focus restore beyond existing coverage; Architecture; full suite; manual settings/palette/search smoke | **[x] 2026-07-19** |
| **M6** | **Townhall presentation maintainability:** structural cleanup only (constructor clarity, local helpers, token leftovers). No ViewModel/domain API change. Optional internal seam only if required by extraction. | Build; Townhall tests; Architecture; full suite; manual Townhall filter/send/channel smoke |
| **M7** | **Agent panel presentation maintainability:** structural/token cleanup in host/view only; preserve `IAgentPanelHost` and send event. No panel retirement. | Build; agent presentation tests; Architecture; full suite; manual multi-panel send smoke |
| **M8** | **Closeout:** docs/status truth-sync, optional architecture ratchet notes, confirm BP checklist, record LOC/public baseline, confirm Phase 14 still unauthorized. | Build; Architecture; full suite; `git diff --check`; manual evidence review; no open TOFIX |

### Milestone slice rule

If any of M2–M5 exceeds one session, split as `M2a`/`M2b` (example: M2a extract
type with dual-path construction still in `MainWindow`; M2b delete inline path
after parity). Do **not** invent `refactor-8.1` for an oversized milestone.

### M1 literal inventory and substitution contract

M1 is intentionally a bounded baseline, not permission to replace every UI
literal found by grep. The implementer must preserve the listed value in both
the normal resource-backed app and the no-resource fallback path used by unit
tests. Any literal not listed here stays for its owning later milestone (or is
documented as a residual); it must not be swept into M1.

| Live location | Current value / role | M1 replacement contract | Explicitly not part of M1 |
|---------------|----------------------|-------------------------|---------------------------|
| `MainWindow.axaml.cs` bottom-mode `Output`, `Test Results`, and `Debug` buttons | `FontSize = 12` | Replace with one internal DesignSystem typography accessor backed by a named `12` resource/token. Add that resource only if none already exists, and test the accessor/value. **Do not route `Button` controls through `TextStyles`, which is a `TextBlock` factory.** | Terminal/Problems theme-default sizes; unrelated shell/Agent literals. |
| `TownhallInputArea.cs` resource fallbacks | `#E3E4F4` → `TextPrimaryBrush`; `#066ADB` → `PrimaryAccentBrush`; `#8B95A5` → `TextSecondaryBrush` | Use a DesignSystem palette resolver/helper that keeps the exact fallback colors and resolves the existing named resource roles. Add focused tests for the resource-missing fallback values. | The `0x0D` white input overlay and any input-layout change. |
| `TownhallAvatarFactory.cs` resource fallbacks | `#243352` → `SurfaceRaisedBrushColor`; `#066ADB` → `PrimaryAccentBrushColor`; `#28A745` → `SuccessBrush`; `#1A2540` → `SurfacePanelBrush`; `#E3E4F4` → `TextPrimaryBrush` | Route through the same DesignSystem palette resolver/helper (or an equivalently tested typed API) while retaining exact color/brush fallback values. | Derived avatar-border alpha/lightening calculations and all avatar geometry. |

The M1 closeout must record the exact source lines removed, the resource/token
names used or added, and proof that each replacement retains the listed value.
If the only viable change would alter a resource-less/test rendering path,
stop and revise the M1 plan rather than calling it pixel-identical.

### Authorization rule

Human acceptance of **M0** authorizes plan completeness only. Each later
milestone requires its own explicit authorization after the previous milestone
is accepted. Completing M8 closes Refactor 8; it does **not** authorize Phase 14.

---

## Verification commands

### Global gates (every implementation milestone)

```bash
dotnet build Zaide.slnx

dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.Architecture'

dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build

git diff --check
```

Record exact pass counts (passed/failed/skipped) and build warning count in the
milestone verification section. Pre-existing warnings may remain only if
unchanged and already known.

### Focused filters (use as applicable)

```bash
# Shell
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.App.Shell'

# Townhall
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.Features.Townhall'

# Agents presentation
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents.Presentation'

# Design system
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.UI.DesignSystem'

# Settings presentation (required for M5; BP-06 settings open/close/focus)
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.Features.Settings.Presentation'

# Editor presentation (required for M5; search/focus wiring and editor surface)
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.Features.Editor.Presentation'

# R7 ownership must remain green whenever mirror/projection files are nearby
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Refactor7M7OwnershipRatchetTests'
```

### M5 settings/wiring gate (mandatory when applicable)

M5’s verification gate is not “shell tests alone.” In addition to the shell
filter, M5 must run the **Settings Presentation** and **Editor Presentation**
filters above and record their pass counts.

If M5 extraction moves **settings attach/detach** or **focus-restoration**
logic out of `MainWindow` (or otherwise beyond existing coverage), M5 must
also **add a focused shell wiring/lifecycle test** in the same milestone that
proves:

- settings panel attach and detach still occur on the existing open/close path;
- focus restoration after settings close matches pre-extraction behavior;
- the extracted helper does not leave dangling handlers or skip cleanup on
  deactivate/close.

That test is plan-required under `docs-rules.md` §12g when the extraction
moves those paths. Prefer placing it under
`tests/Zaide.Tests/App/Shell/` next to existing shell lifecycle coverage.
Missing the test when the move applies = incomplete M5.

### Animation / design guards (when animations or chrome move)

```bash
tools/check-animations.sh
```

Expected: zero illegal hits per Refactor 4 guard rules.

### Manual smoke (required after M2–M7; optional after M1)

Record truthful results (run / not run + reason):

1. Launch app at default size (~1280×800) and min size (~960×600): layout stable,
   no clipped critical chrome.
2. Townhall: switch channels, toggle All/Chat/Activity, send a message, confirm
   history and filters.
3. Agent Panel: open/switch tabs, send direct message, confirm output prefixes
   and busy state; confirm public mirror still appears on active admission
   channel without changing R7 attribution rules.
4. Bottom panel: cycle Terminal/Problems/Output/Test Results/Debug; hide/show.
5. Left panel: Explorer ↔ Source Control animation and SC refresh.
6. Settings open/close and focus return; command palette open/dismiss.
7. Splitters: left width, townhall/editor width, editor/agent height, bottom
   height behave as before.

Screenshots are optional evidence under
`docs/refactor/refactor-8/verification/` (create when first used). They do not
replace automated gates.

### M0-only verification

```bash
test -f docs/refactor/refactor-8/IMPLEMENTATION_PLAN.md
git status --short
# Expected for pure M0: only the new plan (and directory) unless user expands docs scope
```

No `dotnet` production change is required for M0.

---

## Entry / exit conditions

### Entry conditions for M0 (this document)

- [x] Refactor 6.1–6.3 accepted and closed.
- [x] Refactor 7 complete and closed (`a7d2887` / docs `945f0e7`).
- [x] Live post-R7 shell/Townhall/Agent UI audited against `src/`.
- [x] V3 §16.4 scope read and bounded.
- [x] Phase 14 explicitly excluded.
- [x] Production/test code unmodified by M0.

### Exit conditions for M0

- [x] `docs/refactor/refactor-8/IMPLEMENTATION_PLAN.md` exists with scope, BP
      boundaries, inventory, milestones, commands, rollback, and stop rules.
- [x] Human accepts this M0 plan.
- [x] Human explicitly authorizes **M1 only** (or declines / revises plan).

### Exit conditions for M1

- [x] M1 literal inventory substitutions applied with pixel-identical fallbacks.
- [x] Focused DesignSystem, Townhall, shell, Architecture, and full-suite gates green.
- [x] Manual Townhall input/send and Output/Test Results/Debug bottom-mode smoke run.
- [x] Human accepts M1 closeout before M2 authorization.

### Exit conditions for M2

- [x] `BottomPanelHost` extracted under `src/App/Shell/`; `MainWindow.BuildLayout`
      delegates bottom splitter/panel construction and `WhenActivated` visibility/mode
      wiring to the host.
- [x] `MainWindowViewModel` signature and command surface unchanged.
- [x] Focused shell VM bottom-panel tests remain green; no new host construction
      tests added (existing `MainWindowViewModelBottomPanelModeTests` already prove
      mode flags; shell `RxAppBuilder` static-ctor ordering blocks reliable Avalonia
      `Application.Current` bootstrap for full host construction tests in this
      milestone).
- [x] Architecture baselines updated for `BottomPanelHost` (+1 internal type,
      +1 App source file).
- [x] Build, shell filter, Architecture, and full suite green.
- [x] Manual smoke: full bottom-panel mode cycle, hide/show, and splitter resize at
      default (1280×800) and minimum (960×600) window sizes on Linux `DISPLAY=:1`
      (see M2 verification record).
- [x] Human accepts M2 closeout before M3 authorization.

### Entry conditions for M3 (authorized 2026-07-19)

- [x] Human accepted M2 closeout at `20a4283` (code `22208fc`; smoke docs
      `20a4283`).
- [x] Human authorized **M3 only**.
- [x] Working tree based on accepted M2 boundary is clean of unrelated edits.
- [x] Implementer re-reads BP-01–BP-10 and stop rules before editing.

### Exit conditions for M3

- [x] `RightColumnHost` extracted under `src/App/Shell/`; `MainWindow.BuildLayout`
      delegates column 5 to the host; constructor aliases host surfaces for existing
      `WhenActivated` wiring.
- [x] `MainWindowViewModel`, `GridLayoutResizeHelper`, `AgentPanelHost`,
      `IAgentPanelHost`, and agent send orchestration unchanged.
- [x] `RightColumnHostSourceTests` added (type/source placement ratchets); full host
      construction not unit-tested due to `EditorView` requiring resource-backed
      `Application.Current` (see M3 verification record).
- [x] Architecture baselines updated for `RightColumnHost` (+1 internal type,
      +1 App source file).
- [x] Build, shell, Agents.Presentation, Editor.Presentation, Architecture, and full
      suite green.
- [x] Manual smoke: editor tab/search, welcome/editor switching, editor/agent
      splitter resize, agent panel tab create/send at default (1280×800) and minimum
      (960×600) window sizes on Linux `DISPLAY=:1` (see M3 verification record).
- [x] Human accepts M3 closeout at `19bb674` (code `73fc66c`). M4 remains
      unauthorized until explicit authorization.

### Entry conditions for M4 (authorized 2026-07-19)

- [x] Human accepted M3 closeout at `05f6c48`.
- [x] Human authorized **M4 only**.
- [x] Working tree based on accepted M3 boundary is clean of unrelated edits.
- [x] Implementer re-reads BP-01–BP-10 and stop rules before editing.

### Exit conditions for M4

- [x] `MainLayoutBuilder` extracted under `src/App/Shell/`; `MainWindow` delegates
      layout construction and maps the build result to existing field aliases.
- [x] Inline `BuildLayout` removed from `MainWindow`; `GridLayoutResizeHelper`
      splitter callbacks preserved unchanged.
- [x] `MainWindowViewModel`, `WhenActivated` wiring, settings attach, and command
      palette overlay placement unchanged.
- [x] `MainLayoutBuilderSourceTests` added (type/source placement ratchets); full
      builder construction not unit-tested due to transitive `EditorView` resource
      bootstrap limitation (see M3/M4 verification records).
- [x] Architecture baselines updated for `MainLayoutBuilder` (+1 internal type,
      +1 App source file).
- [x] Build, shell, Architecture, and full suite green.
- [x] Manual smoke: left/townhall and townhall/editor splitter drags at default
      (1280×800) and minimum (960×600) window sizes on Linux `DISPLAY=:1` (see M4
      verification record).
- [x] Human accepts M4 closeout at `09ccde9` (code `b3c8684`). M5 remains
      unauthorized until explicit authorization.

### Entry conditions for M5 (authorized 2026-07-19)

- [x] Human accepted M4 closeout at `09ccde9` (code `b3c8684`).
- [x] Human authorized **M5 only**.
- [x] M6+, Phase 14, and adjacent cleanup remain unauthorized.

### Exit conditions for M5

- [x] `SettingsPanelAttachHost` extracted under `src/App/Shell/`; settings
      attach/detach, open/close interaction, left-panel restore, focus restore,
      and deactivate cleanup moved out of `MainWindow`.
- [x] `ShellOverlayFocusWiring` extracted; command-palette and editor-search
      focus wiring moved out of `MainWindow.WhenActivated` without behavior change.
- [x] `MainWindowViewModel`, keybinding materialization, and layout hosts unchanged.
- [x] `SettingsPanelAttachHostTests` added (attach/detach, toggle, left-panel
      restore, deactivate cleanup, focus-restore seam).
- [x] `SettingsUiTests` updated for host seam; existing settings open/close
      proofs remain green.
- [x] Architecture baselines updated for `SettingsPanelAttachHost` and
      `ShellOverlayFocusWiring` (+2 internal types, +2 App source files).
- [x] Build, shell, Settings.Presentation, Editor.Presentation, Architecture, and
      full suite green.
- [x] Manual smoke: settings open/close/focus, command palette open/dismiss,
      search open/dismiss/focus at default (1280×800) and minimum (960×600) on
      Linux `DISPLAY=:1` (see M5 verification record).
- [x] Human accepts M5 closeout (code `d947efa`).

### Entry conditions for M6 (authorized 2026-07-19)

- [x] Human accepted M5 closeout at `d947efa`.
- [x] Human authorized **M6 only**.
- [x] M7+, Phase 14, and adjacent cleanup remain unauthorized.
- [x] Implementer re-reads BP-01–BP-10 and stop rules before editing.

### Exit conditions for M6

- [x] TownhallView constructor extracted into focused builder methods (sidebar,
      filter group, chat area, main layout) for readability.
- [x] WireViewModel filter wiring extracted into a shared `WireFilterButton` helper
      eliminating three nearly-identical subscriptions.
- [x] Remaining `(IBrush?)Application.Current!.Resources[...]` lookups in Townhall
      presentation files replaced with `PaletteTokens` equivalents
      (`SurfacePanelBrush`, `SurfaceBaseBrush`, `SeparatorBrush`, `WarningBrush`,
      `TextPrimaryBrush`, `TextSecondaryBrush`).
- [x] Three new palette token accessors added to `PaletteTokens` (`SurfaceBaseBrush`,
      `SeparatorBrush`, `WarningBrush`) with pixel-identical resource-name and
      fallback values from `App.axaml`.
- [x] Hardcoded hover/active overlay colors in `TownhallChannelPanel` and
      `TownhallPeoplePanel` extracted to named static fields.
- [x] No ViewModel, domain, entry-projection, or DesignSystem token API changed
      beyond the three additive palette accessors.
- [x] No new production types or source files; architecture baseline unchanged.
- [x] Build; Townhall tests; Architecture; full suite green.
- [x] Manual smoke: Townhall channel switch, filter toggle, send message.
- [x] Human accepts M6 closeout at `1ca1e08` (smoke docs `3b40af8`). M7 remains unauthorized
      until explicit authorization.

### Exit conditions for Refactor 8 (after M8)

- [ ] `MainWindow` is no longer the monolithic imperative layout owner; hosts
      own construction seams.
- [ ] R8-owned Townhall/Agent/shell surfaces use DesignSystem tokens for values
      that previously were identical hardcodes (residuals documented if any).
- [ ] BP-01–BP-10 hold; full suite and Architecture green.
- [ ] Public baseline intentional and recorded.
- [ ] No Phase 14 feature behavior landed.
- [ ] Docs/status surfaces match live code.
- [ ] Human accepts M8 closeout.

---

## Limitations by design

- Discord-like Townhall visual language remains temporary scaffolding (V3).
- Dedicated Agent Panel remains until Phase 14 proves parity.
- Public agent→Townhall mirror remains.
- No resumable Agent Session UI (`R61-LT02` still deferred).
- `UI/Shared` may remain empty if no type meets admission rules.
- Not every `FontSize` literal in Editor/Terminal/Settings must be cleared by
  R8; only R8-owned extraction surfaces are obligated.
- Accessibility improvements beyond preserving current keyboard paths are out
  of scope (Phase 14 design brief owns deeper a11y targets).
- Line count of `MainWindow.axaml.cs` is a success indicator, not a hard gate.

---

## Rollback points

| Point | Commit / state | Meaning |
|-------|----------------|---------|
| **M0 baseline** | `945f0e7` | Clean post–R7 docs closeout; start of R8 planning |
| **R7 code closeout** | `a7d2887` | Last domain ownership ratchet; behavior freeze reference |
| Per milestone | One commit per accepted milestone/slice on top of previous accepted boundary | Revert that commit to restore last accepted R8 boundary |

Rules:

- Prefer one commit per milestone or milestone slice
  (`refactor-8: …` area prefix).
- Do not batch M2–M4 into one commit.
- If an extraction cannot prove BP gates, revert the milestone commit rather
  than layering product fixes.
- Structural rollback of an accepted implementation requires
  `docs/refactor/refactor-8/REVERT_LOG.md` per `docs-rules.md`.
- Do not roll back unrelated user work on other branches.

---

## Explicit stop rules

Stop work and ask the user (do not continue the milestone) when:

1. **Authorization** — about to start any milestone not explicitly authorized.
2. **Phase 14 creep** — change would add DM navigation, hide/retire Agent Panel,
   remove public mirror, or add persistence/unread/privacy UX.
3. **Domain creep** — change would alter Conversations/Agents domain,
   orchestration, or R7 ownership boundaries.
4. **Behavior change temptation** — extraction reveals a product bug and the fix
   would change observable behavior; file issue/deferred finding instead.
5. **Visual redesign** — proposed change alters palette, spacing language, or
   Townhall IA beyond pixel-identical token substitution.
6. **UI/Shared or Infrastructure admission** — first file under those roots
   needs explicit ownership proof and architecture entry; if contested, stop.
7. **New public type** — required public surface expansion needs explicit
   rationale and baseline update in the same change; if unclear, stop.
8. **New NuGet / assembly** — not authorized by this refactor.
9. **Architecture red after two fix attempts** — stop; do not disable tests.
10. **Manual smoke blocked with no truthful “not run” reason** — do not mark
    visual-structure milestones complete on automation alone when the plan
    required smoke.
11. **CONVENTIONS conflict** — extraction would force Views into Services or
    ViewModels into Views; redesign the seam instead of violating MVVM.
12. **Scope expansion disguised as cleanup** — drive-by renames outside inventory
    or “fix” of Editor/Terminal feature interiors.

---

## M0 verification record (2026-07-19)

- Live audit against `945f0e7` (Refactor 7 closed; working tree clean at plan
  authoring).
- Confirmed V3 §16.4 goals and Phase 14 exclusion.
- Confirmed `MainWindow.axaml.cs` ~995 LOC residual (R61-V15).
- Confirmed Townhall already split into sub-panels; residual token/hardcode debt.
- Confirmed public baseline 339/104/443 and `UI/Shared` deny-by-default.
- Confirmed DesignSystem exists (`LayoutTokens`, `TextStyles`, `Icons.axaml`).
- **No production or test code modified for M0.**
- **M1+ unauthorized** until human acceptance of this plan and explicit M1
  authorization.

---

## Entry conditions for M1 (future)

- [x] Human accepted this M0 plan.
- [x] Human authorized **M1 only**.
- [x] Working tree based on accepted M0 baseline (or later accepted R8
      boundary) is clean of unrelated edits.
- [x] Implementer re-reads BP-01–BP-10 and stop rules before editing.

---

## M1 verification record (2026-07-19)

### Removed source literals (exact lines at M1 start)

| File | Removed line(s) | Replacement |
|------|-----------------|-------------|
| `src/App/Shell/MainWindow.axaml.cs` | `749` `FontSize = 12,` (Output tab) | `TypographyTokens.FontSizeSm` |
| `src/App/Shell/MainWindow.axaml.cs` | `765` `FontSize = 12,` (Test Results tab) | `TypographyTokens.FontSizeSm` |
| `src/App/Shell/MainWindow.axaml.cs` | `781` `FontSize = 12,` (Debug tab) | `TypographyTokens.FontSizeSm` |
| `src/Features/Townhall/Presentation/TownhallInputArea.cs` | `60` `Foreground = (IBrush?)Application.Current?.Resources["TextPrimaryBrush"] ?? new SolidColorBrush(Color.Parse("#E3E4F4")),` | `PaletteTokens.TextPrimaryBrush` |
| `src/Features/Townhall/Presentation/TownhallInputArea.cs` | `77` `(IBrush?)Application.Current?.Resources["TextPrimaryBrush"] ?? new SolidColorBrush(Color.Parse("#E3E4F4")),` | `PaletteTokens.TextPrimaryBrush` |
| `src/Features/Townhall/Presentation/TownhallInputArea.cs` | `85` `Background = (IBrush?)Application.Current?.Resources["PrimaryAccentBrush"] ?? new SolidColorBrush(Color.Parse("#066ADB")),` | `PaletteTokens.PrimaryAccentBrush` |
| `src/Features/Townhall/Presentation/TownhallInputArea.cs` | `103` `(IBrush?)Application.Current?.Resources["TextSecondaryBrush"] ?? new SolidColorBrush(Color.Parse("#8B95A5")),` | `PaletteTokens.TextSecondaryBrush` |
| `src/Features/Townhall/Presentation/TownhallAvatarFactory.cs` | `20–23` inline `GetColor`/`GetBrush` calls with `#243352`, `#066ADB`, `#28A745`, `#1A2540` | `PaletteTokens.SurfaceRaisedColor`, `PrimaryAccentColor`, `GetBrush(..., CreateSuccessStatusFallbackBrush())`, `SurfacePanelBrush` |
| `src/Features/Townhall/Presentation/TownhallAvatarFactory.cs` | `28` `GetBrush("TextPrimaryBrush", new SolidColorBrush(Color.Parse("#E3E4F4")))` | `PaletteTokens.TextPrimaryBrushOrFallback` |
| `src/Features/Townhall/Presentation/TownhallAvatarFactory.cs` | `103–123` private `GetColor` / `GetBrush` helpers | removed (centralized in `PaletteTokens`) |

Preserved unchanged: `TownhallInputArea` `0x0D` input overlay; avatar
`Lighten` / derived-alpha ring; Terminal/Problems theme-default sizes; all
unlisted literals.

### Token / resource names added or used

| Name | Kind | Value / role |
|------|------|----------------|
| `FontSizeSm` | `App.axaml` `x:Double` resource | `12` — bottom-mode button typography |
| `TypographyTokens.FontSizeSm` | internal accessor | resolves `FontSizeSm` with fallback `12` |
| `PaletteTokens` | internal helper | resource-backed brushes/colors with M1-listed hex fallbacks |

Palette fallback values (no-resource path): `#E3E4F4` (`TextPrimaryBrush`),
`#066ADB` (`PrimaryAccentBrush` / `PrimaryAccentBrushColor`),
`#8B95A5` (`TextSecondaryBrush`), `#243352` (`SurfaceRaisedBrushColor`),
`#28A745` (`SuccessBrush` / status fallback), `#1A2540` (`SurfacePanelBrush`).

### New production types (internal)

- `Zaide.UI.DesignSystem.TypographyTokens`
- `Zaide.UI.DesignSystem.PaletteTokens`

Visibility baseline updated: **339** public / **106** internal / **445** total.

Source-file inventory baseline updated: **407** total production source files
(**UI** folder **2 → 4** for `TypographyTokens.cs` and `PaletteTokens.cs`).

### Automated verification (2026-07-19, resubmission after architecture source-file ratchet fix)

```text
dotnet build Zaide.slnx
  → succeeded, 4 warnings (pre-existing, unchanged)

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.UI.DesignSystem'
  → Passed: 30, Failed: 0, Skipped: 0, Total: 30

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Features.Townhall'
  → Passed: 81, Failed: 0, Skipped: 0, Total: 81

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.App.Shell'
  → Passed: 160, Failed: 0, Skipped: 0, Total: 160

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Architecture'
  → Passed: 26, Failed: 0, Skipped: 0, Total: 26
  (includes source-file ratchets: total 407, UI 4)

dotnet test (full suite)
  → Passed: 2508, Failed: 0, Skipped: 0, Total: 2508

git diff --check
  → clean
```

Prior `f046142` submission failed Architecture **24/26** until
`ArchitectureInventoryTests.Read_SourceFolderPlacement_MatchesM0TrackedFileCounts`
and `ArchitectureVisibilityTests` UI source-file assertions were updated.

New focused tests: `TypographyTokensTests` (1), `PaletteTokensTests` (11).

### Post-M1 full-suite gate repair (2026-07-19)

The first full-suite rerun after M1 resubmission failed in
`DapContentLengthTransportTests.DisposeRacingOutstandingRequests_CancelsAllWithoutCollectionExceptions`.
The timeout occurred before disposal while the test polled for all eight
outbound frames; it did not indicate a Townhall or shell regression.

The user explicitly authorized repair of the gate. Tightening the test to
dispose against the admitted pending set then exposed an actual DAP transport
lifecycle race: `_writeGate` could be disposed before an in-flight request
released it, producing `ObjectDisposedException`. The repair links admitted
request writes to disposal cancellation and makes `DisposeAsync` await all
admitted requests before disposing the gate. The test now asserts the pending
set directly, eliminating its scheduling-dependent outbound-frame polling.

This repair is recorded in `TOFIX.md` as `R8-M1-001`. It changes no Townhall,
shell, Agent Panel, Phase 14, or user-visible behavior. M2 remains
unauthorized until the repaired full-suite gate is recorded green and M1 is
accepted.

Verification after the repair:

```text
dotnet build Zaide.slnx
  → succeeded, 4 pre-existing warnings on a clean rebuild

dotnet test --filter 'FullyQualifiedName~DapContentLengthTransportTests'
  → Passed: 5, Failed: 0, Skipped: 0, Total: 5

DisposeRacingOutstandingRequests_CancelsAllWithoutCollectionExceptions
  → passed in 40 consecutive focused runs

dotnet test (full suite)
  → Passed: 2508, Failed: 0, Skipped: 0, Total: 2508

git diff --check
  → clean
```

### Manual smoke (2026-07-19)

Run on Linux with `DISPLAY=:1` and `xdotool`: `dotnet run` launched Zaide
(1280×800). Townhall input received typed message + Enter (send path); bottom
mode strip tabs Output, Test Results, and Debug clicked in sequence. Window
remained alive with no logged errors.

---

---

## M2 verification record (2026-07-19)

### Production changes

| File | Change |
|------|--------|
| `src/App/Shell/BottomPanelHost.cs` | **Added** internal host: mode strip, five panel surfaces, border chrome, splitter/panel grid attach, `WireToViewModel` visibility/mode routing, Terminal focus/start seam |
| `src/App/Shell/MainWindow.axaml.cs` | **Modified** `BuildLayout` delegates bottom section to `BottomPanelHost`; `WhenActivated` calls `_bottomPanelHost.WireToViewModel` instead of inline subscriptions |

`MainWindowViewModel` unchanged. `GridLayoutResizeHelper` untouched.

### Architecture baseline updates

| Metric | Before M2 | After M2 |
|--------|-----------|----------|
| Public / internal / total types | 339 / 106 / 445 | 339 / 107 / 446 |
| Tracked production source files | 407 | 408 |
| `App` folder source files | 37 | 38 |
| `Zaide.App.Shell` namespace types | 19 (14p / 5i) | 20 (14p / 6i) |

### Automated verification (2026-07-19)

```text
dotnet build Zaide.slnx
  → succeeded, 4 warnings (pre-existing, unchanged on clean rebuild)

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.App.Shell'
  → Passed: 160, Failed: 0, Skipped: 0, Total: 160

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Architecture'
  → Passed: 26, Failed: 0, Skipped: 0, Total: 26

dotnet test (full suite)
  → Passed: 2508, Failed: 0, Skipped: 0, Total: 2508

git diff --check
  → clean
```

No new focused `BottomPanelHost` construction tests in this milestone: existing
`MainWindowViewModelBottomPanelModeTests` retain bottom-panel mode routing
coverage at the ViewModel seam; attempted host construction tests could not
reliably bootstrap `Application.Current` when co-loaded with shell
`RxAppBuilder` static constructors in the same test assembly.

### Manual smoke (2026-07-19, completed for M2 resubmission)

Linux `DISPLAY=:1` with `xdotool` (`/tmp/zaide-m2-smoke.sh`):

1. **Default size (1280×800):** `dotnet run` launched Zaide; bottom panel shown
   via `Ctrl+J`; mode strip clicked in order — **Terminal**, **Problems**,
   **Output**, **Test Results**, **Debug**; panel hidden then reshown via
   `Ctrl+J`; bottom horizontal splitter dragged ~40px to resize panel height.
2. **Minimum size (960×600):** same cycle repeated — all five modes, hide/show,
   splitter drag.
3. Window remained alive throughout; app log contained no errors or exceptions.

Prior partial smoke (toggle-only) superseded by this run.

---

## M3 verification record (2026-07-19)

### Production changes

| File | Change |
|------|--------|
| `src/App/Shell/RightColumnHost.cs` | **Added** internal host: editor tab bar, search bar, editor/welcome grid, vertical splitter, agent panel host, `AttachToLayoutGrid` |
| `src/App/Shell/MainWindow.axaml.cs` | **Modified** `BuildLayout` delegates column 5 to `RightColumnHost`; constructor aliases host surfaces for existing `WhenActivated` wiring |
| `tests/Zaide.Tests/App/Shell/RightColumnHostSourceTests.cs` | **Added** type/source ratchet tests for host surface and layout placement |

`MainWindowViewModel`, `GridLayoutResizeHelper`, `AgentPanelHost`, `IAgentPanelHost`, and agent send orchestration unchanged.

### Architecture baseline updates

| Metric | Before M3 | After M3 |
|--------|-----------|----------|
| Public / internal / total types | 339 / 107 / 446 | 339 / 108 / 447 |
| Tracked production source files | 408 | 409 |
| `App` folder source files | 38 | 39 |
| `Zaide.App.Shell` namespace types | 20 (14p / 6i) | 21 (14p / 7i) |

### Host construction test note

Full `RightColumnHost` construction is not exercised in unit tests: the host always builds
`EditorView`, whose constructor allocates `EditorCompletionPopup` and reads
`Application.Current.Resources` at construction time. In `Zaide.Tests`,
`RxAppBuilder.BuildApp()` leaves `Application.Current` null and Avalonia does not
expose a supported setter in this stack version. Coverage uses
`RightColumnHostSourceTests` (public surface + source placement ratchets) plus
existing Editor/Agent/Shell presentation and activation tests for the wiring seams
`MainWindow` still owns.

### Automated verification (2026-07-19)

```text
dotnet build Zaide.slnx
  → succeeded, 4 warnings (pre-existing, unchanged)

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.App.Shell'
  → Passed: 164, Failed: 0, Skipped: 0, Total: 164

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents.Presentation'
  → Passed: 49, Failed: 0, Skipped: 0, Total: 49

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Features.Editor.Presentation'
  → Passed: 323, Failed: 0, Skipped: 0, Total: 323

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Architecture'
  → Passed: 26, Failed: 0, Skipped: 0, Total: 26

dotnet test (full suite)
  → Passed: 2512, Failed: 0, Skipped: 0, Total: 2512

git diff --check
  → clean
```

New focused tests: `RightColumnHostSourceTests` (4).

### Manual smoke (2026-07-19)

Linux `DISPLAY=:1` with `xdotool` (`/tmp/zaide-m3-smoke.sh`):

1. **Default size (1280×800):** opened workspace folder, opened README tab, invoked search
   (`Ctrl+F`), dragged editor/agent vertical splitter, created agent panel tab, sent
   `m3 smoke hello` via Enter.
2. **Minimum size (960×600):** repeated the same editor/search/splitter/agent-send cycle.
3. Window remained alive throughout; app log contained no errors or exceptions.

---

## M4 verification record (2026-07-19)

### Production changes

| File | Change |
|------|--------|
| `src/App/Shell/MainLayoutBuilder.cs` | **Added** internal builder: root grid column/row definitions, nav/left/townhall placement, horizontal splitters, status bar, host attachment |
| `src/App/Shell/MainWindow.axaml.cs` | **Modified** delegates layout construction to `MainLayoutBuilder`; removes inline `BuildLayout` |
| `tests/Zaide.Tests/App/Shell/MainLayoutBuilderSourceTests.cs` | **Added** type/source ratchet tests for builder surface and geometry |
| `tests/Zaide.Tests/App/Shell/RightColumnHostSourceTests.cs` | **Modified** right-column delegation ratchet now targets `MainLayoutBuilder` |

`MainWindowViewModel`, `GridLayoutResizeHelper`, `WhenActivated` wiring, settings attach,
and command-palette overlay placement unchanged.

### Architecture baseline updates

| Metric | Before M4 | After M4 |
|--------|-----------|----------|
| Public / internal / total types | 339 / 108 / 447 | 339 / 109 / 448 |
| Tracked production source files | 409 | 410 |
| `App` folder source files | 39 | 40 |
| `Zaide.App.Shell` namespace types | 21 (14p / 7i) | 22 (14p / 8i) |

### Automated verification (2026-07-19)

```text
dotnet build Zaide.slnx
  → succeeded, 4 warnings (pre-existing, unchanged)

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.App.Shell'
  → Passed: 168, Failed: 0, Skipped: 0, Total: 168

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Architecture'
  → Passed: 26, Failed: 0, Skipped: 0, Total: 26

dotnet test (full suite)
  → Passed: 2516, Failed: 0, Skipped: 0, Total: 2516

git diff --check
  → clean
```

New focused tests: `MainLayoutBuilderSourceTests` (4).

### Manual smoke (2026-07-19)

Linux `DISPLAY=:1` with `xdotool` (`/tmp/zaide-m4-smoke.sh`):

1. **Default size (1280×800):** dragged left/townhall and townhall/editor horizontal
   splitters; layout remained stable.
2. **Minimum size (960×600):** repeated splitter drags at reduced geometry.
3. Window remained alive throughout; app log contained no errors or exceptions.

---

## M5 verification record (2026-07-19)

### Production changes

| File | Change |
|------|--------|
| `src/App/Shell/SettingsPanelAttachHost.cs` | **Added** internal host: settings panel factory/attach/detach, `ShowSettings` interaction, left-panel restore, editor focus restore, deactivate `ClosePanel` |
| `src/App/Shell/ShellOverlayFocusWiring.cs` | **Added** internal static wiring: command-palette open/dismiss + editor-search focus/selection/dismiss restore |
| `src/App/Shell/MainWindow.axaml.cs` | **Modified** delegates settings lifecycle and overlay focus wiring to extracted helpers; `WhenActivated` shrinks (~630 → ~486 LOC) |

`MainWindowViewModel`, `GridLayoutResizeHelper`, layout hosts, and keybinding
materialization unchanged.

### Architecture baseline updates

| Metric | Before M5 | After M5 |
|--------|-----------|----------|
| Public / internal / total types | 339 / 109 / 448 | 339 / 111 / 450 |
| Tracked production source files | 410 | 412 |
| `App` folder source files | 40 | 42 |
| `Zaide.App.Shell` namespace types | 22 (14p / 8i) | 24 (14p / 10i) |

### Automated verification (2026-07-19)

```text
dotnet build Zaide.slnx
  → succeeded, 4 warnings (pre-existing, unchanged)

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.App.Shell'
  → Passed: 175, Failed: 0, Skipped: 0, Total: 175

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Features.Settings.Presentation'
  → Passed: 66, Failed: 0, Skipped: 0, Total: 66

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Features.Editor.Presentation'
  → Passed: 323, Failed: 0, Skipped: 0, Total: 323

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Architecture'
  → Passed: 26, Failed: 0, Skipped: 0, Total: 26

dotnet test (full suite)
  → Passed: 2523, Failed: 0, Skipped: 0, Total: 2523

git diff --check
  → clean
```

New focused tests: `SettingsPanelAttachHostTests` (7).

### Manual smoke (2026-07-19)

Linux `DISPLAY=:1` with `xdotool` (`/tmp/zaide-m5-smoke.sh`):

1. **Default size (1280×800):** opened workspace folder and README tab; toggled
   settings via status-bar gear (open + close); opened command palette
   (`Ctrl+Shift+P`), typed query, dismissed with Escape; opened search (`Ctrl+F`),
   typed query, dismissed with Escape.
2. **Minimum size (960×600):** repeated the same settings/palette/search cycle.
3. Window remained alive throughout; app log contained no errors or exceptions.

---

## M6 verification record (2026-07-19)

### Production changes

| File | Change |
|------|--------|
| `src/UI/DesignSystem/PaletteTokens.cs` | **Modified** added `SurfaceBaseBrush` (#0A0F19), `SeparatorBrush` (#070C16), `WarningBrush` (#FCBB47) with pixel-identical fallback values from `App.axaml` |
| `src/Features/Townhall/Presentation/TownhallView.cs` | **Modified** constructor extracted into `BuildSidebar`, `BuildFilterGroup`, `BuildChatArea`, `BuildMainLayout` helpers; `WireViewModel` filter wiring extracted into `WireFilterButton` helper; resource lookups replaced with `PaletteTokens` |
| `src/Features/Townhall/Presentation/TownhallChannelPanel.cs` | **Modified** `Application.Current!.Resources` lookups replaced with `PaletteTokens.TextPrimaryBrush`/`TextSecondaryBrush`; hover/active overlay colors extracted to named constants |
| `src/Features/Townhall/Presentation/TownhallPeoplePanel.cs` | **Modified** `WarningBrush` resource lookup replaced with `PaletteTokens.WarningBrush`; hover overlay extracted to named constant |
| `src/Features/Townhall/Presentation/TownhallChatPanel.cs` | **Modified** all `resources?[...]` lookups replaced with `PaletteTokens` equivalents (`WarningBrush`, `TextSecondaryBrush`, `SurfacePanelBrush`) |

No ViewModel, domain, or entry-projection files changed. No new production types or source files.

### Architecture baseline

No change — `PaletteTokens` already exists as an internal type; only additive property accessors were added. Public type count remains **339** public / **111** internal / **450** total (same as M5 baseline). Tracked production source files unchanged at **412**.

### Automated verification (2026-07-19)

```text
dotnet build Zaide.slnx
  → succeeded, 4 warnings (pre-existing, unchanged)

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Features.Townhall'
  → Passed: 81, Failed: 0, Skipped: 0, Total: 81

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.Architecture'
  → Passed: 26, Failed: 0, Skipped: 0, Total: 26

dotnet test --filter 'FullyQualifiedName~Zaide.Tests.UI.DesignSystem'
  → Passed: 30, Failed: 0, Skipped: 0, Total: 30

dotnet test (full suite)
  → Passed: 2523, Failed: 0, Skipped: 0, Total: 2523

git diff --check
  → clean
```

### Manual smoke (2026-07-19)

Linux `DISPLAY=:1` with `xdotool` (`/tmp/zaide-m6-smoke.sh`):

1. **Channel switch:** clicked second channel row (~70, 110) in left sidebar to trigger `SelectChannelCommand`.
2. **Filter toggle — Chat:** clicked Chat button position (~225, 55) — activates `ChatOnly` mode.
3. **Filter toggle — Activity:** clicked Activity button position (~300, 55) — activates `ActivityOnly` mode.
4. **Filter toggle — All:** clicked All button position (~160, 55) — activates `All` mode (default).
5. **Send message:** clicked input area (~640, 700), typed "m6 smoke test", pressed Enter to send.
6. Window remained alive throughout; app log contained no errors or exceptions.

All three M6-required operations exercised: channel selection, filter toggle
(Chat/Activity/All through the extracted `WireFilterButton` helper), and send.

---

*Last updated: 2026-07-19 (Refactor 8 M6 accepted at `1ca1e08`; M7+ unauthorized; Phase 14 unauthorized)*
