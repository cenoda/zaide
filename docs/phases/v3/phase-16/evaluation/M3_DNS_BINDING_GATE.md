# Phase 16 M3 — DNS Binding Gate (Design Lock)

**Status:** **Defined and published 2026-07-23 (docs-only).** **Executed under
M3 qualification sessions** (`M3_QUALIFICATION_EVIDENCE.md`), including latest
`m3q-20260724T060109Z-45dd1c5f`: host-side resolution, hosts map, nft
rule-text triple-consistency, and inner allow/block egress reprobes **GO**
(`BOUND_IPV4=3.173.21.63`, `CONSISTENT=YES`; operator-finalized
`BINDING_VERDICT=GO` after script stopped on Qwen exit 53 before writing DNS
GO). Earlier sessions also recorded host-side binding GO before stopping at
credential, slirp attach, Bubblewrap resolv, auth-type, turn-limit, wall-time,
or plan-only failures. This gate locks how the Qwen Code credential-and-execution
slice must bind `api.deepseek.com` to a single verified IPv4 address inside a
sandbox-only resolution path. It does **not** by itself declare M3
qualification complete.

**Campaign path:** single-candidate observational only
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`). OpenCode and Grok Build remain blocked
at M1.

**Prerequisite evidence (complete; not re-run by this gate):**

- M3 egress proof **GO** for `api.deepseek.com:443` allow + non-allowlisted
  block (`M3_EGRESS_PROOF_EVIDENCE.md`, 2026-07-23).
- M3a acquisition-and-inspection **GO**; project-owner C-05 license clearance
  **approved 2026-07-23** (`M3A_ACQUISITION_EVIDENCE.md` §4).

**Artifact root (outside Zaide repository):**

```text
/tmp/phase16-artifacts/phase-16/records/dns-binding/
  <session-id>/
    summary.env              # verdict, hostname, bound IPv4, timestamps
    resolution.env           # resolver command, exit code, raw A-record set
    hosts-injection.txt      # exact sandbox /etc/hosts content (one line)
    nft-allowlist.txt        # exact nft destination match for bound IPv4
    consistency-check.env    # hosts == nft == fresh resolution
    commands-ledger.txt      # ordered pre-launch steps with exit codes
```

Each credential-and-execution session uses a fresh `<session-id>` directory.
Prior egress-proof records remain immutable reference only.

---

## 1. Scope and Non-Effects

| Allowed in this docs-only publication | Performed |
|---|---|
| Define deterministic sandbox-only resolution path for `api.deepseek.com` | Yes |
| Define hosts injection, nft binding, TLS/SNI preservation, stop conditions | Yes |
| Define mandatory immediate pre-launch re-verification for execution slice | Yes |
| Cross-reference completed egress and license evidence | Yes |

| Forbidden in this publication | Status |
|---|---|
| Rerun M3 egress probe | **Not authorized** |
| Create, read, or inject credentials | **Not performed** |
| Call DeepSeek or any provider API | **Not performed** |
| Launch Qwen Code or any upstream binary | **Not performed** |
| Modify `src/`, tools, tests, or production configuration | **Not performed** |

---

## 2. Threat Addressed

M3 egress proof demonstrated that an ephemeral user+net namespace with
`slirp4netns` and in-netns `nftables` can allow HTTPS to a **pre-resolved**
allowlisted IPv4 while blocking other destinations. It used a repository-controlled
probe with explicit `--resolve` pinning.

A live Qwen Code trial must **not** rely on ambient DNS inside the candidate
Bubblewrap sandbox. Uncontrolled resolution would let the candidate (or bundled
Node HTTP stack) reach arbitrary addresses if resolver policy drifts, if
multiple A records appear, or if a compromised runtime selects a non-allowlisted
answer.

This gate closes that gap by requiring:

1. **Host-side, pre-sandbox resolution** immediately before launch.
2. **Sandbox-only static mapping** via a dedicated `/etc/hosts` (or equivalent
   mechanism with identical semantics).
3. **Exact IPv4 parity** between the hosts mapping and the nft output allowlist.
4. **Unchanged TLS hostname validation** — HTTPS URLs and SNI remain
   `api.deepseek.com`; certificate verification is not weakened.

All non-allowlisted DNS and network paths remain **denied**.

---

## 3. Locked Allowlist Identity

| Field | Locked value |
|---|---|
| Hostname (A-05) | `api.deepseek.com` |
| Port (A-13 / C-02) | **443/TCP only** |
| URL shape for candidate | `https://api.deepseek.com` (host literal URLs forbidden) |
| Credential env (A-07) | `DEEPSEEK_API_KEY` only (injection remains a separate grant) |
| Proof-time reference IPv4 | `3.173.21.63` (`M3_EGRESS_PROOF_EVIDENCE.md`; **not** a permanent pin) |

The reference IPv4 documents what succeeded at egress proof. **Every execution
session must re-resolve and re-bind**; stale IPs are not reused without a fresh
successful binding sequence.

---

## 4. Deterministic Resolution Path (Host-Side Only)

Resolution runs on the **host**, in the credential-and-execution grant,
**immediately before** sandbox/network assembly and **before** upstream process
launch. The candidate sandbox must never perform the authoritative lookup.

### 4.1 Resolver command (locked)

Use exactly one primary command per session (record which in
`resolution.env`):

```bash
getent ahostsv4 api.deepseek.com
```

Acceptance rule: among returned rows, collect distinct IPv4 addresses from the
first field. Alternative only if `getent` is unavailable on the host:
`dig +short A api.deepseek.com` with `+nocookie +noedns` (record substitution
in `commands-ledger.txt`).

### 4.2 Success criteria

| Check | Requirement |
|---|---|
| Exit code | **0** |
| Distinct IPv4 count | **Exactly 1** |
| Address family | **IPv4 only** for binding (no AAAA binding in Phase 16) |
| Hostname | Must match locked `api.deepseek.com` exactly |
| Recorded fields | IPv4, UTC timestamp, full stdout, resolver command |

Optional informational fields (do **not** override single-A acceptance):

- CNAME chain (egress proof observed `d3bbv8sr76az5s.cloudfront.net`)
- TTL (informational only; never used to skip re-resolution)

### 4.3 Ambient DNS inside candidate sandbox — forbidden

Inside the future Qwen Bubblewrap launch:

- **No** inherited host `/etc/resolv.conf` with working upstream resolvers.
- **No** slirp4netns DNS proxy that could resolve arbitrary names unless it is
  provably unable to resolve non-allowlisted names (default: omit working
  resolver; static hosts only).
- **No** `NDOTS`, `search`, or `systemd-resolved` stubs visible to the
  candidate mount namespace.

DNS for the allowlisted host is satisfied **only** by the injected hosts map.
Any other name must fail resolution or fail connection under default-deny egress.

---

## 5. Recording, Hosts Injection, and nft Binding

### 5.1 Record the verified endpoint

After §4 succeeds, write under `<session-id>/`:

| Artifact | Content |
|---|---|
| `resolution.env` | `RESOLVER_CMD`, `EXIT_CODE`, `RESOLVED_IPV4`, `RESOLVED_AT_UTC`, `DISTINCT_A_COUNT=1` |
| `summary.env` | `HOSTNAME=api.deepseek.com`, `BOUND_IPV4=<value>`, `BINDING_VERDICT=PENDING` |

### 5.2 Sandbox-only `/etc/hosts` injection

Create a **sandbox-private** hosts file containing **exactly one** mapping line
(plus mandatory localhost lines if the runtime requires them):

```text
127.0.0.1 localhost
::1 localhost
<BOUND_IPV4> api.deepseek.com
```

Mount it read-only into the candidate namespace (Bubblewrap `--ro-bind` or
equivalent). The candidate must see this file as `/etc/hosts`. The host's live
`/etc/hosts` must **not** be bind-mounted wholesale.

Persist the exact content to `hosts-injection.txt`.

**Forbidden:**

- Wildcard or additional provider hostnames unless separately authorized and
  bound with matching nft rules.
- Multiple `api.deepseek.com` lines.
- Injecting IPs not equal to `BOUND_IPV4`.

### 5.3 nft allowlist binding

Reuse the M3 egress-proof architecture (`M3_EGRESS_PROOF_EVIDENCE.md` §3):

1. `unshare --user --map-root-user --net --mount --fork --pid --mount-proc`
2. `slirp4netns --configure --mtu=65520 --disable-host-loopback <ns-pid> tap0`
3. In-netns `nftables` table `inet phase16_egress` (or successor name recorded in
   ledger):
   - default **drop** input/output
   - allow loopback + established/related
   - allow **TCP dport 443** to **`BOUND_IPV4/32` only**

Persist the effective destination match to `nft-allowlist.txt`. No host-wide
persistent firewall changes.

### 5.4 Triple-consistency check (mandatory)

Before launch, compute and record in `consistency-check.env`:

```text
FRESH_IPV4=<from §4>
HOSTS_IPV4=<api.deepseek.com line from hosts-injection.txt>
NFT_IPV4=<destination from nft-allowlist.txt>
CONSISTENT=YES|NO
```

**Launch requires `CONSISTENT=YES`.** All three values must be identical.

---

## 6. TLS / HTTPS Hostname and SNI Validation (No Weakening)

| Rule | Requirement |
|---|---|
| Request URL | Candidate and bundled SDK must use `https://api.deepseek.com/...`, not `https://<ip>/...` |
| SNI | Client hello Server Name Indication = `api.deepseek.com` |
| Certificate validation | **Full chain verification enabled**; standard CA trust store inside sandbox or runner-controlled bundle |
| Hostname check | Certificate must be valid for `api.deepseek.com` (or matching SAN) |
| Forbidden bypasses | No `--insecure`, no custom `SSL_VERIFY_NONE`, no `NODE_TLS_REJECT_UNAUTHORIZED=0`, no pinned cert substitution unless explicitly added in a future amended gate |
| OpenAI-compatible base URL | Remains `https://api.deepseek.com` per A-05; env `DEEPSEEK_API_KEY` only |

Static inspection noted the candidate recognizes `api.deepseek.com` and
`*.api.deepseek.com` (`M3A_ACQUISITION_EVIDENCE.md` §6.3). Phase 16 binds
**only** `api.deepseek.com` unless a future amendment expands the allowlist.

The hosts map provides address stability; **TLS still validates the hostname**,
preserving SNI semantics.

---

## 7. Stop Conditions (Immediate NO-GO)

The credential-and-execution slice must **abort before launch** if any condition
applies:

| ID | Condition | Action |
|---|---|---|
| **D-01** | Resolution exit non-zero or empty result | **STOP** — record failure; do not launch |
| **D-02** | **Multiple distinct IPv4** answers for `api.deepseek.com` | **STOP** — DNS ambiguity |
| **D-03** | **Zero** IPv4 answers | **STOP** |
| **D-04** | Only AAAA/IPv6 answers, no acceptable IPv4 | **STOP** |
| **D-05** | `FRESH_IPV4`, `HOSTS_IPV4`, and `NFT_IPV4` not all equal | **STOP** — hosts/firewall mismatch |
| **D-06** | Hosts file contains more than one `api.deepseek.com` line or wrong IP | **STOP** |
| **D-07** | nft allowlist permits any destination other than `BOUND_IPV4/32:443` | **STOP** |
| **D-08** | Candidate sandbox can resolve or TCP-connect to a **non-allowlisted** probe host (repository-controlled negative test) | **STOP** |
| **D-09** | Pre-launch repository-controlled HTTPS probe to `https://api.deepseek.com/` fails TLS or TCP while binding claims GO | **STOP** |
| **D-10** | Bound IPv4 differs from egress-proof reference **and** triple-consistency passes but operator policy requires human acknowledgment of CDN drift | Record drift in `summary.env`; **STOP until human re-accepts** if IP changed since last successful binding in the same campaign day |

**D-10 note:** IP rotation alone is not automatically a failure if D-01–D-09 pass.
The gate requires fresh binding, not a static pin to `3.173.21.63`. Campaign
operators must treat unexpected provider infrastructure changes as review events.

Any **STOP** invalidates the launch attempt for that session. Credentials must
not be injected and the upstream binary must not start.

---

## 8. Mandatory Pre-Launch Sequence (Credential-and-Execution Grant)

When the human issues the separate credential-and-execution grant, the slice
must execute these steps **in order**, **immediately before** first Qwen Code
launch, with no intervening network policy drift:

```text
1. Host-side resolution (§4) → record resolution.env
2. Build sandbox-only hosts file (§5.2) → record hosts-injection.txt
3. Create ephemeral netns + slirp4netns + nft allowlist to BOUND_IPV4 (§5.3)
4. Triple-consistency check (§5.4) → record consistency-check.env
5. Repository-controlled negative probe: non-allowlisted HTTPS blocked
6. Repository-controlled positive probe: https://api.deepseek.com/ reachable
   with valid TLS (unauthenticated HTTP 401 acceptable, same as egress proof)
7. Assemble Bubblewrap launch with ro-bind injected /etc/hosts and netns join
8. Inject DEEPSEEK_API_KEY via env allowlist only (C-04 / A-07)
9. Launch upstream Qwen Code binary (A-02 path) under owner-locked argv (A-03)
10. Record BINDING_VERDICT=GO in summary.env only after steps 1–7 pass
```

Steps 5–6 may reuse the egress-proof probe scripts from the artifact root
without rerunning the full M3 egress-proof grant evidence collection.

If any step fails, **do not** proceed to step 8–9.

---

## 9. Relationship to Completed Evidence

| Prior gate | Status | This gate |
|---|---|---|
| M3 egress proof (C-01/C-02 / A-13) | **Complete 2026-07-23** | Reuse architecture; do **not** rerun proof grant |
| M3a license clearance (C-05 / A-12) | **Owner approved 2026-07-23** | Not a launch blocker |
| A-02 / A-03 invocation | **Resolved at M3a** | Still require owner argv lock before step 9 |
| DNS binding | **Executed** (sessions in `M3_QUALIFICATION_EVIDENCE.md`, including `m3q-20260723T164355Z-c421b379` binding sequence **GO**) | Mandatory at each launch |

---

## 10. GO / NO-GO for Later Credential-and-Execution Grant

### 10.1 DNS binding design publication (this document)

**GO — complete (docs-only).** The deterministic sandbox-only resolution path,
hosts injection, nft binding, TLS preservation, and stop conditions are locked.

### 10.2 Credential-and-execution slice (not yet authorized)

**GO to authorize when the human issues that grant.** Remaining requirements
before first launch:

1. Execute §8 pre-launch binding sequence successfully for the session.
2. Create dedicated Phase 16 DeepSeek sub-key; inject `DEEPSEEK_API_KEY` only
   (C-04 / A-07 / A-09).
3. Owner lock of smoke argv policy: `--approval-mode`, TASK_CORPUS ceilings,
   output format, smoke task id (A-03).
4. Isolation re-check (`processLaunchEnabled` remains denied until qualification
   grant).
5. USD 1 M3 smoke cost ceiling and USD 3 cumulative cap (C-03 / A-08).
6. Reuse provider-restricted egress enforcement equivalent to M3 egress proof
   **plus** this DNS binding gate.

Egress proof and license clearance are **no longer blockers**. DNS binding
execution, credentials, argv lock, isolation re-check, and explicit qualification
grant remain blockers.

**Until that grant and §8 pass:** do **not** create credentials, do **not** call
authenticated provider APIs, and do **not** launch Qwen Code.

---

## 11. Cross-References

- `M1_AMENDMENT_QWEN_OBSERVATIONAL.md` — A-05, A-13, A-14 (DNS binding)
- `M3_EGRESS_PROOF_EVIDENCE.md` — nft + slirp4netns proof architecture
- `M3A_ACQUISITION_EVIDENCE.md` — hostname recognition; license clearance
- `THREAT_MODEL.md` — §2 egress; §2.1 DNS binding
- `CAMPAIGN_LOCK.md` — §4 provider-restricted egress + DNS binding
- `ISOLATION_EVIDENCE.md` — M3 eligibility
- `CANDIDATE_ARTIFACTS.md` — A-14 field
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 DNS binding gate — defined and published 2026-07-23 (docs-only). No egress
reprobe. No credentials. No provider API calls. No upstream binary launch.*
