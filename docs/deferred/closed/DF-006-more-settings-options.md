# DF-006: Add more options to the settings tab

**Area:** settings
**Status:** closed
**Priority:** medium
**Discovered:** 2026-07-29
**Related:** settings panel, configuration surface, Phase 23 F5

## Observation

The settings tab exposed only a small set of options (Editor, Terminal, LLM).
Agent and transparency durable configuration lived in Townhall inspection chrome.

## Resolution

**Resolved by Phase 23 F5** (commit `7c2e491c`; schema `84dd8666`).

Settings now includes an **Agents** section (schema v4): trace/usage capture
defaults, trace page size limits, ACP executable/arguments/expected identity
(non-secret), and application-default context policy level.

## Outcome

- **Outcome:** closed
- **Fix/issue/phase:** Phase 23 F5
- **Commit or date:** `7c2e491c`
