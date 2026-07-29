# Phase 21 M3 — Usage and Cost Evidence

**Milestone:** M3 — Usage and cost evidence ledger
**Depends on:** M1 published at `4db83202` (`feat(phase-21): establish M1 durable record storage foundation`)
**Status:** Complete; published; verification gates pass with zero failures.

M3 preserves and presents truthful usage/cost evidence without converting
missing or backend-reported data into false billing certainty. The usage
ledger distinguishes reported, measured, calculated, estimated, invoiced,
unavailable, and disputed values. Cost is never defaulted to zero when
pricing or billing evidence is absent.

---

## 1. Outcome and ownership decision

| Decision | M3 lock |
|----------|---------|
| Usage capture sink | `AgentUsageCaptureSink` (application) over the M1 `IAgentDurableRecordStore` |
| Usage inspection | `IAgentUsageInspector` over the M1 Usage record class; read-side only |
| Backend evidence adapters | `NativeHarnessAgentUsageSource` and `AcpAgentUsageSource` — narrow, produce neutral usage inputs, never share backend-private internals |
| Presentation | `AgentUsageAvailabilityProjection` (state), `AgentUsageAvailabilityState` (observable), `AgentUsageInspectionViewModel` (entry point) — read-only seam for the existing Townhall/Agents presentation |
| Storage route | M1 Usage record class (`AgentDurableRecordClass.Usage`) under the workspace-isolated `{config}/agents-durable/{wsKey}/` partition |
| Composition root | `AgentsServiceCollectionExtensions.AddZaideAgents`; the M1 store flush on shutdown persists usage records |
| Architecture ratchet | `Phase21UsageRatchetTests` — zero-cost guard, feature ownership, backend-private isolation, no Conversation store coupling, usage kind/value-origin completeness |

The owner is `Zaide.Features.Agents.Application.Transparency.Usage.*` and
the M1-approved persistence adapter. The usage sink never writes to the
conversation store and never defaults missing cost to zero.

---

## 2. Usage evidence ledger

```text
Backend evidence (Native Harness, ACP) [future wiring]
   ↓ neutral AgentUsageCaptureRequest via IAgentUsageBackendEvidenceSource
AgentUsageCoordinator.TrySubmit
   ↓ validation, origin check
AgentUsageCaptureSink.TrySubmit
   ↓ zero-cost guard (reject Reported/Estimated/Invoiced cost at zero)
   ↓ idempotency key
AgentUsageCaptureSink.Admit
   ↓ typed JSON payload wrap
IAgentDurableRecordStore.TryAppend (Usage class)
   ↓ M1 durable partition
{config}/agents-durable/{wsKey}/records/Usage/{seq}_{recordId}.json
```

| Stage | Guarantee |
|-------|-----------|
| Backend source | Produces neutral usage evidence only. Never references `NativeHarnessLoopRunner`, `INativeHarnessProviderTransport`, `AcpAgentSessionAdapter`, `IAcpSessionClient`, or other backend-private types |
| Zero-cost guard | Cost entries (`EstimatedCost`, `InvoicedCost`, `TotalCost`) with zero value and non-Unavailable origin are rejected as `InvalidRequest`. Unavailable origin is the only path for missing cost |
| Value origins | `Reported`, `Measured`, `Calculated`, `Estimated`, `Invoiced`, `Unavailable`, `Disputed` — each explicitly recorded in the payload |
| Pricing provenance | Optional `PricingSourceId`, `PricingSourceVersion`, `PricingFormula`, `Currency`, `PricingEffectiveTime`, `RoundingDecimals`, `Uncertainty` preserved per entry when a calculation or estimate is recorded |
| Idempotency | Duplicate `idempotencyKey` within the same Usage class returns `DuplicateIgnored` (M1 store behavior) |
| Capture states | Capture is either enabled or disabled. All admitted entries are persisted or duplicate-ignored. No backpressure mechanism is needed — usage data is low-volume and writes are synchronous |
| Agent event pipeline | Usage writes are synchronous but nonblocking at the store level. The agent event pipeline is not delayed by usage capture |

---

## 3. Usage value taxonomy

### 3.1 Usage kinds (`AgentUsageKind`)

| Kind | Meaning | Typical unit |
|------|---------|--------------|
| `TokensInput` | Input/prompt tokens consumed | `count` |
| `TokensOutput` | Output/completion tokens produced | `count` |
| `TotalTokens` | Sum of input and output tokens | `count` |
| `EstimatedCost` | Cost estimated from token counts and a pricing catalog | `USD`, `EUR` |
| `InvoicedCost` | Cost reported by the provider's invoice or billing API | `USD`, `EUR` |
| `TotalCost` | Aggregated cost from one or more sources | `USD`, `EUR` |
| `RequestCount` | Number of API or execution requests | `count` |
| `LatencyMs` | Observed or reported latency in milliseconds | `ms` |
| `Other` | Custom metric not covered by the standard taxonomy | user-defined |

### 3.2 Value origins (`AgentUsageValueOrigin`)

| Origin | Meaning |
|--------|---------|
| `Reported` | Value as reported by the backend or provider, not independently verified |
| `Measured` | Value measured locally by Zaide (e.g., wall-clock latency, locally counted tokens) |
| `Calculated` | Value computed from other usage metrics and a versioned formula/pricing source |
| `Estimated` | Value estimated from available evidence with explicit uncertainty |
| `Invoiced` | Value from a provider invoice or billing statement |
| `Unavailable` | Value cannot be determined; missing evidence is not zero |
| `Disputed` | Value is contested; the discrepancy between reported and expected is recorded |

---

## 4. Zero-cost guard

The capture sink enforces a **never-zero-cost** rule:

- Cost-kind entries (`EstimatedCost`, `InvoicedCost`, `TotalCost`)
  submitted with `Value == 0` and an origin other than `Unavailable`
  are **rejected** as `InvalidRequest`.
- Cost entries with `Origin == Unavailable` are **admitted** with
  `Value == 0` — the origin marker truthfully indicates that the cost
  evidence is missing rather than confirmed zero.
- Non-cost entries (token counts, request counts, latency) with zero
  values are admitted normally.

This rule prevents the ledger from silently recording `$0.00` as a
confirmed cost fact when the actual cost is simply unknown.

---

## 5. Pricing provenance

When a cost is calculated or estimated, the caller may supply:

| Field | Purpose |
|-------|---------|
| `PricingSourceId` | Canonical name of the pricing catalog or provider pricing page |
| `PricingSourceVersion` | Version or revision of the pricing catalog used |
| `PricingFormula` | Human-readable formula showing the calculation (e.g., `input_tokens * 0.00001 + output_tokens * 0.00003`) |
| `Currency` | ISO 4217 currency code |
| `PricingEffectiveTime` | When the pricing source was effective |
| `RoundingDecimals` | Number of decimal places to which the value was rounded |
| `Uncertainty` | Estimated uncertainty/error margin for the value |

These fields are optional. When absent, the value is presented as-is
without claiming verified pricing.

---

## 6. Correction and dispute

| Concern | M3 behavior |
|---------|-------------|
| Correction | A corrected entry is submitted as a new record with the corrected value and origin. The original entry is not modified or deleted. Correction reason may be supplied through the `EvidenceSourceDescription` field |
| Dispute | A disputed entry uses `AgentUsageValueOrigin.Disputed`. The original reported value is preserved alongside the dispute marker |
| Retention | M1 default retention for Usage class: 365 days |
| Export | No separate export API in M3; records are readable through `IAgentUsageInspector` |
| Delete | M1 partition-level deletion applies. Individual usage record deletion is not implemented at M3 |
| Backup | M1 `index.json.lastknowngood` covers the partition. Redacted usage payloads are what gets backed up |
| Migration | M1 migration (backup-before-migration) covers Usage records. No M3-specific migration |
| Replay | Usage records are replayable through the M1 `Replay` API by class and ordering sequence |
| Duplicate | Idempotent append is safe; duplicate payload changes are ignored |
| Workspace isolation | Usage records are partitioned by the M1 workspace storage key |

---

## 7. Backend evidence adapters

### 7.1 `NativeHarnessAgentUsageSource`

- Exposes kinds `TokensInput`, `TokensOutput`, `TotalTokens`,
  `EstimatedCost`, `TotalCost`, `RequestCount`, `LatencyMs`.
- Refuses `Other` and unlisted kinds with `InvalidRequest`.
- Uses `AgentBackendIds.NativeHarnessValue` as backend identity.

### 7.2 `AcpAgentUsageSource`

- Exposes kinds `TokensInput`, `TokensOutput`, `TotalTokens`,
  `EstimatedCost`, `TotalCost`, `RequestCount`, `LatencyMs`.
- Refuses `Other` and unlisted kinds with `InvalidRequest`.
- Uses `AgentBackendIds.AcpValue` as backend identity.

Both adapters are independent sibling backends. Neither references
the other's private types. Both route through
`AgentUsageBackendEvidenceSourceWriter` for consistent coordinator
submission.

---

## 8. Inspection entry point

The M3 presentation surface is read-only and admits only the existing
Agents and Townhall presentation patterns:

- `AgentUsageAvailabilityProjection` (presentation layer) — timer-driven
  (5-second period) projection that publishes
  `AgentUsageAvailabilityState` (capture enabled, total records, total
  cost value/currency, counts by origin).
- `AgentUsageInspectionViewModel` (presentation layer) — lightweight VM
  delegating to coordinator and availability projection.
- The existing Townhall and Agents presentation consume the projection
  state for "usage availability" and "cost summary" labels.

The M3 scope does not change the visual design, the existing settings
window, or the conversation projection.

---

## 9. M3 behavior checklist

| Required behavior | M3 evidence |
|-------------------|-------------|
| Retain original metrics, units, evidence source, backend, model where reported, run/session attribution | `AgentUsageCaptureRequest` carries `MetricName`, `Unit`, `Value`, `BackendId`, `Model`, `Scope` (conversation/session/run/backend); the M1 Usage envelope preserves scope references |
| Distinguish reported, measured, calculated, estimated, invoiced, unavailable, and disputed | `AgentUsageValueOrigin` enum with all seven values; each record origin is preserved in the JSON payload |
| Retain versioned pricing source and formula for calculations | `PricingSourceId`, `PricingSourceVersion`, `PricingFormula` fields on request and record |
| Preserve currency, effective time, rounding, and uncertainty | `Currency`, `PricingEffectiveTime`, `RoundingDecimals`, `Uncertainty` fields on request and record |
| Define correction/dispute behavior | Correction = new record with corrected value; dispute = `Disputed` origin |
| Define retention, export, delete, backup, migration, replay, duplicate, workspace-isolation behavior | M1 coordinator provides retention metadata; M1 store provides replay/idempotency/workspace isolation |
| Never default missing cost to zero | `AgentUsageCaptureSink` rejects zero cost with non-Unavailable origin |
| Do not label backend/provider claims as Zaide-verified without evidence | `Origin.Reported` indicates backend-reported; `Origin.Measured` indicates locally measured; `Origin.Calculated` indicates formula-based with source |
| Preserve M1 record ownership and M2 redaction boundaries | Usage data is stored in its own `AgentDurableRecordClass.Usage` partition; trace redaction is unchanged |
| Keep Native Harness and ACP as independent sibling backends | Both adapters registered independently through `IAgentUsageBackendEvidenceSource` |
| Capability snapshot mapping for truthful usability/degradation | Backend adapters use `CanExpose(kind)` to refuse unsupported kinds |

---

## 10. Tests and ratchets

### 10.1 Test files

| Test file | Surface |
|-----------|---------|
| `tests/Zaide.Tests/Features/Agents/Transparency/Usage/Phase21UsageTestSupport.cs` | Shared fixtures (temp workspace, store, sink, coordinator, requests) |
| `tests/Zaide.Tests/Features/Agents/Transparency/Usage/Phase21UsageLifecycleTests.cs` | Capture lifecycle: disabled, admitted, zero-cost guard, duplicate, ordering, class separation, scope references |
| `tests/Zaide.Tests/Features/Agents/Transparency/Usage/Phase21CostEvidenceTests.cs` | Cost preservation: currency, pricing source, origin distinction, disputed, unavailable, summary aggregation |
| `tests/Zaide.Tests/Features/Agents/Transparency/Usage/Phase21UsageCalculationTests.cs` | Calculation: formula/source version, measured latency, model attribution, summary grouping by origin |
| `tests/Zaide.Tests/Features/Agents/Transparency/Usage/Phase21UsageBackendAdapterTests.cs` | Native Harness and ACP sources: backend identity, exposed kinds, rejection, token/cost submission, pricing info, sibling independence |

### 10.2 Architecture and ratchet updates

- `ArchitectureInventoryReader.M0TotalTopLevelTypes`: `854 → 875` (+21 usage evidence types).
- `ArchitectureInventoryReader.M0InternalTopLevelTypes`: `503 → 524`.
- `PublicProductionTypeBaseline.TotalTopLevelTypes` and
  `InternalTopLevelTypes` aligned to the same values.
- `ArchitectureInventoryTests` adds per-namespace expectations for
  `Zaide.Features.Agents.Domain.Transparency.Usage (9, 0, 9)`,
  `Zaide.Features.Agents.Contracts.Transparency.Usage (3, 0, 3)`,
  `Zaide.Features.Agents.Application.Transparency.Usage (6, 0, 6)`,
  and raises `Presentation.Transparency` to `(6, 0, 6)`,
  source file count to `761`, and Features folder count to `716`.
- `Phase21UsageRatchetTests` enforces: zero-cost guard, feature ownership,
  M1 Usage class routing, no conversation coupling, backend-private
  isolation, no trace namespace coupling, positive limits, and
  complete value-origin enum.

### 10.3 M3 verification gates (exact commands)

```bash
git diff --cached --check
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21Usage|FullyQualifiedName~Phase21Cost"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

---

## 11. M3 limitations preserved

- Usage capture is not silently enabled by display settings; it is enabled
  by composition.
- No pricing catalog, hosted pricing service, network lookup, or external
  activity is added. Pricing source fields are user-supplied evidence fields,
  not fetched or resolved values.
- The M3 surface does not change the visual design, the existing settings
  window, or the conversation projection.
- No export, delete, backup, or migration API specific to usage records is
  added; M1 covers these at the partition level.
- Backend adapters are admitted in composition; the M3 implementation does
  not yet wire Native Harness and ACP execution paths to push usage
  evidence. That wiring is the responsibility of future milestones.
- Cross-workspace usage aggregation is not implemented; usage records are
  isolated by the M1 workspace storage key.
- Encryption at rest is not selected at M3.
- The legacy allowlist is unchanged; no new LocatorSite or root admission
  is admitted by M3.

---

## 12. Rollback

If M3 must be reverted:

1. Disable usage capture at the composition root (no submissions reach
   the sink).
2. Revert the single M3 commit.
3. Quarantine M3 usage records whose schema is no longer readable. Never
   rewrite them as zero or verified.
4. Conversation persistence, settings, trace records, and audit evidence
   are unaffected.

---

## 13. Exit

Usage and cost evidence is preservable and inspectable for supported
backends. Values are distinguishable by origin and provenance. Missing
cost is never defaulted to zero. Backend claims are labeled as claims
without Zaide-verification. M1 record ownership and M2 redaction
boundaries are preserved. Native Harness and ACP remain independent
sibling backends. M4–M7 remain not started and not authorized.
