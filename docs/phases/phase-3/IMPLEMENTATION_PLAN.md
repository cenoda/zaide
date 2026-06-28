# Phase 3: Terminal (Linux MVP) — Implementation Plan

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

## Direction

- Treat Linux as the only supported terminal platform in this phase.
- Keep PTY handling behind an interface so later phases can add other backends.
- Prefer a working terminal MVP over a feature-rich emulator.
