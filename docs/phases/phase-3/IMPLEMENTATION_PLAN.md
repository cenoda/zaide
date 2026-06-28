# Phase 3: Terminal (Linux MVP) — Implementation Plan

## Pre-Implementation Verification

- [ ] Native PTY approach verified compatible with .NET 10 on Linux
- [ ] Proof-of-concept: PTY master/slave allocation works via libc P/Invoke
- [ ] Proof-of-concept: shell spawn path works without managed code running in a post-fork child branch
- [ ] Proof-of-concept: PTY session spawns a shell, reads output, and writes input
- [ ] Proof-of-concept: `TIOCSWINSZ` ioctl resizes the PTY
- [ ] Avalonia `TextBox` confirmed viable as a read-only output display with manual key forwarding (known limitations documented in §TextBox Surface)

## Entry Gate (M0)

- [ ] `dotnet build Zaide.slnx` passes with 0 warnings
- [ ] `dotnet test Zaide.slnx` passes (all existing tests green — 90+ at time of writing)
- [ ] Run Zaide, toggle bottom panel with Ctrl+` and Ctrl+J — both work
- [ ] Bottom panel placeholder visible in `MainWindow.axaml.cs` `BuildLayout()`

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

- [ ] Verify native PTY allocation via libc P/Invoke
- [ ] Verify shell spawn path does not depend on managed child-branch logic after `fork`
- [ ] Verify PTY session spawns `/bin/bash`, reads prompt, writes `echo hello\n`, reads `hello\n`
- [ ] Verify `TIOCSWINSZ` ioctl resizes the PTY without errors
- [ ] If PTY path fails, fall back to `Process` redirected streams (document limitations)
- [ ] `dotnet build Zaide.slnx` — 0 warnings after any package or interop changes

### Step 2: Service Interface + Linux Implementation (M1)

- [ ] Create `ITerminalService` interface:
  ```csharp
  public interface ITerminalService : IDisposable
  {
      event Action<byte[]>? OutputReceived;
      event Action? ProcessExited;

      Task StartAsync(string shell = "/bin/bash", CancellationToken ct = default);
      Task WriteAsync(byte[] data);
      void Resize(int columns, int rows);
      bool IsRunning { get; }
  }
  ```
- [ ] Create `LinuxTerminalService` implementing `ITerminalService`:
  - Uses native PTY allocation + native shell spawn path
  - Background thread reads from PTY fd → raises `OutputReceived`
  - `WriteAsync` writes bytes to PTY fd
  - `Resize` calls `TIOCSWINSZ` ioctl on PTY fd
  - Reader thread owns exit detection; service reaps child and raises `ProcessExited` once
  - `Dispose` is idempotent: kills child process if needed, closes fd, joins reader thread, tolerates repeated calls
- [ ] Register in `Program.cs` DI container

**Tests:**
- `LinuxTerminalService_StartAsync_RaisesOutput` — starts bash, verifies prompt output received
- `LinuxTerminalService_WriteAsync_EchoesInput` — sends `echo hello\n`, verifies output contains `hello`
- `LinuxTerminalService_Dispose_KillsProcess` — dispose, verify `IsRunning == false`
- Mark as `[Trait("Category", "Integration")]` for CI skip on non-Linux

### Step 3: Terminal ViewModel (M2)

- [ ] Create `TerminalViewModel : ReactiveObject, IDisposable`:
  - `StringBuilder _outputBuffer` (max 200,000 characters, configurable, acts as ring buffer)
  - `string OutputText` — reactive property, derived from `_outputBuffer`
  - `bool IsRunning` — reactive property from service
  - `string? StartupError` — reactive property, set when `StartAsync` fails (null on success or before start)
  - `ReactiveCommand<Unit, Unit> ClearCommand` — clears buffer
  - Subscribes to `ITerminalService.OutputReceived`:
    - Decodes `byte[]` with a stateful UTF-8 `Decoder` carried across read chunks
    - Appends to `_outputBuffer`
    - Trims from front if capacity exceeded (acceptable for MVP at 200K buffer size)
    - Raises `PropertyChanged` for `OutputText`
  - Subscribes to `ITerminalService.ProcessExited`:
    - Updates `IsRunning`
    - Appends "\r\n[Process exited]\r\n" to buffer
  - `SendInputAsync(byte[] data)` → `_service.WriteAsync(data)`
    - Safe before startup completes: early input must not throw; either no-op or buffer until the PTY is ready
  - `EnsureStartedAsync()` lazily starts the terminal on first reveal/focus
  - `Dispose` is idempotent and disposes the service safely
- [ ] Start terminal lazily when the terminal panel is first shown

**Tests:**
- `OutputReceived_AppendsToBuffer` — mock service (using `Mock<ITerminalService>`), raise event, verify `OutputText`
- `OutputReceived_DecodesUtf8AcrossChunkBoundaries` — split a multibyte sequence across two events, verify correct decoded output
- `BufferTrimsWhenFull` — append beyond capacity, verify oldest chars removed (acceptable for MVP at 200K buffer size)
- `ClearCommand_EmptiesBuffer` — verify `OutputText` is empty after clear
- `ProcessExited_UpdatesIsRunning` — verify state change
- `StartupError_SetOnStartFailure` — mock throws on `StartAsync`, verify `StartupError` is set
- `StartupError_NullOnStartSuccess` — verify `StartupError` is null after successful `StartAsync`

### Step 4: Terminal Panel View (M3)

- [ ] Create `TerminalPanel.cs` inheriting `ReactiveUserControl<TerminalViewModel>`
- [ ] **C# view construction** (not `.axaml`, per DESIGN.md §1):
  - `TextBox` with `FontFamily = "Cascadia Code, JetBrains Mono, monospace"`
  - `IsReadOnly = true` for output display (input handled via KeyDown)
  - `TextWrapping = NoWrap`, horizontal scrollbar
  - `Padding = 16px` (per DESIGN.md §5)
  - `KeyDown` handler converts keys to raw input:
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
- [ ] Monospace font specified on the `TextBox`

**No unit tests (UI-only milestone).**

### Step 5: Layout Integration (M4)

- [ ] Modify `BuildLayout()` in `MainWindow.axaml.cs`:
  - Preserve the existing bottom-panel visibility wiring
  - Keep the existing `Border` as the bottom-panel container, or change the field type intentionally and update all visibility bindings
  - Host `TerminalPanel` inside the bottom-panel container
  - Keep existing `Grid.SetColumnSpan(bottomPanel, 4)` spanning
- [ ] Add `TerminalViewModel` property to `MainWindowViewModel`
- [ ] Add subscription in `MainWindowViewModel.Activate()`:
  ```csharp
  _disposables.Add(
      this.WhenAnyValue(x => x.TerminalViewModel.StartupError)
          .Where(err => err is not null)
          .Subscribe(err => StatusText = $"Terminal: {err}"));
  ```
- [ ] Wire bottom panel toggle to show/hide `TerminalPanel`
- [ ] On first terminal reveal, call `TerminalViewModel.EnsureStartedAsync()`
- [ ] Reuse the same visibility toggle path to refocus the terminal surface when shown
- [ ] If `TerminalViewModel` remains DI-managed singleton, either do not dispose it from `MainWindowViewModel`, or require fully idempotent disposal semantics

**Tests:**
- `MainWindowViewModel_EnsuresTerminalStartsOnFirstReveal` — verify lazy startup trigger

### Step 6: Cleanup and Polish (M5)

- [ ] Verify `exit` in shell → panel shows "[Process exited]", service reports stopped
- [ ] Verify app shutdown → no zombie processes (`ps aux | grep bash`)
- [ ] Verify `dotnet build Zaide.slnx` — 0 warnings
- [ ] Verify `dotnet test Zaide.slnx` — all tests pass
- [ ] Verify Ctrl+C in terminal sends SIGINT, not clipboard copy
- [ ] Verify Tab key sends `\x09` (Tab completion works in bash)
- [ ] Verify arrow keys navigate bash history
- [ ] Verify terminal panel uses monospace font
- [ ] Verify terminal panel auto-focuses on TextBox when visible
- [ ] Verify StatusText shows terminal startup errors
- [ ] Remove any temporary/prototype code

## Exit Conditions

- [ ] `dotnet build Zaide.slnx` passes with 0 warnings and 0 errors
- [ ] `dotnet test Zaide.slnx` passes (all existing + new tests green)
- [ ] Terminal panel toggles with Ctrl+` and Ctrl+J
- [ ] Can type `echo hello` in terminal and see `hello` output
- [ ] Can type `exit` and terminal stops cleanly (no crash, no hang)
- [ ] Closing the app does not leave zombie processes
- [ ] `ITerminalService` is the only terminal interface referenced by ViewModels and Views
- [ ] Monospace font is used in the terminal panel
- [ ] Terminal panel auto-scrolls to bottom on new output
- [ ] No `LinuxTerminalService` referenced outside `Program.cs` DI registration

## Test Criteria

| Milestone | Automated Test | Manual Verification |
|-----------|---------------|---------------------|
| M0 | Build passes, existing tests green | Bottom panel toggles correctly |
| M1 | `LinuxTerminalService` integration tests pass | — |
| M2 | `TerminalViewModel` unit tests pass | — |
| M3 | — | Terminal panel renders in bottom area with monospace font |
| M4 | `MainWindowViewModel_EnsuresTerminalStartsOnFirstReveal` passes | Panel toggle shows/hides terminal |
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
4. Update docs/phases/phase-3/IMPLEMENTATION_PLAN.md to reflect the reduced scope
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
