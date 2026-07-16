# Phase 13 M4b: Linux Release Smoke Evidence

**Status: COMPLETE WITH EXPLICIT LIMITATIONS (2026-07-16)**
**Scope:** Evidence only. No production, feature, performance, or M5 change.

M4b closes because every required matrix row below has an explicit status.
There are no `fail` rows and no Phase 13 `TOFIX.md` finding. Several rows remain
`not validated`: the available session had a real visible Wayland display, but
no non-synthetic remote keyboard/pointer path, and NetCoreDbg was unavailable.
Automated command-path tests are cited only as context and are not promoted to
desktop UX, Avalonia rendering, focus, gesture, or command-routing proof.

## Environment and method

| Item | Observation |
|---|---|
| Date / host | 2026-07-16; Arch Linux `7.1.3-arch1-1`, x86_64 |
| Desktop | Real visible Wayland session: `DISPLAY=:1`, `WAYLAND_DISPLAY=wayland-0`, `XDG_SESSION_TYPE=wayland` |
| App under smoke | `src/bin/Release/net10.0/Zaide`, built with `dotnet build src/Zaide.csproj -c Release --no-restore` from commit `556e782` |
| Release build result | pass — 0 warnings, 0 errors |
| LSP environment | `csharp-ls` available at `/home/cenoda/.dotnet/tools/csharp-ls` |
| Debug adapter environment | NetCoreDbg unavailable: `ZAIDE_NETCOREDBG_PATH` unset, no `netcoredbg` on `PATH`, and `/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` absent. Nothing was downloaded or bundled. |
| Input method | No synthetic keyboard or pointer injection was used as evidence. The session did not expose a non-synthetic remote input path, so input-dependent rows are `not validated`. |
| Observation method | Release process/window presence, visible screenshot inspection, empty app log, and direct filesystem/environment inspection. This is smoke observation, not stopwatch performance measurement. |

## Platform matrix

| Platform | Status | Concrete observation |
|---|---|---|
| Linux x64 real desktop | **pass** | The fresh Release binary created a visible 1280×800 `Zaide` main window on the real Arch/Wayland desktop. The shell, file-tree affordance, Townhall panes, editor empty state, activity glyphs, and bottom status text rendered legibly. The process emitted an empty log and terminated cleanly on request. Coverage limitations are itemized below. |
| Windows | **not validated** | No Windows host or existing real Windows desktop evidence was available in this checkout/evidence set. No parity claim is made. |
| macOS | **not validated** | No macOS host or existing real macOS desktop evidence was available in this checkout/evidence set. No parity claim is made. |

## Linux release smoke matrix

| Surface / action | Status | Concrete observation / limitation |
|---|---|---|
| Launch and usable main window | **pass** | Fresh Release launch produced a responsive-looking visible main window with intact shell layout and readable empty-editor/status presentation. Window discovery reported 1280×800; the app log was empty. “Usable” here means launch/render/no immediate crash, not proof of untested input paths. |
| Open `workflow-console` | **not validated** | The current Release session could not receive non-synthetic folder-picker input. M0 §7 records an earlier real-desktop view of `Program.cs` and `WorkflowConsole.csproj`, but that earlier observation is supporting history, not a current Release re-smoke. |
| Open, edit, and save a C# file | **not validated** | Non-synthetic editor input was unavailable. M0 §7 records an earlier real-desktop create/edit/save with filesystem verification, while M4a proves the headless command path; neither is promoted to a current Release desktop pass. |
| Editor search | **not validated** | Search requires live keyboard routing and editor focus. Synthetic delivery is explicitly excluded, so no search UI/result claim is made. |
| Build surface and Output projection | **not validated** | The current Release session could not open the fixture and invoke Build without synthetic input. M4a real-child Build is PASS, but it is not Output-panel UI proof. |
| Run surface and Output projection | **not validated** | The current Release session could not invoke Run without synthetic input. M4a real-child Run is PASS, but it is not Output-panel UI proof. |
| Test surface and Test Results projection | **not validated** | The current Release session could not invoke Test without synthetic input. Existing workflow tests are not Test Results desktop projection evidence. |
| LSP readiness / status | **not validated** | `csharp-ls` is available, and M0 previously observed `C# · Ready` on a real desktop, but no C# workspace could be opened in this Release session without synthetic input. M4a real-server PASS is not UI readiness/status proof. |
| Debug surface status | **not validated** | NetCoreDbg was unavailable for this session, and no synthetic navigation was used to open or exercise the Debug surface. Phase 12 real-adapter tests remain historical functional evidence only. |

## Keyboard, focus, and readable-status matrix

Each cell contains its own explicit status. The row status is the conservative
aggregate; one readable static status observation does not prove keyboard or
focus behavior.

| Surface / path | Keyboard / discoverability | Visible focus | Readable status | Row status |
|---|---|---|---|---|
| Command Palette and keybinding discoverability | **not validated** — no non-synthetic `Ctrl+Shift+P` path | **not validated** — palette not opened | **not validated** — command rows/gestures not displayed | **not validated** |
| Editor focus and search keyboard path | **not validated** — editor/search input unavailable | **not validated** — no keyboard focus traversal | **not validated** — search result/status not displayed | **not validated** |
| Tab focus / navigation | **not validated** — no real tab traversal input | **not validated** — no focused tab state exercised | **not validated** — no tab navigation status exercised | **not validated** |
| Bottom-panel focus / navigation | **not validated** — no real focus traversal input | **not validated** — panel focus not exercised | **not validated** — panel mode/status not exercised | **not validated** |
| Workflow controls | **not validated** — Build/Run/Test/Cancel not invoked through real keyboard input | **not validated** — control focus not exercised | **not validated** — Output/Test Results status not displayed | **not validated** |
| Debug controls when available | **not validated** — adapter absent and no real keyboard input | **not validated** — Debug controls not exercised | **not validated** — no live debug state; exact missing-adapter reason recorded above | **not validated** |
| Launch shell / bottom status bar | **not validated** — no command routing claimed | **not validated** — static screenshot cannot establish focus indication | **pass** — `Zaide`, line/column, workspace/repository, and configured-provider text were legible | **not validated** |

## Phase 12 display and gesture matrix

No row below is inferred from Phase 12 unit or real-adapter tests. The current
adapter/input limitations prevented creation of the live states needed for
display verification.

| Display / gesture row | Status | Concrete observation / limitation |
|---|---|---|
| Breakpoint and current-location appearance | **not validated** | NetCoreDbg was absent, no breakpoint-stop state was created, and synthetic F9/F5 input was excluded. The current-location arrow and breakpoint glyph were therefore not observed. |
| Call Stack and Variables presentation | **not validated** | Requires a live stopped adapter session. NetCoreDbg was absent, so no stack/scope/variable content was rendered for inspection. |
| Debug Console colors / readability | **not validated** | Requires live debug output/error projection. The adapter was absent, and automated line-kind tests are not color/readability proof. |
| Debug-panel proportions and glyph rendering | **not validated** | The general shell rendered cleanly, but the Debug panel was not opened into a meaningful live state. The Phase 12 three-column Debug proportions, splitters, headers, and debug glyphs were not inspected. |
| Live keyboard routing | **not validated** | No non-synthetic remote keyboard path was available. Synthetic X11/Wayland injection was not used and cannot establish Avalonia routing. |
| Multi-thread picker | **not validated** | NetCoreDbg was absent and `workflow-console` is a single-thread/shallow-frame fixture. No real multi-thread stopped session existed, so the picker was not displayed or exercised. |

## Screenshot evidence

| Path | Use | Limitation |
|---|---|---|
| `/tmp/zaide-phase13/m4b-smoke-current/01-release-main-window.png` | Material support for current Release launch, shell rendering, glyph rendering, empty-editor presentation, and readable bottom status text | Cropped from the real desktop screenshot; proves only the visible static launch state. It does not prove keyboard/pointer routing, focus, workspace/editor/workflow actions, or Debug UI. |
| `/tmp/zaide-phase13/m4b-smoke-current/desktop-launch.png` | Raw local full-desktop capture retained outside the repository | Contains surrounding desktop content and is not committed. Same static-observation limitation as the crop. |

No screenshot asset was committed because the raw local paths are sufficient for
this evidence and the repository has no M4b convention requiring committed
assets.

## Findings and gate result

- **Fail rows:** none.
- **Unsupported rows:** none; unexercised rows are `not validated`, not declared
  unsupported.
- **Phase 13 TOFIX items:** none. No real defect was observed, so no
  `docs/phases/v2/phase-13/TOFIX.md` was created.
- **M4a boundary preserved:** M4a headless/real-child command-path PASS results
  remain functional evidence only and are not desktop UX/render proof.
- **M4b close decision:** complete with explicit limitations. The milestone
  permits closure when every row is documented even if some are
  `not validated`; there is no unresolved `fail` to block the next milestone.

## Verification

The required full sequential gate is recorded after the documentation sync:

| Command | Result |
|---|---|
| `dotnet build Zaide.slnx --no-restore` | **pass** — 0 errors, 1 warning (`CS0067`, pre-existing unused fake event in `ProjectDebugTargetResolverTests.cs:34`) |
| `dotnet test Zaide.slnx --no-build` | **pass** — 2172 passed, 0 failed, 0 skipped |
| `git diff --check` | **pass** — no output |

## Exact next milestone

**M5 — Release closeout.** M5 has not started in this milestone.
