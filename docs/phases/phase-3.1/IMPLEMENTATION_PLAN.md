# Phase 3.1: Terminal (Windows Backend) — Implementation Plan

## Scope

**Goal:** Extend the embedded terminal to Windows without changing the Linux-first app structure.

**In scope:**
- Windows-specific terminal process host behind the existing abstraction
- ConPTY-based shell session support
- Shell startup and resize wiring for Windows
- Compatibility updates needed in shared terminal UI or buffer logic

**Out of scope:**
- Re-architecture of the Linux implementation
- macOS terminal backend
- Advanced Windows-only terminal features
- Feature parity beyond what is needed for the shared MVP experience

## Direction

- Keep the shared UI and view model layer platform-agnostic.
- Add only the Windows-specific code needed to match the Linux MVP contract.
- Prefer thin platform adapters over shared code that bakes in OS assumptions.
