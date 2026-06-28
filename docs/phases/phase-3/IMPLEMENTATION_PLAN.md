# Phase 3: Terminal (Linux MVP) — Implementation Plan

## Pre-Implementation Verification
- [ ] Library/tool understanding confirmed
- [ ] Minimal proof-of-concept works
- [ ] Dependencies verified compatible

## Scope

**Goal:** Add an embedded terminal that works on Linux and fits the existing bottom panel workflow.

**In scope:**
- Bottom terminal panel integrated into the current shell layout
- Toggle terminal visibility from the existing UI pathway
- Linux PTY-backed shell session
- Basic input/output flow between UI and shell process
- Terminal-specific service boundary so platform code stays out of views and view models
- Basic rendering that is good enough for normal command-line usage
- Reuse of existing `StatusText` connection points if terminal startup fails

**Out of scope:**
- Windows terminal backend
- macOS terminal backend
- Full terminal emulation completeness
- Cross-platform parity in this phase
- Tabs, splits, or multiple terminal sessions
- Shell/profile settings UI
- Copy/paste/search polish beyond what is already easy to support

## Implementation Details

### Key Components to Create

| Component | Path | Description |
|-----------|------|-------------|
| `ITerminalService` | `src/Services/ITerminalService.cs` | Interface for terminal operations |
| `LinuxTerminalService` | `src/Services/LinuxTerminalService.cs` | Linux PTY implementation |
| `TerminalPanel` | `src/Views/TerminalPanel.axaml[.cs]` | Terminal UI panel |
| `TerminalViewModel` | `src/ViewModels/TerminalViewModel.cs` | Terminal state and commands |
| `TerminalOutputEvent` | `src/Models/TerminalOutputEvent.cs` | Terminal output event model |

### Implementation Sequence

#### Step 1: Entry Gate (M0)
- [ ] Run Zaide and verify bottom panel structure exists
- [ ] Identify the existing panel container in `MainWindow.axaml`
- [ ] Confirm the toggle mechanism for bottom panel visibility

#### Step 2: Terminal Panel UI (M1)
- [ ] Create `TerminalPanel.axaml` with a `TextBox` or `RichTextBox` for output
- [ ] Add input `TextBox` at the bottom for command entry
- [ ] Add `TerminalPanel` to the bottom panel area in `MainWindow.axaml`
- [ ] Make panel visible by default or toggleable

#### Step 3: Terminal Service Boundary (M2, M4)
- [ ] Create `ITerminalService` interface:
  ```csharp
  public interface ITerminalService
  {
      event Action<string>? OutputReceived;
      Task StartAsync(string shell = "/bin/bash");
      Task SendInputAsync(string input);
      void Stop();
      bool IsRunning { get; }
  }
  ```
- [ ] Register `ITerminalService` in `App.axaml.cs` DI container
- [ ] Inject into `TerminalViewModel`

#### Step 4: Linux PTY Implementation (M3)
- [ ] Create `LinuxTerminalService` implementing `ITerminalService`
- [ ] Use `System.Diagnostics.Process` with `UseShellExecute=false`
- [ ] Set up PTY using pseudo-terminal (via `forkpty` or `pty` package)
- [ ] Stream output from `stdout`/`stderr` to `OutputReceived` event
- [ ] Write input to `stdin` when `SendInputAsync` is called

#### Step 5: Terminal ViewModel (M3, M5)
- [ ] Create `TerminalViewModel` with:
  - `ObservableCollection<string> OutputLines` for display
  - `string CurrentInput` for command buffer
  - `ICommand SendCommand` to execute input
  - `ICommand ClearCommand` to clear output
- [ ] Subscribe to `ITerminalService.OutputReceived` and add to `OutputLines`
- [ ] Handle terminal startup failures gracefully

#### Step 6: Terminal Visibility Toggle (M2)
- [ ] Add toggle button in the bottom panel header or status bar
- [ ] Bind to `TerminalViewModel.IsVisible` or reuse existing panel toggle
- [ ] Persist visibility state if needed

### Test Criteria

| Milestone | Test | Verification |
|-----------|-----|--------------|
| M0 | Bottom panel exists | Run app, bottom panel visible |
| M1 | Terminal panel renders | Terminal panel shows in bottom area |
| M2 | Toggle works | Click toggle, panel shows/hides |
| M3 | PTY accepts input | Type `echo hello`, see output |
| M4 | Service boundary clean | No `LinuxTerminalService` in views/viewmodels |
| M5 | Basic rendering | Commands and output display correctly |

### Dependency Notes

- Consider using `PortableTerminal` or `xterm` package for terminal rendering
- Or use basic Avalonia `TextBox` for MVP (simpler, less feature-complete)
- PTY handling may need a native library or `Process` with redirected streams

## Direction

- Treat Linux as the only supported terminal platform in this phase.
- Keep PTY handling behind an interface so later phases can add other backends.
- Prefer a working terminal MVP over a feature-rich emulator.

## Phase 3 Limitations
- Only Linux is supported in this phase.
- No support for Windows or macOS terminal backends.
- Terminal emulation is basic and may not support all features.
- No tabs, splits, or multiple terminal sessions.
- No UI for shell/profile settings.
- Copy/paste/search functionality is limited to basic support.
