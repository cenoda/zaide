# ISSUE-008: Agent response does not appear in the chat

**Label:** BUG
**Status:** open
**Priority:** high
**Related:** agent response, conversation, chat surface

## Description

When a message is sent from a direct agent conversation, no agent response or
actionable failure appears in the chat. The initial report does not yet include
a precise backend selection, status label, or runtime trace.

Live source inspection found a higher-confidence pre-render failure path:
actor/backend bindings start empty, the production UI has no binding action,
and an unbound send is rejected before backend execution. The router returns
that rejection as an execution result, but Townhall clears the draft without
projecting the rejection into the conversation. Runtime reproduction is still
required before declaring this the sole root cause.

## Steps to Reproduce

1. Launch the application.
2. Select the agent panel.
3. Record the displayed backend binding and authentication status.
4. Send a message that should elicit a response.
5. Observe the chat surface.

**Expected behavior:** The agent's response appears in the chat as it
streams or once it is complete.
**Actual behavior:** No response or actionable failure appears in the chat.
The exact runtime state still needs to be captured.

## Debug Log

> If the fix is not obvious in 2 attempts, record everything here.

### Attempt 1
- **Hypothesis:** The send is rejected because the actor is unbound, and the
  rejection is not projected into the conversation.
- **Action:** Trace production binding callers and the Townhall →
  `AgentRouter` → `AgentExecutionCoordinator` send path.
- **Result:** `AgentActorBackendBindingStore` begins empty; no production UI
  calls `BindNativeHarness` or `BindAcpRuntime`;
  `AgentExecutionCoordinator.SendAsync` returns `Rejected` with
  `No explicit backend binding exists for this actor`; `AgentRouter` returns
  that result as a successful route; Townhall clears the draft when an
  execution result exists but does not append the rejection to the
  conversation.
- **Error / Output:** Source evidence only. Confirm in a clean-profile runtime
  with the backend status and trace captured.

### Attempt 2
- **Hypothesis:**
- **Action:**
- **Result:**
- **Error / Output:**

## Resolution

- **Root cause:** Leading hypothesis: no production backend-binding workflow,
  followed by silent presentation of the resulting unbound rejection. Runtime
  confirmation pending.
- **Fix:**
- **Commit:**
- **Closed date:**
