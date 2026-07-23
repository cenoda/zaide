# Phase 16 M3a — Qwen Code Acquisition and Inspection Evidence

**Status:** Completed under explicit human M3a acquisition-and-inspection grant
(2026-07-23). **Scope limited to download, hash verification, tag and in-archive
license/notice scan, and static layout/argv inspection.** No upstream binary
launch, no egress tooling install, no credentials, no provider API calls, no
production code change, no commit/push.

**Campaign path:** single-candidate observational only
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`). OpenCode and Grok Build remain blocked
at M1.

**Artifact root (outside Zaide repository):**

```text
/tmp/phase16-artifacts/phase-16/
  artifacts/qwen-code/v0.20.1/
    download/          # pinned archive + SHA256SUMS + tag LICENSE re-check
    inspect/qwen-code/ # extract for inspection only
  records/m3a/         # durable hashes, listings, license copies, summary
```

Runner default when `PHASE16_ARTIFACT_ROOT` is unset:
`Path.GetTempPath()/phase16-artifacts` → `/tmp/phase16-artifacts` on this host
(see `evaluation/RUNNER_CONTRACT.md` §3).

---

## 1. Scope and Non-Effects

| Allowed in this grant | Performed |
|---|---|
| Download pinned `qwen-code-linux-x64.tar.gz` | Yes |
| Download official `SHA256SUMS` | Yes |
| Verify pinned SHA-256 | Yes |
| Re-check tag `LICENSE` (C-05) | Yes |
| Extract for inspection under artifact root | Yes |
| Scan archive `LICENSE` / `NOTICE` / `THIRD-PARTY-NOTICES` | Yes |
| Resolve post-extract executable path (A-02) | Yes |
| Resolve supported headless argv from static sources (A-03) | Yes |

| Forbidden in this grant | Status |
|---|---|
| Launch upstream Qwen Code binary | **Not performed** |
| Install egress tooling (`slirp4netns`, `pasta`, `socat`, …) | **Not performed** |
| Create or inject credentials | **Not performed** |
| Call DeepSeek / any provider API | **Not performed** |
| Modify production code | **Not performed** |
| Commit or push | **Not performed** |

---

## 2. Tag LICENSE re-check (C-05, before acquisition)

| Check | Result |
|---|---|
| Tag `refs/tags/v0.20.1` | Resolves to commit `305b049100606fa093a14b5cd849bff3be16e31a` (unchanged from M1) |
| `LICENSE` at tag `v0.20.1` | HTTP 200; Apache License Version 2.0 text |
| Tag `LICENSE` SHA-256 | `55367b61ccd2a016a0159ad886bd66a3ee6cb5e873d0c75c803c897dd245b075` |
| `NOTICE` at tag `v0.20.1` | Absent (HTTP 404); same as M1 re-verification |

**SPDX detection (text match):** Apache-2.0.

---

## 3. Acquisition and integrity (A-01)

| Field | Value |
|---|---|
| URL | `https://github.com/QwenLM/qwen-code/releases/download/v0.20.1/qwen-code-linux-x64.tar.gz` |
| Pinned SHA-256 (M1) | `2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e` |
| Downloaded file size | 82,048,902 bytes |
| Computed SHA-256 | `2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e` |
| Pinned match | **YES** |
| Official `SHA256SUMS` line | `2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e  qwen-code-linux-x64.tar.gz` |
| `sha256sum -c` (linux-x64 line) | **OK** |
| GitHub API asset digest (re-query) | `sha256:2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e` |
| GitHub API size | 82048902 |
| Acquisition timestamp (UTC) | `2026-07-23T10:33:59Z` (summary record) |

Triple agreement: M1 pin, downloaded bytes, official `SHA256SUMS`, and GitHub
asset digest.

---

## 4. In-archive license / notice scan (A-12 / C-05)

Extract root top-level layout:

```text
qwen-code/
  LICENSE
  README.md
  package.json
  manifest.json
  bin/
  lib/
  node/
```

| Artifact path | Present? | Result |
|---|---|---|
| `qwen-code/LICENSE` | **Yes** | Apache License Version 2.0; SHA-256 `55367b61…` **identical** to tag `LICENSE` |
| `qwen-code/lib/LICENSE` | **Yes** | Identical to root `LICENSE` |
| `qwen-code/NOTICE` | **No** | Absent (matches tag absence) |
| `qwen-code/THIRD-PARTY-NOTICES` | **No** | Absent |
| Consolidated third-party notice file | **No** | Not present at package root |
| Dependency licenses under tree | Yes | Many individual `LICENSE*` files under `node/` (bundled Node/npm) and `lib/node_modules/` / `lib/vendor/` (e.g. ripgrep `COPYING`) |

**package.json `license` field:** absent in both root and `lib/package.json`
(version `0.20.1`, name `@qwen-code/qwen-code`). Primary SPDX evidence remains
the Apache-2.0 `LICENSE` file.

**License clearance posture for later execution:**

- Primary package license is clear Apache-2.0 (tag + archive identical).
- Named `NOTICE` / `THIRD-PARTY-NOTICES` files are **absent**.
- C-05 makes the **project owner** the final license approver and requires
  **block on uncertainty** before execution.
- M3a records the absence; it does **not** substitute for owner clearance of
  third-party notice completeness.

**Project-owner C-05 decision (2026-07-23):** approved the recorded execution
license posture: Apache-2.0 root license identical to the pinned tag, absent
root `NOTICE` / `THIRD-PARTY-NOTICES`, and the observed per-dependency license
files under the archive tree. This is an approval of the recorded Phase 16
execution posture, not an assertion that every third-party obligation has been
independently audited beyond this bounded scan.

---

## 5. A-02 — Post-extract executable path (**RESOLVED**)

| Field | Value |
|---|---|
| Relative path from extract root | `qwen-code/bin/qwen` |
| File type | POSIX shell launcher (`#!/usr/bin/env sh`), mode `0755` |
| Launcher behavior (static) | Sets `ROOT` to parent of `bin/`; execs `"$ROOT/node/bin/node" "$ROOT/lib/cli-entry.js" "$@"` with `QWEN_CODE_LAUNCHER_PATH` set |
| Bundled Node | `qwen-code/node/bin/node` — ELF 64-bit x86-64 (Node per `manifest.json`: `node-v22.23.1-linux-x64`) |
| package.json bin (npm shape) | `"qwen": "scripts/cli-entry.js"` (source layout); standalone maps to `lib/cli-entry.js` |
| Windows sidecar | `qwen-code/bin/qwen.cmd` (not used on Linux evaluation path) |

**Absolute path on this host after inspect extract:**

```text
/tmp/phase16-artifacts/phase-16/artifacts/qwen-code/v0.20.1/inspect/qwen-code/bin/qwen
```

**Evaluation invocation executable:** that absolute path (or equivalent under a
future extract location under the same artifact root), never a floating `PATH`
install.

**Binary was not executed.**

---

## 6. A-03 — Structured non-interactive argv (**RESOLVED for support surface**)

Sources (static only; no `--help` process launch):

1. Archive `README.md` headless form: `qwen -p "..."`.
2. `lib/cli.js` top-level help option table and usage string:
   `use -p/--prompt for non-interactive mode`.
3. Main yargs option registration in
   `lib/chunks/chunk-D53NR4GO.js` (excerpt retained under
   `records/m3a/cli-options-excerpt.js.txt`).

### 6.1 Supported headless-relevant options (inspection lock)

| Concern | Supported argv / mechanism | Notes |
|---|---|---|
| Prompt mode | `-p` / `--prompt <string>` **or** positional `query..` | Cannot combine positional + `-p`; `--prompt` is deprecated in favor of positional in help text |
| Interactive after prompt | `-i` / `--prompt-interactive` | Forbidden for headless eval; conflicts with `-p` |
| Model | `-m` / `--model <id>` | Locked campaign model: `deepseek-v4-flash` |
| Output capture | `-o` / `--output-format` ∈ {`text`,`json`,`stream-json`} | Prefer `json` or `stream-json` for machine parse |
| Turn limit | `--max-session-turns <int>` | Integer session-turn cap |
| Wall clock | `--max-wall-time <duration>` | e.g. `30s`, `5m`; exit 55 on exceed |
| Tool-call cap | `--max-tool-calls <n>` | Optional additional budget |
| Approval / non-interactive tools | `--approval-mode` ∈ {`plan`,`default`,`auto-edit`,`auto`,`yolo`} **or** `--yolo` / `-y` | Cannot combine `--yolo` with `--approval-mode`; headless with `yolo` warns without sandbox |
| Sandbox (candidate-internal) | `-s` / `--sandbox` | Distinct from Phase 16 Bubblewrap host isolation |
| Auth type | `--auth-type` ∈ {`openai`,`anthropic`,`qwen-oauth`,`gemini`,`vertex-ai`} | DeepSeek preset uses OpenAI-compatible protocol |
| OpenAI-compatible flags | `--openai-api-key`, `--openai-base-url` | Prefer env injection over flags for secrets |
| Workspace root | **Process CWD** | No top-level `--cwd` on main `$0` command; `--cwd` exists only on channel subcommands. Runner must set working directory to the task workspace |
| Extra dirs | `--include-directories` / `--add-dir` | Optional |
| Minimal startup | `--bare`, `--safe-mode` | Available for reducing ambient discovery |
| Version / help | `-v` / `--version`, `-h` / `--help` | Not for smoke task body |

### 6.2 Proposed locked evaluation argv skeleton (not executed)

Executable: `<extract>/qwen-code/bin/qwen`

Working directory: task workspace root (runner-controlled).

Argument vector (structured; no shell interpolation):

```text
-p
<smoke-prompt>
--model
deepseek-v4-flash
--output-format
json
--max-session-turns
<N from TASK_CORPUS ceiling>
--max-wall-time
<T from TASK_CORPUS ceiling>
--approval-mode
<owner-chosen non-interactive mode; not locked in M3a>
```

Optional flags for later qualification (still not executed): `--bare`,
`--safe-mode`, candidate `--sandbox` only if isolation design requires it in
addition to host Bubblewrap.

### 6.3 Credential / provider surface observed (static; not injected)

DeepSeek provider preset in bundled code (`packages/core/src/providers/presets/deepseek.ts` via chunk):

| Preset field | Value |
|---|---|
| `id` | `deepseek` |
| `baseUrl` | `https://api.deepseek.com` |
| `envKey` | `DEEPSEEK_API_KEY` |
| Models listed | includes `deepseek-v4-flash`, `deepseek-v4-pro` |
| Protocol | OpenAI-compatible (`USE_OPENAI`) |

Headless auth docs in binary strings also mention
`OPENAI_API_KEY` + `OPENAI_BASE_URL` + `OPENAI_MODEL` for CI. A-07 already
locks sandbox allowlist to **`DEEPSEEK_API_KEY` only**; that remains the
campaign credential class. Final injection mechanics require a **separate
execution grant** and must not use ambient `~/.config` credentials.

Hostname recognition for DeepSeek includes `api.deepseek.com` and
`*.api.deepseek.com` (static inspection only).

---

## 7. Bundle identity (informational)

From `qwen-code/manifest.json`:

```json
{
  "name": "@qwen-code/qwen-code",
  "version": "0.20.1",
  "target": "linux-x64",
  "nodeArchive": "node-v22.23.1-linux-x64.tar.xz",
  "createdAt": "2026-07-21T16:49:24.115Z"
}
```

A-10 remains **explicitly unmapped** (archive bytes not claimed equal to tag
commit). A-11 `SOURCE_REV` remains **`UNRESOLVED`**.

---

## 8. Raw evidence under artifact root

| Path | Content |
|---|---|
| `records/m3a/summary.env` | Timestamp, hashes, path, license flags, `binary_executed=NO` |
| `records/m3a/SHA256SUMS` | Official release checksum file |
| `records/m3a/tag-LICENSE` | Tag LICENSE re-check copy |
| `records/m3a/archive-LICENSE` | In-archive LICENSE copy |
| `records/m3a/tar-listing.txt` | Full archive member list (6822 entries) |
| `records/m3a/cli-options-excerpt.js.txt` | Yargs option registration excerpt |
| `artifacts/qwen-code/v0.20.1/download/` | Archive + SHA256SUMS + tag LICENSE |
| `artifacts/qwen-code/v0.20.1/inspect/` | Extract tree for inspection |

**No upstream bytes under the Zaide git worktree.**

---

## 9. GO / NO-GO for later grants

### 9.1 M3a acquisition-and-inspection slice

**GO — complete.** A-01 verified; A-02/A-03 resolved from inspection without
execution; tag and in-archive primary `LICENSE` are Apache-2.0 and identical;
findings recorded.

### 9.2 Later provider-restricted egress-proof grant (C-01/C-02)

**GO — complete (2026-07-23).** See `M3_EGRESS_PROOF_EVIDENCE.md`.

Allowlisted `api.deepseek.com:443` HTTPS succeeded (unauthenticated);
non-allowlisted HTTPS destinations were blocked; evidence preserved under the
phase artifact root. No package install was required.

### 9.3 Later credential / execution / M3 qualification grant

**GO to authorize when the human issues that grant.** Egress is no longer a
blocker. License clearance (C-05) is **complete** (owner approved 2026-07-23).
DNS binding design is **complete** (`M3_DNS_BINDING_GATE.md`). Execution
remains **NO-GO to perform** until all of the following are satisfied under
that separate grant:

1. **Egress proof success** for `api.deepseek.com:443` only (C-01/C-02) —
   **done 2026-07-23**.
2. **Dedicated Phase 16 DeepSeek sub-key** creation and sandbox injection of
   `DEEPSEEK_API_KEY` only (C-04 / A-07); never ambient credentials.
3. **Owner lock of remaining argv policy:** non-interactive
   `--approval-mode` (or explicit rejection of `yolo` without sandbox),
   turn/time ceilings from `TASK_CORPUS`, output format, and smoke task id.
4. **Isolation re-check** before first process launch (`processLaunchEnabled`
   remains denied until qualification grant).
5. **USD 1 M3 smoke cost ceiling** and USD 3 cumulative cap tracking.
6. Reuse of provider-restricted egress enforcement equivalent to the egress
   proof architecture **and** immediate DNS binding execution at launch (A-14)
   per `M3_DNS_BINDING_GATE.md` §8 (host-side re-resolution, sandbox-only
   `/etc/hosts`, nft IPv4 parity, pre-launch stop conditions).

Until those gates pass, the candidate remains
**`eligible for later M3 qualification`** but **not qualified**, and must not
be launched.

---

## 10. Cross-references

- `M1_AMENDMENT_QWEN_OBSERVATIONAL.md` — observational path and A-01…A-13
- `CANDIDATE_ARTIFACTS.md` §4 — disposition and field updates
- `ISOLATION_EVIDENCE.md` §7 — M3 eligibility
- `M3_EGRESS_PROOF_EVIDENCE.md` — egress complete
- `M3_DNS_BINDING_GATE.md` — DNS binding gate for launch
- `CAMPAIGN_LOCK.md` — campaign path
- `RUNNER_CONTRACT.md` — artifact root
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3a acquisition-and-inspection evidence — produced 2026-07-23 under explicit
M3a grant. Upstream binary not launched. Subsequent M3 egress proof completed
2026-07-23 (`M3_EGRESS_PROOF_EVIDENCE.md`). M3 DNS binding gate defined
2026-07-23 (`M3_DNS_BINDING_GATE.md`). Credentials and execution remain
unauthorized until a separate grant.*
