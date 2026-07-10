# Phase 3.7: Interactive Shell Quality — Brief

## Goal

Make the new renderer feel correct for normal shell workflows, not just for
simple command output.

## Summary

Phase 3.7 improves the quality of day-to-day interactive shell usage on top of
the renderer foundation from Phase 3.6. The focus is the experience of
prompts, redraws, colors, cursor movement, and resize behavior during ordinary
terminal use.

## Intended Outcome

- `clear` behaves correctly
- Prompt redraw and in-place line updates look natural
- Common shell color output renders correctly
- Resize and redraw behavior are more stable during active sessions
- Everyday terminal interaction feels much closer to a modern IDE terminal

## Boundaries

- Still not a promise of full TUI compatibility
- No alternate-screen heavy application support as a primary goal
- No advanced mouse reporting or terminal settings work here
