# Phase 3: Terminal (Linux MVP) — Implementation Plan

## Pre-Implementation Verification

- [x] Native PTY approach verified compatible with .NET 10 on Linux
- [x] Proof-of-concept: PTY master/slave allocation works via libc P/Invoke
- [x] Proof-of-concept: shell spawn path works without managed code running in a post-fork child branch
- [x] Proof-of-concept: PTY session spawns a shell, reads output, and writes input
- [x] Proof-of-concept: `TIOCSWINSZ` ioctl resizes the PTY
- [x] Avalonia `TextBox` confirmed viable as a read-only output display with manual key forwarding (known limitations documented in §TextBox Surface)

> **PoC result (M0):** Verified by the throwaway spike in `spike/PtySpike/`
> (run: `dotnet run` with `--project spike/PtySpike`). All four PTY claims pass:
> `posix_openpt`/`grantpt`/`unlockpt`/`ptsname_r` allocate the pair,
> `posix_spawn` + `POSIX_SPAWN_SETSID` spawns `/bin/bash` with the slave wired
> to fds 0/1/2 (no managed post-fork code), `echo hello` round-trips, and
> `TIOCSWINSZ` returns 0. `exit` terminates cleanly and `waitpid` reaps the
> child with no zombie. **PTY path adopted — Outcome A.** The spike is the
> reference for Step 2; delete `spike/` once `LinuxTerminalService` lands.

## Entry Gate (M0)

- [x] `dotnet build Zaide.slnx` passes with 0 warnings
  - Note: `Directory.Build.props` sets `TreatWarningsAsErrors=false`, so the build
    will not fail on warnings on its own — "0 warnings" is a manual discipline,
    verify the build output is clean.
- [x] `dotnet test Zaide.slnx` passes (all existing tests green — 90+ at time of writing)
- [x] Run Zaide, toggle bottom panel with Ctrl+` and Ctrl+J — both work
- [x] Bottom panel placeholder visible in `MainWindow.axaml.cs` `BuildLayout()` <!-- historical entry-gate check; placeholder has since been replaced in Step 5 -->

## Scope

**Goal:** Add an embedded terminal in the bottom panel. Linux PTY-backed shell
session with character-level input, raw text output, and proper lifecycle management.

**In scope:**
- Bottom terminal panel integrated into the existing shell layout
- Toggle terminal visibility via existing `ToggleBottomPanelCommand` (Ctrl+` / Ctrl+J)
- Linux PTY-backed shell session via native libc interop
- Character-level input forwarding (every keystroke sent to PTY immediately)
- Raw text output buffer (no ANSI/VT100 parsing in this phase)
- Terminal resize support (`TIOCSWINSZ` ioctl)
- Process lifecycle: start, exit detection, clean shutdown, zombie prevention
- Service boundary so platform code stays out of views and view models
- Monospace font rendering
- StatusText integration for terminal startup failures

**Out of scope:**
- Windows terminal backend (Phase 3.1)
- macOS terminal backend (Phase 3.2)
- ANSI/VT100 escape code parsing and rendering (future polish phase)
- Cross-platform parity in this phase
- Tabs, splits, or multiple terminal sessions
- Shell/profile settings UI
- Copy/paste beyond basic clipboard passthrough
- Scrollback limit tuning (ring buffer fixed size is fine for MVP)

## Technical Decisions

### PTY Approach

Do not rely on `forkpty()` followed by managed child-branch logic. Forking a
multi-threaded .NET process and then running managed code in the child before
`exec` is unsafe.

The preferred Phase 3 approach is:
- allocate a PTY master/slave pair through native libc interop
- spawn the shell through a native path that does not return managed execution
  into a post-fork child branch
- keep all PTY/session lifecycle control in the Linux terminal service

The exact native API combination is left to the proof-of-concept, but the
design target is the same: a real PTY without managed child-branch execution.

This gives a real PTY: programs like `ls --color`, `git status`, `python -i`,
and bash readline (Tab completion, arrow-key history, Ctrl+C) all detect a
TTY and behave correctly. Cursor-addressed programs like `vim` and `htop`
emit raw ANSI sequences visible as text — terminal emulator rendering is a
future phase.

**Fallback (reduced-scope outcome):** If the PTY path proves unworkable on the
current stack, a `System.Diagnostics.Process` with redirected streams can replace
the PTY backend. This is **not** an equivalent swap — it changes the product contract:

- No TTY detection (programs see a pipe, not a terminal)
- No `Resize()` — terminal dimension ioctls have no effect
- No color output, no interactive prompts (`python -i`, `git commit`)
- No Ctrl+C signal routing (SIGINT is not delivered to the child)
- Key handling falls back to line-buffered input

`ITerminalService` preserves the same API surface, but the semantics are weaker
in degraded mode. The ViewModel and View must treat this as a reduced-scope
outcome. The scope in §Scope narrows correspondingly: character-level input
forwarding becomes line-level; `echo hello` still works but `vim` / `htop` /
`python -i` are non-functional. Upgrade to PTY is a future-phase requirement.

### Data Model

Terminal output is a **character stream**, not a list of lines. The ViewModel
holds a `StringBuilder` (with a configurable max capacity acting as a ring buffer,
default 200,000 characters) for raw output. No line-splitting, no ANSI parsing.
The view renders the buffer contents in a monospace `TextBox`.

**Future:** ANSI parsing will replace the raw `TextBox` with a styled text
renderer. The `StringBuilder` buffer model can remain as the source buffer, but
stream decoding must stay stateful across read boundaries.

### Input Model

Input is **character-level**, not line-based. The `TerminalPanel` captures
`KeyDown` events and converts them to raw bytes:
- Printable characters → UTF-8 bytes
- Enter → `\r` (carriage return, as PTY expects)
- Backspace → `\x7F` (DEL)
- Tab → `\x09`
- Ctrl+C → `\x03` (ETX, SIGINT)
- Ctrl+D → `\x04` (EOT)
- Arrow keys → ANSI escape sequences (`\x1B[A`, `\x1B[B`, etc.)

This ensures Tab completion, Ctrl+C interruption, readline, and PTY-backed
interactive shell workflows work correctly.

**Ctrl+C conflict:** When the terminal panel is focused, Ctrl+C sends `\x03`
to the PTY (SIGINT). When the editor panel is focused, Ctrl+C is copy.
No global conflict — focus determines behavior.

### Font

Terminal panel uses `FontFamily = "Cascadia Code, JetBrains Mono, monospace"`
with a fallback chain. Consistent monospace character width is required for
correct terminal rendering.

### TextBox Surface

The MVP terminal panel uses a **read-only `TextBox`** as the output surface with
manual `KeyDown`/`TextInput` capture for input forwarding. This is an **output
viewer plus minimal key relay**, not a terminal emulator widget.

**Known limitations (accepted for MVP):**

- **No selection/copy from output:** `IsReadOnly = true` with `KeyDown` handling
  makes text selection unreliable. Recommended pattern: handle Ctrl+Shift+C for
  copy separately, or add a context-menu "Copy" action. Pure mouse selection
  inside the TextBox is not guaranteed.
- **Input focus fragility:** `TextBox` may lose focus to other controls when the
  panel is toggled or when Avalonia routes a modifier key. The panel must
  refocus the `TextBox` on visibility toggle.
- **Modifier key routing:** Ctrl+<key> combinations are captured by `KeyDown`
  before `TextInput` fires. The handler must not accidentally consume keys that
  should reach the shell (e.g., Ctrl+L for clear, Ctrl+U for kill-line).
- **IME/composition input:** `TextInput` events from input method editors are
  passed through as encoded UTF-8 bytes. Composition preview (underline) is not
  displayed in read-only mode — the composed character appears only on commit.
- **Non-printable keys:** Keys without a `TextInput` event (function keys,
  Escape) must be mapped in `KeyDown`. The mapping table covers the common
  subset (arrows, Enter, Backspace, Tab, Ctrl+C/D). Untested keys (F1–F12,
  Insert, Page Up/Down) are not forwarded.

**Future:** A custom `TerminalSurface` control (derived from `Control` with
direct drawing) will replace the `TextBox` when ANSI parsing and cell-based
rendering are added.

**Design rule for Step 4:** If the `TextBox` surface proves too limiting during
implementation (e.g., focus handling is unusable), switch to a plain `Border`
with a `TextBlock` child and direct `DrawingContext` rendering. Do not add XAML
controls or third-party terminal widgets.

## Key Components to Create

| Component | Path | Description |
|-----------|------|-------------|
| `ITerminalService` | `src/Services/ITerminalService.cs` | Interface for terminal operations |
| `LinuxTerminalService` | `src/Services/LinuxTerminalService.cs` | Linux PTY implementation via native libc interop |
| `TerminalPanel` | `src/Views/TerminalPanel.cs` | Terminal UI panel (C# view, monospace `TextBox`) |
| `TerminalViewModel` | `src/ViewModels/TerminalViewModel.cs` | Terminal state, raw output buffer, input forwarding |

### DI Registration (in `Program.cs`)

```csharp
services.AddSingleton<ITerminalService, LinuxTerminalService>();
services.AddSingleton<TerminalViewModel>();
```

**Shutdown disposal (required for the no-zombie exit condition):** A singleton
`ITerminalService` is only disposed when the DI container is disposed. `Program.cs`
builds the provider through `UseReactiveUIWithMicrosoftDependencyResolver` but does
**not** currently dispose it on application exit, so `LinuxTerminalService.Dispose()`
would never fire and the shell child process would be orphaned. The phase must wire
an explicit shutdown hook — dispose the `ITerminalService` (or the provider) from the
desktop lifetime's `ShutdownRequested`/exit path — otherwise the "no zombie processes"
exit condition cannot be met.

When Phase 3.1 (Windows) or 3.2 (macOS) adds a backend, the registration
swaps to a platform-conditional factory:

```csharp
services.AddSingleton<ITerminalService>(sp =>
{
    if (OperatingSystem.IsLinux()) return new LinuxTerminalService();
    if (OperatingSystem.IsWindows()) return new WindowsTerminalService();
    throw new PlatformNotSupportedException();
});
```

## Implementation Sequence

### Step 1: Native PTY Proof-of-Concept (M0)

- [x] Verify native PTY allocation via libc P/Invoke
- [x] Verify shell spawn path does not depend on managed child-branch logic after `fork`
  - Recommended non-fork path: `posix_openpt` / `grantpt` / `unlockpt` / `ptsname`
    to allocate the PTY, then `posix_spawn` with file actions to dup the slave fd
    onto the child's stdin/stdout/stderr. This satisfies the "no managed
    post-fork child branch" rule directly (the child is replaced by the shell
    image without any managed code running between fork and exec).
  - Confirmed by the `spike/PtySpike/` PoC using exactly this path, with
    `POSIX_SPAWN_SETSID` so the child becomes a session leader and opening the
    slave (without `O_NOCTTY`) acquires it as the controlling terminal.
- [x] Verify PTY session spawns `/bin/bash`, reads prompt, writes `echo hello\n`, reads `hello\n`
- [x] Verify `TIOCSWINSZ` ioctl resizes the PTY without errors
- [ ] If PTY path fails, fall back to `Process` redirected streams (document limitations) — N/A, PTY path passed
- [x] `dotnet build Zaide.slnx` — 0 warnings after any package or interop changes (spike is outside the solution; main build remains 0 warnings)

### Step 2: Service Interface + Linux Implementation (M1) — DONE

> **Status:** Implemented. `ITerminalService`, `LinuxTerminalService`, and
> `LinuxPtyInterop` (P/Invoke) live in `src/Services/`; DI registration in
> `Program.cs`; app-exit disposal hook in `App.axaml.cs`. 3 integration tests
> pass; full suite is 107 green with 0 build warnings; no orphaned/zombie
> processes after run. The `spike/` PoC was deleted (superseded).

- [x] Create `ITerminalService` interface:
  ```csharp
  public interface ITerminalService : IDisposable
  {
      event Action<byte[]>? OutputReceived;
      event Action? ProcessExited;

      Task StartAsync(string shell = "/bin/bash", CancellationToken ct = default);
      Task WriteAsync(byte[] data, CancellationToken ct = default);
      void Resize(int columns, int rows);
      bool IsRunning { get; }
  }
  ```
  > **Note:** `OutputReceived` and `ProcessExited` are raised from the
  > background reader thread. They carry **no** UI-thread guarantee — the
  > `TerminalViewModel` is responsible for marshaling to the UI thread before
  > touching bound state (see Step 3).
- [x] Create `LinuxTerminalService` implementing `ITerminalService`:
  - Uses native PTY allocation + native shell spawn path
  - Background thread reads from PTY fd → raises `OutputReceived`
  - `WriteAsync` writes bytes to PTY fd
  - `Resize` calls `TIOCSWINSZ` ioctl on PTY fd
  - Reader thread owns exit detection; service reaps child and raises `ProcessExited` once
  - `Dispose` is idempotent: kills child process if needed, closes fd, joins reader thread, tolerates repeated calls
- [x] Register in `Program.cs` DI container

**Tests:**
- [x] `LinuxTerminalService_StartAsync_RaisesOutput` — starts bash, verifies prompt output received
- [x] `LinuxTerminalService_WriteAsync_EchoesInput` — sends `echo hello\n`, verifies output contains `hello`
- [x] `LinuxTerminalService_Dispose_KillsProcess` — dispose, verify `IsRunning == false`
- [x] Mark as `[Trait("Category", "Integration")]` for CI skip on non-Linux

### Step 3: Terminal ViewModel (M2) — DONE

> **Status:** Implemented as `src/ViewModels/TerminalViewModel.cs`, registered
> as a singleton in `Program.cs`. UTF-8 decode happens on the reader thread; the
> buffer append + `OutputText` change is marshaled to the UI thread via a seam
> (`Dispatcher.UIThread.Post` in production, run-inline in tests). Buffer access
> is lock-guarded. 8 unit tests pass; full suite 115 green, 0 build warnings.
>
> Implementation note: the UI-marshal seam is an internal constructor
> (`InternalsVisibleTo Zaide.Tests`), so the DI-facing public constructor stays
> a clean single `(ITerminalService)` — no constructor ambiguity for the
> container.

- [x] Create `TerminalViewModel : ReactiveObject, IDisposable`:
  - `StringBuilder _outputBuffer` (max 200,000 characters, configurable, acts as ring buffer)
  - `string OutputText` — reactive property, derived from `_outputBuffer`
  - `bool IsRunning` — reactive property from service
  - `string? StartupError` — reactive property, set when `StartAsync` fails (null on success or before start)
  - `ReactiveCommand<Unit, Unit> ClearCommand` — clears buffer
  - Subscribes to `ITerminalService.OutputReceived`:
    - **Marshals to the UI thread first.** The event fires on the background
      reader thread, so the handler body must run via
      `Dispatcher.UIThread.Post(...)` (or observe on `RxApp.MainThreadScheduler`)
      before mutating the buffer or raising `PropertyChanged`. Mutating bound
      state off the UI thread will throw or corrupt rendering in Avalonia.
    - Decodes `byte[]` with a stateful UTF-8 `Decoder` carried across read chunks
      (decode happens before the UI-thread hop; only buffer mutation needs the UI thread)
    - Appends to `_outputBuffer`
    - Trims from front if capacity exceeded (acceptable for MVP at 200K buffer size)
    - Raises `PropertyChanged` for `OutputText`
  - Subscribes to `ITerminalService.ProcessExited`:
    - **Also marshals to the UI thread** (same reader-thread origin)
    - Updates `IsRunning`
    - Appends "\r\n[Process exited]\r\n" to buffer
  - `SendInputAsync(byte[] data)` → `_service.WriteAsync(data)`
    - Safe before startup completes: early input must not throw; either no-op or buffer until the PTY is ready
  - `EnsureStartedAsync()` lazily starts the terminal on first reveal/focus
  - `Dispose` is idempotent and disposes the service safely
- [x] Start terminal lazily when the terminal panel is first shown <!-- EnsureStartedAsync; wired to reveal in Step 5 -->

> **Perf note (accepted for MVP):** `OutputText` is rebuilt via
> `_outputBuffer.ToString()` on every output chunk, and front-trimming a
> `StringBuilder` is an O(n) copy. Under chatty output (e.g. `yes`, large build
> logs) this is measurably expensive at the 200K buffer size. Acceptable for the
> MVP; the future ANSI/cell-renderer phase replaces this with an incremental
> model.

**Tests:**
- [x] `OutputReceived_AppendsToBuffer` — mock service (using `Mock<ITerminalService>`), raise event, verify `OutputText`
- [x] `OutputReceived_DecodesUtf8AcrossChunkBoundaries` — split a multibyte sequence across two events, verify correct decoded output
- [x] `BufferTrimsWhenFull` — append beyond capacity, verify oldest chars removed (acceptable for MVP at 200K buffer size)
- [x] `ClearCommand_EmptiesBuffer` — verify `OutputText` is empty after clear
- [x] `ProcessExited_UpdatesIsRunning` — verify state change
- [x] `StartupError_SetOnStartFailure` — mock throws on `StartAsync`, verify `StartupError` is set
- [x] `StartupError_NullOnStartSuccess` — verify `StartupError` is null after successful `StartAsync`
- [x] `Dispose_UnsubscribesAndDisposesService` — events after dispose are ignored; service disposed once (added)

### Step 4: Terminal Panel View (M3) — DONE

> **Status:** Implemented as `src/Views/TerminalPanel.cs`. The view is built in
> C# with a read-only monospace `TextBox`, one-way output binding, auto-scroll,
> auto-focus on reveal, and routed key forwarding for printable input plus the
> common control/navigation keys. Manual verification confirmed typing, Enter,
> Backspace, Tab, arrow-key history, and Ctrl+C all reach the PTY correctly.
> Raw ANSI/control sequences still render as visible text by design in this MVP.

- [x] Create `TerminalPanel.cs` inheriting `ReactiveUserControl<TerminalViewModel>`
- [x] **C# view construction** (not `.axaml`, per DESIGN.md §1):
  - `TextBox` with `FontFamily = "Cascadia Code, JetBrains Mono, monospace"`
  - `IsReadOnly = true` for output display (input handled via routed `KeyDown`/`TextInput`)
  - `TextWrapping = NoWrap`
  - `Padding = 16px` (per DESIGN.md §5)
  - `KeyDown` handler converts keys to raw input
  - Auto-focus `TextBox` when panel becomes visible
    ```csharp
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        byte[]? bytes = e.Key switch
        {
            Key.Enter => [(byte)'\r'],
            Key.Back => [0x7F],
            Key.Tab => [0x09],
            Key.Left => "\x1B[D"u8.ToArray(),
            Key.Right => "\x1B[C"u8.ToArray(),
            Key.Up => "\x1B[A"u8.ToArray(),
            Key.Down => "\x1B[B"u8.ToArray(),
            _ when e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.C
                => [0x03],
            _ when e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.D
                => [0x04],
            _ => null // handled by TextInput for printable chars
        };
        if (bytes is not null)
        {
            await ViewModel!.SendInputAsync(bytes);
            e.Handled = true;
        }
    }
    ```
  - `TextInput` handler for printable characters:
    ```csharp
    private async void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is { Length: > 0 })
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(e.Text);
            await ViewModel!.SendInputAsync(bytes);
        }
        e.Handled = true;
    }
    ```
  - Bind `ViewModel.OutputText` → `TextBox.Text` (one-way)
  - Auto-scroll to end when output changes
- [x] Monospace font specified on the `TextBox`

**No unit tests (UI-only milestone).**

### Step 5: Layout Integration (M4) — DONE

> **Status:** Implemented in `src/MainWindow.axaml.cs` and
> `src/ViewModels/MainWindowViewModel.cs`. The bottom panel now hosts
> `TerminalPanel`, the window wires in the singleton `TerminalViewModel`, and
> first reveal triggers lazy startup plus terminal refocus. `StatusText`
> subscribes to terminal startup failures. `MainWindowViewModelTests` covers the
> startup-error propagation path; lazy startup itself is currently verified
> manually through the real window integration.

- [x] Modify `BuildLayout()` in `MainWindow.axaml.cs`:
  - Preserve the existing bottom-panel visibility wiring
  - Keep the existing `Border` as the bottom-panel container, or change the field type intentionally and update all visibility bindings
  - Host `TerminalPanel` inside the bottom-panel container
  - Keep existing `Grid.SetColumnSpan(bottomPanel, 4)` spanning
- [x] Add `TerminalViewModel` property to `MainWindowViewModel`
  - Add a `TerminalViewModel` parameter to the `MainWindowViewModel` constructor
    (currently `(FileTreeViewModel, EditorTabViewModel)`) so DI supplies the
    singleton, and expose it as a property.
- [x] Add subscription in `MainWindowViewModel.Activate()`:
  ```csharp
  _disposables.Add(
      this.WhenAnyValue(x => x.TerminalViewModel.StartupError)
          .Where(err => err is not null)
          .Subscribe(err => StatusText = $"Terminal: {err}"));
  ```
- [x] Wire bottom panel toggle to show/hide `TerminalPanel`
- [x] On first terminal reveal, call `TerminalViewModel.EnsureStartedAsync()`
- [x] Reuse the same visibility toggle path to refocus the terminal surface when shown
- [x] If `TerminalViewModel` remains DI-managed singleton, either do not dispose it from `MainWindowViewModel`, or require fully idempotent disposal semantics

**Tests:**
- [x] `MainWindowViewModel_StartupError_UpdatesStatusText`
- [x] Lazy startup trigger verified manually through the real window integration

### Step 6: Cleanup and Polish (M5) — DONE

- [x] Verify `exit` in shell → panel shows "[Process exited]", service reports stopped
- [x] Verify app shutdown → no zombie processes (`ps aux | grep bash`)
- [x] Verify the shutdown disposal hook actually fires `ITerminalService.Dispose()` on app exit (DI does not dispose the provider automatically here)
- [x] Verify output rendering works while bash streams rapidly (UI-thread marshaling holds; no cross-thread exceptions)
- [x] Verify `dotnet build Zaide.slnx` — 0 warnings
- [x] Verify `dotnet test Zaide.slnx` — all tests pass
- [x] Verify Ctrl+C in terminal sends SIGINT, not clipboard copy
- [x] Verify Tab key sends `\x09` (Tab completion works in bash)
- [x] Verify arrow keys navigate bash history
- [x] Verify terminal panel uses monospace font
- [x] Verify terminal panel auto-focuses on TextBox when visible
- [x] Verify StatusText shows terminal startup errors
- [x] Remove any temporary/prototype code

## Exit Conditions

- [x] `dotnet build Zaide.slnx` passes with 0 warnings and 0 errors
- [x] `dotnet test Zaide.slnx` passes (all existing + new tests green)
- [x] Terminal panel toggles with Ctrl+` and Ctrl+J
- [x] Can type `echo hello` in terminal and see `hello` output
- [x] Can type `exit` and terminal stops cleanly (no crash, no hang)
- [x] Closing the app does not leave zombie processes
- [x] `ITerminalService` is the only terminal interface referenced by ViewModels and Views
- [x] Monospace font is used in the terminal panel
- [x] Terminal panel auto-scrolls to bottom on new output
- [x] No `LinuxTerminalService` referenced outside `Program.cs` DI registration

## Test Criteria

| Milestone | Automated Test | Manual Verification |
|-----------|---------------|---------------------|
| M0 | Build passes, existing tests green | Bottom panel toggles correctly |
| M1 | `LinuxTerminalService` integration tests pass | — |
| M2 | `TerminalViewModel` unit tests pass | — |
| M3 | — | Terminal panel renders in bottom area with monospace font |
| M4 | `MainWindowViewModel_StartupError_UpdatesStatusText` passes | Panel toggle shows/hides terminal; first reveal starts the terminal |
| M5 | All tests pass | `echo hello` works, `exit` cleans up, no zombie processes |

## Rollback Plan

Phase 3 has two possible outcomes depending on PTY viability:

### Outcome A: PTY works (target scope)
Full scope as defined above — character-level input, resize, TTY detection,
interactive shells, `echo hello`, `ls --color`, `python -i`, readline.

### Outcome B: PTY fails, redirected-stream fallback (reduced scope)
If the PTY integration fails (runtime errors, interop breakage on .NET 10,
or native API incompatibility), the fallback is a `System.Diagnostics.Process` with
redirected streams. This is a **different product** with a narrower contract:

| Capability | Outcome A (PTY) | Outcome B (redirected) |
|---|---|---|
| Input mode | Character-level, immediate | Line-buffered (`Process.StandardInput`) |
| `Resize()` | ioctl on PTY fd | No-op |
| TTY detection | Programs see `/dev/pts/N` | Programs see a pipe |
| `ls --color` | Colors emitted | No color (detects pipe) |
| `python -i` | Interactive REPL | Hangs or exits (no PTY) |
| Ctrl+C routing | SIGINT to child | Kills process tree |
| Tab completion | Full readline support | Shell-dependent, degraded |

**If Outcome B is adopted:**

1. Replace `LinuxTerminalService` with a redirected-stream implementation
2. `ITerminalService` interface stays the same (same API surface, weaker semantics) — ViewModel and View code is unchanged, but `WriteAsync` data is now line-buffered and `Resize` is a no-op
3. Remove any PTY-specific package or interop scaffolding that is no longer needed
4. Update docs/phases/v1/phase-3/IMPLEMENTATION_PLAN.md to reflect the reduced scope
5. Mark the redirected-stream implementation as temporary, with PTY upgrade as the
   next-phase requirement

**Outcome C: Even redirected streams fail**
If even `Process` with redirected streams is problematic, revert the entire phase
(`git revert`) and document the blocking issue in TOFIX.md.

## Direction

- Treat Linux as the only supported terminal platform in this phase.
- Keep PTY handling behind `ITerminalService` so later phases can add other backends.
- Prefer a working terminal MVP over a feature-rich emulator.
- **MVP Scope:** Raw text output (no ANSI parsing). Programs that emit ANSI codes
  will show raw escape sequences. This is acceptable for Phase 3.
- Every keystroke goes directly to the PTY — no line-buffering, no command model.
- Prefer lazy terminal startup on first reveal over eager process launch at app startup.

## Phase 3 Limitations

- Only Linux is supported in this phase.
- No support for Windows or macOS terminal backends.
- ANSI/VT100 escape codes are **not parsed** — raw escape sequences may appear in output.
- No tabs, splits, or multiple terminal sessions.
- No UI for shell/profile settings.
- Copy/paste is limited to basic clipboard passthrough.
- Scrollback buffer is fixed-size (200K characters, configurable) — oldest output is discarded.
- Terminal rendering uses a plain `TextBox` — no grid-based character cell rendering.
- Cascadia Code and JetBrains Mono fonts may not be installed on minimal Linux systems (falls back to system monospace).
