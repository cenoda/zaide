# Phase 15 M1a Upstream Candidates

## Status and authorization

This document is a read-only research record for Phase 15 M1a. Every upstream
item inspected here is classified as **ideas-only/read-only**. M1a authorizes
no copying, adaptation, translation, porting, vendoring, dependency, binary
installation, executable evaluation, ranking, or adoption.

Verification date: **2026-07-21**.

The repository references below were resolved from upstream Git refs and
GitHub release metadata on the verification date. Source findings use immutable
commit URLs. No upstream repository was cloned into Zaide, and no upstream
source was built or executed.

## Verified snapshot register

| Candidate | Repository / default branch | Exact inspected repository commit | Release or tag at verification | Release target and publication | Top-level license |
|-----------|-----------------------------|-----------------------------------|--------------------------------|--------------------------------|-------------------|
| Qwen Code | [`QwenLM/qwen-code`](https://github.com/QwenLM/qwen-code), `main` | [`3fb1b98a279d4c36ef05366f5e8e24517564548e`](https://github.com/QwenLM/qwen-code/commit/3fb1b98a279d4c36ef05366f5e8e24517564548e) | Latest stable GitHub release [`v0.20.0`](https://github.com/QwenLM/qwen-code/releases/tag/v0.20.0); tag ref commit [`92fda5603e84ef62a1b29bf6faf4f6a8124a2bf7`](https://github.com/QwenLM/qwen-code/commit/92fda5603e84ef62a1b29bf6faf4f6a8124a2bf7). A newer preview tag, `v0.20.1-preview.7215`, existed and was not treated as the stable release. | Release metadata targets branch `release/v0.20.0`; published `2026-07-19T07:27:26Z`. | Apache-2.0; pinned [`LICENSE`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/LICENSE). |
| OpenCode | [`anomalyco/opencode`](https://github.com/anomalyco/opencode), `dev` | [`849c2598abc7d2b40261e74b5826bc74ffc78308`](https://github.com/anomalyco/opencode/commit/849c2598abc7d2b40261e74b5826bc74ffc78308) | Latest GitHub release [`v1.18.4`](https://github.com/anomalyco/opencode/releases/tag/v1.18.4); tag ref commit [`49c69c5ed3ccf706b61b3febb43c8aaff7f8325e`](https://github.com/anomalyco/opencode/commit/49c69c5ed3ccf706b61b3febb43c8aaff7f8325e). | Release metadata separately targets commit [`4872c48c230728150e8e3406722943450ed58dcb`](https://github.com/anomalyco/opencode/commit/4872c48c230728150e8e3406722943450ed58dcb); published `2026-07-20T15:28:21Z`. The tag-ref commit and metadata target are recorded separately rather than assumed equivalent. | MIT; pinned [`LICENSE`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/LICENSE). |
| Grok Build | [`xai-org/grok-build`](https://github.com/xai-org/grok-build), `main` | Public repository commit [`a881e6703f46b01d8c7d4a5437683546df30449d`](https://github.com/xai-org/grok-build/commit/a881e6703f46b01d8c7d4a5437683546df30449d); root [`SOURCE_REV`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/SOURCE_REV) records private-monorepo commit `c5c4ce03436b4bb2cec43d3feaa27dee0109bf37`. | **No GitHub release and no Git tag existed.** | The official [open-source announcement](https://x.ai/news/grok-build-open-source) was published `2026-07-15`. The separately published [Grok Build changelog](https://x.ai/build/changelog) listed `0.2.106` dated `2026-07-18` as the latest changelog version observed; no primary evidence maps it to the public repository commit, `SOURCE_REV`, a distributed binary hash, or the hosted service. | First-party code Apache-2.0; pinned [`LICENSE`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/LICENSE). Third-party and ported material retains other licenses. |

Repository commits, release targets, tag refs, monorepo revisions, distributed
binary versions, and changelog entries are different identifiers. This record
does not manufacture equivalence between them.

## Qwen Code

### Verified architecture inventory

| Concern | Verified fact | Pinned primary source |
|---------|---------------|-----------------------|
| Task/agent loop | `GeminiClient.sendMessageStream` assembles a turn, consumes streamed `ServerGeminiStreamEvent` values, reacts to tool-call and compression events, and continues or terminates according to the returned event type. `Turn.run` converts provider stream chunks into typed model, tool-call, citation, retry, compression, and error events. | [`packages/core/src/core/client.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core/client.ts), [`packages/core/src/core/turn.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core/turn.ts) |
| Context discovery and selection | Hierarchical instruction discovery walks from the working directory toward the project root, combines configured memory filenames and imports, adds extension context files, and loads scoped `.qwen/rules/`. The client separately assembles startup, IDE, hook, date, tool, and recalled-memory context. | [`packages/core/src/utils/memoryDiscovery.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/utils/memoryDiscovery.ts), [`packages/core/src/core/client.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core/client.ts) |
| Model-response parsing | `Turn.run` consumes the content-generator stream and emits typed events. Tool calls are normalized from provider parts before scheduling; invalid or incomplete stream conditions become explicit error events rather than direct tool invocation. | [`packages/core/src/core/turn.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core/turn.ts), [`packages/core/src/core/geminiChat.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core/geminiChat.ts) |
| Tool dispatch | `CoreToolScheduler` validates calls, applies permission/confirmation flow, schedules execution, and reports tool results back into the conversation. The registry joins built-in, discovered, and MCP tools behind declarations and invocation objects. | [`packages/core/src/core/coreToolScheduler.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core/coreToolScheduler.ts), [`packages/core/src/tools/tool-registry.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/tools/tool-registry.ts) |
| Filesystem, search, edit, and command tools | The core tool set has explicit implementations for file reads, directory listing, glob/grep search, file write/edit, shell execution, and patch application. | [`packages/core/src/tools/`](https://github.com/QwenLM/qwen-code/tree/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/tools) |
| Cancellation/interruption | `AbortSignal` is threaded through client turns, stream generation, the scheduler, tool invocation, and retry waits. Parent abort is propagated to background recall work, and turn-interruption logic has focused tests. | [`packages/core/src/core/client.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core/client.ts), [`packages/core/src/core/turn-interruption.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core/turn-interruption.ts), [`packages/core/src/utils/retry.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/utils/retry.ts) |
| Failure and retry behavior | Retry classification separates aborts, fail-fast conditions, quota failures, normal bounded retry, optional persistent retry, `Retry-After`, and abort-aware backoff. Session recovery has a separate repair path and tests. | [`packages/core/src/utils/retry.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/utils/retry.ts), [`packages/core/src/core/session-recovery.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core/session-recovery.ts) |
| Compaction/context reduction | `ChatCompressionService` creates a summary-based replacement history. The client supports automatic and manual compression, restores startup context after automatic compression, and preserves tool/context state explicitly. | [`packages/core/src/services/chatCompressionService.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/services/chatCompressionService.ts), [`packages/core/src/core/client.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core/client.ts) |
| Test strategy | Adjacent Vitest unit tests cover the client, turn parser, tool scheduler, permissions, cancellation, recovery, compression, and tools. Root scripts also define workspace CI tests plus unsandboxed, Docker, Podman, interactive, SDK, and terminal-benchmark integration suites. M1a inspected definitions only and ran none. | [`package.json`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/package.json), [`packages/core/src/core/client.test.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core/client.test.ts), [`integration-tests/`](https://github.com/QwenLM/qwen-code/tree/3fb1b98a279d4c36ef05366f5e8e24517564548e/integration-tests) |

### Lineage and provenance lock

The pinned README states that Qwen Code was originally based on Google Gemini
CLI v0.8.2 and stopped syncing with upstream starting at Qwen Code v0.1.
This is verified lineage, not a file-by-file provenance map. No top-level
`NOTICE` file was present in the inspected Qwen Code snapshot. Therefore any
future adoption would have to trace the exact candidate files to Gemini CLI,
Qwen changes, retained notices, dependencies, prompts, assets, and generated
artifacts before use. The root Apache-2.0 license alone is insufficient.

Source: pinned [`README.md`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/README.md), pinned [`package-lock.json`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/package-lock.json).

### Interpretation and unknowns

- **Interpretation:** Qwen Code exposes a comparatively explicit separation
  among stream parsing, scheduling, tool registry, and tool execution. This is
  an architectural observation, not a recommendation.
- **Unknown:** M1a did not establish which current files remain derived from
  Gemini CLI v0.8.2, the exact upstream source commit for each such file, or
  whether all required upstream notices are represented.
- **Unknown:** No executable evaluation established behavior, performance,
  source-to-package equivalence, or service-side semantics.
- **Rejected:** inferring component clearance from the root Apache-2.0 file.

## OpenCode

### Verified architecture inventory

| Concern | Verified fact | Pinned primary source |
|---------|---------------|-----------------------|
| Task/agent loop | `SessionPrompt` owns the repeated session loop: it loads messages, handles pending subtask/compaction work, resolves model and tools, assembles system/context messages, invokes the processor, and continues, compacts, or stops from the processor result. | [`packages/opencode/src/session/prompt.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/prompt.ts), [`packages/opencode/src/session/processor.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/processor.ts) |
| Context discovery and selection | System prompts are model/provider dependent. Instruction discovery walks project/global locations and attaches relevant files once per assistant message. The loop combines skills, environment context, instructions, MCP instructions, and converted message history. | [`packages/opencode/src/session/system.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/system.ts), [`packages/opencode/src/session/instruction.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/instruction.ts), [`packages/opencode/src/session/prompt.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/prompt.ts) |
| Model-response parsing | `SessionProcessor` consumes the model stream into typed reasoning, text, tool-call, usage, finish, error, and retry state while settling incomplete tool calls during cleanup. `MessageV2` converts stored parts to model messages. | [`packages/opencode/src/session/processor.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/processor.ts), [`packages/opencode/src/session/message-v2.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/message-v2.ts) |
| Tool dispatch | `SessionTools.resolve` converts registered and MCP tools into model-callable handlers with session, permission, metadata, and abort context. `ToolRegistry` combines built-ins, custom project tools, plugins, tasks/subagents, and MCP visibility. | [`packages/opencode/src/session/tools.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/tools.ts), [`packages/opencode/src/tool/registry.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/tool/registry.ts) |
| Filesystem, search, edit, and command tools | Separate tool modules implement read, write, edit, apply-patch, glob, grep, shell, LSP, skill, task/subagent, web fetch/search, and truncation behavior. | [`packages/opencode/src/tool/`](https://github.com/anomalyco/opencode/tree/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/tool) |
| Cancellation/interruption | Session and task scopes use abort controllers/effect interruption. Abort signals reach model and tool execution, and cleanup records aborted unfinished tool calls. | [`packages/opencode/src/session/prompt.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/prompt.ts), [`packages/opencode/src/session/processor.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/processor.ts), [`packages/opencode/src/session/tools.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/tools.ts) |
| Failure and retry behavior | The processor maps provider failures to session errors, publishes retry state, and uses a retry schedule. Retry classification excludes context overflow, respects provider retry hints and `Retry-After`, and stops on non-retryable errors. | [`packages/opencode/src/session/processor.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/processor.ts), [`packages/opencode/src/session/retry.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/retry.ts) |
| Compaction/context reduction | Compaction selects a retained tail, creates a summary through a dedicated compaction agent with tools disabled, permits plugin context/prompt contribution, can auto-continue, and separately prunes older tool outputs. | [`packages/opencode/src/session/compaction.ts`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session/compaction.ts) |
| Test strategy | The package uses Bun tests. Focused suites cover prompt processing, messages, retry, compaction, tools, permissions, ACP, MCP, skills, plugins, CLI run/TUI behavior, provider adapters, and recorded native tool loops. M1a inspected definitions only and ran none. | [`packages/opencode/package.json`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/package.json), [`packages/opencode/test/session/`](https://github.com/anomalyco/opencode/tree/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/test/session), [`packages/opencode/test/fixtures/recordings/session/`](https://github.com/anomalyco/opencode/tree/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/test/fixtures/recordings/session) |

### Interpretation and unknowns

- **Interpretation:** OpenCode concentrates loop orchestration in the session
  layer and lets the processor, permission service, tool registry, and
  compactor own narrower concerns. This is not an adoption recommendation.
- **Unknown:** M1a did not establish the licensing or provenance of every
  dependency, provider adapter, prompt, fixture recording, asset, or generated
  artifact in the workspace.
- **Unknown:** The release metadata target and tag-ref commit differ; M1a did
  not infer build provenance beyond the published metadata.
- **Unknown:** No executable evaluation established package/source equivalence,
  runtime behavior, performance, or hosted-service semantics.
- **Rejected:** treating the root MIT license as blanket clearance.

## Grok Build

### Source identity and publication boundary

The official announcement describes the published code as the harness and TUI,
including context assembly, model-response parsing, tool dispatch, tools, and
extension systems. The pinned repository README says that the public tree is
periodically synchronized from a private SpaceXAI monorepo, and the pinned
`SOURCE_REV` records the corresponding monorepo commit. The pinned
`CONTRIBUTING.md` says that the project is developed internally and does not
accept external pull requests or unsolicited patches.

Sources: [official announcement](https://x.ai/news/grok-build-open-source),
pinned [`README.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/README.md),
pinned [`SOURCE_REV`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/SOURCE_REV),
pinned [`CONTRIBUTING.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/CONTRIBUTING.md).

Neither the announcement nor the repository proves that public commit
`a881e670...` exactly matches every separately distributed `grok` binary or
every server-side component. Source-to-binary equivalence and
client-to-service equivalence are therefore **unknown**.

### Verified architecture inventory

| Concern | Verified fact in the public source snapshot | Pinned primary source |
|---------|---------------------------------------------|-----------------------|
| Agent/session loop | `run_session` is the session actor command loop. Turn execution builds the request, performs preflight/automatic compaction, samples a model response, records it, executes returned tool calls, and either continues the loop or ends the turn. | [`run_loop.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_impl/run_loop.rs), [`turn.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_impl/turn.rs) |
| Context assembly | Prompt construction combines templated user input, working-directory/VCS state, project rules, skills, MCP server descriptions, images, reminders, and bounded/offloaded oversized prompts. Project instructions have explicit de-duplication across spawn and compaction. | [`prompt_build.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_impl/prompt_build.rs), [`prompt/context.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-agent/src/prompt/context.rs), [`prompt/agents_md.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-agent/src/prompt/agents_md.rs) |
| Model-response parsing | The sampler transforms raw Responses API and Chat Completions streams into typed sampling events, accumulates streamed tool arguments, validates terminal responses, maps output to conversation items, reports usage/stop reason, and makes malformed, empty, stalled, or failed streams explicit. | [`stream/responses.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-sampler/src/stream/responses.rs), [`stream/chat_completions.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-sampler/src/stream/chat_completions.rs), [`events.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-sampler/src/events.rs) |
| Tool dispatch | `execute_tool_calls` prepares permission-aware calls, supports concurrent dispatch with same-file edit serialization, handles plan-mode gates and cancellation, and records results. `dispatch_tool` delegates to workspace operations and reports parse errors back to the model. | [`tool_calls.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_impl/tool_calls.rs), [`tool_dispatch.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_impl/tool_dispatch.rs), [`common/xai-tool-runtime/src/dispatch.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/common/xai-tool-runtime/src/dispatch.rs) |
| Filesystem tools | The tool crate includes first-party Grok Build read/list/search-replace implementations plus separately attributed Codex and OpenCode ports. Host filesystem, VCS, checkpoint, permission, and process behavior is owned by workspace/runtime crates. | [`xai-grok-tools/src/implementations/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/implementations), [`xai-grok-workspace/src/file_system/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-workspace/src/file_system) |
| Search tools | Grok Build grep delegates through a ripgrep-backed implementation. Separate Codex and OpenCode search ports are present and retain their own provenance. Release builds can embed third-party search binaries according to build-time inputs. | [`grok_build/grep/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/implementations/grok_build/grep), [`xai-grok-tools/THIRD_PARTY_NOTICES.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/THIRD_PARTY_NOTICES.md) |
| Edit tools | Grok Build has `search_replace` and hashline edit paths; the tree also contains a Codex `apply_patch` port and OpenCode `edit`/`write` ports. These categories must not be collapsed into first-party code. | [`grok_build/search_replace/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/implementations/grok_build/search_replace), [`grok_build_hashline/edit/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/implementations/grok_build_hashline/edit), [`implementations/codex/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/implementations/codex), [`implementations/opencode/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/implementations/opencode) |
| Command tools | The Grok Build and OpenCode-derived bash modules use the local computer/terminal/process abstractions. Direct bang-command handling is surfaced through the session dispatcher. | [`grok_build/bash/mod.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/implementations/grok_build/bash/mod.rs), [`computer/local/terminal.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/computer/local/terminal.rs), [`tool_dispatch.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_impl/tool_dispatch.rs) |
| Cancellation/interruption | Session commands, sampling, tool calls, task cancellation, interjections, and background work use explicit cancellation paths. Tool-call cleanup distinguishes permission rejection, user cancellation, and follow-up interruption. | [`run_loop.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_impl/run_loop.rs), [`tool_calls.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_impl/tool_calls.rs), [`tasks_cancel.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_impl/tasks_cancel.rs) |
| Failure recovery | Sampling separates terminal failure, context-overflow compaction/resubmission, authentication refresh/resubmission, retryable stream failures, empty responses, idle timeout, and detected doom loops. Workspace recovery and MCP restart are separate owners. | [`sampler_turn.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_impl/sampler_turn.rs), [`xai-grok-sampler/src/retry.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-sampler/src/retry.rs), [`workspace/src/recovery.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-workspace/src/recovery.rs), [`session/mcp_restart.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/mcp_restart.rs) |
| Compaction | The shell supports manual, threshold-triggered, preflight, error-recovery, and two-pass compaction, with validation, preserved prefixes, memory flush, and checkpoints. The shared compaction crate separates code, inter-, intra-, and history compaction algorithms. | [`session/compaction.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/compaction.rs), [`common/xai-grok-compaction/src/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/common/xai-grok-compaction/src) |
| TUI boundary | `xai-grok-pager` identifies itself as the TUI and owns rendering/input/modals/diffs. `xai-grok-shell` owns agent, session, sampling, extensions, auth, and runtime concerns. The binary crate is the composition root. | [`xai-grok-pager/src/lib.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-pager/src/lib.rs), [`xai-grok-shell/src/lib.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/lib.rs), [`xai-grok-pager-bin/src/main.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-pager-bin/src/main.rs) |
| Headless mode | The binary dispatches separately to TUI, headless, leader, and stdio-agent entry points. Headless single-turn output and permission behavior are documented and implemented outside the interactive rendering path. | [`xai-grok-pager-bin/src/main.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-pager-bin/src/main.rs), [`xai-grok-pager/src/headless.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-pager/src/headless.rs), [`14-headless-mode.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-pager/docs/user-guide/14-headless-mode.md) |
| Skills | Skill discovery merges local, repository, user, configured, server, bundled, and plugin sources using explicit precedence and disable/ignore rules. | [`prompt/skills.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-agent/src/prompt/skills.rs), [`xai-grok-shell/skills/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/skills) |
| Plugins | A plugin bundles skills, agents, MCP configuration, and hooks; discovery, manifest, trust, registry, marketplace, installation, and refresh are separate modules. | [`xai-grok-agent/src/plugins/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-agent/src/plugins), [`xai-grok-plugin-marketplace/src/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-plugin-marketplace/src) |
| Hooks | The hook crate discovers file-defined command hooks for session and tool events. Its documented v0 policy makes pre-tool hooks blocking but other events non-blocking and failures fail-open. | [`xai-grok-hooks/src/lib.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-hooks/src/lib.rs), [`hook_dispatch.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_impl/hook_dispatch.rs) |
| MCP | The MCP crate owns local child-process and streamable-HTTP transports, credentials/OAuth, liveness, invocation, error classification, and reconnect/backoff behavior. Session code owns discovery, dispatch, managed refresh, and restart. | [`xai-grok-mcp/src/lib.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-mcp/src/lib.rs), [`session/mcp_dispatcher.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/mcp_dispatcher.rs) |
| Subagents | A coordinator receives spawn/query/cancel events, derives child context from the parent session, shares selected MCP/hook/tool snapshots, and tracks child lifecycle separately from the parent. | [`subagent_coordinator.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/agent/mvp_agent/subagent_coordinator.rs), [`agent/subagent/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/agent/subagent) |
| Sandboxing | The sandbox crate defines profile, deny-path/glob, child-network, and OS-specific enforcement seams. Its source states process-level network remains available for model access while configured child network restriction is applied on supported paths. Actual enforcement depends on platform, features, profile, and runtime configuration. | [`xai-grok-sandbox/src/lib.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-sandbox/src/lib.rs), [`profiles.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-sandbox/src/profiles.rs), [`deny/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-sandbox/src/deny) |
| ACP support | The public tree depends on the Agent Client Protocol crates, implements `acp::Agent` for its agent, owns session conversion/transport code, and exposes a stdio-agent path. This verifies client-side ACP support in source, not compatibility with every ACP client/version. | [`xai-grok-shell/src/agent/mvp_agent/acp_agent.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/agent/mvp_agent/acp_agent.rs), [`xai-acp-lib/src/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-acp-lib/src), [`Cargo.toml`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/Cargo.toml) |
| Test strategy | Unit tests are colocated throughout the crates; integration suites exist for session/ACP behavior, tools, MCP, sandbox, workspace, telemetry, parser/renderer, and PTY behavior. The repository README recommends per-crate `cargo test`; the generated workspace is large. M1a inspected tests only and ran none. | [`README.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/README.md), [`xai-grok-shell/src/session/acp_session_tests/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/session/acp_session_tests), [`xai-grok-tools/tests/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/tests), [`xai-grok-pager-pty-harness/tests/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-pager-pty-harness/tests) |

### Provenance and dependency audit

The following facts are verified for the inspected public snapshot:

- First-party source is declared Apache-2.0, with SpaceXAI copyright and the
  Apache patent and trademark clauses. Source: pinned [`LICENSE`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/LICENSE).
- Root [`THIRD-PARTY-NOTICES`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/THIRD-PARTY-NOTICES)
  covers the resolved product dependency closure, UI themes, in-tree source
  ports, multiple license families, and special bundled-library notices.
- Crate-local [`xai-grok-tools/THIRD_PARTY_NOTICES.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/THIRD_PARTY_NOTICES.md)
  records the Codex and OpenCode tool ports and build-time bundled search
  binaries. It explicitly says the ported files were translated where
  applicable, adapted to the local tool/runtime interfaces, extended, and
  modified. This was the only crate-local file named
  `THIRD_PARTY_NOTICES.md` in the inspected snapshot; the vendored Mermaid
  subtree separately contains an extensionless `THIRD_PARTY_NOTICES` file.
- [`third_party/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/third_party)
  contains vendored Mermaid rendering material under MIT and Apache-2.0
  licenses, with ancestry and local-modification notes. Root
  [`third_party/NOTICE`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/third_party/NOTICE)
  is an index, not a replacement for adjacent license files and the Mermaid
  subtree notices.
- Root [`Cargo.toml`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/Cargo.toml)
  declares itself generated and includes first-party and `third_party/`
  workspace members. Root [`Cargo.lock`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/Cargo.lock)
  is Cargo-generated and pins registry plus Git dependencies. Generated
  workspace metadata is evidence of a dependency snapshot, not license
  clearance or proof of a particular distributed binary.

Special source-port treatment:

| Published path | Declared original project/path | Declared license | Modification notice and classification | Unresolved provenance |
|----------------|--------------------------------|------------------|----------------------------------------|-----------------------|
| [`crates/codegen/xai-grok-tools/src/implementations/codex/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/implementations/codex) | `openai/codex`, `codex-rs/core/src/tools/handlers/` and the apply-patch crate | Apache-2.0, Copyright 2025 OpenAI | Published notices call `apply_patch`, `grep_files`, `list_dir`, and `read_file` **ported/derived portions**, modified, adapted to the Grok Build `Tool` trait/runtime, translated where applicable, and extended. They are not classified as unmodified copies. | The published notice does not pin the exact original Codex commit or a file-by-file source mapping. Missing mapping is a hard adoption stop. |
| [`crates/codegen/xai-grok-tools/src/implementations/opencode/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/implementations/opencode) | Declared as `sst/opencode`, `packages/opencode/src/tool/` | MIT, Copyright (c) 2025 opencode | Published notices call `bash`, `edit`, `glob`, `grep`, `read`, `skill`, `todowrite`, and `write` **ported/derived portions**, modified, adapted to local interfaces/runtime, translated where applicable, and extended. They are not classified as unmodified copies. | The published notice does not pin the exact original OpenCode commit or a file-by-file source mapping. The declared origin is preserved as written and is not silently replaced with the current candidate repository identity. Missing mapping is a hard adoption stop. |

### Runtime, service, and data-handling separation

The following table deliberately separates source architecture from runtime or
service claims. Policy text and the open-source announcement are not used as
proof of actual execution behavior.

| Topic | Verified source observation | Runtime/service conclusion |
|-------|-----------------------------|----------------------------|
| Authentication | The source contains browser/session-token, API-key, OIDC, device-code, and refresh-aware credential seams. | Which method is used, token handling by deployed services, account entitlements, and server-side auth behavior are configuration/deployment facts and remain unknown. Source: [`xai-grok-auth/src/lib.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-auth/src/lib.rs), [`02-authentication.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-pager/docs/user-guide/02-authentication.md). |
| Telemetry | The client source has distinct product-event, Mixpanel, Sentry, OpenTelemetry, session-metric, and unified-log modules plus runtime switches. | Whether a particular distribution enables them, emitted payloads after configuration, destinations, server processing, and retention are not established by static inspection. Source: [`xai-grok-telemetry/src/lib.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-telemetry/src/lib.rs), [`24-monitoring-usage.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-pager/docs/user-guide/24-monitoring-usage.md). |
| Repository/content upload | The source includes a configuration-gated per-turn trace upload subsystem for selected artifacts; `config_files.json` content upload is explicitly disabled at this commit. Prompt/tool architecture can transmit user-selected context to a configured model endpoint. | Static inspection did not prove an automatic whole-repository upload, nor did it establish the exact content sent in every mode. Activation, destination, payload, server-side use, and equivalence to shipped binaries remain unknown. Source: [`upload/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/upload), [`upload/config_files.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell/src/upload/config_files.rs). |
| Retention/privacy | User-guide and configuration surfaces discuss privacy, trace upload, external telemetry, and retention modes. | Actual provider/service retention, deletion, training use, contractual privacy, and organization-specific enforcement are external runtime/policy facts and remain unknown in this source-architecture audit. Source: [`24-monitoring-usage.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-pager/docs/user-guide/24-monitoring-usage.md). |
| Remote service behavior | Sampler code supports configurable OpenAI-compatible Responses/Chat Completions and Anthropic-style message paths; the announcement also states local-inference configuration is possible. | Hosted routing, model versions, server tools, safety filters, retry behavior beyond client evidence, and client-to-service equivalence remain unknown without primary deployment evidence. Source: [`xai-grok-sampler/src/config.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-sampler/src/config.rs), [official announcement](https://x.ai/news/grok-build-open-source). |

### Interpretation and unknowns

- **Interpretation:** The public Grok Build tree exposes a broad, layered
  client architecture: pager presentation, shell/session orchestration,
  sampling, tool/runtime/workspace ownership, extensions, and protocol seams.
  This is an architectural description only.
- **Interpretation:** Provenance is more explicit than a root-license-only
  repository because it identifies in-tree ports and vendored subtrees, but
  the missing exact original commits for the Codex and OpenCode ports prevents
  adoption-grade traceability.
- **Unknown:** public source commit to distributed binary/changelog mapping.
- **Unknown:** public source to private monorepo completeness beyond the
  recorded `SOURCE_REV` value.
- **Unknown:** client code to hosted service equivalence and all server-side
  components.
- **Unknown:** runtime privacy, repository-content transmission, retention,
  telemetry activation, authentication flow, and remote-service behavior for
  any particular user/configuration/distribution.
- **Rejected:** treating the official open-source announcement as proof of
  binary identity, service behavior, privacy behavior, or complete provenance.
- **Rejected:** treating first-party Apache-2.0 as the license for ports,
  vendored source, dependencies, bundled binaries, themes, prompts, or assets.

## Cross-candidate observations without ranking

| Concern | Qwen Code | OpenCode | Grok Build |
|---------|-----------|----------|------------|
| Loop boundary | Streaming client + turn events + scheduler | Session prompt loop + stream processor | Session actor + sampler + explicit tool-call pipeline |
| Context sources | Hierarchical QWEN/context files, rules, IDE, hooks, memory | System/provider prompts, instruction files, skills, MCP, history | Project rules, VCS/workspace, skills/plugins/hooks/MCP, images, reminders, memory/compaction |
| Tool boundary | Registry + scheduler + confirmations | Registry + session wrappers + permission service | Tool runtime/workspace dispatch + permission/plan gates |
| Reduction | Summary compression with startup-context restoration | Summary compaction, retained tail, tool-output pruning | Manual/auto/error/preflight/two-pass compaction with validation/checkpoints |
| Provenance warning | Gemini CLI v0.8.2 lineage lacks file-level mapping in this audit | Root MIT does not clear the workspace closure | Explicit ports/vendors exist, but exact source commits for declared ports are missing |
| Evaluation status | Not executed | Not executed | Not executed |

These differences are inputs to a later controlled protocol. They are not a
harness ranking, selection, or adoption decision.

## M1a authorization result

- All three candidates remain **ideas-only/read-only**.
- No source, binary, prompt, fixture, asset, dependency, schema, or generated
  artifact was adopted.
- No executable comparison was performed.
- Missing provenance remains a hard adoption stop.
- M1b remains unauthorized.
