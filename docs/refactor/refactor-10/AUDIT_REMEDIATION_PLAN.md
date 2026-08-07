# Refactor 10: Post-M3 Audit Remediation Plan

## Status

**In progress (2026-08-07).** Closes findings from the full commit audit of
Refactor 10 M0–M3. This is **not** M4. Shared control layer work stays blocked
until R1–R4 land (R5 is optional polish).

| ID | State |
|---|---|
| R1 | done — light `SurfaceOverlayBrush` is `#000000` @ 0.40 |
| R2 | done — editor TextMate follows app variant (Light+/Dark+) |
| R3 | done — guard enforces Parse/FromArgb/FromRgb literals; Makefile tracked (policy A) |
| R4 | pending |
| R5 | optional / pending |

## Why this exists

M0–M3 built real theme infrastructure, but the M3 closeout overstated
completeness:

| Claim in TOFIX / plan | Live truth (2026-08-07 audit) |
|---|---|
| M3 guard enforces zero color literals | Guard only greps `Color.Parse("#…")` |
| Guard wired into Makefile / CI | `Makefile` is gitignored; no CI hook |
| Light default is shippable | ~~`SurfaceOverlayBrush` (light) white 40%~~ **fixed in R1** (`#000000` @ 40%) |
| App is light-default | Editor TextMate still pins `DarkPlus` |
| M1 exit: variant flip repaints every panel | One-shot `ThemeBinding` + ~95 `Resources[]` sites; no flip test |
| IMPLEMENTATION_PLAN status | Still says **Planned**; M1 gate checkboxes unchecked |

Full-suite repaint and hover unification remain **M4**. This plan only fixes
bugs, honesty gaps, and guard/docs debt so M4 starts on solid ground.

## Scope

**In scope**

- Light scrim token fix
- Editor syntax theme alignment with app theme variant
- Guard script strength + Makefile tracking policy
- Docs truth-sync (plan, TOFIX, residual allowlist)
- Small, targeted tests for fixed behavior

**Out of scope**

- M4 control factories / `ControlThemeCatalog`
- M5 glass / blur / motion
- Theme switcher UI and settings persistence
- Mass migration of remaining `Application.Current.Resources[]` call sites
  (document only; M4 owns structural fix)
- Terminal ANSI palette literals (remain allowed)

## Pre-Implementation Verification (live)

- [x] `src/UI/DesignSystem/Tokens/Light.axaml` — `SurfaceOverlayBrush` is
      `Color="#FFFFFF" Opacity="0.60"`-style white translucent (scrim bug).
- [x] `src/Features/Editor/Presentation/EditorView.cs` — `ThemeName.DarkPlus`
      with comment “matches app dark theme”.
- [x] `scripts/check-theme-tokens.sh` — only matches `Color.Parse` hex form;
      excludes `TerminalRenderControl.cs` and `TextStyles.cs`.
- [x] `.gitignore` line 75 ignores `Makefile`; on-disk Makefile has
      `check-theme-tokens` but is not versioned.
- [x] Residual production literals (approx.): `Color.Parse` 3, `FromArgb` 13,
      `FromRgb` 20, `new Thickness(` 27; `Resources[]` indexer ~95.
- [x] Design-system tests 32/32 pass; `dotnet build Zaide.slnx` clean.

## Milestones

| ID | Description | Verification |
|---|---|---|
| R1 | Fix light modal scrim token | visual reasoning + optional unit assert on resolved color; build |
| R2 | Align editor TextMate theme with app variant | code path + focused test or manual note; build |
| R3 | Honest, stronger theme-token guard + Makefile policy | `bash scripts/check-theme-tokens.sh`; make target if tracked |
| R4 | Docs truth-sync (plan, TOFIX, allowlist, M1/M3 honesty) | docs review only |
| R5 | Optional polish: indent guide token + ThemeBinding flip smoke test | focused tests + build |

Do **R1 → R2 → R3 → R4** in order. **R5** only if time remains before M4.

Each milestone is one reviewable commit (docs for R4 may be docs-only).

---

## R1 — Light `SurfaceOverlayBrush` scrim fix

### Problem

`CommandPaletteOverlay` maps its backdrop to `SurfaceOverlayBrush`. On dark the
token is navy translucent (dims correctly). On light it is **white translucent**,
which washes the UI instead of dimming it. Light is the default theme, so this
is a user-visible regression from the M3 mapping.

### Work

1. Change light `SurfaceOverlayBrush` to a dark translucent scrim (recommended
   starting point: `Color="#000000" Opacity="0.40"` or a near-black navy at
   similar alpha). Keep dark ramp as a dark translucent scrim (tune only if
   needed for parity of intent, not key set).
2. Confirm key parity still holds (same key name both ramps).
3. Do **not** invent a second scrim key unless contrast/review demands it;
   prefer fixing the existing semantic token.
4. Update any DESIGN.md wording that describes overlay as light-on-light if
   present.

### Exit

- [x] Light `SurfaceOverlayBrush` reads as a dimming scrim, not a white wash
- [x] `ThemeTokenParityTests` still green
- [x] `dotnet build Zaide.slnx` clean
- [x] One commit: `fix(refactor-10-r1): correct light SurfaceOverlayBrush scrim`

### Result (2026-08-07)

Light `SurfaceOverlayBrush` changed from `#FFFFFF` @ 0.40 to `#000000` @ 0.40.
Dark ramp unchanged (`#0A0F19` @ 0.40). Key parity preserved.

### Agent prompt (copy-paste)

```
You are working on Zaide Refactor 10 audit remediation milestone R1 only.

Read and follow:
- docs/refactor/refactor-10/AUDIT_REMEDIATION_PLAN.md (R1 section)
- docs/refactor/refactor-10/TOFIX.md
- docs/CONVENTIONS.md

Goal: Fix Light theme SurfaceOverlayBrush so modal scrims dim the background
instead of washing it white. CommandPaletteOverlay uses this token for its
backdrop.

Constraints:
- R1 only. Do not start R2–R5, M4, or M5.
- Prefer editing the existing SurfaceOverlayBrush in
  src/UI/DesignSystem/Tokens/Light.axaml. Keep Light/Dark key sets identical.
- Dark ramp: keep a dark translucent scrim; only retune if clearly wrong.
- No new features. No control-layer work.
- English for commits/docs/comments.

Verify:
- ThemeTokenParityTests (or full design-system filter) pass
- dotnet build Zaide.slnx
- Summarize what changed, why, files, verification, residual risks

When done, update TOFIX.md R1 row to done with a short result note.
```

---

## R2 — Editor TextMate theme vs app light default

### Problem

App default is light after M1, but `EditorView` still installs TextMate
`ThemeName.DarkPlus` and comments that it matches the dark app theme. Syntax
highlighting stays dark-on-dark while chrome goes light.

### Work

1. Inspect how TextMateSharp / AvaloniaEdit theme switching works in this repo
   and package version (live code first).
2. Prefer: pick TextMate theme from `ThemeBinding.CurrentVariant` (or
   `Application.ActualThemeVariant`) at editor init, and re-apply on
   `ActualThemeVariantChanged` if the API allows without a full editor rebuild.
3. If runtime swap is unsafe or unsupported in this version, implement
   init-time selection for the current variant and document runtime swap as
   M4/M5 follow-up — but **default light must open with a light syntax theme**.
4. Remove or rewrite the stale “matches app dark theme” comment.

### Exit

- [x] Fresh editor under light default does not force DarkPlus
- [x] Dark variant still gets a dark syntax theme
- [x] Focused editor/design tests green; build clean
- [x] Commit: `fix(refactor-10-r2): align editor TextMate theme with app variant`

### Result (2026-08-07)

`EditorView` selects `ThemeName.LightPlus` or `ThemeName.DarkPlus` from
`ThemeBinding.CurrentVariant` at init and re-applies via
`TextMate.Installation.SetTheme` on `ActualThemeVariantChanged`.
`GetTextMateThemeName` is unit-tested; syntax paint still needs manual
visual check (headless tests do not render tokens).

### Agent prompt (copy-paste)

```
You are working on Zaide Refactor 10 audit remediation milestone R2 only.

Read and follow:
- docs/refactor/refactor-10/AUDIT_REMEDIATION_PLAN.md (R2 section)
- docs/refactor/refactor-10/TOFIX.md
- src/Features/Editor/Presentation/EditorView.cs (TextMate install site)

Goal: Align AvaloniaEdit TextMate syntax theme with the app theme variant.
Light app default must not keep ThemeName.DarkPlus solely because the old app
was dark-only.

Constraints:
- R2 only. R1 must already be done or not regress it.
- Do not build the M4 control layer.
- Prefer theme selection from ThemeBinding.CurrentVariant /
  ActualThemeVariant. If live swap is not safe in this Avalonia/TextMate
  version, init-time selection for the active variant is acceptable; document
  the limitation in TOFIX.
- No backend or settings UI for theme switching.

Verify:
- Build clean
- Relevant editor tests if any; otherwise note manual verification needed
- Update TOFIX R2 row

Summarize changes, files, verification, residual risks.
```

---

## R3 — Guard strength + Makefile policy

### Problem

1. `scripts/check-theme-tokens.sh` comments claim `FromArgb` / `FromRgb`
   coverage but only greps `Color.Parse("#…")`.
2. TOFIX claims Makefile wiring; `Makefile` is gitignored so the target is not
   in the repository.

### Work

1. Expand the guard to catch common literal forms in `src/**/*.cs`:
   - `Color.Parse("…")` with hex
   - `Color.FromArgb(` / `Color.FromRgb(` with numeric literals
   - Optional: `Colors.OrangeRed` / obvious named brush abuse if low noise
2. Keep explicit allowlist/excludes with comments:
   - `TerminalRenderControl.cs` (ANSI palette)
   - Computed alpha from theme colors (e.g. `Color.FromArgb(alpha, accent.R, …)`)
     if the guard cannot distinguish — prefer pattern that allows
     non-literal channel sources, or path-based allow with justification
   - Design-system fallbacks only if still required (`TextStyles`,
     `Elevation` fallbacks) — prefer shrinking fallbacks over broad excludes
3. Decide Makefile policy (pick one, document in TOFIX):
   - **A (preferred):** stop ignoring `Makefile`, commit a minimal Makefile
     with `check-theme-tokens` (and existing local targets if any), OR
   - **B:** delete the local-only Makefile claim; document running
     `bash scripts/check-theme-tokens.sh` only
4. Run the guard; fix any newly reported true violations that are trivial, or
   allowlist with a one-line reason. Do not mass-refactor unrelated panels.

### Exit

- [x] Guard script matches its own header comment
- [x] `bash scripts/check-theme-tokens.sh` exits 0 on current tree after
      intentional allowlists
- [x] Makefile policy recorded and consistent with git
- [x] Commit: `chore(refactor-10-r3): strengthen theme-token guard`

### Result (2026-08-07)

`scripts/check-theme-tokens.sh` now flags `Color.Parse("#…")`,
`Color.FromArgb(…)`, and `Color.FromRgb(…)` when all channel arguments are
numeric literals. Computed channels (e.g. `accent.R`) are allowed without
file-level bans. Path excludes: `TerminalRenderControl.cs` (ANSI palette),
`TextStyles.cs` and `Elevation.cs` (justified ThemeBinding fallbacks).
**Makefile policy A:** removed `Makefile` from `.gitignore` and committed the
on-disk Makefile with `check-theme-tokens` plus existing local targets.
No new production violations; residual literal debt outside these patterns
remains documented for M4.

### Agent prompt (copy-paste)

```
You are working on Zaide Refactor 10 audit remediation milestone R3 only.

Read and follow:
- docs/refactor/refactor-10/AUDIT_REMEDIATION_PLAN.md (R3 section)
- scripts/check-theme-tokens.sh
- .gitignore (Makefile entry)
- docs/refactor/refactor-10/TOFIX.md

Goal:
1) Make check-theme-tokens.sh actually enforce the patterns its comments claim
   (at least Color.Parse hex, Color.FromArgb, Color.FromRgb), with a clear
   allowlist for terminal ANSI and justified design-system fallbacks.
2) Resolve Makefile policy: either commit Makefile (remove from .gitignore)
   with check-theme-tokens target, OR stop claiming Makefile wiring and
   document the bash invocation only.

Constraints:
- R3 only. Do not redesign the token system or start M4.
- Prefer small allowlist + accurate detection over silencing everything.
- If the stronger guard flags real easy wins (a few Color.Parse leftovers),
  fix them in the same commit. Large panel rewrites → list in TOFIX, do not
  expand scope.
- English commits/docs.

Verify:
- bash scripts/check-theme-tokens.sh → PASS
- dotnet build Zaide.slnx
- Update TOFIX R3 row with what the guard covers and Makefile decision

Summarize changes, files, verification, residual risks.
```

---

## R4 — Docs truth-sync

### Problem

Plan/TOFIX still advertise completed exits that the audit disproved. Future
agents will trust the board and skip real work.

### Work

1. Update `IMPLEMENTATION_PLAN.md`:
   - Status: M0–M3 implemented; audit remediation R1–R4 (and optional R5)
     before M4
   - Check off only gates that are actually true
   - Explicitly note M1 “full panel repaint on variant flip” is **deferred to
     M4** (one-shot resolution + Resources indexer debt)
2. Update `TOFIX.md`:
   - Insert remediation board rows R1–R5
   - Rewrite M3 result to honest scope (what was replaced vs residual)
   - Next task = remaining R* then M4
3. Keep this file’s milestone results filled as each R* lands.
4. No code changes in R4 unless a one-line doc link in a script header.

### Exit

- [ ] Plan and TOFIX agree with live code
- [ ] No claim of zero color literals unless the R3 guard truly enforces it
- [ ] Commit: `docs(refactor-10-r4): truth-sync plan and TOFIX after audit`

### Agent prompt (copy-paste)

```
You are working on Zaide Refactor 10 audit remediation milestone R4 only
(docs only).

Read and follow:
- docs/refactor/refactor-10/AUDIT_REMEDIATION_PLAN.md
- docs/refactor/refactor-10/IMPLEMENTATION_PLAN.md
- docs/refactor/refactor-10/TOFIX.md
- Live scripts/check-theme-tokens.sh and token files if needed to verify claims

Goal: Truth-sync documentation with post-audit reality after R1–R3 code work.

Constraints:
- Documentation only unless a single comment in the guard script must match.
- Do not implement M4/M5.
- English docs.
- Mark completed R1–R3 accurately; leave incomplete R* pending.
- State clearly that full theme-flip repaint is M4, not M1 done.

Verify:
- Claims in TOFIX/plan match live code (spot-check guard, SurfaceOverlay,
  editor theme, Makefile policy)
- git diff --check if applicable

Summarize doc edits and any remaining doc risks.
```

---

## R5 — Optional polish (before M4)

### Work (pick any subset; each should still be reviewable)

1. **Indent guide token:** Revisit `EditorView` indent guide brush —
   `AccentSubtleBgBrush` may be too opaque vs old translucent secondary accent.
   Prefer a subtle border/separator-derived or dedicated low-alpha token.
2. **ThemeBinding flip smoke test:** Force `ThemeVariant.Light` / `Dark` on the
   test `Application` and assert `ThemeBinding.GetColor` / key brushes change
   for at least one palette key. Does **not** require full visual tree repaint.
3. **NavBar stale comment** referencing `#12FFFFFF`.

### Exit

- [ ] Chosen items done with tests where applicable
- [ ] Commit: `test/fix(refactor-10-r5): …` matching the subset

### Agent prompt (copy-paste)

```
You are working on Zaide Refactor 10 audit remediation milestone R5
(optional polish) only.

Read:
- docs/refactor/refactor-10/AUDIT_REMEDIATION_PLAN.md (R5 section)
- docs/refactor/refactor-10/TOFIX.md

Implement only the R5 items still marked pending in TOFIX (indent guide token,
ThemeBinding variant smoke test, stale comments). Skip anything already done.

Constraints:
- No M4 control layer.
- Keep changes minimal and tested.
- English commits/docs.

Verify focused tests + build. Update TOFIX R5 row.
```

---

## Explicitly deferred to M4 / M5

| Item | Owner |
|---|---|
| `ControlThemeCatalog`, `AppButton`, `ListRow`, `PanelChrome`, shared hover | M4 |
| Replace per-panel hover wiring | M4 |
| Mass convert `Application.Current.Resources[]` → live theme binding | M4 |
| Focus rings as shared policy | M4 |
| Glass / `TransparencyLevelHint` / elevation on overlays | M5 |
| Motion 150–200 ms transitions | M5 |
| User theme switcher + persistence | separate phase |

## Risks

| Risk | Mitigation |
|---|---|
| Stronger guard floods with FromRgb noise | Allowlist computed-alpha and terminal; fix only true hex leftovers in R3 |
| TextMate theme swap tears editor state | Prefer init-time selection; document live swap limit |
| Docs-only R4 races unfinished R1–R3 | Run R4 last; base claims on live tree |
| Scope creep into M4 | Each prompt forbids control-layer work |

## Rollback

Revert the single R* commit. R1 is the only likely visual rollback; R3 guard
tightening can be softened by expanding allowlist without reverting tokens.

## After this plan

When R1–R4 are done (R5 optional), set TOFIX next task to **M4 — shared
control layer** and implement from `IMPLEMENTATION_PLAN.md` M4 section only.
