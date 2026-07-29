# ISSUE-008: Agent response does not appear in the chat

**Label:** BUG
**Status:** open
**Priority:** high
**Related:** agent response, conversation, chat surface

## Description

When the agent is used, the agent's response does not appear in the
chat. The user has not yet captured a precise reproduction sequence,
test name, or any debug output, so the failure surface (no event, no
stream, no render, no error) is not yet known.

## Steps to Reproduce

1. Launch the application.
2. Select the agent panel.
3. Send a message that should elicit a response.
4. Observe the chat surface.

**Expected behavior:** The agent's response appears in the chat as it
streams or once it is complete.
**Actual behavior:** The agent's response does not appear in the chat.
The precise failure mode (silent drop, error toast, stuck spinner, etc.)
is not yet captured.

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
