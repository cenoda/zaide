# Phase 3.6: Terminal Renderer Foundation — Brief

## Goal

Replace the current raw-text terminal surface with the first real rendering
foundation for terminal output.

## Summary

Phase 3.6 starts the transition from a `TextBox`-based output viewer to a true
terminal rendering pipeline. The focus is not full compatibility yet, but the
core architecture needed to stop showing visible escape junk and to support
screen-based rendering in future phases.

## Intended Outcome

- Stateful ANSI/CSI parsing begins
- A screen-buffer model replaces plain text as the authoritative terminal state
- A custom terminal control becomes the rendering surface
- Basic cursor movement, clear operations, and styled cell rendering become
  possible

## Boundaries

- Do not aim for full `vim` / `htop` compatibility yet
- Do not add Windows or macOS backends here
- Do not mix renderer work with terminal tabs, splits, or settings UI
- Keep the supported escape-sequence set intentionally small and testable
