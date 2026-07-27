# Phase 19 M2 — Production Threat Model

**Milestone:** M2 — harness contracts and architecture lock  
**Lock date:** 2026-07-27  
**Status:** Complete (gates M3 tool execution; M6 adversarial tests exercise this model)  
**Scope:** Zaide Native Harness first production backend with autonomous file/command
operations through the Phase 17 control plane.

This threat model is required by V3 §15 and P19-D15. It must be accepted before M3
implementation begins.

---

## 1. Assets and trust boundaries

| Asset | Description |
|-------|-------------|
| User workspace files | Source, config, secrets on disk within captured scope |
| User credentials | Provider API keys, tokens in settings/environment |
| Conversation history | `IConversationStore` entries including prior user/assistant text |
| IDE context manifest | Phase 18 assembled snapshot (editor, terminal, SCM, diagnostics) |
| Host process privileges | Everything the Zaide process can access (network, filesystem outside workspace via commands) |
| Audit/event records | `AgentEvent`, action audit summaries, conversation projections |
| User trust | Permission decisions, capability claims, Townhall presentation |

| Trust boundary | Inside | Outside |
|----------------|--------|---------|
| B1: Model provider | Harness HTTP client with configured credentials | Remote model host |
| B2: Action broker | `ContractAgentActionBroker`, permission review, workspace scope capture | Harness turn loop (initiates requests only) |
| B3: Workspace scope | Paths resolved through `IWorkspaceActionAuthority` | Model content, command output, symlink targets |
| B4: Conversation store | Authoritative in-memory entries | Harness replay reader (read-only) |
| B5: User | Permission review UI, cancellation | Model and tool-loop autonomy |

---

## 2. Explicit exclusions (out of Phase 19 scope)

| Exclusion | Notes |
|-----------|-------|
| ACP / external agent processes | Phase 20; no external agent protocol in Phase 19 |
| Persistence, memory, resume | Phase 21; interrupted runs are terminal or indeterminate |
| Public agent API | V3 non-goal |
| Dedicated agent-requested network tools | No `FetchUrl`/`HttpRequest`/browser tools; provider transport and approved-command network access only (P19-D16) |
| Backend selection UI | Single production backend in M4 |

---

## 3. Threat register

Each row: **Assets**, **Trust boundary**, **Attack path**, **Current mitigation**,
**Residual risk**, **Future mitigation owner**.

### T-01 — Prompt injection from repository content

| Field | Detail |
|-------|--------|
| Assets | Model context, tool arguments, user trust |
| Boundary | B3 → B1 (file content into prompt) |
| Attack path | Malicious or misleading content in repo files is read via `ReadFile` tool (broker-mediated) and injected into model context; model follows hidden instructions |
| Current mitigation | Phase 17 permission review for sensitive actions; user-initiated runs; evidence-level labeling (Zaide-mediated reads); hard exclusions for some context sources (Phase 18) |
| Residual risk | **High** — no automatic instruction/data boundary in file content; model may exfiltrate or execute further tools |
| Future owner | **M6** adversarial tests; **M3** tool-result summarization limits; user permission defaults |

### T-02 — Prompt injection and secret exfiltration via prior conversation replay

| Field | Detail |
|-------|--------|
| Assets | Prior chat text, provider prompts, credentials in chat history |
| Boundary | B4 → B1 |
| Attack path | Earlier conversation entries contain attacker-controlled instructions or pasted secrets; bounded replay includes them in model context |
| Current mitigation | Replay limited to `UserChat`/`AssistantResponse`; token/entry caps; current message excluded; no automatic secret scanning of replay text |
| Residual risk | **Medium** — secrets or instructions in history can influence model behavior |
| Future owner | **M3** optional replay redaction hook; **M6** adversarial replay tests; **Phase 21** durable memory policy |

### T-03 — Prompt injection from command output and diagnostics

| Field | Detail |
|-------|--------|
| Assets | Model context, IDE manifest (diagnostics), terminal snapshots |
| Boundary | B3 → B1 |
| Attack path | `ExecuteCommand` output or IDE diagnostic text contains adversarial instructions; model treats output as authoritative |
| Current mitigation | Phase 18 redaction fail-closed for context assembly; command output bounded in tool-result summaries (M3); permission review for commands |
| Residual risk | **High** — command and diagnostic channels are attacker-influenced |
| Future owner | **M3** output truncation; **M6** injection fixtures |

### T-04 — Secret exfiltration through prompts and provider transport

| Field | Detail |
|-------|--------|
| Assets | API keys, env secrets, file contents, audit records |
| Boundary | B1 |
| Attack path | Model embeds secrets in assistant text or tool args; harness sends to provider over TLS; secrets appear in logs or audit summaries |
| Current mitigation | `AgentActionAuditStore` redacts `api_key=`/`password=`/`token=` patterns; provider TLS; settings not embedded in manifest by default |
| Residual risk | **Medium** — novel secret formats, provider-side logging, model paraphrase exfiltration |
| Future owner | **M3** transport error sanitization; **M6** exfiltration tests; **Phase 21** raw-trace policy |

### T-05 — Secret exfiltration through tool arguments and command execution

| Field | Detail |
|-------|--------|
| Assets | Workspace files, host environment |
| Boundary | B2 → B3 |
| Attack path | Model issues `ExecuteCommand` or file writes that encode secrets in outbound network calls or new files |
| Current mitigation | Phase 17 permission review; command denylist for shell interpreters; workspace-relative path enforcement; disclosed non-sandbox boundary |
| Residual risk | **High** — approved binaries can access network (Phase 17 inherited behavior) |
| Future owner | **M6** adversarial command tests; roadmap network-tool decision |

### T-06 — Workspace escape and path traversal

| Field | Detail |
|-------|--------|
| Assets | Files outside workspace, system files |
| Boundary | B3 |
| Attack path | `../` segments, absolute paths, symlink/reparse targets escape captured scope |
| Current mitigation | Phase 17 `AgentWorkspaceRelativePath`, scope capture at admission, broker path resolution, stale-scope rejection |
| Residual risk | **Low–Medium** — OS-specific symlink/reparse edge cases, TOCTOU |
| Future owner | **M6** traversal/adversarial tests; Phase 17 residual monitoring |

### T-07 — Stale workspace scope after workspace change

| Field | Detail |
|-------|--------|
| Assets | Workspace integrity |
| Boundary | B2 |
| Attack path | User changes workspace while run active; harness uses stale broker scope |
| Current mitigation | `OnWorkspaceScopeInvalidated` revokes broker; `StaleWorkspace` failure kind |
| Residual risk | **Low** — race between invalidation and in-flight action |
| Future owner | **M6** cancellation/invalidation race tests |

### T-08 — Command substitution and shell interpretation

| Field | Detail |
|-------|--------|
| Assets | Host process, user data |
| Boundary | B2 → host OS |
| Attack path | Model invokes shell (`bash -c`, `cmd /c`) to bypass argument-vector hygiene |
| Current mitigation | Phase 17 denies shell executables in command resolver; argument vector execution |
| Residual risk | **Medium** — interpreter scripts, polyglot binaries, approved tools that spawn shells |
| Future owner | **M6** residual shell tests; permission disclosure in UI |

### T-09 — Denial of service via token and context volume

| Field | Detail |
|-------|--------|
| Assets | Provider quota, host memory, run latency |
| Boundary | B1 |
| Attack path | Large files, many turns, or replayed history exhaust tokens/memory |
| Current mitigation | Turn budget (25); Phase 18 manifest token budget; replay token cap; M3 tool-result summarization |
| Residual risk | **Medium** — provider limits vary; summarization loss |
| Future owner | **M3** enforcement; **M6** budget tests |

### T-10 — Denial of service via processes and files

| Field | Detail |
|-------|--------|
| Assets | CPU, disk, process table |
| Boundary | B2 → host |
| Attack path | Model runs expensive builds, fork bombs (if allowed), or writes huge files |
| Current mitigation | Phase 17 command permission review; action budgets; process-tree cleanup (M3) |
| Residual risk | **Medium** — approved long-running commands |
| Future owner | **M3** timeouts; **M6** process cleanup tests |

### T-11 — Denial of service via event and recursion volume

| Field | Detail |
|-------|--------|
| Assets | Event stream, UI responsiveness |
| Boundary | B2 → event projection |
| Attack path | Runaway tool loop emits excessive broker events or recurses until turn budget |
| Current mitigation | Turn budget; Phase 17 non-terminal action slot; broker event normalization |
| Residual risk | **Low–Medium** — many permitted actions within budget |
| Future owner | **M6** runaway-turn tests |

### T-12 — Provider transport security and error disclosure

| Field | Detail |
|-------|--------|
| Assets | Credentials, error detail |
| Boundary | B1 |
| Attack path | MITM if TLS misconfigured; verbose provider errors leak paths/keys into UI or logs |
| Current mitigation | HTTPS default; 120s timeout; M3 sanitizes failure reasons for `AgentBackendEvent` |
| Residual risk | **Low–Medium** — misconfigured base URL, provider error verbosity |
| Future owner | **M3** transport implementation; **M6** error disclosure tests |

### T-13 — Cancellation correctness and late completion

| Field | Detail |
|-------|--------|
| Assets | User expectation, workspace integrity |
| Boundary | B2, B1 |
| Attack path | User cancels; provider or command continues; harness reports wrong terminal state |
| Current mitigation | `NativeHarnessCancellationState`; broker `Revoke()`; `Indeterminate` outcome; late-completion disposition locked in M2 |
| Residual risk | **Medium** — races between cancellation, provider stream end, and process exit |
| Future owner | **M3** implementation; **M6** cancellation adversarial tests |

### T-14 — Process-tree cleanup after command execution

| Field | Detail |
|-------|--------|
| Assets | Host processes |
| Boundary | B2 → host |
| Attack path | Child processes survive cancellation or run completion |
| Current mitigation | Phase 17 executor hygiene (inherited); M3 ties cleanup to cancellation token |
| Residual risk | **Medium** — detached grandchildren, platform-specific behavior |
| Future owner | **M3** cleanup; **M6** process-tree tests |

### T-15 — Dependency and supply-chain risk (P19-D08)

| Field | Detail |
|-------|--------|
| Assets | Build integrity, license compliance |
| Boundary | Build → production |
| Attack path | Copied harness code or new dependency introduces license conflict or vulnerable code |
| Current mitigation | M1 provenance record; M2 locks no new NuGet; any future copy requires `M1_PROVENANCE.md` entry before adoption |
| Residual risk | **Low** at M2 — increases if P19-D13 library adopted later |
| Future owner | **Plan amendment** before dependency; **M6** supply-chain review |

### T-16 — Agent-to-Agent loops and runaway delegation

| Field | Detail |
|-------|--------|
| Assets | User time, workspace |
| Boundary | Application routing |
| Attack path | Future multi-agent routing causes mutual tool invocation or deadlock |
| Current mitigation | Phase 19 does not implement Agent-to-Agent; single user→agent run admission |
| Residual risk | **Low** in Phase 19 |
| Future owner | **Phase 20+** routing design |

### T-17 — Capability overstatement

| Field | Detail |
|-------|--------|
| Assets | User trust |
| Boundary | Harness → session/UI |
| Attack path | UI shows `CurrentlyUsable=Supported` when workspace, provider, or permission facts disagree |
| Current mitigation | Six-fact `AgentCapabilityState`; `NativeHarnessCapabilityRows` truth rules; M4 snapshot updates |
| Residual risk | **Low** if M4 honors rules |
| Future owner | **M4** integration; **M6** adversarial capability tests |

### T-18 — Action broker bypass

| Field | Detail |
|-------|--------|
| Assets | Entire workspace |
| Boundary | B2 |
| Attack path | Harness reads/writes/executes without `IAgentActionBroker` |
| Current mitigation | Architecture ratchets (`Phase17BypassRatchetTests`); code review; M2 contracts require broker dispatch in M3 |
| Residual risk | **Low** if ratchets hold |
| Future owner | **M6** bypass adversarial tests |

### T-19 — IDE context leak under policy Off or redaction failure

| Field | Detail |
|-------|--------|
| Assets | Sensitive IDE state |
| Boundary | Phase 18 assembly → B1 |
| Attack path | Manifest includes excluded content or `ProcessingFailed` items not dropped |
| Current mitigation | Phase 18 hard exclusions; redaction fail-closed (`content = empty`); Off policy |
| Residual risk | **Low** — assembly bugs |
| Future owner | **M3** consumption tests; **M6** context leak tests |

---

## 4. Turn/output budgets and non-terminal actions

| Control | Value / behavior | Threats addressed |
|---------|------------------|-------------------|
| Model turn budget | 25 default | T-09, T-11 |
| Phase 18 manifest budget | Policy-level (2k/4k/8k) | T-09 |
| Prior replay budget | 4k tokens, 50 entries | T-02, T-09 |
| Tool-result summary size | M3 bounded UTF-8 summary before re-prompt | T-03, T-09 |
| Non-terminal action slot | Phase 17 `AgentActionResult.IsTerminal` | T-11 |
| Broker revocation on cancel | `Revoke()` + no new tool requests | T-13 |

---

## 5. M3 implementation obligations derived from this model

M3 must implement mitigations marked **M3** above, including:

- OpenAI tool parsing with schema validation before broker dispatch
- Provider SSE with timeout and sanitized failure reasons
- `INativeHarnessPriorConversationReader` with policy enforcement
- Cancellation-aware turn loop with late-completion → `Indeterminate` when required
- Process/command cleanup wired to cancellation token
- Tool-result summarization bounds

M3 must **not** weaken Phase 17 permission, path, or shell-denylist behavior.

---

## 6. M6 adversarial coverage map

| Threat IDs | M6 test theme |
|------------|---------------|
| T-01, T-03 | Prompt injection via file/command/diagnostic fixtures |
| T-02 | Replay of adversarial history entries |
| T-04, T-05 | Secret patterns in tool args and audit output |
| T-06, T-07 | Path traversal and stale scope |
| T-08 | Shell/interpreter residual vectors |
| T-09–T-11 | Budget and runaway-turn exhaustion |
| T-12 | Transport error disclosure |
| T-13, T-14 | Cancellation race and process cleanup |
| T-17, T-18 | Capability truthfulness and broker bypass |
| T-19 | Context Off/redaction fail-closed |

---

## 7. M1 comparative-execution limitation (retained)

M1 runners were disposable-workspace isolation only, not a production security
sandbox. That limitation is retained here and must not be cited as a Phase 19
production control.

---

## 8. Acceptance gate

This threat model is accepted for M2 when reviewed alongside `M2_ARCHITECTURE_LOCK.md`.
M3 tool execution must not begin until both documents are accepted and
`Phase19Contracts` tests pass.
