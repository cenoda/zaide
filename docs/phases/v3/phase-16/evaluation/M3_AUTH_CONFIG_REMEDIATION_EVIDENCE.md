# Phase 16 M3 — Auth Configuration Remediation Evidence

**Status:** **Complete (remediation only)** — static inspection of pinned Qwen Code
v0.20.1 under the phase artifact root; **no** credential creation, reading,
injection, or deletion; **no** API calls, egress reprobe, Qwen launch,
artifact acquisition, M4 work, or comparative/quality claims; **not** a
qualification retry.

**Prior qualification at remediation time:** session
`m3q-20260723T151512Z-6996af5f` was **NO-GO** at auth-type. **Post-remediation
qualifications (separate grants):** session `m3q-20260723T164355Z-c421b379`
used this locked argv/modelProviders contract and was **NO-GO** at Qwen max
session turns under the then-locked 5-turn ceiling (`qwen_exit=53`). Latest
session `m3q-20260724T035603Z-2c06e1a4` used the locked 12-turn contract with
the same auth wiring and cleared the auth-type failure mode (`qwen_exit=0`
plan-only). Latest session `m3q-20260724T054307Z-481ad1de` used write-capable
yolo + the same auth wiring; TC-T01 rename verified but **`qwen_exit=55`**
(wall 60s); overall **NO-GO** — see `M3_QUALIFICATION_EVIDENCE.md`.

---

## 1. Scope

| Authorized | Performed |
|---|---|
| Static inspection of already acquired pinned Qwen v0.20.1 bundle | **Yes** |
| Repository orchestrator + policy + fixture wiring remediation | **Yes** |
| Focused Phase 16 tests for argv/modelProviders contract | **Yes** |
| Qualification retry / new grant / credential handling | **No** |

---

## 2. Discovered Non-Interactive Auth Contract (A-07 compliant)

Qwen Code v0.20.1 headless mode requires an explicit auth type
(`validateNonInterActiveAuth-J2DM4LWZ.js`; prior smoke JSON error). DeepSeek is
**not** a separate `--auth-type` choice; the bundled DeepSeek preset uses the
OpenAI-compatible protocol (`deepseek.ts` via `chunk-GEB2CGUV.js`):

| Preset field | Locked campaign value |
|---|---|
| `protocol` / `--auth-type` | `openai` |
| `baseUrl` | `https://api.deepseek.com` |
| `envKey` | `DEEPSEEK_API_KEY` |
| model id | `deepseek-v4-flash` |

`AUTH_ENV_MAPPINGS.openai` maps only `OPENAI_API_KEY` / `OPENAI_BASE_URL`
(`chunk-5G2SK2OO.js`). With **only** `DEEPSEEK_API_KEY` supplied (A-07), the
runtime must declare a `modelProviders.openai[]` entry whose `envKey` is
`DEEPSEEK_API_KEY` so `resolveModelConfig` reads the sandbox allowlisted
credential (`model-providers.md`; `chunk-GEB2CGUV.js` `resolveModelConfig`).

### 2.1 Locked smoke argv tail (policy + orchestrator)

Executable remains the pinned M3a binary path. Prompt (`-p`) is task-specific and
recorded separately. Policy-locked tail:

```text
--auth-type openai
--openai-base-url https://api.deepseek.com
--approval-mode yolo
--model deepseek-v4-flash
--output-format json
--max-session-turns 12
--max-wall-time 120s
```

**Turn ceiling note:** remediation originally locked auth argv with
`--max-session-turns 5`. Repository policy was later aligned to **12** turns
(human-approved 2026-07-24 policy alignment; **not** a qualification retry).
**Wall-time note:** auth remediation originally documented **60s**; active lock
is **120s** after wall-time + reap remediation
(`M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md`). **USD 1** smoke / **USD 3**
cumulative caps remain unchanged. Environment allowlist (unchanged):
**`DEEPSEEK_API_KEY` only**.

**Approval-mode note:** this auth remediation originally shipped with
`--approval-mode plan`. **Write-capable remediation (2026-07-24)** replaced
plan-only with `--approval-mode yolo` for mutation-required TC-T01 future
retry (`M3_WRITE_CAPABLE_REMEDIATION_EVIDENCE.md`). Auth-type / base-url /
modelProviders contract is unchanged.

### 2.2 Locked workspace modelProviders wiring (fixture)

Materialized TC-T01 workspace includes `.qwen/settings.json`:

```json
{
  "modelProviders": {
    "openai": [
      {
        "id": "deepseek-v4-flash",
        "envKey": "DEEPSEEK_API_KEY",
        "baseUrl": "https://api.deepseek.com"
      }
    ]
  }
}
```

No credential values are stored in settings; credentials remain env-only per
A-07.

---

## 3. Repository Changes

| Path | Change |
|---|---|
| `tools/Phase16NativeHarnessEvaluation/Phase16M3QualificationPolicy.cs` | Auth/service constants; `BuildLockedSmokeArgvTail()` |
| `tools/Phase16NativeHarnessEvaluation/Scripts/m3-qualification-smoke.sh` | Emit/run locked auth argv |
| `tools/Phase16NativeHarnessEvaluation/Fixtures/TC-T01/workspace/.qwen/settings.json` | `modelProviders` DeepSeek envKey wiring |
| `tests/Zaide.Tests/Phase16Evaluation/Phase16M3QualificationPolicyTests.cs` | argv contract tests |
| `tests/Zaide.Tests/Phase16Evaluation/Phase16M3QualificationModelProvidersTests.cs` | fixture settings contract test |

---

## 4. Verification (this slice)

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
git diff --check
```

Qualification smoke execution remains **out of scope** for this remediation
slice.

---

## 5. Cross-References

- `M3A_ACQUISITION_EVIDENCE.md` — `--auth-type` support surface; DeepSeek preset
- `M3_QUALIFICATION_EVIDENCE.md` — prior NO-GO (auth-type missing)
- `M1_AMENDMENT_QWEN_OBSERVATIONAL.md` — A-05/A-06/A-07 locks
- `CAMPAIGN_LOCK.md` — campaign status

---

*M3 auth-configuration remediation — static evidence only; not a qualification
retry.*
