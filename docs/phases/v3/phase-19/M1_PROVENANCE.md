# Phase 19 M1 — Provenance Record

**Milestone:** M1 — open-source harness research and provenance  
**Research date:** 2026-07-27  
**Status:** Research/provenance complete with limitation (the full-corpus
benchmark gate was retired by explicit user-directed plan amendment on
2026-07-27; M1 closes as a research/provenance milestone).  
**Reuse decision:** No candidate source, dependency, binary, prompt, fixture, or
generated/adapted artifact was copied into Zaide.

This record covers every material considered during the corrective pass, not only
the three top-level licenses. It keeps repository source, release identity,
SOURCE_REV, binary, dependency, vendored source, prompt, fixture, and generated
artifact identities separate.

## 1. Verification method and evidence boundary

For each candidate, the checkout identity was verified with:

~~~bash
git -C /var/tmp/zaide-m1-reconstruct/candidates/CAND rev-parse HEAD
git -C /var/tmp/zaide-m1-reconstruct/candidates/CAND status --short
git -C /var/tmp/zaide-m1-reconstruct/candidates/CAND ls-files
~~~

The exact source paths and legal files below are paths inside those pinned
checkouts. Runtime/build evidence is retained under
/var/tmp/zaide-m1-reconstruct/evidence. The M1 research record identifies the
command that produced each runtime artifact where the command was retained. If
the producer command was not retained, this record says so explicitly; no
command is invented.

No source file was copied into /home/cenoda/zaide/src, /home/cenoda/zaide/tests,
or /home/cenoda/zaide/tools. No Zaide production component was assigned as an
adopter. Any future reuse requires a new P19-D08 review before copying.

## 2. Candidate identity, license, notice, and modification matrix

| Candidate | Exact identity | First-party license and location | NOTICE/copyright locations | Dependency metadata and transitive obligations | Local modifications in research checkout | Verification evidence and reuse decision |
|---|---|---|---|---|---|---|
| Qwen Code | https://github.com/QwenLM/qwen-code at 1f9a1a90a3dfe166e355a8c847611627eaff1105; checkout /var/tmp/zaide-m1-reconstruct/candidates/qwen-code | Apache-2.0 at /var/tmp/zaide-m1-reconstruct/candidates/qwen-code/LICENSE | No root NOTICE is tracked by git at this pin. Additional legal material includes packages/core/vendor/ripgrep/COPYING, packages/desktop/NOTICE, packages/desktop/LICENSE, packages/mobile-mcp/LICENSE, packages/sdk-java/qwencode/LICENSE, packages/vscode-ide-companion/LICENSE, and packages/zed-extension/LICENSE. Copyright/license obligations in the Apache license require preserving the license, notices, and modified-file notices if reused; vendored ripgrep has separate obligations. | package.json and package-lock.json are the npm dependency metadata. Workspace package manifests are the package.json files under packages/. package-lock.json is the transitive dependency resolution. No complete machine-generated license report was retained; therefore transitive obligations are not cleared for reuse. | The pinned checkout was not clean after the Qwen install: package.json gained an allowScripts block for three esbuild versions and package-lock.json lost peer fields. These are disposable install mutations, not upstream identity, and are excluded from reuse. | HEAD verification above; /var/tmp/zaide-m1-reconstruct/evidence/qwen-install.log, qwen-npm-install2.log, qwen-cli-build.log, qwen-bundle.log, and qwen/hash-verify.log. Source observation paths are listed in §3. No reuse. |
| OpenCode | https://github.com/anomalyco/opencode at bc2d3df05f882dcc3291208e69881e625fd55c31; checkout /var/tmp/zaide-m1-reconstruct/candidates/opencode | MIT at /var/tmp/zaide-m1-reconstruct/candidates/opencode/LICENSE | No root NOTICE is tracked at this pin. Package-local license files are packages/docs/LICENSE, packages/http-recorder/LICENSE, and packages/ui/LICENSE. The MIT copyright and permission notice must be retained for reused source. | package.json and bun.lock are the primary dependency metadata; workspace package.json files under packages/ describe package-level dependencies. bun.lock records resolved transitive packages. No complete machine-generated license report was retained; transitive obligations therefore remain unverified for reuse. | git status was clean after install at the time of identity inspection. The disposable install produced node_modules and lockfile resolution outside Zaide; no source modification was considered. | HEAD verification; /var/tmp/zaide-m1-reconstruct/evidence/opencode-install.log; /var/tmp/zaide-m1-reconstruct/evidence/opencode/docs-ollama.txt; minimal-run.txt, minimal-run2.out, minimal-run2.err, and minimal-run2.exit; corpus summaries. OpenCode is runnable through Bun/Ollama, but no code reuse. |
| Grok Build | https://github.com/xai-org/grok-build at b41c75a578f98bddbd326ab02cd53618451d97ee; SOURCE_REV 91d8cf309110a3b879c1b8198f7525aed545dfb4; checkout /var/tmp/zaide-m1-reconstruct/candidates/grok-build | Apache-2.0 at root LICENSE; README.md §License identifies first-party Apache licensing. | Root THIRD-PARTY-NOTICES; third_party/NOTICE; third_party/mermaid-to-svg/THIRD_PARTY_NOTICES; third_party/mermaid-to-svg/LICENSE; third_party/dagre_rust/LICENCE; third_party/graphlib_rust/LICENCE; third_party/ordered_hashmap/LICENCE; crates/codegen/xai-grok-tools/THIRD_PARTY_NOTICES.md; crates/codegen/xai-ratatui-inline/NOTICE; crates/codegen/xai-ratatui-textarea/NOTICE; crates/codegen/xai-grok-mermaid/assets/Roboto-LICENSE.txt; and target dependency COPYING where present. xai-grok-tools explicitly records ported OpenAI Codex and SST OpenCode components, modifications, copyrights, and bundled ripgrep/PCRE2 obligations. | Cargo.toml, Cargo.lock, workspace crate Cargo.toml files, SOURCE_REV, vendored third_party Cargo.toml files, and the legal notice matrix are the dependency/provenance metadata. THIRD-PARTY-NOTICES records transitive license elections and obligations including MIT, Apache-2.0, BSD, ISC, Zlib, Unicode, BSL, MPL, CDLA, Unicode-DFS, and libgit2 COPYING/GPL-linking-exception material. Any reuse would require carrying the applicable notices and ported-source obligations. | The pinned checkout was clean at identity inspection. cargo build created target/ build artifacts and the debug binary; these are generated build outputs, not upstream source changes. | HEAD/SOURCE_REV in /var/tmp/zaide-m1-reconstruct/evidence/grok-build/inventory.log; build.log, telemetry.log, elapsed_sec.txt, and exit_code.txt; corpus summaries. Build succeeded in 142 seconds with active telemetry, but corpus gate failed. No reuse. |

The top-level candidate labels are not sufficient provenance by themselves. The
notice and dependency rows above are intentionally conservative: absent a
retained complete transitive license report, the candidate is research-only and
not cleared for adoption.

## 3. Source paths and ranges/components considered

The following paths were inspected as research components. They are not copied
ranges and do not imply an adoption decision.

### Qwen Code

- Task loop/tool scheduling: packages/core/src/core/coreToolScheduler.ts;
  packages/cli/src/ui/hooks/useGeminiStream.ts.
- Prompt/context/history/compaction: packages/core/src/core/prompts.ts;
  packages/core/src/services/chatCompressionService.ts;
  packages/core/src/services/microcompaction/microcompact.ts;
  packages/core/src/services/postCompactAttachments.ts;
  packages/acp-bridge/src/compactionEngine.ts.
- Search: packages/core/src/tools/ripGrep.ts;
  packages/core/src/utils/ripgrepUtils.ts.
- File/tool operations: packages/core/src/tools/edit.ts,
  write-file.ts, read-file.ts, shell.ts, and tool-error.ts.
- Recovery/recording: packages/core/src/services/toolUseSummary.ts,
  packages/core/src/services/chatCompressionService.ts, and the CLI stream
  runtime under packages/cli/src/ui/hooks/useGeminiStream.ts.
- Relevant test/integration observations: packages/core/src/tools/ripGrep.test.ts,
  packages/core/src/tools/write-file.test.ts, and
  packages/core/src/services/chatCompressionService.test.ts.

### OpenCode

- Task loop/session state: packages/opencode/src/session/prompt.ts,
  processor.ts, llm.ts, run-state.ts, status.ts, and session.ts.
- Prompt/context/compaction: packages/opencode/src/session/system.ts,
  instruction.ts, compaction.ts, summary.ts, and util/local-context.ts;
  prompt templates under packages/opencode/src/session/prompt/.
- Search/read: packages/opencode/src/tool/grep.ts, glob.ts, and read.ts.
- Editing/command tools: packages/opencode/src/tool/edit.ts, write.ts,
  apply_patch.ts, read.ts, and shell.ts.
- Recovery/error/overflow/revert: packages/opencode/src/session/retry.ts,
  message-error.ts, overflow.ts, and revert.ts.
- Tool registry and loop integration: packages/opencode/src/tool/registry.ts,
  tool.ts, and packages/opencode/src/cli/cmd/run/tool.ts.

### Grok Build

- Task loop/agent state: crates/codegen/xai-grok-agent/src/agent.rs,
  builder.rs, compaction.rs, discovery.rs, and
  crates/codegen/xai-grok-pager/src/app/agent.rs.
- Prompt/context: crates/codegen/xai-grok-agent/src/prompt/context.rs,
  agents_md.rs, ignore.rs, skills.rs, template.rs, and user_message.rs.
- Search/read/edit/execute: crates/codegen/xai-grok-pager/src/search/mod.rs,
  search/matcher.rs, scrollback/blocks/tool/search.rs,
  scrollback/blocks/tool/read.rs, scrollback/blocks/tool/edit.rs,
  scrollback/blocks/tool/execute.rs, and scrollback/blocks/tool/list_dir.rs.
- Recovery/retry/compaction: crates/codegen/xai-grok-agent/src/config.rs,
  compaction.rs; crates/codegen/xai-grok-pager/src/app/acp_handler/
  session_notification.rs, app/agent.rs, and slash/commands/compact.rs.
- Ported/adapted source boundary: crates/codegen/xai-grok-tools/
  THIRD_PARTY_NOTICES.md, src/implementations/codex/,
  src/implementations/opencode/, and the build.rs/bundling paths described by
  that notice. These were explicitly not adopted.

## 4. Corpus, fixture, prompt, and generated-artifact provenance

| Material | Exact path/identity | Origin and modifications | License/notice obligation | Verification and reuse decision |
|---|---|---|---|---|
| Frozen corpus repository | /var/tmp/zaide-m1-reconstruct/corpus/corpus-repo at 8dd22023fb4e2e4c3084452a465e4720bb55e773 | Reconstructed synthetic textutils repository. TASKS.md says the earlier corpus was lost in the crash; analyzer/transformer/tests and __init__ were recovered from surviving Qwen chat tool dumps, and formatter was rebuilt to the recovered public API. | No third-party dependency is declared in pyproject.toml; dev optional dependency is pytest>=7.0. README calls it a synthetic research artifact. No Zaide production license/notice is implicated. | git rev-parse/status and the 43-test baseline at /var/tmp/zaide-m1-reconstruct/evidence/corpus-identity.txt and corpus-baseline.txt. Research fixture only; not reused. |
| Revealed prompts | /var/tmp/zaide-m1-reconstruct/corpus/prompts/T1.txt through T5.txt; mirrored in corpus-repo/TASKS.md | Campaign prompts authored for this M1 reconstruction; no upstream candidate text was copied. | Research prompts have no external license obligation identified. | Exact files are supplied to each runner after fresh clone; task rows are in candidate summary TSVs. Not production assets. |
| Held-out prompts | /var/tmp/zaide-m1-reconstruct/corpus/prompts/H1.txt through H3.txt; commitments in corpus/held-out/H1.sha256 through H3.sha256 | Campaign prompts authored/sealed for M1; plaintext is outside the corpus repository until execution. | Research prompts have no external license obligation identified. | sha256sum plus TASKS.md commitment check in run_corpus.sh/run_selected.sh; corrected hash logs under each candidate evidence directory. Not production assets. |
| Recovered Qwen chat dumps | /var/tmp/zaide-m1-reconstruct/evidence/recovered-from-qwen-chats/ | Surviving tool dumps used only to reconstruct the synthetic fixture. The recovery process is documented in corpus-repo/TASKS.md. | Their presence does not grant a code-reuse license. They remain outside Zaide. | Files are retained as evidence of reconstruction, not as source candidates. No content copied to Zaide. |
| Candidate runtime outputs | /var/tmp/zaide-m1-reconstruct/evidence/qwen/, opencode/, grok/ | Generated by the exact candidate commands in M1_RESEARCH_RECORD.md. Outputs, errors, meta files, summaries, and hash logs are campaign evidence. | No source redistribution is intended. If a future publication redistributes output containing upstream text, it requires a separate review. | Each task meta records exit and elapsed time; summary TSVs are the gate evidence. Not production assets. |
| Grok debug binary | /var/tmp/zaide-m1-reconstruct/candidates/grok-build/target/debug/xai-grok-pager | Generated by cargo build -p xai-grok-pager-bin -j 28 from the pinned Grok checkout. | Binary inherits the candidate's Apache/third-party obligations; it was not copied or distributed by Zaide. | exit_code.txt=0 and elapsed_sec.txt=142; build.log and telemetry.log. Disposable runtime artifact; not reused. |
| OpenCode installation artifacts | node_modules and Bun resolution from the pinned OpenCode checkout; metadata in bun.lock and evidence/opencode-install.log | Generated by bun install. | Transitive obligations remain tied to the candidate lockfile and package legal files; no redistribution. | Install log exit 0 and pinned HEAD. Disposable; not reused. |
| Qwen installation artifacts | node_modules and package-manager changes in the Qwen checkout | Generated by the Qwen install. package.json and package-lock.json were modified in the disposable checkout; those exact diffs are not upstream source. | Transitive obligations remain tied to package-lock.json and package legal files; no redistribution. | Qwen install logs and git diff in the disposable checkout. Disposable; not reused. |
| Grok source/build generated artifacts | target/ tree and debug build outputs | Generated by Cargo from pinned source; not hand-authored or adapted by Zaide. | Inherits Cargo dependency notices if redistributed. | build.log, telemetry.log, exit_code.txt, elapsed_sec.txt. Disposable; not reused. |

The corpus's recovered/rebuilt files are classified as reconstructed research
fixtures, not translated candidate code. The Grok repository's own ported
Codex/OpenCode tools are classified as modified/ported upstream code according
to xai-grok-tools/THIRD_PARTY_NOTICES.md; they are not treated as ideas-only and
are not cleared for copying.

## 5. Transitive obligations and reuse status

- Qwen Code: preserve Apache-2.0 license and any applicable NOTICE/copyright
  material; separately review vendored ripgrep COPYING and package-level legal
  files. The absence of a retained complete npm license report means reuse is
  not cleared.
- OpenCode: preserve MIT license/copyright/permission text and review package
  licenses plus the full Bun lockfile closure. No root NOTICE was found; that
  does not prove that transitive packages have no notices. Reuse is not cleared.
- Grok Build: preserve Apache-2.0 and all applicable entries from
  THIRD-PARTY-NOTICES, vendored third_party notices, ported-source notices,
  binary-tool notices, and crate-local notices. Reuse is not cleared.
- Corpus and prompts: research-only synthetic material; no candidate license is
  inherited merely because a surviving Qwen chat dump helped reconstruct it.
- Generated binaries, logs, and outputs: no Zaide redistribution or production
  adoption. Their presence under /var/tmp does not change candidate provenance.

## 6. Modifications, date, owner, and tracking

- Date considered: 2026-07-27.
- Zaide responsible component: none. M1 is research-only and M2 owns any later
  architecture decision.
- Local research modifications: disposable package-manager changes in the Qwen
  checkout; generated install/build/trial files in disposable paths; no edits to
  pinned candidate source intended for reuse.
- Zaide modifications: only the permitted M1 records and status-only surfaces
  are being synchronized. No candidate or corpus code was added.
- Update/security tracking: no candidate dependency or source was adopted, so
  no Zaide update stream is opened. Any future adoption must record exact source
  ranges, modifications, notices, transitive closure, security/update owner, and
  a new plan-authorized decision before copying.

## 7. Verification limitations

The following limitations remain explicit:

1. The original Ollama health JSON lacked its producer command. During this
   corrective pass the exact artifact was refreshed at
   /var/tmp/zaide-m1-reconstruct/evidence/opencode/ollama-chat-health.json with
   the explicit curl command in M1_RESEARCH_RECORD.md. The response content is
   provider output only and is not a task-pass signal.
2. The initial Qwen/OpenCode held-out hash rows have empty expected values, and
   the retry runner has a syntax error after OpenCode H3. Corrected hash logs and
   the runner defects are retained; no false green result is inferred.
3. No complete npm/Bun/Cargo license-report artifact was retained for the whole
   transitive closure. Existing lockfiles and upstream notice files are
   documented, but they do not constitute adoption clearance.
4. The full-corpus benchmark gate was retired by explicit user-directed plan
   amendment. The local model capability is insufficient for this campaign to
   produce meaningful architectural evidence. Failed comparative execution is
   retained as research evidence, not treated as a candidate-selection winner
   or benchmark result.
5. M1 is complete with the above limitations. M2 is the next milestone but has
   not started. M2 implementation and M2-owned architecture decisions are not
   resolved in this pass.
