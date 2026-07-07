# Phase 3.8: TUI Compatibility — Brief

## Goal

Expand the renderer and screen model so terminal user-interface programs can
run with much better fidelity.

## Summary

Phase 3.8 moves beyond ordinary shell quality and targets the behaviors needed
by text user interfaces such as `vim`, `less`, and `htop`. This phase is about
terminal correctness under more demanding screen-control patterns.

## Intended Outcome

- Cursor save/restore support (ESC 7/8, DECSET/DECRST ?1048)
- Alternate screen buffer support (DECSET/DECRST ?1047, ?1049)
- Narrow scroll-region support (DECSTBM `CSI top;bottom r`) if justified by target transcripts
- Higher compatibility with full-screen terminal applications
- Fewer visual mismatches in editor- and dashboard-style terminal programs

## Boundaries

- Do not chase total xterm compatibility in one pass
- Keep unsupported escape families explicitly documented
- Product polish concerns like search and UX extras remain out of scope
