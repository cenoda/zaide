# Phase 23 — TOFIX

## Status

**Active work board only.** No formal `IMPLEMENTATION_PLAN.md` and **no M0**
gate. Phase 22 critical path remains complete (G5 PASS, 2026-08-05). This board
is for urgent product fixes that should not wait for V4 planning or a residual
debt program.

Product readiness is still not claimed. V4 / successor-roadmap planning still
requires a separate human decision. Phase 22.5 remains optional and separate.

**Indexing only (2026-08-05):** findings F1–F13 catalogued from live screenshots
and product direction; **difficulty indexed** (XS→XL). **No implementation in
this pass.** Empty-evidence semantics (missing ≠ zero / not fabricated) remain
intentional.

**High-priority bug (2026-08-05):** **F14 → [ISSUE-009](../../../issues/closed/ISSUE-009-production-di-test-contaminates-conversation-store.md)**
(production DI test wrote a test marker into user conversation drafts).

**New finding (2026-08-06):** F15 catalogued from screenshot/report (multiple clicks on "Open Folder" spawn concurrent picker dialogs).

**New finding (2026-08-06):** F16 catalogued from user report (Source Control commit message input box height fixed single-line with `AcceptsReturn = false`).

**XS/S wave complete (F13, F11, F2, F4, F14, F6, F15, F16 fixed). M wave complete (F1, F12, F9, F10, F3 fixed). L/XL wave: F5 fixed; remaining: F7 → F8 (optional).**

## Design direction (locked for Phase 23 indexing)

**Most configuration belongs in the Settings window, not in Townhall
inspection panels.**

| Surface | Owns |
|---------|------|
| **Settings window** | Durable / profile preferences and agent platform config (capture defaults, LLM endpoint already there, ACP path/args/identity expectations, application-default context policy, page-size limits, other knobs that survive restart) |
| **Townhall Trace / Memory / Usage** | Inspect evidence, refresh/retry, lifecycle actions that act on records (memory CRUD), close the surface — **not** a second settings form |
| **Townhall DM chrome** | Conversation-scoped **session** actions (bind/unbind for this actor, end session, temporary context override) may stay near the chat if they are not durable profile config; durable defaults still go to Settings |

Related deferred note closed by F5: `docs/deferred/closed/DF-006-more-settings-options.md`.

Current Settings UI only exposes **Editor / Terminal / LLM**
(`SettingsPanelView` + `SettingsModel` schema v3). Agent/transparency config
is largely absent from Settings and instead appears (or only exists) as
Townhall panel chrome.

## Temporal screenshots

| Artifact | Notes |
|----------|--------|
| `temp-screenshots/Screenshot_20260805_171418.png` | Session capture (moved from repo root 2026-08-05) |
| `temp-screenshots/Screenshot_20260805_171913.png` | Session capture (moved from repo root 2026-08-05) |
| `temp-screenshots/townhall-trace-memory-usage-all-open.png` | `#townhall-main` selected; All filter; Trace + Memory + Usage panels all open; workspace `global.json` shared in channel; clean empty stores |
| `temp-screenshots/townhall-chat-filter-all-panels-open-dm.png` | Chat filter selected; Zaide Agent DM; Trace + Memory + Usage still all open; backend unbound strip visible; reinforces F1 (filters ≠ panel exclusivity) |
| `temp-screenshots/shell-bottom-panel-crushes-townhall.png` | Problems bottom panel open and expanded; Townhall column crushed to a thin top band; Channels/memory chrome clipped; large empty Problems body; editor shows “Open a file to begin” |
| `temp-screenshots/bottom-panel-output-empty.png` | Output mode: title only, no lines, no next-action copy; mode strip has no selected-state highlight |
| `temp-screenshots/bottom-panel-test-results-empty.png` | Test Results mode: “No test results yet.” only; same opaque mode strip |
| `temp-screenshots/bottom-panel-debug-empty.png` | Debug mode: Debug Console + Call Stack / Variables unavailable captions; no start-debug affordance on the panel |
| `temp-screenshots/file-tree-selection-highlight-tail.png` | Explorer: pointer over `docs-rules.md` while selection/hover highlight still paints `qodana.yaml` and `townhall-trace-memory-…` (visual “tail” lag) |
| `temp-screenshots/source-control-unreadable-icons.png` | Source Control header: decorative branch glyph + circular refresh glyph unreadable at 14px; title text is the only clear label |
| `temp-screenshots/status-bar-dead-segment-buttons.png` | Status bar: `Zaide` (settings) + document / Ln,Col / language / C# Ready / project / branch look like buttons; only settings works |
| `temp-screenshots/source-control-selection-and-no-unstage-all.png` | SC: Changes(0) / Staged(10); multiple staged rows look selected at once; per-row `−` only; no Unstage All; Stage All absent when unstaged empty |
| `temp-screenshots/settings-right-aligned.png` | Settings overlay: 520px form column pinned to the right; large empty region on the left |
| `temp-screenshots/open-folder-multi-click-picker.png` | File tree header / Ctrl+O: multiple clicks on "Open Folder" spawn concurrent picker dialogs |
| (workspace status bar in several captures) | Selecting a `.png` in the explorer shows `Unsupported file type: png` — images not openable in editor |

## Difficulty index (2026-08-05)

Scale (effort for a careful implementer who already knows the repo):

| Level | Meaning |
|-------|---------|
| **XS** | One surface, ~1–20 lines, root cause known, low test risk |
| **S** | One feature area, clear change, small tests or manual smoke |
| **M** | Multi-file + intentional tests; possible a11y / selection edge cases |
| **L** | Multi-surface UX + copy + tests; product choices affect shape |
| **XL** | Schema / migration / IA move; Phase 22 proof updates; high blast radius |

| ID | Title | Severity | **Difficulty** | Confidence in root cause | Suggested batch |
|----|-------|----------|----------------|--------------------------|-----------------|
| **F14** | Production DI test contaminates conversation drafts (ISSUE-009) | **High** | **XS–S** | High (marker + dispose flush) | Solo data-safety |
| **F13** | Settings right-aligned | Low–Med | **XS** | High (`HorizontalAlignment.Right`) | Solo or with F5 later |
| **F11** | Status bar dead segment buttons | High | **S** | High (explicit no-op `StatusSegmentCommand`) | Solo shell chrome |
| **F2** | Duplicate empty-state captions | Med | **S** | High (status + summary both set) | With F3 |
| **F4** | Filter vs Trace/Memory/Usage toolbar | Low–Med | **S** | High (two control systems, one strip) | With F1 if cheap |
| **F6** | Bottom panel crushes content | Med | **S** | High (no content `MinHeight`; splitter) | Solo shell layout |
| **F1** | Trace/Memory/Usage stack, kill chat | High | **M** | High (independent open flags + Auto rows) | Solo transparency host |
| **F12** | SC weird selection + no Unstage All | Med–High | **M** | High (dual bind; no `UnstageAll` API) | Solo Source Control |
| **F9** | File tree highlight “tail” | Med | **M** | Med–High (hover animation + full repaint) | Solo explorer |
| **F10** | Unreadable icons | Med | **M** | Med–High (stroke on fill paths) | Solo icon system |
| **F3** | Empty surfaces full chrome | Med | **M–L** | High (panels always paint full control stack) | With F2; align F5 |
| **F7** | Bottom panel purpose unclear | Med–High | **L** | High (product IA; empty producers) | After F6; links DF-007 |
| **F8** | PNG / images not openable | Low–Med | **M–L** | High (unsupported type) — **policy choice A/B** | Optional / later |
| **F5** | Move most config to Settings | High | **XL** | High direction; implementation wide | Last / own milestone |
| **F15** | Multiple clicks on "Open Folder" spawn concurrent picker dialogs | Low–Med | **XS–S** | High (`PointerPressed` / picker re-entrancy guard missing) | Solo explorer / shell |
| **F16** | Commit message input box height fixed single-line | Cosmetic | **XS** | High (`SourceControlPanel.cs` `_commitInput` `AcceptsReturn = false`, `Height = 32`) | Solo Source Control / UX |

**Difficulty-first fix order (not severity-only):**

1. **Data-safety first:** **F14 / ISSUE-009** (user store contamination)
2. **XS/S wave:** F13 → F11 → F2 → F4 → F6 → F15
3. **M wave:** F1 → F12 → F9 → F10 → F3 (with F2 if not done)
4. **L/XL wave:** F7 → F8 (optional); **F5 complete**

Do **not** start F5 in the same commit as XS/S polish. Do **not** claim product
readiness from this board alone.

## Work Board

### F14 — Production DI test contaminates persisted conversation drafts (ISSUE-009)

- [x] Fixed (2026-08-05) — `ProgramConfigureServices_ResolvesTownhallServicesAsSingletons`
      no longer assigns `DraftText` / marker `m6g-townhall-di-singleton-sync` on a
      production-composed provider; singleton identity only (`Assert.Same`). Source
      guard blocks reintroduction of the marker and of `DraftText` assignment in that
      method. Local machine scrub: backup then delete draft keys whose value equals
      the marker (conversations / last-read / active id untouched). See
      [ISSUE-009](../../../issues/closed/ISSUE-009-production-di-test-contaminates-conversation-store.md).

**Severity:** High (user-data + false “default” composer text)  
**Difficulty:** XS–S  
**Area:** Test isolation + conversation persistence  
**Source:** Live composer showed `m6g-townhall-di-singleton-sync` on channel-1 and
Zaide Agent DM; confirmed in `~/.config/zaide/conversations/conversations.json`
and `.lastknowngood`.

**Observed behavior:** Running the Townhall production DI singleton test wrote a
test marker into the user’s production conversation drafts via
`ConversationPersistenceService` dispose flush.

**Expected behavior:** Tests never read/write production conversation (or
settings/secrets) paths when mutating presentation state. Composer shows only
user drafts or normal product placeholders.

**Notes:** Data scrub is machine-local and must not be committed. Restart Zaide
or reselect the conversation after scrub if the app was already running.

---

### F1 — Opening Trace / Memory / Usage stacks all three and displaces chat

- [x] Fixed (2026-08-06) — option **B** dedicated inspect side sheet
      (`AgentInspectHost`) beside the chat message list inside the Star band;
      Trace / Memory / Usage no longer Auto-row under chat. Plan:
      [F1_HOST_PLAN.md](./F1_HOST_PLAN.md). Covered by
      `Phase23InspectHostChatStarRowTests` + toggle tests.
- [x] Follow-up (2026-08-06) — inspect surfaces **mutually exclusive**: opening
      one closes the others via `CloseSiblingInspectSurfaces` on open paths;
      toggle-close and per-panel Close still affect only the active surface.
- [x] Session 2c slice (2026-08-05) — toolbar Trace / Memory / Usage openers
      now **toggle** (open when closed, close when open) via
      `ToggleTraceCommand` / `ToggleMemoryCommand` / `ToggleUsageCommand` on
      `AgentTransparencyManagementViewModel`; per-panel Close still works; open
      flags remain independent (no forced exclusivity). This is a
      discoverability slice of F1, not the F1 layout fix. Covered by
      `Phase23ToggleTransparencyOpenersTests`.

**Severity:** High
**Difficulty:** M
**Area:** Townhall chat column layout + transparency panel open flags
**Source:** `temp-screenshots/townhall-trace-memory-usage-all-open.png`

**Observed behavior:**
- Trace, Memory, and Usage each have independent open flags
  (`IsTracePanelOpen` / `IsMemoryPanelOpen` / `IsUsagePanelOpen`). Opening one
  does not close the others.
- All three panels host as separate `Auto` rows under the chat `Star` row in
  `TownhallView.BuildChatArea`. With more than one panel open, the chat message
  surface is squeezed; with all three open the message list effectively
  disappears and only the composer remains at the bottom.
- Each panel has its own Close control, so the user must close them one by one
  to recover chat space.

**Expected behavior:**
- Opening a transparency surface should either:
  - be mutually exclusive (switch Trace ↔ Memory ↔ Usage), or
  - use a dedicated host that does not permanently steal the chat column
    (overlay / side sheet / single stacked host with one active surface).
- The primary chat message list for the selected conversation must remain
  usable while inspecting evidence.

**Notes for implementers:**
- Do not “fix” by inventing evidence when stores are empty.
- Prefer the smallest presentation/layout change; open-flag independence may be
  intentional for tests — verify Phase 22.4 / 22.5 proofs before changing
  exclusivity semantics.

---

### F2 — Empty-state captions are duplicated inside each panel

- [x] Fixed (2026-08-05) — status is the primary empty/unavailable channel on Trace,
  Memory, and Usage; panel `ApplyProjection` no longer rewrites the same fact into
  summary. Empty Memory clears summary; empty Usage and capture-disabled Trace keep
  policy help (`not zero` / `not empty fabrication`) once in summary only.
  Covered by `Phase23TransparencyCaptionProjectionTests`.
- [x] Residual (2026-08-05 Session 2b) — Unavailable / workspace-required dual-write:
  Memory and Usage `ApplyProjection` cleared summary for `Unavailable` (status keeps
  the primary denial). Tests: `MemoryPanel_Unavailable_DoesNotDualWriteStatusIntoSummary`,
  `UsagePanel_Unavailable_DoesNotDualWriteStatusIntoSummary`.

**Severity:** Medium
**Difficulty:** S
**Area:** Trace / Memory / Usage panel caption projection
**Source:** same screenshot

**Observed behavior:**
- **Trace:** status line shows `Trace capture disabled.`; summary immediately
  under it shows `Trace capture disabled — missing evidence is not empty
  fabrication.` (same fact twice with different wording).
- **Memory:** status caption and summary caption both show
  `No durable memory records for the opened workspace.` (identical string).
- **Usage:** status shows `No usage or cost evidence for the opened workspace.`;
  summary repeats that sentence and appends `Missing evidence is not zero.`

**Expected behavior:**
- One primary empty / unavailable message per surface.
- Secondary policy wording (missing ≠ fabricated / missing ≠ zero) may appear
  once, preferably as help text or a single short subtitle — not a near-clone
  of the status line.

**Notes for implementers:**
- Status captions come from inspection ViewModels; panel `ApplyProjection`
  often writes a second summary string for the same surface state. Collapse
  those roles before rewriting copy.

---

### F3 — Empty surfaces still render full inspection chrome

- [x] Fixed (2026-08-06) — empty / capture-off inspect surfaces hide record
      selectors, paging, lifecycle toolbars, and standing create-denial copy;
      operational actions (Create, Refresh, Close, Open Settings when capture
      off) remain. Plan: [F3_EMPTY_CHROME_PLAN.md](./F3_EMPTY_CHROME_PLAN.md).
      Commit `1a2c78a6`. Covered by `Phase23EmptyInspectChromeTests` (+ caption/a11y
      test updates).

**Severity:** Medium
**Difficulty:** M–L
**Area:** Empty / disabled Trace, Memory, Usage panel chrome
**Source:** same screenshot

**Observed behavior:** With zero records (and, for Trace, capture disabled),
each open panel still shows a dense control stack:

| Control class | Trace | Memory | Usage |
|---------------|-------|--------|-------|
| Record selector (empty / disabled) | Yes | Yes | Yes |
| `No records.` + `No record selected.` | Yes | Yes | Yes |
| Paging caption (`Page size 64 (max 256)`) | Yes | — | — |
| Capture / Refresh / Close actions | Yes | Refresh / Retry / Close | Yes |
| Full lifecycle toolbar (Create / Correct / Disable / Supersede / Delete) | — | Yes (mostly disabled) | — |
| Draft text box + scope selector | — | Yes | — |
| Standing create denial (`Content is required to create memory.`) | — | Yes, with empty draft | — |
| Standing influence disclaimer | — | Yes | — |

The empty state reads like a broken form, not a quiet “nothing here yet /
capture off” surface.

**Expected behavior:**
- Empty or capture-disabled states should show a short status, the one or two
  **operational** actions that make sense next (e.g. Create first memory,
  Refresh, Close, or a single “Open Settings” affordance when capture is off
  by policy — see **F5**), and hide or collapse selectors, paging, selection
  captions, and mutation toolbars that cannot act yet.
- Create-denial text should appear after a denied submit attempt (or while
  the user is actively editing an invalid draft), not as permanent chrome on
  an empty panel.
- Capture toggles and page-size knobs are **config** (F5), not empty-state
  chrome that should stay permanently on the inspection panel.

**Notes for implementers:**
- Keep failure / unavailable states distinct from empty (do not collapse Failed
  into Empty). Phase 21 / 22 contracts care about that distinction.
- Accessibility names and keyboard paths must remain coherent if controls are
  hidden rather than merely disabled.

---

### F4 — Toolbar confuses message filters with transparency openers

- [x] Fixed (2026-08-05) — `BuildFilterGroup` nests All/Chat/Activity message-filter
  toggles separately from Trace/Memory/Usage opener buttons, with a vertical separator
  and distinct automation group names; filter toggles keep exclusive semantics and
  named a11y labels. Covered by `Phase23TownhallToolbarTests`.
- [x] Residual (2026-08-05 Session 2b) — visual punch-up: pill toggles on a raised filter
  cluster, outlined opener buttons (`CreateTransparencyOpenerButton`), wider/contrasty
  separator + larger inter-group gap so the strip no longer reads as one peer tab row.

**Severity:** Low–Medium
**Difficulty:** S
**Area:** Townhall filter / entry toolbar affordance
**Source:** same screenshot; `TownhallView.BuildFilterGroup`

**Observed behavior:**
- One horizontal group presents `All` / `Chat` / `Activity` / `Trace` /
  `Memory` / `Usage` as peer controls.
- The first three are mutually exclusive message-filter toggles.
- The last three are independent panel openers and do not participate in the
  filter toggle group.
- Visually they look like one tab strip; behaviorally they are two different
  control systems. That encourages opening multiple evidence panels while
  believing one “tab” replaced another (feeds F1).

**Expected behavior:**
- Filters and transparency entry points should be visually and/or structurally
  separated (grouping, spacing, control type, or placement) so “filter the
  feed” and “open an evidence surface” are not the same gesture family.

---

### F5 — Most agent / transparency config should live in Settings, not Townhall panels

- [x] Fixed (2026-08-06) — **Agents** section in Settings (schema v4); capture defaults,
      ACP identity, context policy, and trace paging persisted via `ISettingsService`;
      Townhall trace/usage capture toggles removed; backend binding shows read-only ACP
      defaults with Open Settings deep-link (same `ShowSettings` path as status bar).
      Plan: [F5_SETTINGS_HOME_PLAN.md](./F5_SETTINGS_HOME_PLAN.md). Commits
      `84dd8666` (schema), `2a9ce53f` (Settings UI + sync), `f2ab335b` (Townhall chrome),
      `7c2e491c` (tests + docs). Covered by `Phase23SettingsAgentsTests`,
      `Phase23F5TownhallConfigTests`, updated Phase 22/23 transparency tests.

**Severity:** High (product direction; unblocks F3 chrome reduction)
**Difficulty:** XL
**Area:** Settings window vs Townhall Trace / Memory / Usage / backend binding
**Source:** Human product direction 2026-08-05; screenshot chrome inventory; current `SettingsModel` / `SettingsPanelView` surface

**Observed behavior:**
- Settings today: Editor, Terminal, LLM only (`SettingsModel` schema v3 —
  `Editor` / `Llm` / `Keybindings` / `Debug`; panel sections Editor / Terminal /
  LLM).
- Townhall Trace / Usage panels host **Enable/Disable capture** toggles next to
  inspection chrome.
- Trace panel surfaces **page size** as inspection caption/constants
  (`DefaultPageSize` / `MaxPageSize` on transparency management VM) without a
  Settings home.
- Townhall backend binding panel hosts **ACP executable path, arguments,
  expected name/version** inputs plus bind/unbind/session actions
  (`AgentBackendBindingPanel`).
- Context policy application default is hardcoded; only a **session override**
  selector appears on DM Townhall (lost on restart — see A3 context-policy
  notes). No Settings entry for the durable default.
- Net result: users hit dense “settings-like” forms while trying to read chat
  or empty evidence, and durable prefs are split or missing from the real
  Settings window.

**Inventory — move to Settings (durable / profile config)**

| Item | Today | Target |
|------|-------|--------|
| Trace capture default (on/off) | Trace panel toggle | Settings → Agents / Transparency |
| Usage capture default (on/off) | Usage panel toggle | Settings → Agents / Transparency |
| Trace page size / max page size | Constants + panel caption | Settings (if user-configurable); else keep constants and drop panel chrome |
| LLM model / base URL / API key | Already in Settings | Stay in Settings |
| ACP executable, non-secret args, expected name/version | Backend binding panel text boxes | Settings → Agents / ACP (or LLM-adjacent Agent section) |
| Application-default context policy level | Hardcoded `Standard` | Settings → Agents |
| Any future retention / export / redaction prefs | N/A or non-UI | Settings when exposed |

**Inventory — keep out of Settings (ops / inspection / session)**

| Item | Home |
|------|------|
| Open Trace / Memory / Usage, list/select records, Refresh / Retry / Close | Townhall evidence surface |
| Memory create / correct / disable / supersede / delete + draft | Townhall Memory surface |
| Bind / Unbind / End session / Probe / Authenticate / Logout for the **active** actor | Townhall DM workflow (reads defaults from Settings; does not own durable path fields) |
| Session-only context policy override + clear override | Townhall DM (optional thin control; default still Settings) |
| Status captions that evidence is missing (not zero / not fabricated) | Townhall evidence empty state (single copy — F2) |

**Expected behavior:**
- Opening Settings reveals an **Agents** (or equivalent) section where capture
  defaults, ACP non-secret config, and application-default context policy are
  editable and persisted through `ISettingsService` (schema migration as
  needed).
- Townhall Trace / Usage empty states **do not** double as capture settings
  forms; at most a read-only “Capture off — change in Settings” status plus
  optional deep-link to Settings.
- Backend binding panel keeps session actions; durable ACP path/identity
  fields are edited in Settings and shown read-only (or prefilled) at bind
  time.
- Secrets remain on `ISecretStore` (existing API key pattern); do not put
  secrets into ACP args or settings JSON incorrectly.

**Notes for implementers:**
- This is larger than a pure layout fix. Prefer sequencing: (1) F1 layout
  exclusivity so chat stays usable, (2) F2/F3 chrome strip on panels, (3) F5
  Settings section + schema only for items already toggled in UI today
  (capture first), then ACP fields / context default as follow-ons if still
  authorized.
- Schema bump (`SettingsModel` + migrator) is required for any new durable
  fields; do not invent parallel config files.
- Phase 22.4 reachability/a11y tests that assert capture buttons on panels
  must be updated deliberately when toggles move — do not silently break
  proofs.
- Cross-link: DF-006 closed by F5 Agents section (see `docs/deferred/closed/DF-006-more-settings-options.md`).

---

### F6 — Bottom panel can crush Townhall / editor content (needs confirmation)

- [x] Fixed (2026-08-05) — content row `MinHeight = 200` so the bottom-panel
      GridSplitter cannot crush Townhall/editor to zero; open bottom panel keeps
      `MinHeight = 80` (default open height still 250). Shell layout only
      (`MainLayoutBuilder` + `BottomPanelHost`).

**Severity:** Medium (High if it happens without intentional drag)
**Difficulty:** S
**Area:** Shell main grid row layout — content vs bottom panel splitter
**Source:** `temp-screenshots/shell-bottom-panel-crushes-townhall.png`
**Classification:** **Probable UX / layout bug**, not yet proven as spontaneous.
  May be user-driven resize; still a product defect if no floor exists.

**Observed behavior:**
- Problems bottom mode is open (`Terminal | Problems | Output | Test Results |
  Debug` strip visible mid-window).
- Townhall (people, channels, Trace/Memory chrome) is compressed into a thin
  top band of the center column; channel list and memory panel text are
  clipped mid-content.
- Problems body occupies most of the vertical space under center + editor
  (by design the bottom panel spans columns 3–5 only; explorer stays full
  height — which makes the crush look even more broken).
- Chat composer and usable conversation surface are gone while Problems shows
  only “Problems 0 / No problems.”

**Why this may or may not be a “hard” bug:**
- Default open height is **250px** (`BottomPanelHost.ApplyBottomPanelVisibility`).
  The capture is far taller than 250px → something resized the panel row
  (almost certainly the NS `GridSplitter` between content and bottom panel).
- Content row 0 is `1*` with **no `MinHeight`**; bottom panel row is a pixel
  height with no max clamp relative to window size. Avalonia splitters can
  therefore grow the bottom row until Townhall/editor content approaches zero.
- So: not necessarily a spontaneous corruption. It **is** a missing layout
  guard: user (or any resize path) can make the primary workspace unusable,
  and recovery is non-obvious (drag the thin splitter back down).

**Expected behavior:**
- Content row (Townhall + editor band) keeps a usable minimum height (e.g.
  floor in px or % of window) so the bottom panel cannot fully consume it.
- Optionally clamp max bottom-panel height (e.g. ≤ 50–60% of content area).
- Double-click splitter or mode re-select could reset to the default 250px
  (nice-to-have).
- Explorer full-height vs bottom-under-center+editor is intentional; do not
  “fix” by spanning the bottom panel under the file tree unless design
  changes.

**Notes for implementers:**
- Repro: open bottom panel → drag top edge of panel upward aggressively →
  observe Townhall/editor crush. Confirm whether any non-drag path (mode
  switch, window, reopen) can also leave a huge pixel height (splitter
  height is not reset on hide/show — `ApplyBottomPanelVisibility` always
  reassigns 250 when becoming visible, so reopen should reset; pure drag is
  the main path).
- If hide/show always resets to 250, document that as recovery for users for
  now; still add MinHeight so drag cannot strand the layout.
- Keep separate from F1 (Townhall-internal panel stack); this is shell grid
  rows.

**Additional evidence note (not a new finding):**
- `temp-screenshots/townhall-chat-filter-all-panels-open-dm.png` shows Chat filter
  active while all three transparency panels remain open — additional F1
  confirmation that message filters and Trace/Memory/Usage openers are
  independent systems.

---

### F7 — Bottom panel: only Terminal feels usable; other modes have no clear product job

- [ ] Not fixed

**Severity:** Medium–High (product clarity / discoverability)
**Difficulty:** L
**Area:** Shell bottom panel as a whole
**Source:** clarified user report 2026-08-05 + empty-mode captures below
**Related:** `docs/deferred/open/DF-007-debugger-output-discoverability.md`

**User report (clarified):** They do not know **what the bottom panel is for**.
From live use, **only Terminal is a usable surface**. Problems / Output /
Test Results / Debug read as empty dead zones, not as tools with a job.

**What those five modes are *supposed* to be (code / product intent — not
user-visible today):**

| Mode | Intended purpose | When it becomes useful | Clickable when populated |
|------|------------------|------------------------|--------------------------|
| **Terminal** | Interactive shell | Always (user types) | Input / session tabs |
| **Problems** | Diagnostics (LSP + build) | After language/build diagnostics exist | Double-click / Enter → source |
| **Output** | Read-only build/run/test process stdout/stderr | After a project workflow run | Mostly read-only; Cancel while running |
| **Test Results** | Structured last `dotnet test` outcomes | After a test run | Double-click / Enter when location known |
| **Debug** | Debug Console + Call Stack + Variables | During an active DAP debug session | Stack / variable selection when stopped |

**Observed behavior:**
1. **Product job of the bottom panel is not explained anywhere in UI.** There
   is no strip subtitle, empty-state mission copy, or onboarding that says
   “Terminal + tool results for build/test/debug/diagnostics.”
2. **Only Terminal is self-sufficient.** The other four depend on external
   actions (build, test, debug, LSP) that are themselves hard to discover
   (DF-007). On a clean idle workspace they stay empty forever.
3. **Empty states do not say what to do next** (see captures).
4. **No selected-state styling on the mode strip** — all five labels share
   `TextSecondaryBrush`; active mode is only implied by body content.
5. Empty bodies feel non-interactive (correct with zero rows; wrong combined
   with missing purpose). Mode handlers *do* switch content (user captured
   four empty modes); this is not primarily a dead-click bug.

**Expected behavior:**
- User can answer in one sentence: “Bottom panel = Terminal plus tool
  results (problems, build output, tests, debug).”
- Active mode is visually selected on the strip.
- Each empty mode states purpose + how content appears (or deep-links the
  command that produces it). Idle workspace still feels intentional, not broken.
- Do not invent fake diagnostics/output to look busy.

**Evidence:**
`temp-screenshots/shell-bottom-panel-crushes-townhall.png`,
`temp-screenshots/bottom-panel-output-empty.png`,
`temp-screenshots/bottom-panel-test-results-empty.png`,
`temp-screenshots/bottom-panel-debug-empty.png`

**Notes for implementers:**
- Selected style: consume existing `Is*BottomMode` flags (already on VM).
- Prefer honest empty copy over fake data. If a mode has no user-reachable
  producer yet, say that — do not leave a blank panel.
- F7 is IA/empty-state; F6 is layout crush. Keep separate.

---

### F8 — Workspace `.png` (and other images) are not openable in the editor

- [ ] Not fixed

**Severity:** Low–Medium (workflow friction while indexing UI evidence)
**Difficulty:** M–L (policy choice A vs B)
**Area:** Editor supported file types / explorer open path
**Source:** status bar text in bottom-panel captures: `Unsupported file type: png`
when a screenshot under the workspace is selected

**Observed behavior:**
- Selecting or opening a `.png` from the file tree does not show the image.
- Status bar reports `Unsupported file type: png` via
  `SupportedFileTypes` messaging.
- Screenshots used as phase evidence therefore cannot be reviewed inside Zaide;
  the user must use an external viewer.

**Expected behavior (product choice — pick one when implementing):**
- **A (minimal):** Keep images unsupported, but explorer should not feel like a
  broken open (clearer empty-state in the editor pane, not only status-bar
  text).
- **B (useful):** Preview images (read-only image view) for common formats
  (`png` / `jpg` / `webp` / `gif`) without claiming full binary editing.

**Notes for implementers:**
- Orthogonal to bottom-panel modes; indexed here because it blocked reviewing
  the same evidence files inside the app.
- YAGNI: do not build a full media suite; preview-only is enough if chosen.

---

### F9 — File tree selection/hover highlight lags the pointer (“tail”)

- [x] Fixed (2026-08-06) — instant hover (no `Animations.RunAsync` tail),
  `_hoveredRow` clears the previous row on enter and restores selection on
  exit; `RepaintAllFileTreeRows` keeps selection chrome in sync. Covered by
  `Phase23FileTreeHoverSelectionTests`.

**Severity:** Medium (interaction polish; looks like a paint bug)
**Difficulty:** M
**Area:** Explorer `FileTreeView` row decoration
**Source:** user report 2026-08-05 +
`temp-screenshots/file-tree-selection-highlight-tail.png`

**Observed behavior:**
- Moving the mouse (or selection) through the left file tree is slightly
  slower than the pointer.
- Capture shows the cursor over / near `docs-rules.md` while highlight paint
  still sits on **earlier rows** (`qodana.yaml`, and another
  `townhall-trace-memory-…` row further down) — a visible **tail** of
  selection/hover decoration trailing the pointer.
- Feels like sticky multi-highlight rather than a single row tracking the
  mouse/selection.

**Expected behavior:**
- At most one hover row and one selected row decoration at a time (plus
  intentional parent-folder tint of the selected file, if kept).
- Highlight tracks pointer/selection without a multi-row lag trail under
  normal mouse speeds.

**Likely implementation notes (for whoever fixes — not verified as root cause):**
- Custom row paint: hover on `PointerEntered` / `PointerExited`, selection via
  `ViewModel.SelectedFile` + `RepaintAllFileTreeRows()` walking **all** visual
  descendant `Border`s (`FileTreeView`).
- **Hover is animated:** enter/exit call `Animations.RunAsync(..., HoverBackground(...))`.
  Fast pointer motion leaves multiple rows mid-transition → classic visual
  “tail.” Strong first hypothesis for the screenshot.
- Full-tree repaint on every selection change can also lag behind pointer
  motion; enter/exit ordering races remain possible.
- Prefer: snappier hover (no or shorter animation), cancel in-flight hover
  animation on exit, invalidate only previous + new row; or drive selection
  visuals from TreeView selection pseudo-classes instead of O(n) manual repaint.


**Notes for implementers:**
- Reproduce by scrubbing the mouse quickly up/down a long root file list;
  screenshot or slow-mo if needed.
- Keep parent-of-selected folder tint if it still matches design; ensure it
  does not look like a second “selected” file row.
- Separate from F8 (unsupported png open).

---

### F10 — Icon symbols are unrecognizable (Source Control header captured)

- [x] Fixed (2026-08-06) — migrated to **Lucide.Avalonia 0.2.16** behind
      `IconFactory` + `IconLucideMap`; deleted `Icons.axaml` and unified NavBar
      (`Icon.Explorer`, `Icon.SourceControl`). Stroke width scales with size
      (~1.25–2.0).       Covered by `Phase23IconFactoryTests`
      (`IconFactory_Create_UsesLucideIconContract`,
      `IconFactory_SetForeground_UpdatesLucideForeground`,
      `IconLucideMap_AllKnownKeys_Resolve`,
      `IconLucideMap_IncludesLegacyAndNavKeys`,
      `NavBar_UsesIconFactory_NotInlinePaths`,
      `IconsAxaml_RemovedFromAppResources`,
      `SourceControlPanel_RefreshButton_HasTooltipAndAutomationName`,
      `IconOnlyControls_SourceHaveTooltipAndAutomationName`). Manual
      before/after screenshots at 14–16px belong in the PR description only.

- [x] Partial (2026-08-06) — icon-only controls got tooltip +
      `AutomationProperties.Name`; decorative Source Control header glyph removed.
      Covered by `Phase23IconFactoryTests` (a11y contracts; superseded by full
      Lucide migration above).

**Severity:** Medium (legibility / icon system quality)
**Difficulty:** M
**Area:** `IconFactory` + Phosphor `Icons.axaml` + icon-only controls
**Source:** user report 2026-08-05 +
`temp-screenshots/source-control-unreadable-icons.png`

**User report:** Cannot recognize what the icon symbols mean.

**Observed (this capture):**
- Source Control header shows a small abstract glyph left of the title
  (`Icon.GitBranch` via `IconFactory`, 14px) that does not read as a clear
  git-branch symbol.
- Right-side control is icon-only (`Icon.ArrowClockwise`, 14px) with **no
  tooltip / automation name** in `SourceControlPanel` — even if the shape
  were a clean refresh arrow, the meaning is not discoverable from text.
- Title string “Source Control” carries the panel identity; the icons add
  noise rather than recognition.

**Broader risk (same factory, not fully re-audited in this pass):**
- `IconFactory.Create` always paints geometry as **Stroke only**
  (`StrokeThickness = 16` on a 256×256 path, **no Fill**).
- `Icons.axaml` Phosphor paths are typical **filled** outline shapes. Stroke
  rendering of fill-oriented paths produces thick, mushy, or incomplete
  glyphs — especially at 14–16px Viewbox size.
- Any icon-only chrome that depends on this factory (refresh, nav, file-type,
  status) can fail the same “what is this symbol?” test.

**Expected behavior:**
- Icons either:
  - use **Fill** (and correct fill-oriented paths), or
  - use true stroke-oriented icon sets designed for outline rendering —
  not a mix that turns filled glyphs into unreadable outlines.
- Glyphs remain identifiable at the sizes used in headers/toolbars (~14–20px).
- Icon-only buttons always have **tooltip + accessible name** (e.g. “Refresh
  source control”).
- Decorative icons next to a text title may be dropped if they never become
  legible; do not keep mystery glyphs for decoration.

**Notes for implementers:**
- First verify: switch a known icon (ArrowClockwise / GitBranch) to
  `Fill = foreground`, `Stroke = null` (or dual-mode) and re-screenshot at
  14px — if recognition jumps, factory paint mode is the root fix.
- Audit `Icon.GitBranch` path data; the current geometry may also be a bad
  / nonstandard path independent of stroke vs fill.
- Nav bar uses **inline** stroke geometry (`NavBar.CreateNavIcon`), separate
  from `IconFactory` — check it in the same pass if users still cannot read
  explorer vs source-control rail icons (tooltips exist there: “Explorer” /
  “Source Control”).
- **Do not expand** hand-embedded Phosphor paths in `Icons.axaml`. Prefer a
  catalogued icon pack (`docs/LIBRARIES.md`) and an `IconFactory` adapter.
- **Implementation plan:** `F10_ICON_PACK_IMPLEMENTATION_PLAN.md`

---

### F11 — Status bar segments look clickable but do nothing (only Settings works)

- [x] Fixed (2026-08-05) — **Option A (display-only):** removed no-op
  `StatusSegmentCommand`; document/caret/language/project/branch (and language
  intelligence) are plain layout segments (no `Button`, no Hand cursor, no
  hover/press chrome). Settings remains the only interactive control
  (`OpenSettingsCommand`, tooltip + automation name “Settings”). Covered by
  `StatusBarTests`. Icon legibility tracked under F10 (icon pack migration).

**Severity:** High (false affordance / dead controls)
**Difficulty:** S
**Area:** Shell `StatusBar` segment buttons
**Source:** user report 2026-08-05 +
`temp-screenshots/status-bar-dead-segment-buttons.png`

**User report:** Bottom buttons are clickable but do nothing; only the config
(settings) control works.

**Observed behavior:**
- Status bar left cluster presents multiple **Button** segments with Hand
  cursor and hover/press backgrounds:
  - Settings (`Icon.Config` + “Zaide”) — **works** (`OpenSettingsCommand`)
  - Document (`Icon.Text` + name / “—”)
  - Caret (`Icon.Selection` + `Ln,Col …`)
  - Language (`Icon.Code` + language id)
  - Language intelligence (when visible)
  - Project (`Icon.Project` + project name)
  - Branch (`Icon.GitBranch` + branch name)
- All non-settings segments share:

  ```csharp
  private static readonly ICommand StatusSegmentCommand =
      ReactiveCommand.Create(() => { }); // intentional no-op
  ```

  wired via `BuildStatusSegmentButton(content)` →
  `Command = StatusSegmentCommand`, still with `Cursor = Hand` and hover
  chrome. Clicks succeed as button activations and run an empty command.
- Icons on those segments are also hard to read (same `IconFactory` stroke
  issue as **F10**).

**Expected behavior (pick a coherent product policy):**
- **A — Display-only status:** segments that have no action must not look like
  buttons (no Hand cursor, no hover/press fill, not a `Button` — use plain
  layout + text). Settings remains the only interactive control until others
  gain real commands.
- **B — Real actions:** keep button chrome only where a command exists, e.g.
  document → focus/open path, language → language picker, branch → Source
  Control / branch switch, project → project panel, caret → go-to-line.
  No segment may keep Hand + hover without a non-empty command.
- Hybrid is fine: settings + branch clickable; pure status text stays text.

**Notes for implementers:**
- Root cause is explicit in `StatusBar.cs` (`StatusSegmentCommand` no-op).
  This is not a binding failure.
- Do not “fix” by adding fake toast noise; either remove affordance or wire
  real navigation.
- When wiring actions, align with F5 (settings owns durable config) and F7
  (bottom tool panels) — status bar should deep-link, not duplicate forms.
- Accessibility: if remaining as buttons, each needs a name/tooltip that
  matches the real action; if display-only, do not expose as buttons to AT.

---

### F12 — Source Control: weird multi-row selection + no Unstage All

- [x] Fixed (2026-08-06) — exclusive SC list selection (no dual two-way
      `SelectedFileChange` bind); `UnstageAll` API + command + header button
      when `StagedCount > 0`. Covered by `Phase23SourceControlSelectionTests`,
      `UnstageAllCommand_*`, `GitMutationServiceTests.UnstageAll_*`.

**Severity:** Medium–High (broken selection UX + missing bulk action)
**Difficulty:** M
**Area:** Source Control panel change lists
**Source:** user report 2026-08-05 +
`temp-screenshots/source-control-selection-and-no-unstage-all.png`

**User report:** Source Control selection is weird; there is no Unstage All
button.

#### F12a — Selection looks wrong / multi-highlight

**Observed:**
- Capture shows **Staged (10)** with several adjacent rows simultaneously
  painted as selected/highlighted while `SelectionMode = Single`.
- Unstaged section is empty (`Changes (0)`); staged rows use per-file `−`.

**Code smell (leading hypothesis):**
- One VM property `SelectedFileChange` is **two-way bound to both**
  `_unstagedList.SelectedItem` and `_stagedList.SelectedItem`
  (`SourceControlPanel` WhenActivated).
- Selecting a staged file forces the unstaged ListBox to adopt the same
  object (not in its items) and vice versa → selection churn, nulling,
  and/or stale `ListBoxItem` selected visuals across rows.
- Both lists also fire `SelectionChanged` → `SelectFileCommand` independently
  while the shared bind updates the other list.

**Expected:**
- Exactly one selected change file at a time across the whole SC panel
  (or explicit multi-select only if product chooses multi — today is Single).
- Only the owning list (staged vs unstaged) shows selected chrome for that
  item; the other list has no selection.
- Selection still drives diff open via `SelectFileCommand` without flicker.

**Fix direction (for implementers):**
- Stop dual two-way bind to one property. Prefer: on each list’s
  `SelectionChanged`, set VM selection and **clear the other list’s**
  `SelectedItem`; or use one logical selection id and project into the
  list that owns the path.
- Verify after stage/unstage/refresh that selection is restored or cleared
  cleanly (existing refresh paths already try path matching).

#### F12b — Unstage All missing (Stage All exists)

**Observed:**
- **Stage All** exists on the unstaged header when `UnstagedCount > 0`
  (`StageAllCommand` → `IGitMutationService.StageAll`).
- Staged header is caption-only (`Staged (N)`); **no Unstage All** control.
- Per-row unstage is only the tiny `−` button (`UnstageFileCommand` →
  `Unstage` single path).
- `IGitMutationService` has `Stage` / `StageAll` / `Unstage` — **no
  `UnstageAll`** API.

**Expected:**
- Symmetric bulk action: when `StagedCount > 0`, show **Unstage All** next
  to the staged header (mirror Stage All placement/style).
- Unstage All unstages every currently staged path in one user action,
  then refreshes from repository truth (same pattern as Stage All failure
  handling: refresh even on partial failure).
- Optional: also expose command-palette `sourcecontrol.unstageAll` for
  parity with stage/refresh if those are registered.

**Notes for implementers:**
- Add `UnstageAll` (or reuse loop of `Unstage` inside one repo open) on the
  mutation seam; do not only loop from UI without a service method if other
  stage-all work batches inside one open.
- Keep per-row `+` / `−`; bulk does not replace them.
- `−` / `+` glyphs are cryptic (related to F10); bulk labels “Stage All” /
  “Unstage All” are the discoverable path for many files (screenshot has
  10 staged evidence files).
- Do not invent multi-select requirement just to unstage all — bulk button
  is enough.

---

### F13 — Settings panel content is right-aligned (odd; prefer left or center)

- [x] Fixed (2026-08-05) — form column `HorizontalAlignment.Right` → `Left`
  (520px column; labels/fields stay start-aligned inside). DF-003 closed as
  promoted here. Covered by
  `SettingsPanelViewTests.FormColumn_IsLeftAligned_NotRightPinned`.

**Severity:** Low–Medium (layout comfort; one-line fix surface)
**Difficulty:** XS
**Area:** Settings overlay / panel content alignment
**Source:** user report 2026-08-05 + `temp-screenshots/settings-right-aligned.png`;
pre-existing `docs/deferred/closed/DF-003-settings-alignment.md`

**User report:** Settings are right-aligned; that feels odd. Center or left
alignment is more normal.

**Observed:**
- `SettingsPanelView` builds a fixed-width column:

  ```csharp
  Width = 520,
  HorizontalAlignment = HorizontalAlignment.Right,
  ```

  so the form sits on the **right** edge of the settings host while the left
  side of the overlay stays empty.
- DF-003 already recorded the same observation (2026-07-11) without choosing
  left vs center.

**Expected (product preference from this report):**
- **Do not keep right alignment** as the default form placement.
- Prefer **left** (normal reading/editing flow for labeled fields) or
  **center** (balanced overlay). Recommendation for implementers unless the
  human picks otherwise: **left** for a settings form; use center only if the
  host is a full-bleed modal where a single 520px column looks better centered.
- Labels and controls inside the column stay start-aligned relative to the
  column (do not right-align field text).

**Notes for implementers:**
- Likely one-line change: `HorizontalAlignment.Right` → `Left` or `Center` on
  the settings `StackPanel` (and any matching host alignment).
- Close or link DF-003 when this lands (“promoted into Phase 23 F13”).
- If F5 adds an Agents section, keep the same column alignment so new blocks
  do not reintroduce right-pinning.
- Optional: max-width column + horizontal center of the column is a third
  option (column centered, content left-aligned inside) — often best of both.

---

### F15 — Multiple clicks on "Open Folder" spawn concurrent folder picker dialogs

- [x] Fixed (2026-08-06) — Bridged `FileTreeView` header click to `FileTreeViewModel.PickFolderRequested` interaction, wired via `MainWindowActivationHost` to `MainWindowViewModel.OpenFolderCommand`. Added an `Interlocked` re-entrancy guard (`IsPickingFolder`) in `OpenFolderCommand` and in `MainWindow.axaml.cs` native picker handler to prevent concurrent picker dialogs from opening on rapid clicks or keybindings. Verified with unit tests (`OpenFolderCommand_ConcurrentCalls_GuardedByIsPickingFolder` and `FileTree_PickFolderRequested_BridgesToOpenFolderCommand`).

**Severity:** Low–Medium (UX glitch / multiple system dialogs)  
**Difficulty:** XS–S  
**Area:** File tree header (`FileTreeView.cs`) + shell open folder command (`MainWindowViewModel.cs`)  
**Source:** Live screenshot / user report (`temp-screenshots/open-folder-multi-click-picker.png`)

**Observed behavior:**  
- Rapidly clicking "Open Folder..." (or double-clicking the header text/command) spawns multiple native folder picker dialogs concurrently.  
- `FileTreeView` `_headerText.PointerPressed` handler and `MainWindowViewModel.OpenFolderCommand` lack re-entrancy protection or input disabling while `OpenFolderPickerAsync` is awaiting user action.  

**Expected behavior:**  
- "Open Folder..." trigger should be re-entrancy guarded (subsequent clicks ignored while a picker dialog is open).  
- Only a single folder picker dialog should be active at any given time.  

**Notes for implementers:**  
- `FileTreeView.cs` attaches a raw `PointerPressed` handler to `_headerText` which calls `topLevel.StorageProvider.OpenFolderPickerAsync(...)` without checking a busy/picking flag.  
- Ensure both `FileTreeView` header click and `MainWindowViewModel.OpenFolderCommand` (Ctrl+O) use an `isPicking` flag or `ReactiveCommand` execution lock to prevent parallel folder pickers.  

---

### F16 — Commit message input box height is fixed to single-line 32px

- [x] Fixed (2026-08-06) — `_commitInput` is now multi-line: `AcceptsReturn = true`,
      `TextWrapping = TextWrapping.Wrap`, fixed `Height = 32` replaced with
      `MinHeight = 32` / `MaxHeight = 120` so it grows with wrapped text and
      expands up to a comfortable cap. Enter inserts line breaks inside the box;
      commit still fires from the primary action button. Covered by existing
      `SourceControlViewModelTests` + `SourceControlMutationFlowTests` (no
      regression).

**Severity:** Cosmetic (UX readability/usability)  
**Difficulty:** XS  
**Area:** Source Control panel (`SourceControlPanel.cs`)  
**Source:** User report (multiline commits work properly, but input box height does not expand visually)

**Observed behavior:**  
- `SourceControlPanel.cs` initializes `_commitInput` as a `TextBox` with `AcceptsReturn = false` and fixed `Height = 32`.  
- Users typing or pasting multi-line commit messages only see a single 32px line height, making multi-line message editing/reading difficult.  

**Expected behavior:**  
- The commit message input box should expand its height (or support multiline editing with flexible/min height and `AcceptsReturn = true` / `TextWrapping = TextWrapping.Wrap`) so multi-line commit messages are easily readable and editable.  

**Notes for implementers:**  
- In `SourceControlPanel.cs`, update `_commitInput` instantiation: set `AcceptsReturn = true`, `TextWrapping = TextWrapping.Wrap`, change fixed `Height = 32` to `MinHeight = 32` (or `MinHeight = 32`, `MaxHeight = ...`) or dynamic expansion.  

---

## Blockers

- None recorded. Indexing pass does not authorize implementation.

## Next Task

**Session 1 landed (2026-08-05):** F13 + F11 fixed (shell chrome polish).

**Session 2 landed (2026-08-05):** F2 + F4 fixed (transparency caption dedupe;
Townhall filter vs opener toolbar split).

**Session 2c landed (2026-08-05):** Trace / Memory / Usage toolbar openers
toggle open/close (F1 discoverability slice); open flags stay independent.

**Session ISSUE-009 / F14 landed (2026-08-05):** Production Townhall DI
singleton test isolated from conversation-store mutation; polluted drafts
scrubbed on this machine.

**Session F6 landed (2026-08-05):** Content-row MinHeight floors Townhall/editor
against bottom-panel splitter drag; XS/S wave complete.

**Session F1 landed (2026-08-06):** Dedicated `AgentInspectHost` side sheet;
chat Star band preserved; open-flag exclusivity unchanged. F3 not bundled.

Remaining difficulty-first waves:

1. **L/XL:** F7 → F8 (optional)

F5 complete (2026-08-06). One reviewable commit per coherent outcome.







## Notes

- Prefer user-visible breakages, data-safety, and broken happy paths over A4
  package-9 / deferred backlog polish.
- Do not treat this board as automatic authorization for V4 or Phase 22.5.
- Open trackers that may still matter, but are **not** pre-loaded as Phase 23
  scope: `docs/issues/open/`, `docs/deferred/open/` (DF-006 closed by F5).
- Empty evidence copy that states “missing is not fabrication / not zero” is
  product policy, not a bug. Only the **duplication and density** of that copy
  are in scope for F2/F3.
- **Config vs ops split (F5)** is the standing presentation rule for new
  Townhall chrome: if it is durable preference, put it in Settings.
