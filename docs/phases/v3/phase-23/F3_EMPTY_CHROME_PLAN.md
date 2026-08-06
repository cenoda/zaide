# F3 — Empty inspect surfaces: minimal chrome

One-page plan for stripping dense control stacks from Trace / Memory / Usage
when there is nothing to inspect yet (or trace capture is off by policy).
**Presentation only** — no Settings schema (F5), no open-flag changes (F1).

## Decision: hide (not collapse / disable-only)

| Approach | Choice | Why |
|----------|--------|-----|
| **`IsVisible = false`** | **Selected** | Hidden controls must not remain tab stops or expose stale automation names (constraint #4). Disabled-but-visible chrome still reads as a broken form. |
| Collapse to zero height only | Rejected | Leaves phantom focus targets and AT noise unless visibility is also cleared. |
| Remove controls from tree | Rejected | Larger diff; existing `ApplyProjection` ownership stays on one panel class per surface. |

Chrome mode is derived from the **existing** surface states (`Empty`, `Failed`,
`Unavailable`, `Ready`, trace capture flag). **Do not collapse Failed into
Empty.**

## Per-panel inventory

### Trace

| Chrome | Minimal capture-off | Minimal empty (capture on) | Full (records / summary) |
|--------|--------------------|-----------------------------|--------------------------|
| Status caption | Keep | Keep | Keep |
| Summary (policy help) | **Hide** | Keep when non-empty policy line | Keep |
| Record selector | Hide | Hide | Show |
| Records / selection captions | Hide | Hide | Show |
| Paging caption | Hide | Hide | Show |
| Capture toggle | Hide | Hide | Show |
| Refresh | Hide | Keep | Keep |
| Close | Keep | Keep | Keep |
| Open Settings | Keep | Hide | Hide |

**Capture-off minimal** is the quietest surface: status + Close + one Open
Settings affordance (wired to shell `ShowSettings`; no new Settings section).

### Memory

| Chrome | Minimal empty | Full (Ready / Failed / Unavailable / Loading) |
|--------|---------------|-----------------------------------------------|
| Status + summary | Keep | Keep |
| Record selector, records, selection | Hide | Show |
| Influence disclaimer | Hide | Show |
| Scope + draft + lifecycle toolbar | Hide until user starts create | Show |
| Create denial caption | **Only after denied submit** | Show when submit denied |
| Surface actions (Refresh / Retry / Close) | Keep | Keep |
| Create (operational) | Keep on minimal empty | In lifecycle row |

**Create flow on empty:** first **Create** click reveals compose chrome (scope +
draft); denial copy appears only after a denied `CreateFromDraft` attempt, not
from standing `SubmitDenialReason` on an untouched panel.

### Usage

| Chrome | Minimal empty | Full (Ready / Failed / Unavailable / Loading) |
|--------|---------------|-----------------------------------------------|
| Status + summary (policy once) | Keep | Keep |
| Record selector, records, selection | Hide | Show |
| Capture toggle | Hide | Show |
| Refresh / Retry / Close | Keep (Retry when `CanRetry`) | Keep |

## Accessibility

- When hiding: `IsVisible = false` and `IsTabStop = false` on interactive
  controls; restore `IsTabStop = true` when shown again.
- Automation names unchanged on controls that remain in the tree; hidden
  controls are not reachable.
- Operational buttons that stay visible (Close, Refresh, Create, Open Settings)
  keep existing `AutomationProperties.Name` values.

## Wiring: Open Settings

- `Action? OpenSettingsRequested` on trace/usage panels and
  `AgentInspectHost` / `TownhallView`, set from `MainWindow.axaml.cs` to
  `ShowSettings` (same path as status bar). No new command ids or Settings
  fields.

## Tests (`Phase23EmptyInspectChromeTests`)

- Empty Memory: minimal chrome; lifecycle toolbar + standing denial hidden;
  denial after denied create only.
- Empty Usage: no record selector; capture hidden.
- Trace capture disabled: status + Close + Open Settings only.
- Failed / Unavailable: full chrome regression (selectors/toolbars visible).
- Reachability: remaining actions still named, focusable, tab stops.

Update `Phase22TransparencyAccessibilityTests` paging assertions to apply only
when full trace chrome is active.

## Non-goals

- F5 Settings schema, capture/page-size migration, deep-link section targets.
- Renaming open flags or command ids.
- Changing `Empty` / `Failed` / `Unavailable` semantics for consumers.
- Bundling F7, F8, or other findings.
