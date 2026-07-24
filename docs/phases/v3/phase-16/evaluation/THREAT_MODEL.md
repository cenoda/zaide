# Phase 16 M1: Threat Model

**Status:** M1 explicitly human-accepted on 2026-07-23. This threat model
describes required safety properties. Properties marked **unproven** are future
qualification requirements, not established enforcement facts. **M2a was explicitly
human-accepted on 2026-07-23** (standalone offline runner contract and
fake-candidate core). **M2b remains unauthorized.**

---

## 1. Trust Boundary

### 1.1 Untrusted Candidate Assumption

All candidate runtimes, external repositories, downloaded artifacts, and
generated agent output are treated as **untrusted**. Known repository identity
and license do not confer trust. Candidates must never receive direct access to:

- The host filesystem outside the designated trial workspace
- User home directories (`~`, `$HOME`)
- SSH keys, GPG keys, or credential files
- Cloud provider configuration or cached credentials
- The Zaide repository or any unrelated Git repository
- Package manager caches (NuGet, npm, pip, etc.)
- Browser sessions, cookies, or user-level secret stores

### 1.2 Default-Deny Environment

- **Filesystem:** Execution sandboxes enforce default-deny writable paths. The
  designated trial workspace is the only writable location. The host repository
  and system paths are either read-only or excluded.
- **Network:** Default-deny outbound network access. No candidate may assume
  general internet access.
- **Environment variables:** The sandbox starts with an empty or
  explicitly-allowlisted environment. No host environment variable is inherited
  unless explicitly allowlisted in the candidate qualification record.
- **Devices and capabilities:** The sandbox must deny device access, new
  privileges, and kernel capability escalation.

---

## 2. Provider-Restricted Egress — UNPROVEN

**Status at M1: Unproven.**

The M0 substrate audit established that Bubblewrap `0.11.2` is installed and
that full network isolation (`--unshare-net`) works. The following are absent
from the host:

- `slirp4netns`
- `pasta`
- `socat`
- Docker daemon access
- Podman

Provider-restricted egress — allowing outbound HTTPS only to specific,
allowlisted provider API endpoints while denying all other traffic — **cannot
currently be enforced** with the available host tools. This is a future
qualification requirement for M2b.

M2b must either:

1. Demonstrate provider-only egress with a reproducible, tool-verified proof
   (e.g., a rootless network namespace with a proxy or firewall rule that passes
   an allowlisted request and blocks a non-allowlisted request), OR
2. Record that provider-restricted egress cannot be enforced and classify all
   trials as observational with explicit egress limitations.

Until M2b resolves this, the threat model assumes default-deny full network
isolation. Candidates requiring network access cannot execute.

---

## 3. Credential Isolation

- Production API keys and user credentials are never stored in evaluation
  configurations, candidate workspaces, or runner state.
- Evaluation runs use **phase-specific, least-privilege, allowlisted, and
  independently revocable** credentials.
- Credentials are injected as temporary scoped environment variables for the
  duration of a single trial only.
- Credential values are redacted from all logs, ledger entries, and output
  artifacts before persistence. Redaction is verified, not assumed.
- No credential survives sandbox teardown.

---

## 4. Process Lifecycle and Cleanup

### 4.1 Process-Tree Ownership

- The runner solely owns each candidate process tree.
- Executables and arguments use structured argument lists; no shell
  interpolation.
- Working directory, environment allowlist, streams, wall/idle timeouts, output
  cap, and exit expectations are explicit per trial.

### 4.2 Cleanup Sequence

1. Send cooperative cancellation signal.
2. Wait for graceful termination (bounded grace period).
3. Force recursive process-tree termination (`SIGKILL`).
4. Verify no descendant process remains.
5. Verify no writable mount remains.
6. Record terminal state.

### 4.3 Cleanup Failure

Cleanup failure invalidates the trial and **blocks later real-candidate trials**
pending human review. The runner must not proceed to the next trial if the
current trial leaves orphan processes or writable mounts.

---

## 5. Workspace Mutation Boundary

- Trials use generated disposable repositories under an explicit phase artifact
  root, never Zaide or an arbitrary user path.
- The host is read-only except for exact trial workspace and artifact paths.
- Each trial starts from a content-addressed fixture and records diff, file
  inventory, Git status, reset, and cleanup state.
- Fake-candidate tests must cover symlink/path traversal, mount escape, absolute
  path, and output/file-count limits before any real candidate executes.
- Commit, push, publication, external messaging, and destructive host operations
  are forbidden.

---

## 6. Evidence Retention and Disposal

### 6.1 Retention

Raw evidence is stored under `<artifact-root>/phase-16/records/` (immutable
append-only trial ledger) and `<artifact-root>/phase-16/artifacts/` (output
diffs, logs, hashes per trial). The artifact root path is defined at M2a.

### 6.2 Quota

Maximum total evidence storage: **10 GiB**. Maximum per-trial record:
**256 MiB**. Quota exhaustion is a campaign-halting event.

### 6.3 Access Boundary

- Records are append-only during active campaign phases.
- Only the runner process may write records.
- Read access for manual reconciliation is limited to the human reviewer.

### 6.4 Disposal Trigger

Evidence is retained until **explicit human disposition** at or after M5
closeout. The runner does not automatically delete evidence. Disposal is a
separate reviewed decision triggered by human acceptance of the M5 closeout
recommendation.

---

## 7. Escalation and Circuit Breakers

### 7.1 Escalation Criteria

An evaluation session is immediately aborted and escalated to manual review if:

- A sandbox escape attempt is detected (file access outside workspace mount,
  mount escape, symlink traversal to host paths).
- A candidate process attempts privilege escalation or unauthorized network
  activity.
- Cumulative runner resource consumption exceeds safety thresholds (disk
  exhaustion, process count explosion).

### 7.2 Circuit Breaker

If three consecutive trials encounter unhandled runner crashes or security
boundary violations, the campaign **automatically halts** further candidate
execution. Resumption requires human review and explicit re-authorization.

---

## 8. Pre-Execution Proof Requirements (M2b Gate)

Before any candidate artifact is executed, M2b must prove with repository-owned
fake candidates:

- Exact argv construction (no shell interpolation)
- Environment deny-by-default and explicit allowlist
- Read-only host filesystem except exact writable roots
- Symlink and path-traversal escape blocking
- Wall-clock and idle timeout enforcement
- Cooperative cancellation followed by bounded forced termination
- Descendant process absence after cleanup
- Output and file-count caps
- Hash binding and content-addressable fixture integrity
- Redaction of credential-bearing output
- Dirty/reset detection after trial
- Cleanup-failure detection and downstream trial blocking

---

*M1 threat model — human-accepted 2026-07-23. Provider-restricted egress is
unproven. No candidate has been executed. All security properties are required
but not yet demonstrated. At the M1 boundary M2a was unauthorized; M2a was
subsequently authorized and explicitly human-accepted on 2026-07-23. M2b remains
unauthorized.*
