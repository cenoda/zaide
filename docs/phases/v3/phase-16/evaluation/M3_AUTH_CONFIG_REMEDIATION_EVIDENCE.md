# Phase 16 M3 — Auth Configuration Remediation Evidence

**Status:** **Complete (remediation only)** — static inspection of pinned Qwen Code
v0.20.1 under the phase artifact root; **no** credential creation, reading,
injection, or deletion; **no** API calls, egress reprobe, Qwen launch,
artifact acquisition, M4 work, or comparative/quality claims; **not** a
qualification retry.

**Prior qualification verdict (unchanged):** session
`m3q-20260723T151512Z-6996af5f` remains **NO-GO** at auth-type
(`M3_QUALIFICATION_EVIDENCE.md`).

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
--approval-mode plan
--model deepseek-v4-flash
--output-format json
--max-session-turns 5
--max-wall-time 60s
```

Environment allowlist (unchanged): **`DEEPSEEK_API_KEY` only**.

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
