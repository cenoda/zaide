# ISSUE-009: Chat shows `m6g-townhall-di-singleton-sync` text on agent panel selection

**Label:** BUG
**Status:** open
**Priority:** high
**Related:** agent panel, townhall, chat surface, placeholder text, DI registration key

## Description

When the user selects the agent panel, the chat box shows the text
`m6g-townhall-di-singleton-sync` with no apparent reason. The string
looks like an internal identifier or DI registration key that is
leaking into the user-facing chat surface as if it were content or a
placeholder.

## Steps to Reproduce

1. Launch the application.
2. Select the agent panel.
3. Inspect the chat box.

**Expected behavior:** The chat box should be empty, show a normal
placeholder, or show the previous conversation — not a DI identifier.
**Actual behavior:** The chat box shows `m6g-townhall-di-singleton-sync`.

## Debug Log

> If the fix is not obvious in 2 attempts, record everything here.

### Attempt 1
- **Hypothesis:**
- **Action:**
- **Result:**
- **Error / Output:**

### Attempt 2
- **Hypothesis:**
- **Action:**
- **Result:**
- **Error / Output:**

## Resolution

- **Root cause:**
- **Fix:**
- **Commit:**
- **Closed date:**
