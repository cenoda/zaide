# Phase 14 M9 — Closeout and acceptance evidence

**Milestone:** Final closeout — design brief, keyboard/focus/a11y, screenshots,
UI acceptance lock, docs truth-sync  
**Date:** 2026-07-21  
**Environment:** Linux desktop, X11 `DISPLAY=:1`, 1920×1080, **96×96 DPI**  
**Baseline:** M8 accepted at `75c6d4c` / product commit `ec5c680`  
**M9 acceptance commit:** `b74653e`

## Authorization

- Human authorized **M9 only** (2026-07-21).
- Uncommitted plan edit at M9 start preserved: M8 “**Goal delivered:**” line repair
  (truncated acceptance text after `75c6d4c`).

## Automated verification (2026-07-21)

| Command | Result |
|---------|--------|
| `dotnet build Zaide.slnx` | **0 errors**, **4 warnings** (pre-existing test analyzer / unused event) |
| `dotnet test … ~Zaide.Tests.Features.Conversations` | **102** passed |
| `dotnet test … ~Zaide.Tests.Features.Townhall` | **112** passed (+1 accessible-name test) |
| `dotnet test … ~Zaide.Tests.Features.Agents` | **178** passed |
| `dotnet test … ~Zaide.Tests.App.Shell` | **134** passed |
| `dotnet test … ~Zaide.Tests.Architecture` | **26** passed |
| `dotnet test Zaide.slnx` (full suite) | **2519** passed, 0 failed, 0 skipped |
| `git diff --check` | clean (recorded at closeout) |

Architecture baseline **unchanged** by M9 (no new production types):
**463** total / **337** public / **126** internal.

## M9 code change (acceptance-only)

Primary Townhall controls that lacked accessible names received
`AutomationProperties` names/help text:

- Message input: `Townhall message input`
- Send: `Send message`
- New-message chip: `Jump to new messages` / dynamic unseen label
- People agent rows: `Open direct conversation with {name}`
- Attachment control: `Attachment (unavailable)` (honest non-capability)

No new product capabilities, retry UI, streaming, harness, or panel chrome.

## Design brief

See [`M9_DESIGN_BRIEF.md`](M9_DESIGN_BRIEF.md). Summary: delivered workspace meets
C#/token/dark-palette/OS-chrome intent; glass/blur full aesthetic, attachment
actions, and WCAG certification are intentional limitations.

## Keyboard / focus verification (interactive Linux GUI)

Performed on live `Zaide` window (title `Zaide`) via pointer + `xdotool` keyboard
injection. Automated unit coverage remains for Enter / Shift+Enter
(`TownhallInputAreaTests`).

| Step | Result | Evidence |
|------|--------|----------|
| Conversation list selection (pointer + keyboard Enter on Direct) | **Pass** — Direct “Zaide Agent” selected; header becomes `Zaide Agent`; input placeholder becomes `Direct message with Zaide Agent` | `townhall-dm-default-1280x800.png` |
| Channel list keyboard Down/Up/Return | **Pass** — channel switch activity observed (`Switched to #…`) | OCR after-send shot |
| Focus message input | **Pass** — click focus + type accepted | multiline + after-send shots |
| Enter sends | **Pass** — typed content left the input and appeared in channel history | `townhall-channel-after-send-1400x900.png` |
| Shift+Enter newline | **Pass** — two-line draft before send | `townhall-channel-multiline-input-1400x900.png` |
| Visible focus | **Structural pass / not pixel-asserted** — `ListBox`/`TextBox` focusable; platform focus adorners used; no screenshot pixel proof of ring contrast | Code + interactive use |
| Accessible names on primary controls | **Pass** (automated + code) | `PrimaryControls_HaveAccessibleNames`; nav lists named since M2 |

### Keyboard residual risks

- People open-DM rows are pointer-primary (not Tab-stop keyboard rows); existing
  DMs remain keyboard-selectable via the Direct list.
- Send button is pointer + accessible name; keyboard send is Enter on input.
- Full keyboard-only traversal of the entire IDE shell (file tree, editor, status
  bar) is **out of Phase 14 scope**.

## Screenshots (Linux)

Directory: `docs/phases/v3/phase-14/evidence/m9/`

| File | Purpose |
|------|---------|
| `townhall-default-1400x900.png` | Default-ish workspace width (no Agent Panel chrome) |
| `townhall-dm-default-1280x800.png` | Human-supplied F1 acceptance capture: Direct selected, `Zaide Agent` header, matching DM placeholder |
| `townhall-channel-header-1400x900.png` | Public-channel header and input context |
| `townhall-channel-multiline-input-1400x900.png` | Shift+Enter multiline draft in channel input |
| `townhall-channel-after-send-1400x900.png` | After Enter send in public-channel context |
| `townhall-channel-narrow-960x600.png` | Shell-minimum-width public-channel workspace |
| `townhall-narrow-800x600.png` | 800×600 crop at shell minimum width (`MinWidth` 960) — **F1 corrected** |

### Environment rows

| Row | Status |
|-----|--------|
| Linux X11 GUI screenshots | **Captured** (paths above) |
| High-DPI | **Not available** — host reports **96×96 DPI**; no high-DPI capture |
| macOS / Windows | **Not validated** |
| Hardware screen-reader (Orca/NVDA/VoiceOver) | **Not run** — accessible names verified in code/tests only |
| Interactive human visual design review | **Agent-session capture + OCR**; formal design sign-off remains human |

## UI acceptance lock recheck

| UI acceptance item | Status | Notes / residual risk |
|--------------------|--------|------------------------|
| Incremental or virtualized rendering | **Accepted limitation** | Non-virtualized list remains correct; virtualization not required by measured Phase 14 need. Residual: very large histories may degrade scroll perf. |
| Stable scroll anchoring | **Closed** | M3 `TownhallChatScrollPolicy` + tests; unchanged M8/M9 |
| Near-bottom auto-follow | **Closed** | M3 policy + tests |
| New-message affordance when scrolled away | **Closed** | Chip + accessible name; M3 behavior retained |
| Semantic controls | **Closed** | Nav lists + input/send/chip/people names |
| Keyboard-only navigation (list → select → input → send/newline) | **Closed** | Interactive M9 + unit tests; residuals above |
| Visible focus | **Closed with residual** | Platform focus on focusable chrome; ring contrast not pixel-certified |
| Screen-reader naming | **Closed best-effort** | Names recorded; no Orca/NVDA session |
| Narrow / wide / high-DPI evidence | **Closed with accepted limits** | Default + supported-minimum 960×600 captured; 800×600 is a crop only because shell `MinWidth` is 960; high-DPI unavailable |
| Design brief vs DESIGN.md | **Closed** | `M9_DESIGN_BRIEF.md` |

## Cross-checks (must remain true)

| Claim | Status | Evidence |
|-------|--------|----------|
| M8 Agent Panel retirement complete | **Confirmed** | No panel chrome in screenshots/OCR; `Phase14M8PanelRetirementTests`; thin host residual only |
| DF-001 closed with thin non-visual host limitation | **Confirmed** | `docs/deferred/closed/DF-001-…`; residual still honest |
| M4 privacy intact (no implicit public DM mirror) | **Confirmed** | M4/M7/M8 automated privacy asserts; no M9 reopen |
| Persistence/recovery + no auto-resume | **Confirmed** | M6 matrix + plan D12; M9 did not change persistence |

## Explicit not-fabricated rows

- No high-DPI screenshots (96 DPI host).
- No OS validation outside Linux X11.
- No live screen-reader product certification.
- No claim that every visual focus ring meets a measured contrast ratio.
- No actual 800×600 window validation; shell minimum width is 960 and the
  800×600 artifact is a crop.

## Phase disposition

Phase 14 **engineering closeout complete** with residual risks documented above.
**F1 corrective fix (2026-07-21):** conversation header and input context now follow
`ActiveConversationId` — see [`M9_F1_MANUAL_EVIDENCE.md`](M9_F1_MANUAL_EVIDENCE.md) and
replaced screenshots in `evidence/m9/`. Prior M9 DM captures incorrectly showed
`#townhall-main` while Direct was selected.

**Pending explicit human acceptance** to treat the phase as formally product-closed.
Do **not** begin Phase 15 from this milestone.
