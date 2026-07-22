# Phase 16 M2a: Runner Contract

**Status:** M2a was **explicitly human-accepted on 2026-07-23**. The accepted
M2a outcome is the standalone offline runner contract and deterministic
repository-owned fake-candidate core. M2a added no production behavior, DI,
public production types, upstream artifact acquisition, network access, process
launch, or real candidate execution. **M2b remains unauthorized.**

This document defines the standalone, offline-only evaluation runner contract
implemented under `tools/Phase16NativeHarnessEvaluation/`. It does not authorize
upstream artifact acquisition, network access, process launch, sandbox
invocation, credentials, or real candidate execution.

---

## 1. Scope and Non-Goals (M2a)

### In scope

- Versioned manifest schema (`phase16.manifest.v1`)
- Canonical SHA-256 hashing for runner configuration and fixture trees
- Append-only raw trial records with stable record IDs
- Deterministic metric validation
- Bounded stdout/stderr capture
- Repository-owned deterministic fake candidates (`echo`, `metric_snapshot`)
- Offline fake runs with no I/O beyond the declared ledger path
- Rejection of upstream/blocked candidate execution while M1 dispositions remain
  blocked

### Explicit M2a non-goals

- No Bubblewrap, Docker, Podman, or process-tree lifecycle (M2b)
- No writable workspace mounts, symlink escape tests, or cleanup proof (M2b)
- No upstream artifact download, install, or hash verification (M3)
- No provider/service/model campaign selection or credential handling (M1 lock)
- No held-out task materialization or M4c release gates
- No changes to `src/`, production DI, or public production types

---

## 2. Manifest Schema (`phase16.manifest.v1`)

`manifestSchemaVersion` must be `1`. All identity fields are separate strings;
missing, empty, or ambiguous required fields invalidate the manifest.

| Field | Required | Description |
|---|---|---|
| `manifestSchemaVersion` | yes | Must be `1` |
| `runnerConfigHash` | yes | SHA-256 of canonical runner configuration (§3) |
| `fixtureHash` | yes | SHA-256 of canonical fixture tree (§4) |
| `taskId` | yes | Locked task identifier (e.g. `TC-FAKE-01`, `TC-P01`) |
| `executionMode` | yes | `fake_repository_owned` or `upstream_candidate` |
| `candidate` | yes | Object with separate upstream identity fields (§2.1) |
| `fakeCandidate` | yes | Object with fake-candidate identity (§2.2) |
| `networkEnabled` | no | Must be `false` at M2a; default deny |
| `processLaunchEnabled` | no | Must be `false` at M2a; default deny |
| `upstreamArtifactPath` | no | Must be absent/empty at M2a |

### 2.1 `candidate` identity object (all fields required, non-empty)

| Field | Meaning |
|---|---|
| `candidateSlug` | Campaign-local slug; must not be a blocked M1 slug for execution |
| `publicSourceUrl` | Public repository or synthetic fake URL |
| `publicSourceHead` | Observed moving branch HEAD or synthetic fake value |
| `releaseTag` | Release/tag identity |
| `tagCommit` | Tag commit identity |
| `releaseMetadataTarget` | Release metadata target identity |
| `sourceRev` | SOURCE_REV identity |
| `distributedArtifactHash` | Distributed artifact hash identity |
| `changelogProductIdentity` | Product/changelog identity |
| `providerIdentity` | Provider identity |
| `serviceIdentity` | Service identity |
| `modelIdentity` | Model identity |
| `protocolSdkIdentity` | Protocol/SDK identity |

### 2.2 `fakeCandidate` identity object (all fields required, non-empty)

| Field | Meaning |
|---|---|
| `fakeCandidateId` | Stable fake-candidate identifier bound into each record |
| `fakeCandidateVersion` | Fake-candidate implementation version |
| `fakeCandidateKind` | `echo` or `metric_snapshot` |

---

## 3. Runner Configuration Hash

Runner configuration is hashed from this canonical JSON object (camelCase keys,
recursively sorted object keys, compact arrays):

```json
{
  "artifactRoot": "<absolute path outside worktree>",
  "campaignLockRevision": "<M1 lock revision label>",
  "manifestSchemaVersion": 1,
  "runnerCommit": "<git commit or unknown>"
}
```

Algorithm:

1. Serialize with recursively sorted object property names.
2. UTF-8 encode the canonical string.
3. `runnerConfigHash = SHA-256(hex lowercase)`.

The CLI reads `PHASE16_ARTIFACT_ROOT`, `PHASE16_RUNNER_COMMIT`, and
`PHASE16_CAMPAIGN_LOCK_REVISION` when validating manifests or running fake
trials.

Default artifact root (when env var unset):

```text
${TMPDIR}/phase16-artifacts/phase-16/
  records/
  artifacts/
  held-out/
```

---

## 4. Fixture Tree Canonicalization and Hash

Given a map of relative path → UTF-8 file body:

1. Reject invalid paths before hashing:
   - empty or whitespace-only paths;
   - absolute paths (`/…`, `\\…`, or `X:` drive prefixes);
   - trailing `/`;
   - empty segments from `//`;
   - `.` or `..` segments;
   - duplicate normalized paths (including slash/backslash variants such as
     `a.txt` versus `workspace\\a.txt` when they normalize to the same path).
2. Normalize surviving paths to forward slashes without a leading `/`.
3. Sort paths lexicographically (`Ordinal`).
4. For each path emit:

```text
<path>
<body with trailing whitespace stripped per line>
---
```

5. `fixtureHash = SHA-256(UTF-8 bytes of concatenation)` as exactly 64 lowercase
   hex characters.

Line endings in bodies are normalized to LF before per-line trailing whitespace
strip.

---

## 4.1 SHA-256 Digest Format

Every digest field (`runnerConfigHash`, `fixtureHash`, `recordContentHash`) must
be **exactly 64 lowercase hexadecimal characters** (`[0-9a-f]{64}`). Uppercase,
non-hex characters, incorrect length, and whitespace-padded values are rejected.

---

## 5. Invalidation Behavior

Manifest or record validation fails closed with explicit errors when:

- `manifestSchemaVersion` is unsupported
- Any required identity field is missing or whitespace-only
- Any digest field fails §4.1 format validation
- `runnerConfigHash` does not match the canonical runner configuration
- `fixtureHash` or `taskId` binding checks fail
- Fixture path canonicalization rules in §4 are violated
- `executionMode` is not `fake_repository_owned` at M2a
- `networkEnabled`, `processLaunchEnabled`, or `upstreamArtifactPath` request
  forbidden capabilities
- `candidateSlug` is `qwen-code`, `opencode`, or `grok-build` (blocked at M1)
- Metric fields are negative or malformed
- Record fields do not exactly match manifest binding (schema label, execution
  mode, complete candidate identity, complete fake-candidate identity, runner
  hash, fixture hash, task ID)
- Capture byte counts exceed 64 KiB or truncation flags disagree with captured
  content
- M2a record invariants fail (`observational` evidence with empty
  `invalidationReasons`, authorized fake-candidate kind)
- Record `recordContentHash` does not match the canonical record body
- Append is attempted for an existing `recordId` (duplicate/overwrite)
- Ledger reload finds malformed, incomplete, M2a-invariant-violating, duplicate,
  or hash-inconsistent historical records

Invalid manifests never produce ledger records.

---

## 6. Record Lifecycle

Records are JSON lines in an append-only ledger file.

| Field | Description |
|---|---|
| `recordId` | Stable ID: `p16-{sequence:D8}-{hashPrefix16}` where hashPrefix16 is the first 16 hex chars of SHA-256 over `{runnerConfigHash}:{fixtureHash}:{taskId}:{fakeCandidateId}:{sequence:D8}` |
| `manifestSchemaVersion` | Label `phase16.manifest.v1` |
| `runnerConfigHash`, `fixtureHash`, `taskId` | Bound from manifest |
| `executionMode` | Always `fake_repository_owned` for M2a records |
| `candidate`, `fakeCandidate` | Bound identity objects |
| `metrics` | Deterministic validated metric snapshot |
| `stdout`, `stderr` | Bounded captured text |
| `stdoutTruncated`, `stderrTruncated` | Truncation flags |
| `evidenceClass` | `observational` for successful M2a fake runs |
| `invalidationReasons` | Empty array when valid |
| `recordContentHash` | SHA-256 of canonical record body excluding this field |

Rules:

- Append-only: no in-place edits or deletes
- Duplicate `recordId` rejection
- Overwrite detection via `RejectOverwrite`
- Reload applies the same structural, digest-format, M2a-invariant, capture,
  and hash-consistency checks to every JSONL line before sequence allocation

---

## 7. Capture Limits

| Stream | Maximum captured bytes |
|---|---|
| stdout | 65,536 (64 KiB) |
| stderr | 65,536 (64 KiB) |

When a stream exceeds its limit, capture stops, the corresponding
`*Truncated` flag is set to `true`, and captured bytes must equal the limit.
If `*Truncated` is `false`, captured bytes must be less than or equal to the
limit. The record remains valid only if all other gates pass.

---

## 8. Deterministic Metrics

Metric object fields:

| Field | Constraint |
|---|---|
| `passCount`, `failCount` | Non-negative integers |
| `durationMilliseconds` | Non-negative integer |
| `turnCount` | Non-negative integer |
| `inputTokenEstimate`, `outputTokenEstimate` | Non-negative integers |

Fake candidates must produce identical metrics for identical manifest bindings.

---

## 9. Fake-Run Semantics

Authorized fake candidates:

| `fakeCandidateKind` | Behavior |
|---|---|
| `echo` | Emits deterministic stdout binding `taskId`, `fixtureHash`, and `fakeCandidateId`; fixed minimal metrics |
| `metric_snapshot` | Emits deterministic metrics derived from `{taskId}|{fixtureHash}|{fakeCandidateId}` seed |

Fake runs:

- Execute entirely in-process
- Perform no network I/O
- Launch no child processes
- Read no upstream artifacts
- Write only through the append-only ledger API supplied by the caller

CLI:

```bash
Phase16NativeHarnessEvaluation validate-manifest manifest.json
Phase16NativeHarnessEvaluation fake-run manifest.json ledger.jsonl
```

---

## 10. Upstream Rejection (M1 Lock)

While M1 dispositions remain blocked, the runner rejects:

- `executionMode: upstream_candidate`
- `networkEnabled: true`
- `processLaunchEnabled: true`
- Non-empty `upstreamArtifactPath`
- `candidateSlug` in `{ qwen-code, opencode, grok-build }`

These checks apply before any fake or real execution path.

---

*M2a runner contract — explicitly human-accepted 2026-07-23. M2b remains
unauthorized.*
