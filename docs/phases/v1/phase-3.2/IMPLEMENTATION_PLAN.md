# Phase 3.2: Terminal (macOS Backend) — Implementation Plan

## Scope

**Goal:** Extend the embedded terminal to macOS using the same shared terminal surface established in earlier phases.

**In scope:**
- macOS-specific PTY-backed shell session
- Shell startup, environment, and resize handling for macOS
- Shared-layer adjustments only where macOS behavior exposes real gaps

**Out of scope:**
- Reworking Linux or Windows terminal architecture
- Terminal feature expansion unrelated to platform support
- macOS-specific settings UI or shell customization workflows

## Direction

- Treat macOS support as a backend adaptation, not a new terminal product.
- Keep platform quirks isolated in the process host layer whenever possible.
- Use this phase to validate that the abstraction built in Phase 3 is actually portable.
