# Phase 16 M3 — Write-Capable Qualification Remediation Evidence

**Status:** **Complete (remediation only)** — static inspection of pinned Qwen
Code v0.20.1 under the phase artifact root; repository policy/orchestrator/
tests/docs updated. Remediation itself did **not** create credentials or launch
Qwen. Later authorized write-capable smokes used this lock and are recorded in
`M3_QUALIFICATION_EVIDENCE.md`: `m3q-20260724T054307Z-481ad1de` (**NO-GO** on
exit 55 / 60s wall after verified rename); latest
`m3q-20260724T060109Z-45dd1c5f` (**NO-GO** on exit 53 / 12-turn ceiling after
verified rename under **120s** wall).

**Human decisions (this slice):**

1. Prior session `m3q-20260724T035603Z-2c06e1a4` owner-reported spend is
   **less than USD 0.01** (not a more precise invented figure).
2. A future TC-T01 retry may use Qwen’s supported non-interactive
   **write-capable** mode inside the already locked Bubblewrap sandbox,
   instead of plan-only mode.
3. USD **1** smoke, USD **3** cumulative, **12** turns, and **60s** wall-time
   limits remained locked **for this write-capable slice**. Later human decisions
   raised the **active** turn ceiling to **24** and wall lock to **120s** then
   **240s** (`M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md`,
   `M3_WALL_TIME_240S_POLICY_REMEDIATION_EVIDENCE.md`; see `CAMPAIGN_LOCK.md`);
   historical session records keep the turns/wall used for that run.

---

## 1. Scope

| Authorized | Performed |
|---|---|
| Static inspection of already acquired pinned Qwen v0.20.1 bundle | **Yes** (re-extract under phase artifact root for inspection only; SHA-256 match) |
| Lock write-capable non-interactive argv in policy/orchestrator/tests | **Yes** |
| Fix post-Qwen-exit reaping/timeout so finalization cannot hang | **Yes** |
| Record owner-reported prior spend as less than USD 0.01 | **Yes** |
| Qualification retry / new grant / credential handling | **No** |

---

## 2. Static write-capable argv contract (pinned v0.20.1)

Sources (static only; **no** binary/Node launch):

1. `lib/chunks/chunk-D53NR4GO.js` yargs option registration (excerpt retained
   under `/tmp/phase16-artifacts/phase-16/records/m3-write-capable-remediation/`).
2. `lib/chunks/chunk-WHGVTP7Q.js` `ApprovalMode` enum.
3. `lib/chunks/chunk-6H67XLET.js` headless YOLO safety warning helper.
4. Prior M3a support-surface lock (`M3A_ACQUISITION_EVIDENCE.md` §6.1).

### 2.1 Supported approval modes

| Mode | Semantics (from yargs description) | Non-interactive write? |
|---|---|---|
| `plan` | plan only | **No** (prior NO-GO: 0 lines changed) |
| `default` | prompt for approval | **No** (requires interactive approval surface) |
| `auto-edit` | auto-approve edit tools | Yes (edits only; shell/monitor denied unless allowed) |
| `auto` | LLM classifier auto-approves safe actions | Conditional |
| `yolo` | auto-approve **all** tools | **Yes** |

Boolean `--yolo` / `-y` also selects YOLO mode. **Cannot** combine `--yolo`
with `--approval-mode`; conflict message directs:
`Use --approval-mode=yolo instead.`

### 2.2 Locked contract for future TC-T01 retry

Prefer the structured form (matches existing `--approval-mode` shape; avoids
the forbidden dual-flag combination):

```text
--auth-type openai
--openai-base-url https://api.deepseek.com
--approval-mode yolo
--model deepseek-v4-flash
--output-format json
--max-session-turns 24
--max-wall-time 60s
```

*(Active post-remediation turn ceiling is **24**; wall is **240s** — see
`M3_WALL_TIME_240S_POLICY_REMEDIATION_EVIDENCE.md` and `CAMPAIGN_LOCK.md`. The
block above is the write-capable-slice lock as shipped and used by session
`m3q-20260724T054307Z-481ad1de` with historical 12 turns / 60s wall.)*

| Property | Locked value (this slice) |
|---|---|
| Approval mode | **`yolo`** via `--approval-mode yolo` (not boolean `--yolo`) |
| Host isolation | Existing Bubblewrap sandbox (unchanged) |
| Credential env allowlist | `DEEPSEEK_API_KEY` only (A-07; unchanged) |
| Turn / wall / spend ceilings | **24** (active; historical slice was **12**) / **60s** (this slice; active wall later **240s**) / **USD 1** smoke / **USD 3** cumulative |
| GO criteria | Qwen exit **0** **and** verified TC-T01 `FetchData` → `RetrieveData` |

Headless YOLO emits a safety warning when Qwen’s own sandbox is unset; Phase 16
host Bubblewrap isolation remains the campaign control. No change to provider
egress allowlist or DNS binding rules.

**Artifact integrity re-check (inspection only):** archive SHA-256
`2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e` (pinned match).

---

## 3. Orchestrator post-Qwen-exit reaping fix

Prior defect (session `m3q-20260724T035603Z-2c06e1a4`): after Qwen wrote
`qwen_exit=0`, unbounded `wait` on the unshare child allowed post-Qwen
`dotnet build`/lifecycle hang until an external 300s safety timeout
(`orchestrator_exit=124`), so balance-after, workspace finalization, and cleanup
status were not recorded by the orchestrator.

**Remediation in `Scripts/m3-qualification-smoke.sh`:**

1. **`force_reap_children`** — TERM then KILL unshare process group + slirp;
   idempotent; records `reap.env`.
2. **`wait_inner_with_reap_budget`** — polls until unshare exits, or
   `qwen-result.env` is observed and a **45s** post-Qwen verify budget elapses,
   or overall **120s** inner budget elapses; never unbounded wait.
3. Inner verify uses `timeout 40s` around `dotnet build` / `dotnet test`.
4. **Always-run finalization** after wait/reap: balance-after (when key still in
   memory), host-side TC-T01 workspace counts → `workspace-result.env`,
   cleanup/orphan status, copy of results into session root.
5. **GO** only when `qwen_exit=0` **and** `tc_t01_rename_verified=YES`.

---

## 4. Prior spend (owner-reported)

| Session | Owner-reported spend |
|---|---|
| `m3q-20260724T035603Z-2c06e1a4` | **less than USD 0.01** |

Do **not** invent a more precise USD value. Pre-balance for that session was
USD 3.98; post-balance was unavailable from the hung orchestrator. Token usage
was recorded; USD delta is owner-confirmed only at this precision.

---

## 5. Repository changes

| Path | Change |
|---|---|
| `tools/.../Phase16M3QualificationPolicy.cs` | `AllowedApprovalMode = yolo`; argv tail uses it |
| `tools/.../Scripts/m3-qualification-smoke.sh` | write-capable argv; bounded wait/reap; always finalization; GO gate |
| `tests/.../Phase16M3QualificationPolicyTests.cs` | lock asserts yolo; rejects legacy plan |
| Phase 16 evaluation docs + plan + roadmap status | status/spend/argv sync |

---

## 6. Verification (this slice)

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
git diff --check
```

Qualification smoke execution remains **out of scope**.

---

## 7. Cross-References

- `M3_QUALIFICATION_EVIDENCE.md` — prior plan-only NO-GO; spend note
- `M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md` — auth argv still required
- `M3A_ACQUISITION_EVIDENCE.md` — approval-mode support surface
- `CAMPAIGN_LOCK.md` — campaign status
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 write-capable qualification remediation — static evidence + repository
wiring only; not a qualification retry.*
