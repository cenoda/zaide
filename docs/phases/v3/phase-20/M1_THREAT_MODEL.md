# Phase 20 M1 — Threat Model

M1 implements protocol parsing and in-memory session plumbing only. Process
execution, broker mediation, authentication, and Townhall projection remain out
of scope until later milestones.

## Assets

- Zaide-owned session/run identity and capability truth
- User workspace paths (validated only at session creation in M1)
- Protocol buffers on stdio streams (tested in-memory in M1)

## Trust boundaries

| Actor | M1 trust |
|-------|----------|
| ACP agent process | Untrusted input once M2+ launches a process; M1 treats all wire input as hostile |
| ACP wire payloads | Hostile: JSON depth, frame size, embedded newlines, malformed UTF-8 |
| Native Harness | Independent sibling; ACP code must not reference or fall back to it |
| Phase 17 broker | Not invoked in M1 |

## M1 mitigations

| Threat | Mitigation |
|--------|------------|
| Oversized stdout frames / JSON bombs | `AcpProtocolLimits.MaxFrameBytes`, `MaxJsonDepth`, prompt/update count caps |
| Embedded newline framing injection | `AcpNewlineFrameWriter` and `AcpMessageCodec.ValidateUtf8Frame` reject `\n` inside frames |
| Malformed UTF-8 | Decode failures become `AcpProtocolException` |
| Duplicate/confused JSON-RPC ids | Pending map keyed by `AcpJsonRpcRequestId.ToString()` |
| Unsupported client methods (`terminal/*`, `fs/*`, custom `_`) | `AcpInboundClientRequestRouter` returns JSON-RPC method-not-found |
| False capability advertisement | M1 profile advertises `terminal: false` and both filesystem flags `false` |
| Native Harness coupling | `Phase20ProtocolBypassTests` ratchet forbids `NativeHarness` and `Process` in ACP sources |
| Agent thought chunks as answers | `AcpPromptTurnAccumulator` never appends thought chunks to assistant text |
| Schema drift | Frozen fixture digests; no auto-update |

## Residual risk (accepted for M1)

- No production process sandboxing yet (M2)
- No broker-mediated filesystem or permission boundary yet (M4)
- No authentication UI or credential handling (M5)
- External agent direct workspace mutation remains unobservable until backend
  integration projects honest evidence levels (M3+)

## Stop conditions carried forward

Unknown protocol version, unsupported schema artifact, or requirement to bypass
`IAgentActionBroker` still fail closed per the Phase 20 plan.
