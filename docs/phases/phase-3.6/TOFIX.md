# Phase 3.6: Terminal Renderer Foundation — TOFIX

Track code quality issues found during Phase 3.6 review.

---

## Status

Phase 3.6 is **in progress**. M1 is implemented but not yet fully accepted,
because the M1 code audit found unresolved issues listed below. M2–M5 remain
open in `IMPLEMENTATION_PLAN.md`. The current user-visible terminal still uses
the Phase 3.5 `TerminalOutputBuffer` (TextBox-backed) because the M4 wiring
work has not started yet.

Planning audit passed after review feedback was incorporated. Entry gates were
run serially:

- `dotnet build Zaide.slnx` — 0 warnings, 0 errors
- `dotnet test Zaide.slnx --no-build` — 223 passed, 0 failed

M1 code audit completed on **2026-06-30**. The parser implementation covers
the planned surface area enough to continue review, but M1 should be treated
as incomplete until the issues below are resolved or explicitly deferred.
Seven issues were identified: one real bug, three gaps, and three minor items.
All seven remain reproducible in the live code on **2026-06-30**. Focused
coverage still passes because the missing/broken cases are not yet asserted:
`dotnet test Zaide.slnx --no-build --filter AnsiParserTests` → 15 passed,
0 failed.

## Open Issues

### M1-01: ESC in CSI state is silently swallowed (bug)

**Severity:** Real bug — low practical impact (malformed input only).

`ProcessCsi()` accumulates `\x1B` into `_csiBuffer` because ESC (0x1B) falls
below the final-byte range (0x40–0x7E). Per ECMA-48, ESC in CSI state should
abort the current CSI sequence and start a new escape. Instead, the ESC byte
is absorbed into the parameter buffer.

Example: `"\x1B[\x1B[31m"` should abort the malformed first CSI and then parse
`\x1B[31m` as SGR red. The current implementation drops the entire stream
silently because `_csiBuffer` ends up as `"\x1B[31"`, which fails
`int.TryParse`.

**Fix:** Add an ESC check at the top of `ProcessCsi()`:

```csharp
if (ch == '\x1B')
{
    _csiBuffer.Clear();
    _state = ParserState.Escape;
    return;
}
```

**Location:** `src/ViewModels/AnsiParser.cs:91` (`ProcessCsi()`).

### M1-02: Bare ESC consumes the following character (SCS leak)

**Severity:** Behavioural gap — moderate practical impact for SCS sequences.

`ProcessEscape()` transitions to Ground on the `default` case, but the
triggering character is consumed by the Escape handler and never re-processed
in Ground state. For bare ESC followed by a printable character this is
acceptable, but SCS (Select Character Set) sequences leak visible text.

| Input | Expected | Actual |
|-------|----------|--------|
| `"\x1Bw"` | ESC consumed, `w` printed (xterm) | `w` consumed silently |
| `"\x1B(M"` (SCS G0) | All three consumed | `(` consumed, `M` leaks as text |

**Fix options:**
- Track known 2-char prefixes (`(`, `)`, `*`, `+`) in Escape state and consume
  the next byte as the designator before returning to Ground.
- Or accept the leak and document it here as a known limitation until a fuller
  escape-sequence model is added.

**Location:** `src/ViewModels/AnsiParser.cs:71` (`ProcessEscape()`).

### M1-03: Unterminated OSC/DCS swallows all subsequent output

**Severity:** Robustness gap — low practical impact (requires broken PTY).

If an OSC (`\x1B]`) or DCS (`\x1BP`) sequence is started but never terminated,
the parser stays in `UnsupportedString` state permanently. All subsequent
output, including printable text and CSI sequences, is silently consumed.

**Fix:** Add a max-length guard in `ProcessUnsupportedString()`: after N
characters (for example 4096) without a terminator, force-reset to Ground.
This prevents one broken sequence from permanently silencing the terminal.

**Location:** `src/ViewModels/AnsiParser.cs:104` (`ProcessUnsupportedString()`).

### M1-04: Only one split-point tested for chunk boundaries

**Severity:** Test coverage gap.

`Parse_SplitSequenceAcrossCalls_CompletesOnSecondCall` splits only at the
`\x1B` | `[31m` boundary. Other common split points use the same
`_csiBuffer` accumulation path but are not explicitly verified:

| Split point | Input | Tested |
|-------------|-------|--------|
| ESC \| rest | `\x1B` \| `[31m` | ✅ |
| Mid-CSI params | `\x1B[3` \| `1m` | ❌ |
| Params \| final byte | `\x1B[31` \| `m` | ❌ |
| Mid-OSC | `\x1B]0;ti` \| `tle\a` | ❌ |
| Mid-DCS | `\x1BPde` \| `mo\x1B\\` | ❌ |

**Fix:** Add one or two extra split tests. The "params | final byte" split is
the most important because shells often flush output mid-escape.

**Location:** `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs:113`.

### M1-05: Negative CSI parameters are accepted (minor)

**Severity:** Minor robustness concern.

Input `"\x1B[-1A"` produces `CsiDispatchAction([-1], 'A')` because `-` is not
in the `IndexOfAny` rejection list and `int.TryParse("-1")` succeeds. Real
terminals do not send negative parameters. The screen buffer in M2 should
clamp all parameter values, but the parser could reject them earlier.

**Location:** `src/ViewModels/AnsiParser.cs:126` and `src/ViewModels/AnsiParser.cs:147`.

### M1-06: Incomplete intermediate-byte rejection in CSI (minor)

**Severity:** Minor spec-conformance gap.

The `IndexOfAny` check at `EmitSupportedCsi()` rejects `?`, `>`, `!`, `"`,
`$`, space, and `'`, but ECMA-48 intermediate bytes also include `#`, `%`,
`&`, `(`, `)`, `*`, `+`, `,`, `-`, `.`, and `/`. Most of the missing
characters are still rejected indirectly because `int.TryParse` fails, so the
current net effect is usually still "drop the sequence." The notable exception
is `-1` (Issue M1-05), which parses successfully.

**Location:** `src/ViewModels/AnsiParser.cs:126` (`EmitSupportedCsi()`).

### M1-07: No tests for common non-CSI escape sequences (minor)

**Severity:** Documentation gap.

Several common 2-byte escape sequences such as `\x1Bc` (RIS), `\x1B7`
(DECSC), `\x1B8` (DECRC), `\x1B=` (DECKPAM), and `\x1B>` (DECKPNM) are
currently handled by the `default` case in `ProcessEscape`: consumed,
returned to Ground, and no action emitted. That matches the current
"silently dropped" contract, but no tests document it.

**Fix:** Add one representative test such as `\x1Bc` producing zero actions.

**Location:** `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs:103`.

## Resolved Issues

- **Render test strategy refined (planning only)** — During plan review, the
  required Avalonia headless/`RenderTargetBitmap` tests were removed from this
  phase because they would require a new test dependency that has not been
  approved or cataloged. The M3 automated coverage is now limited to pure
  snapshot/contract/geometry-guard tests, with real rendering verified by the
  manual smoke checklist. `IMPLEMENTATION_PLAN.md` is authoritative.
- **Resize metric guard added (planning only)** — The plan now requires
  `ForwardResize()` to return early while `CellWidth` or `LineHeight` is zero,
  because bounds notifications can arrive before the custom render control has
  measured its first glyph and `TerminalGeometry.Compute()` rejects non-positive
  metrics.
- **TerminalViewModel test migration made explicit (planning only)** — The plan
  now enumerates the constructor-seam and `OutputText` assertion rewrites needed
  when `TerminalOutputBuffer` is replaced by `ScreenSnapshot`.

## Deferred to Future Phases

Items listed here are planned regressions or out-of-scope behaviours that
must be re-checked once the renderer is built. They do not yet correspond to
real code.

| Issue | Reason | Target Phase |
|-------|--------|--------------|
| Scrollback history ring | Planned screen buffer is viewport-only; deliberate regression from Phase 3.5 | Future terminal phase |
| 256-color SGR (38;5;n / 48;5;n) | Parser will recognise and ignore; extended palette deferred | Future terminal renderer phase |
| Mouse selection in terminal | Complex highlight rendering, mouse capture; deliberate regression from Phase 3.5 | Future terminal phase |
| Blinking cursor | Cosmetic; no visual impact on functionality | Future terminal phase |
| Cursor hide/show (DECSET/DECRST) | Not in M1 supported sequence set | Phase 3.8 (TUI compatibility) |
| Alternate screen (`\x1B[?1049h`) | Needed for vim/htop — explicitly out of scope | Phase 3.8 (TUI compatibility) |
| OSC sequences (window title, etc.) | No user-facing need yet | Future terminal phase |
| DCS sequences (tmux, kitty) | Niche; no testable use case | Future terminal phase |
| Bold as separate weight vs. bright color | Plan matches xterm: bold = bright fg. True bold glyph weight is cosmetic | Future terminal renderer phase |
| Underline / italic / strikethrough | SGR 4/3/9 to be recognised but not rendered | Future terminal renderer phase |
