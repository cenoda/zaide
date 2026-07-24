# Phase 16 M3 — Fresh-Session Eligibility Remediation Evidence

**Status:** **Complete (remediation only)** — policy, orchestrator, and focused tests;
**no** credential creation, reading, injection, or deletion; **no** API calls, egress
reprobe, Qwen launch, artifact acquisition, M4 work, or comparative/quality claims;
**not** a qualification retry.

**Prior latest qualification session (unchanged):**
`m3q-20260724T060109Z-45dd1c5f` — **NO-GO** under then-locked **12** turns /
**120s** wall (`qwen_exit=53` after verified TC-T01 rename).

---

## 1. Defect

An authorized fresh **24-turn** qualification grant consumed a dedicated one-shot
sub-key (~2026-07-24T06:31:49Z / host 15:31 JST) but **did not** produce a new
`m3-qualification` session record and **did not** launch Qwen. Operator-facing
outcome incorrectly reused the historical **12-turn** session
`m3q-20260724T060109Z-45dd1c5f` verdict (`qwen_exit=53`) instead of recording a
fresh-session **no provider execution** result.

Root causes:

1. **Credential-before-preflight ordering** — the orchestrator consumed the
   one-shot file before slirp/egress preflight completed, so avoidable aborts
   could burn a key without candidate launch.
2. **No fresh-session execution contract** — policy/tests did not require each
   grant to evaluate a new session ID or forbid substituting historical
   `qwen_exit` / task results for the current session.
3. **Missing consumed-but-unlaunched evidence shape** — when a key was consumed
   without provider execution, evidence did not consistently record
   `no candidate launch / no provider execution` and `qwen_exit_source=none`.

Historical **NO-GO** under the prior **12**-turn ceiling must inform preflight
requirements only; it must **not** block or substitute for a fresh **24-turn**
grant.

---

## 2. Remediation

| Area | Change |
|---|---|
| `Phase16M3FreshSessionPolicy.cs` | Fresh-session preflight; historical hints informative-only; evidence validator forbids historical `qwen_exit` substitution |
| `Phase16M3QualificationPolicy.cs` | Cross-reference fresh-session policy |
| `Scripts/m3-qualification-smoke.sh` | New session ID + `execution.env`; egress preflight **before** credential; `stop_with_no_provider_execution`; `resolve_current_session_qwen_exit` reads **current** `$RUN_DIR` only |
| `Phase16M3FreshSessionEligibilityTests.cs` | Policy + orchestrator contract regressions |
| `Phase16M3QualificationSmokeOrchestratorTests.cs` | Egress-before-credential ordering regression |

### 2.1 Orchestrator ordering (locked)

```text
grant → tools → DNS → workspace → bwrap precheck
  → egress preflight (unshare+slirp; no credential)
  → one-shot credential load + balance-before
  → provider launch attempted → Qwen inner pass
  → finalization (current-session qwen_exit only)
```

### 2.2 Consumed-but-unlaunched evidence (required fields)

When the one-shot key is consumed but Qwen does not run:

```text
provider_launch_attempted=NO
candidate_execution=NO
qwen_exit_source=none
provider_execution_label=no candidate launch / no provider execution
```

No `qwen_exit` from a prior session may appear in the current session record.

---

## 3. Operator-observed event (this remediation slice)

| Field | Recorded value |
|---|---|
| Event | One-shot sub-key file deleted from artifact credentials dir |
| Observed at (host) | ~2026-07-24T15:31:49+09:00 (`credentials/` mtime) |
| Matching session record | **None** under `records/m3-qualification/` |
| Qwen launched | **No** |
| Historical verdict reused in reporting | **Yes** (incorrect — cited `m3q-20260724T060109Z-45dd1c5f` / exit 53) |
| Remediation | Orchestrator/policy fixes above; future grants must always emit a new session ID and the no-provider-execution label when applicable |

No credential value or prefix was logged. This slice did not re-run smoke.

---

## 4. Verification (this slice)

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
git diff --check
```

---

## 5. Cross-References

- `M3_QUALIFICATION_EVIDENCE.md` — historical 12-turn NO-GO session (unchanged)
- `CAMPAIGN_LOCK.md` — active **24**-turn policy for future retry
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 fresh-session eligibility remediation — static/policy/orchestrator only; not a
qualification retry.*
