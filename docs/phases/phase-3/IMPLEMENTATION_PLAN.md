# Phase 3: Terminal (Linux MVP) — Implementation Plan

## Pre-Implementation Verification

- [ ] `Mono.Posix` NuGet verified compatible with .NET 10 on Linux
- [ ] Proof-of-concept: `forkpty()` spawns a shell, reads output, writes input
- [ ] Proof-of-concept: `TIOCSWINSZ` ioctl resizes the PTY
- [ ] Avalonia `TextBox` confirmed suitable for raw terminal output display

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
- Linux PTY-backed shell session (via `Mono.Posix` `forkpty()`)
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

Use `Mono.Posix` NuGet package for `forkpty()`/`openpty()` P/Invoke on Linux.
This gives a real PTY: programs like `ls --color`, `git status`, `python -i`,
`vim`, `htop` all detect a TTY and behave correctly.

**Fallback:** If `Mono.Posix` proves incompatible with .NET 10 or Avalonia's
runtime, fall back to `System.Diagnostics.Process` with redirected streams.
Document the limitations (no color, no interactive programs, no resize) and
upgrade in a later phase. The `ITerminalService` interface makes this swap
invisible to the rest of the codebase.

### Data Model

Terminal output is a **character stream**, not a list of lines. The ViewModel
holds a `StringBuilder` (with a configurable max capacity acting as a ring buffer)
for raw output. No line-splitting, no ANSI parsing. The view renders the buffer
contents in a monospace `TextBox`.

**Future:** ANSI parsing will replace the raw `TextBox` with a styled text
renderer. The `StringBuilder` buffer model will remain as the source of truth.

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

This ensures Tab completion, Ctrl+C interruption, readline, and interactive
programs all work correctly.

**Ctrl+C conflict:** When the terminal panel is focused, Ctrl+C sends `\x03`
to the PTY (SIGINT). When the editor panel is focused, Ctrl+C is copy.
No global conflict — focus determines behavior.

### Font

Terminal panel uses `FontFamily = "Cascadia Code, JetBrains Mono, monospace"`
with a fallback chain. Consistent monospace character width is required for
correct terminal rendering.

## Key Components to Create

| Component | Path | Description |
|-----------|------|-------------|
| `ITerminalService` | `src/Services/ITerminalService.cs` | Interface for terminal operations |
| `LinuxTerminalService` | `src/Services/LinuxTerminalService.cs` | Linux PTY implementation via `Mono.Posix` |
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

### Step 1: NuGet + Proof-of-Concept (M0)

- [ ] Add `Mono.Posix` to `Directory.Packages.props` and `src/Zaide.csproj`
- [ ] Verify `forkpty()` spawns `/bin/bash`, reads "bash-5.1$" prompt, writes `echo hello\n`, reads `hello\n`
- [ ] Verify `TIOCSWINSZ` ioctl resizes the PTY without errors
- [ ] If `Mono.Posix` fails, fall back to `Process` redirected streams (document limitations)
- [ ] `dotnet build Zaide.slnx` — 0 warnings after adding the package

### Step 2: Service Interface + Linux Implementation (M1)

- [ ] Create `ITerminalService` interface:
  ```csharp
  public interface ITerminalService : IDisposable
  {
      event Action<byte[]>? OutputReceived;
      event Action? ProcessExited;

      Task StartAsync(string shell = "/bin/bash");
      Task WriteAsync(byte[] data);
      void Resize(int columns, int rows);
      bool IsRunning { get; }
  }
  ```
- [ ] Create `LinuxTerminalService` implementing `ITerminalService`:
  - Uses `Mono.Unix.Native.Syscall.forkpty()` to create PTY
  - Background thread reads from PTY fd → raises `OutputReceived`
  - `WriteAsync` writes bytes to PTY fd
  - `Resize` calls `TIOCSWINSZ` ioctl on PTY fd
  - `ProcessExited` raised when child process exits (detect via `waitpid`)
  - `Dispose` kills child process, closes fd, joins reader thread
- [ ] Register in `Program.cs` DI container

**Tests:**
- `LinuxTerminalService_StartAsync_RaisesOutput` — starts bash, verifies prompt output received
- `LinuxTerminalService_WriteAsync_EchoesInput` — sends `echo hello\n`, verifies output contains `hello`
- `LinuxTerminalService_Dispose_KillsProcess` — dispose, verify `IsRunning == false`
- Mark as `[Trait("Category", "Integration")]` for CI skip on non-Linux

### Step 3: Terminal ViewModel (M2)

- [ ] Create `TerminalViewModel : ReactiveObject, IDisposable`:
  - `StringBuilder _outputBuffer` (max 100,000 characters, acts as ring buffer)
  - `string OutputText` — reactive property, derived from `_outputBuffer`
  - `bool IsRunning` — reactive property from service
  - `ReactiveCommand<Unit, Unit> ClearCommand` — clears buffer
  - Subscribes to `ITerminalService.OutputReceived`:
    - Decodes `byte[]` → UTF-8 string
    - Appends to `_outputBuffer`
    - Trims from front if capacity exceeded
    - Raises `PropertyChanged` for `OutputText`
  - Subscribes to `ITerminalService.ProcessExited`:
    - Updates `IsRunning`
    - Appends "\r\n[Process exited]\r\n" to buffer
  - `SendInput(string text)` → encodes to UTF-8 bytes → `_service.WriteAsync(bytes)`
  - `Dispose` disposes the service
- [ ] Start terminal on ViewModel construction (or via explicit `StartCommand`)

**Tests:**
- `OutputReceived_AppendsToBuffer` — mock service, raise event, verify `OutputText`
- `BufferTrimsWhenFull` — append beyond capacity, verify oldest chars removed
- `ClearCommand_EmptiesBuffer` — verify `OutputText` is empty after clear
- `ProcessExited_UpdatesIsRunning` — verify state change

### Step 4: Terminal Panel View (M3)

- [ ] Create `TerminalPanel.cs` inheriting `ReactiveUserControl<TerminalViewModel>`
- [ ] **C# view construction** (not `.axaml`, per DESIGN.md §1):
  - `TextBox` with `FontFamily = "Cascadia Code, JetBrains Mono, monospace"`
  - `IsReadOnly = true` for output display (input handled via KeyDown)
  - `TextWrapping = NoWrap`, horizontal scrollbar
  - `KeyDown` handler converts keys to raw input:
    ```csharp
    private void OnKeyDown(object? sender, KeyEventArgs e)
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
            ViewModel?.SendInput(bytes);
            e.Handled = true;
        }
    }
    ```
  - `TextInput` handler for printable characters:
    ```csharp
    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is { Length: > 0 })
            ViewModel?.SendInput(e.Text);
        e.Handled = true;
    }
    ```
  - Bind `ViewModel.OutputText` → `TextBox.Text` (one-way)
  - Auto-scroll to end when output changes
- [ ] Monospace font specified on the `TextBox`

**No unit tests (UI-only milestone).**

### Step 5: Layout Integration (M4)

- [ ] Modify `BuildLayout()` in `MainWindow.axaml.cs`:
  - Replace the bottom panel placeholder `Border` with `TerminalPanel`
  - Inject `TerminalViewModel` from `MainWindowViewModel`
  - Keep existing `Grid.SetColumnSpan(bottomPanel, 4)` spanning
- [ ] Add `TerminalViewModel` property to `MainWindowViewModel`
- [ ] Wire bottom panel toggle to show/hide `TerminalPanel`
- [ ] Wire `TerminalViewModel` disposal in `MainWindowViewModel.Dispose()`

**Tests:**
- `MainWindowViewModel_DisposesTerminalViewModel` — verify disposal chain

### Step 6: Cleanup and Polish (M5)

- [ ] Verify `exit` in shell → panel shows "[Process exited]", service reports stopped
- [ ] Verify app shutdown → no zombie processes (`ps aux | grep bash`)
- [ ] Verify `dotnet build Zaide.slnx` — 0 warnings
- [ ] Verify `dotnet test Zaide.slnx` — all tests pass
- [ ] Verify Ctrl+C in terminal sends SIGINT, not clipboard copy
- [ ] Verify Tab key sends `\x09` (Tab completion works in bash)
- [ ] Verify arrow keys navigate bash history
- [ ] Verify terminal panel uses monospace font
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
| M4 | `MainWindowViewModel` disposal test passes | Panel toggle shows/hides terminal |
| M5 | All tests pass | `echo hello` works, `exit` cleans up, no zombie processes |

## Rollback Plan

If `Mono.Posix` integration fails (runtime errors, package incompatibility, P/Invoke
breakage on .NET 10):

1. Replace `LinuxTerminalService` with a redirected-stream implementation:
   - `Process.StandardOutput` / `Process.StandardInput` for I/O
   - No PTY — document limitations (no color, no interactive programs, no resize)
2. `ITerminalService` interface stays the same — no changes to ViewModel or View
3. Remove `Mono.Posix` from `Directory.Packages.props` and `src/Zaide.csproj`
4. Mark the redirected-stream implementation as temporary, upgrade in a later phase
5. If even redirected streams are problematic, revert the entire phase (git revert)

The key invariant: `ITerminalService` is the only contract the rest of the app
depends on. Swapping backends is a service-layer change, never a view or viewmodel change.

## Direction

- Treat Linux as the only supported terminal platform in this phase.
- Keep PTY handling behind `ITerminalService` so later phases can add other backends.
- Prefer a working terminal MVP over a feature-rich emulator.
- **MVP Scope:** Raw text output (no ANSI parsing). Programs that emit ANSI codes
  will show raw escape sequences. This is acceptable for Phase 3.
- Every keystroke goes directly to the PTY — no line-buffering, no command model.

## Phase 3 Limitations

- Only Linux is supported in this phase.
- No support for Windows or macOS terminal backends.
- ANSI/VT100 escape codes are **not parsed** — raw escape sequences may appear in output.
- No tabs, splits, or multiple terminal sessions.
- No UI for shell/profile settings.
- Copy/paste is limited to basic clipboard passthrough.
- Scrollback buffer is fixed-size (100K characters) — oldest output is discarded.
- Terminal rendering uses a plain `TextBox` — no grid-based character cell rendering.
