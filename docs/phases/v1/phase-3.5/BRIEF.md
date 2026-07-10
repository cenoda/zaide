# Phase 3.5: Terminal UI Normalization — Brief

## Goal

Turn the Linux PTY MVP into a terminal surface that feels normal enough for
daily shell use inside the IDE.

## Summary

Phase 3.5 closes the biggest usability gaps left by the Phase 3 Linux terminal
MVP without attempting to become a full terminal emulator. It keeps the PTY
backend, keeps the single-session scope, and improves the UI and behavior
enough that common shell interaction is practical.

## Intended Outcome

- PTY size follows the visible terminal viewport
- Common shell keys work as expected
- Terminal lifecycle is visible and restart-safe
- The terminal has basic controls for clear, restart, and status
- Raw output behaves better for carriage return, backspace, and line updates

## Boundaries

- Linux only
- No full ANSI/VT100 parsing
- No custom renderer yet
- No tabs, splits, or multiple sessions
- No TUI/full-screen app compatibility guarantees
