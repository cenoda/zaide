# Phase 3.9.1: Terminal Tabs — Brief

## Goal

Add lightweight terminal tabs without widening into a broader terminal architecture rewrite.

## Summary

Phase 3.9.1 isolates the highest-risk part of Phase 3.9 into its own slice:
multiple terminal sessions in the bottom panel. The purpose is to support a
practical multi-terminal workflow while keeping the implementation small,
verifiable, and aligned with the current shell architecture.

This phase exists because terminal tabs may require session-hosting and
composition changes that are larger than ordinary UX polish. Splitting the work
keeps Phase 3.9 focused on selection, scrollback, and search, while giving
terminal-session management its own scope and checkpoints.

## Intended Outcome

- Multiple terminal tabs in the bottom panel
- One shell session per tab
- Clear active-tab switching and focus behavior
- Predictable tab creation, closing, and disposal
- Terminal tabs that feel integrated without introducing pane/split complexity

## Boundaries

- No terminal splits or pane layouts
- No persisted terminal sessions across app restarts
- No detached windows or drag-reorder support
- No backend PTY rewrite
- No broad shell/workspace architecture redesign unless a later refactor owns it
- Do not regress Phase 3.8 alternate-screen correctness or Phase 3.9 terminal UX polish work
