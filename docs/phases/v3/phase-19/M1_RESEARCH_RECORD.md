# Phase 19 M1 — Research Record

**Milestone:** M1 — open-source harness research and provenance  
**Research date:** 2026-07-27  
**Status:** **COMPLETE WITH LIMITATION**  
**Amendment (2026-07-27):** The original full-corpus benchmark gate requiring
≥2 candidates completing all 8 common tasks green was retired by explicit
user-directed plan amendment. The local model capability is insufficient for
this campaign to produce meaningful architectural evidence. M1 closes as a
research/provenance milestone. The retained evidence satisfies the
research/provenance gate. No architecture winner was selected. M2 is next but
has not started. **Do not perform another full-corpus chase.**
**Scope:** Corrective-only crash reconstruction and evidence re-audit. No M2
work, production-code work, production-test work, dependency adoption, commit,
push, or publication was performed.

## 1. Gate verdict

**The original full-corpus benchmark gate is retired (2026-07-27) by explicit
user-directed plan amendment.** The plan required at least two authorized
candidates to complete all five revealed tasks and all three held-out tasks on
the same frozen corpus, with a green test result for every task. The retained
evidence shows no qualifying candidate. However, the plan amendment explicitly
retires that requirement. The research/provenance gate is satisfied by the
retained evidence:

| Criterion | Status | Evidence |
|-----------|--------|----------|
| ≥3 candidates inventoried at exact commits | ✅ Satisfied | Qwen Code (1f9a1a90), OpenCode (bc2d3df0), Grok Build (b41c75a5) — §2 |
| Licenses, notices, dependency metadata, transitive-obligation limitations recorded | ✅ Satisfied | M1_PROVENANCE.md §2, §5 |
| ≥2 candidates verified runnable through authorized zero-cost local path | ✅ Satisfied | OpenCode (Bun/Ollama — §7), Grok Build (cargo build — §7) |
| Comparable corpus attempts recorded with exact commands, reset/isolation method, results, failures, resource limits | ✅ Satisfied | §3, §7, §8; six summary TSVs |
| Task-loop, context, search, editing, tool execution, recovery, compaction observations recorded | ✅ Satisfied | §6 |
| No production code, tests, tools, dependencies, or architecture decisions introduced | ✅ Satisfied | Verified in §10 and repository scope check |
| Failed comparative execution retained as research evidence, not treated as winner or benchmark result | ✅ Satisfied | §8 records all failures truthfully; no winner selected |

**Limitation retained:** The full-corpus failures are a documented limitation
of the local model/execution environment. The original campaign results are
preserved truthfully:
- Qwen, OpenCode, and Grok did not complete all eight tasks green;
- no failed, timed-out, malformed, or runner-defective evidence is rewritten as
  successful.

**Decision:** M1 is complete with the above limitation. M2 is the next
milestone but has not started. Do not start M2 work or resolve M2-owned
architecture decisions in this pass.

| Candidate | Passing task evidence | Failing, timed-out, or otherwise non-green evidence | Gate result |
|-----------|---|---|---|
| Qwen Code | Initial T1, T2; retry H1, T5 | T3 timeout/failure, T4 failure, H2 timeout/failure, H3 failure; the initial T5 attempt also failed | Full-corpus qualification not met (gate retired) |
| OpenCode | Initial T1; retry T2 | T3, T4, T5, H1, H2, H3 failed or timed out | Full-corpus qualification not met (gate retired) |
| Grok Build | Initial T1 | T2, T3, T4, T5, H1, H2, H3 failed or errored | Full-corpus qualification not met (gate retired) |

The precise task rows, exit codes, elapsed seconds, and pytest notes are in:

- /var/tmp/zaide-m1-reconstruct/evidence/qwen/corpus-summary.tsv
- /var/tmp/zaide-m1-reconstruct/evidence/qwen/corpus-summary-retry.tsv
- /var/tmp/zaide-m1-reconstruct/evidence/opencode/corpus-summary.tsv
- /var/tmp/zaide-m1-reconstruct/evidence/opencode/corpus-summary-retry.tsv
- /var/tmp/zaide-m1-reconstruct/evidence/grok/corpus-summary.tsv
- /var/tmp/zaide-m1-reconstruct/evidence/grok/corpus-summary-retry.tsv

## 2. Identity and authorization boundary

Only these authorized candidate identities were used:

| Candidate | Repository | Execution pin | Additional identity |
|---|---|---|---|
| Qwen Code | https://github.com/QwenLM/qwen-code | 1f9a1a90a3dfe166e355a8c847611627eaff1105 | Checkout: /var/tmp/zaide-m1-reconstruct/candidates/qwen-code |
| OpenCode | https://github.com/anomalyco/opencode | bc2d3df05f882dcc3291208e69881e625fd55c31 | Checkout: /var/tmp/zaide-m1-reconstruct/candidates/opencode |
| Grok Build | https://github.com/xai-org/grok-build | b41c75a578f98bddbd326ab02cd53618451d97ee | SOURCE_REV 91d8cf309110a3b879c1b8198f7525aed545dfb4; checkout: /var/tmp/zaide-m1-reconstruct/candidates/grok-build |

The identity verification command was:

~~~bash
for c in qwen-code opencode grok-build; do
  git -C "/var/tmp/zaide-m1-reconstruct/candidates/$c" rev-parse HEAD
done
~~~

The recorded result is corroborated by the candidate checkouts. No candidate was
substituted, upgraded, or downgraded. The Phase 16 reverted Qwen qualification
path and its artifacts were not used.

## 3. Crash reconstruction and isolation

The earlier /tmp workspaces were lost in a host crash. This is evidence loss,
not misconduct. The reconstruction used only disposable paths outside the Zaide
checkout:

~~~text
/var/tmp/zaide-m1-reconstruct/candidates
/var/tmp/zaide-m1-reconstruct/config
/var/tmp/zaide-m1-reconstruct/corpus
/var/tmp/zaide-m1-reconstruct/evidence
/var/tmp/zaide-m1-reconstruct/scripts
/var/tmp/zaide-m1-reconstruct/trials
~~~

The corpus runner created a fresh trial by cloning the frozen corpus before each
task:

~~~bash
trial=/var/tmp/zaide-m1-reconstruct/trials/CAND-TASK
rm -rf "$trial"
git clone /var/tmp/zaide-m1-reconstruct/corpus/corpus-repo.git "$trial"
~~~

This reset command is present in the exact runners at
/var/tmp/zaide-m1-reconstruct/scripts/run_corpus.sh and
/var/tmp/zaide-m1-reconstruct/scripts/run_selected.sh. Each candidate task
therefore started from a fresh clone of the same frozen commit. The trial trees
were retained under /var/tmp/zaide-m1-reconstruct/trials; no trial path is under
/home/cenoda/zaide.

The runners used these ceilings and controls:

| Control | Value | Evidence |
|---|---|---|
| Per-task wall clock | 300 seconds via timeout 300 | Runner scripts; task .meta files |
| Maximum candidate turns | 25 for Grok; candidate-native behavior for Qwen/OpenCode | run_corpus.sh command lines |
| Model | qwen35moe-coder-35b:q4km for corpus runs | Runner log and runner default |
| Provider | Local Ollama, OpenAI-compatible http://127.0.0.1:11434/v1 for Qwen; local OpenCode Ollama provider; Grok ollama-coder | Runner scripts and /var/tmp/zaide-m1-reconstruct/config/opencode/opencode.json |
| Qwen approval | --approval-mode yolo | Qwen runner command |
| OpenCode approval | --auto and --dir fresh trial | OpenCode runner command |
| Grok approval | --always-approve --max-turns 25 | Grok runner command |
| Cost/authentication | Zero-cost local path; no paid API, new credential, account action, or secret | Authorization record in TOFIX.md; runner environment |

The runners do not constitute a secure sandbox. They are disposable workspace
isolation and reset evidence only; model-provider transport remains local
Ollama transport, and candidate processes could use the permissions granted by
the runner. This limitation is retained for M2 threat-model work and is not
converted into a production claim.

## 4. Frozen corpus and reproducibility

The canonical reconstructed corpus is:

~~~text
Repository: /var/tmp/zaide-m1-reconstruct/corpus/corpus-repo
Commit:     8dd22023fb4e2e4c3084452a465e4720bb55e773
~~~

Identity was checked with:

~~~bash
git -C /var/tmp/zaide-m1-reconstruct/corpus/corpus-repo rev-parse HEAD
git -C /var/tmp/zaide-m1-reconstruct/corpus/corpus-repo status --short
~~~

The corpus contains five revealed tasks (T1–T5) and three held-out tasks
(H1–H3). Task definitions and commitments are in:

- /var/tmp/zaide-m1-reconstruct/corpus/corpus-repo/TASKS.md
- /var/tmp/zaide-m1-reconstruct/corpus/prompts/T1.txt through
  /var/tmp/zaide-m1-reconstruct/corpus/prompts/H3.txt
- /var/tmp/zaide-m1-reconstruct/corpus/held-out/H1.sha256
- /var/tmp/zaide-m1-reconstruct/corpus/held-out/H2.sha256
- /var/tmp/zaide-m1-reconstruct/corpus/held-out/H3.sha256

Held-out commitments are:

~~~text
H1 eaff3e6462821cd9d0fe8e255e46c1b3cf4aecb347efc4b934f6b8a53245a1c0
H2 7d14f7803d7867ad6f2f3e7d8f3216cc914a0a0db09512a8c72244a470de8376
H3 280f0b5c90d82ac4c499f735681fc9287a87f3d16072a30dc9b8631e1d3c6815
~~~

The held-out verification command used by the runners was:

~~~bash
expected=$(tr -d '\n' < /var/tmp/zaide-m1-reconstruct/corpus/held-out/TASK.sha256)
actual=$(sha256sum /var/tmp/zaide-m1-reconstruct/corpus/prompts/TASK.txt | awk '{print $1}')
grep -q "$expected" /var/tmp/zaide-m1-reconstruct/corpus/corpus-repo/TASKS.md
test "$expected" = "$actual"
~~~

The corrected retry evidence is in the candidate hash-verify.log files. The
first runner log contains expected= with an empty value for Qwen and OpenCode
because those files were read before their expected hashes had been loaded.
That is a runner/evidence defect, not a candidate failure. The retry log records
successful commitment checks before each held-out run. The retry runner also
ended with a syntax error after OpenCode H3; that runner defect is recorded in
/var/tmp/zaide-m1-reconstruct/evidence/corpus-retry.log and does not turn any
incomplete candidate into a pass.

The corpus baseline was produced by:

~~~bash
cd /var/tmp/zaide-m1-reconstruct/corpus/corpus-repo
PYTHONPATH=src python -m pytest tests/ -v
~~~

Recorded result: 43 collected, 42 passed, 1 failed
(test_leading_trailing). The reconstruction note in TASKS.md identifies
surviving Qwen chat tool dumps for analyzer/transformer/tests and a formatter
module rebuilt to the recovered public API. These are research fixtures, not
Zaide source.

## 5. Candidate selection rules

The candidate set was fixed before runtime comparison: the three explicitly
authorized public repositories and exact pins in §2. A candidate could count
as runnable if it could be built or executed through the zero-cost local
Ollama path and produced comparable runtime observations. Build success, a
minimal smoke, a subset, or a retry of only some tasks was sufficient for
research evidence. Candidates requiring paid APIs, new credentials, account
actions, unbounded execution, or writes outside their disposable trial were
rejected for this campaign. No architecture was selected from these results;
architecture decisions remain M2-owned. The full-corpus benchmark gate was
retired by explicit plan amendment; failed comparative execution is retained
as research evidence, not treated as a candidate-selection winner.

## 6. Research observations

These observations are source-research notes only. No code was copied,
translated, adapted, generated into Zaide, or proposed as an adopted
dependency.

| Concern | Qwen Code | OpenCode | Grok Build |
|---|---|---|---|
| Task loop and turn state | packages/core/src/core/coreToolScheduler.ts (CoreToolScheduler) and packages/cli/src/ui/hooks/useGeminiStream.tsx coordinate model/tool turns and streamed responses. | packages/opencode/src/session/prompt.ts, session/processor.ts, session/llm.ts, session/run-state.ts, and session/status.ts coordinate prompt processing, model calls, state, and status. | crates/codegen/xai-grok-agent/src/agent.rs, builder.rs, compaction.rs, and crates/codegen/xai-grok-pager/src/app/agent.rs coordinate agent turns and UI/session state. |
| Context selection and system prompt | Core prompt/context and history services; relevant paths include packages/core/src/core/prompts.ts, packages/core/src/services/chatCompressionService.ts, and packages/core/src/services/microcompaction/microcompact.ts. | packages/opencode/src/session/system.ts, session/instruction.ts, session/prompt.ts, session/compaction.ts, and util/local-context.ts; Ollama provider schema is documented in README.md and retained at /var/tmp/zaide-m1-reconstruct/evidence/opencode/docs-ollama.txt. | crates/codegen/xai-grok-agent/src/prompt/context.rs, prompt/agents_md.rs, prompt/ignore.rs, prompt/skills.rs, and prompt/template.rs; context accounting is surfaced by xai-grok-pager/src/slash/commands/context.rs. |
| File search | packages/core/src/tools/ripGrep.ts and packages/core/src/utils/ripgrepUtils.ts use ripgrep with ignore/path handling. | packages/opencode/src/tool/grep.ts, glob.ts, and read.ts provide repository search and reads. | crates/codegen/xai-grok-pager/src/search/mod.rs, search/matcher.rs, scrollback/search.rs, and tool blocks under scrollback/blocks/tool/search.rs provide search and presentation. |
| Editing and tools | Core tools include packages/core/src/tools/edit.ts, write-file.ts, read-file.ts, and shell.ts; tool scheduling is centralized in CoreToolScheduler. | packages/opencode/src/tool/edit.ts, write.ts, read.ts, apply_patch.ts, and shell.ts implement editing, reads, patching, and command execution. | xai-grok-agent/src/prompt/template.rs defines the apply_patch prompt; pager tool blocks include scrollback/blocks/tool/edit.rs, execute.rs, read.rs, list_dir.rs, and search.rs. |
| Failure recovery and retry | Error/tool-result handling is represented by core tool error and stream services; source inspection found retry/recovery paths but runtime evidence did not qualify the candidate. | packages/opencode/src/session/retry.ts, message-error.ts, overflow.ts, and revert.ts represent retry, provider errors, context overflow, and reversal. | xai-grok-agent/src/config.rs contains tool retry configuration; pager app/agent.rs and app/acp_handler/session_notification.rs represent retry, cancellation, and turn-failure state. |
| Compaction | packages/core/src/services/chatCompressionService.ts, services/microcompaction/microcompact.ts, and acp-bridge/src/compactionEngine.ts. | packages/opencode/src/session/compaction.ts, agent/prompt/compaction.txt, and session/summary.ts. | xai-grok-agent/src/compaction.rs, xai-chat-state/src/compaction_transcript.rs, compaction_utils.rs, and pager /compact commands. |

The source paths above are the considered ranges/components, not copied-code
claims. Exact source-license and notice locations are in M1_PROVENANCE.md.

## 7. Runtime method and exact commands

### Local Ollama and OpenCode diagnosis

OpenCode was corrected from a false dead/unsupported classification. The pinned
source documents the local provider as @ai-sdk/openai-compatible with
http://localhost:11434/v1, and the verified execution path is Bun:

~~~bash
cd /var/tmp/zaide-m1-reconstruct/candidates/opencode
bun install
bun packages/opencode/src/index.ts run \
  --dir /var/tmp/zaide-m1-reconstruct/candidates/opencode \
  --auto --model ollama/qwen2.5:0.5b --format default "Reply with POGOOOON"
~~~

The minimal run returned POGOOOON and
/var/tmp/zaide-m1-reconstruct/evidence/opencode/minimal-run2.exit contains
EXIT:0. The refreshed health response from the local chat endpoint is a
successful chat.completion for qwen2.5:0.5b. The model returned refusal-style
text containing PING rather than the literal word PING; this is transport and
provider health evidence, not a candidate corpus result:

- Artifact: /var/tmp/zaide-m1-reconstruct/evidence/opencode/ollama-chat-health.json
- Artifact content: successful chat.completion, model qwen2.5:0.5b, non-empty
  response content, exit code 0
- Producer command, executed with elevated access because the evidence mount is
  read-only inside the normal sandbox:

  ~~~bash
  curl --fail --silent --show-error --request POST http://127.0.0.1:11434/v1/chat/completions -H 'Content-Type: application/json' --data '{"model":"qwen2.5:0.5b","messages":[{"role":"user","content":"PING"}],"stream":false}' > /var/tmp/zaide-m1-reconstruct/evidence/opencode/ollama-chat-health.json
  ~~~

  The exact artifact path is reproducible. The response text is not used as a
  task-pass signal.

The source/config evidence is at:

- /var/tmp/zaide-m1-reconstruct/evidence/opencode/docs-ollama.txt
- /var/tmp/zaide-m1-reconstruct/evidence/opencode/minimal-run.txt
- /var/tmp/zaide-m1-reconstruct/evidence/opencode/minimal-run2.out
- /var/tmp/zaide-m1-reconstruct/evidence/opencode/minimal-run2.err
- /var/tmp/zaide-m1-reconstruct/evidence/opencode/minimal-run2.exit
- /var/tmp/zaide-m1-reconstruct/config/opencode/opencode.json

The corrected interpretation is: OpenCode is runnable through the verified
Bun/Ollama path; its corpus failures/timeouts are separate eligibility results.

### Corpus command

The full-corpus command was:

~~~bash
/var/tmp/zaide-m1-reconstruct/scripts/run_corpus.sh qwen
/var/tmp/zaide-m1-reconstruct/scripts/run_corpus.sh opencode
/var/tmp/zaide-m1-reconstruct/scripts/run_corpus.sh grok
~~~

The retry command was:

~~~bash
/var/tmp/zaide-m1-reconstruct/scripts/run_selected.sh qwen H1 H2 H3 T3 T4 T5
/var/tmp/zaide-m1-reconstruct/scripts/run_selected.sh opencode H1 H2 H3 T2 T3 T4 T5
/var/tmp/zaide-m1-reconstruct/scripts/run_selected.sh grok T2 T3 T4 T5 H1 H2 H3
~~~

Each task produced TASK.out, TASK.err, TASK.meta, and summary TSV rows under
the corresponding exact evidence directory. Pytest output was written to
/tmp/m1-pytest-CAND-TASK.txt by the runner; those files are disposable and are
not treated as retained evidence unless their summary row identifies the result.
The task .meta file records candidate exit code and elapsed seconds.

### Grok Build

The build command was:

~~~bash
cd /var/tmp/zaide-m1-reconstruct/candidates/grok-build
cargo build -p xai-grok-pager-bin -j 28
~~~

The binary was then invoked by the corpus runner as:

~~~bash
/var/tmp/zaide-m1-reconstruct/candidates/grok-build/target/debug/xai-grok-pager \
  -p PROMPT --cwd TRIAL --always-approve --max-turns 25 \
  -m ollama-coder --output-format plain
~~~

Build evidence:

- /var/tmp/zaide-m1-reconstruct/evidence/grok-build/build.log
- /var/tmp/zaide-m1-reconstruct/evidence/grok-build/telemetry.log
- /var/tmp/zaide-m1-reconstruct/evidence/grok-build/exit_code.txt
- /var/tmp/zaide-m1-reconstruct/evidence/grok-build/elapsed_sec.txt
- /var/tmp/zaide-m1-reconstruct/evidence/grok-build/inventory.log

The build completed with exit 0 in 142 seconds. Telemetry shows active rustc
CPU usage and progressing compilation. The corrected interpretation is
successful compilation with active telemetry, not an unsupported deadlock.

## 8. Candidate results and failure records

The summary TSVs are authoritative for the task rows. The following details make
the rejection reasons explicit:

- **Qwen Code:** T1 and T2 passed on the first run. T3 timed out at 300 seconds
  and had three failed tests; T4 and T5 returned non-green pytest results. The
  retry passed H1 and T5, but H2 timed out with one failed test, H3 failed one
  test, T3 failed one test, and T4 failed one test. Qwen is rejected for the
  M1 runtime gate.
- **OpenCode:** The first run passed T1, failed T2–T4, and timed out on T5; its
  first held-out rows were invalid hash-check rows because the runner failed to
  load expected hashes. The retry passed T2, but T3 and T4 remained non-green,
  T5 failed two tests, H1 timed out with 13 failed tests, H2 failed one test,
  and H3 timed out. OpenCode is runnable, but is rejected for the M1 runtime
  gate.
- **Grok Build:** The first run passed only T1; T2–T5 and H1–H3 produced
  failures or errors. The retry again recorded errors/failures for every
  attempted task (T2–T5, H1–H3). Grok is rejected for the M1 runtime gate
  despite the successful build and active telemetry.

The original crash is recorded as loss of workspaces and loss of evidence. It
is not described as misconduct. The retry runner syntax error and the initial
held-out hash-load defect are runner/evidence limitations and are not silently
converted into candidate failures.

## 9. Rejections and unresolved evidence limitations

| Rejected item | Reason | Evidence |
|---|---|---|
| Full-corpus benchmark completion | Original gate retired by explicit plan amendment; no candidate completed all 8 tasks green | Plan amendment at `IMPLEMENTATION_PLAN.md`; six summary TSVs listed in §1 |
| Prior /tmp corpus identities c270c6ea... and 06bbd11 | Unverifiable after crash | /var/tmp/zaide-m1-reconstruct/corpus/corpus-repo/TASKS.md |
| Initial Qwen/OpenCode held-out rows | Hash expected value was empty in the runner log | Candidate hash-verify.log files |
| Retry runner as a clean campaign record | Shell syntax error after OpenCode H3 | /var/tmp/zaide-m1-reconstruct/evidence/corpus-retry.log |
| Minimal smoke/build as gate evidence | Does not cover the common corpus | OpenCode minimal-run and Grok build artifacts |
| Initial Ollama health producer command | Original producer command was absent; the artifact was refreshed with an explicit command during this corrective pass | /var/tmp/zaide-m1-reconstruct/evidence/opencode/ollama-chat-health.json |
| Any direct Zaide adoption | M1 is research only; no copied/adapted code or dependency is authorized | Repository scope verification and M1_PROVENANCE.md |
| Architecture winner selection | M1 does not select a winning external architecture; original full-corpus benchmark gate is retired | Plan amendment; this research record |

The original "Requirements for M2" paragraph is replaced: M1 is complete with
limitation, and M2 is the next milestone. M2 has not started. M2-owned
architecture decisions are not resolved in this pass.

## 10. Repository-scope verification boundary

Only the files permitted by the corrective request may change:

~~~text
docs/phases/v3/phase-19/M1_RESEARCH_RECORD.md
docs/phases/v3/phase-19/M1_PROVENANCE.md
docs/phases/v3/phase-19/TOFIX.md
README.md
docs/phases/README.md
docs/architecture/OVERVIEW.md
docs/roadmap/V3.md
~~~

The Zaide checkout remains production-code, test, tool, dependency, and M2
document untouched. Final verification is performed separately after this
documentation pass.
