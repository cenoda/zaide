# Phase 16 M3 — Provider-Restricted Egress Proof Evidence

**Status:** Completed under explicit human M3 egress-proof grant (2026-07-23).
**Scope limited to C-01(b)/C-02 proof for `api.deepseek.com:443` only.** No
credentials, no authenticated DeepSeek API, no monetary spend, no Qwen Code or
other upstream binary launch, no production code change, no commit/push.

**Campaign path:** single-candidate observational only
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`). OpenCode and Grok Build remain blocked
at M1.

**Artifact root (outside Zaide repository):**

```text
/tmp/phase16-artifacts/phase-16/records/egress-proof/
  summary.env
  c01b-change-record.txt
  commands-ledger.txt
  host-inventory.txt
  probe/                 # repository-controlled probe + nft allowlist scripts
  run/                   # timestamps, exit codes, redacted probe logs, nft rules
```

---

## 1. Scope and Non-Effects

| Allowed in this grant | Performed |
|---|---|
| Inventory host egress tooling | Yes |
| Install/configure minimum C-01(b) host tooling if required | Inventory only — **no package install** (tools already present) |
| Ephemeral netns + allowlist configuration for proof | Yes |
| Repository-controlled HTTPS probe (allow + block) | Yes |
| Preserve commands, exit codes, timestamps, redacted logs under artifact root | Yes |

| Forbidden in this grant | Status |
|---|---|
| Launch Qwen Code / any upstream binary | **Not performed** |
| Create, read, inject, or log credentials | **Not performed** |
| Call DeepSeek authenticated API / spend money | **Not performed** (unauthenticated TLS only; HTTP 401) |
| Modify `src/`, production DI, UI, or tests | **Not performed** |
| Alter Zaide checkout except Phase 16 evidence/status docs | **Docs-only updates** |
| Commit or push | **Not performed** |

---

## 2. Host inventory and C-01(b) change record

Full inventory: artifact `host-inventory.txt` / `c01b-change-record.txt`.

| Tool | M0/M2b recorded | At M3 egress-proof start | Action taken |
|---|---|---|---|
| Bubblewrap | 0.11.2 present | 0.11.2 present | None |
| `slirp4netns` | **absent** | **1.3.4 present** | **No install** (already present) |
| `socat` | **absent** | **1.8.1.3 present** | **No install** (already present) |
| `pasta` | absent | **absent** | Not installed (not required) |
| `nft` / `iptables` | (not required at M2b) | present | Ephemeral netns rules only |
| `curl` | present | 8.21.0 present | Probe client |

**Exact durable host change under C-01(b):** **NONE.**

- No package install/remove
- No persistent sysctl, nftables host ruleset, or firewall service change
- Only ephemeral per-proof configuration inside a process-local user+net
  namespace (destroyed on process exit)

---

## 3. Proof architecture

Repository-controlled probe (artifact root only; not upstream):

1. `unshare --user --map-root-user --net --mount --fork --pid --mount-proc`
2. `slirp4netns --configure --mtu=65520 --disable-host-loopback <ns-pid> tap0`
3. In-netns `nftables` table `inet phase16_egress`:
   - default **drop** on input/output
   - allow loopback + established/related
   - allow **TCP dport 443** only to the resolved IPv4 of `api.deepseek.com`
4. HTTPS probes via host `curl` (no credentials, no auth headers):
   - **Allow:** `https://api.deepseek.com/` with `--resolve` to allowlisted IP
   - **Block:** `https://example.com/` pinned to non-allowlisted `93.184.216.34`
   - **Block (supplemental):** `https://1.1.1.1/`

Allowlisted destination identity (C-02 / A-13): **`api.deepseek.com:443` only.**

Resolved allowlisted IPv4 at proof time: **`3.173.21.63`**
(artifact `deepseek-allow-ips.txt`; DNS also observed CNAME
`d3bbv8sr76az5s.cloudfront.net`).

---

## 4. Results

Proof window (UTC): **`2026-07-23T10:50:30Z` – `2026-07-23T10:50:45Z`**.

| Probe | Destination | curl exit | HTTP | Result |
|---|---|---|---|---|
| Allow | `https://api.deepseek.com/` → `3.173.21.63:443` | **0** | **401** | **PASS** (TLS+HTTPS reachability; unauthenticated) |
| Block | `https://example.com/` → `93.184.216.34:443` | **28** (timeout) | 000 | **PASS** (blocked) |
| Block2 | `https://1.1.1.1/` | **28** (timeout) | 000 | **PASS** (blocked) |

**Allow interpretation:** HTTP 401 body sample `Authentication Fails (governor)`
proves TLS handshake and HTTPS to the allowlisted host without credentials.
This is **not** an authenticated API call and spent **no** money.

**Block interpretation:** non-allowlisted destinations could not connect
(`num_connects=0`, connect timeout). Post-run nft counter showed
**20 packets / 1248 bytes** dropped on the default-deny output path.

**Overall egress proof:** **GO**.

---

## 5. Evidence index

| Path under artifact root | Content |
|---|---|
| `summary.env` | Machine-readable overall verdict |
| `c01b-change-record.txt` | Inventory delta + durable vs ephemeral change |
| `commands-ledger.txt` | Commands, PIDs, exit codes, timestamps |
| `host-inventory.txt` | Tool paths and versions |
| `run/session.env` | Session start/end, PIDs |
| `run/verdict.env` | PASS/FAIL per probe + overall |
| `run/allow-result.env` / `block-result.env` / `block2-result.env` | Per-probe exit + timestamps |
| `run/probe-results/*.meta` | Redacted curl metadata |
| `run/nft-ruleset.txt` / `nft-ruleset-after.txt` | Rules + drop counters |
| `run/netns-interfaces.txt` | tap0 / routes inside netns |
| `run/inner_proof.sh` | Exact inner proof script used |
| `probe/egress_https_probe.sh` | Standalone probe helper |
| `probe/apply_allowlist_nft.sh` | nft allowlist applicator |

No credential material was present in evidence (redaction scan for common
secret patterns: none).

**No upstream bytes and no proof scripts under the Zaide git worktree** except
this docs record.

---

## 6. GO / NO-GO for later grants

### 6.1 M3 egress-proof slice

**GO — complete.** C-02 requirements met:

1. Allowlisted `api.deepseek.com:443` HTTPS succeeded (unauthenticated).
2. Non-allowlisted HTTPS destinations were blocked.
3. Commands, exit codes, timestamps, and redacted logs are preserved under
   `/tmp/phase16-artifacts/phase-16/records/egress-proof/`.

A-13 enforcement is **proven for this host proof architecture** (ephemeral
netns + slirp4netns + nft allowlist). Later real-candidate launch must reuse
equivalent allowlist enforcement **and** execute the DNS binding gate
(`M3_DNS_BINDING_GATE.md`) immediately before launch, not host-wide
unrestricted egress.

### 6.2 Later credential-and-execution grant

**GO to authorize when the human issues that grant.**

Egress is no longer a blocker for authorizing the next external-side-effect
grant. License clearance (C-05) is **complete** (owner approved 2026-07-23).
DNS binding design is **complete** (`M3_DNS_BINDING_GATE.md`). That grant still
must separately cover:

1. Create a **dedicated Phase 16 DeepSeek sub-key** only (C-04 / A-09); never
   ambient `~/.config` credentials.
2. Inject **only** `DEEPSEEK_API_KEY` via sandbox env allowlist (A-07); never
   log or persist credential values.
3. Owner lock of remaining argv policy (`--approval-mode`, TASK_CORPUS
   ceilings, output format, smoke task id).
4. Isolation re-check before first upstream process launch
   (`processLaunchEnabled` remains denied until qualification grant).
5. USD 1 M3 smoke cost ceiling and USD 3 cumulative cap tracking (C-03 / A-08).
6. Reuse provider-restricted egress enforcement equivalent to this proof **and**
   execute DNS binding per `M3_DNS_BINDING_GATE.md` §8 immediately before launch.

**Until that grant is issued and those gates pass:** do **not** create
credentials, do **not** call authenticated provider APIs, and do **not** launch
Qwen Code. Candidate remains **`eligible for later M3 qualification`** but
**not qualified**.

---

## 7. Cross-references

- `M1_AMENDMENT_QWEN_OBSERVATIONAL.md` — C-01/C-02 design acceptance
- `M3A_ACQUISITION_EVIDENCE.md` — acquisition complete; binary not launched
- `THREAT_MODEL.md` — provider-restricted egress requirement
- `CAMPAIGN_LOCK.md` — campaign path and egress design
- `ISOLATION_EVIDENCE.md` — M2b isolation; egress was unproven until this record
- `CANDIDATE_ARTIFACTS.md` — A-13 field
- `M3_DNS_BINDING_GATE.md` — DNS binding gate for candidate launch
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 egress-proof evidence — produced 2026-07-23 under explicit egress-proof
grant. No credentials. No authenticated API spend. No upstream binary launch.*
