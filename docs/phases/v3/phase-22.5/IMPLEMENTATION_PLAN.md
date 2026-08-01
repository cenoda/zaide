# Phase 22.5: Debug Positive Path / NetCoreDbg Host Validation — Implementation Plan

## Status and Authorization

**OPTIONAL; planning only; not implemented.** Phase 22.5 is independent of
22.1–22.4 and is not required for G5 or the V4-reconsider gate. M0 is not
accepted. Execution requires explicit human M0 acceptance and separate
activity-specific approval to provision/use NetCoreDbg and run the smoke.

## A4 Ownership

Phase 22.5 owns A4 package 8, BL-12, `A1-XX-04`, and the positive debug path of
`A1-DB-01`.

Baseline evidence:

- [A4 package ledger](../../../audits/v1-v3-product-reality/A4_GAP_REPORT.md#9-corrective-work-required-before-v4-planning)
- [A2 debugging evidence](../../../audits/v1-v3-product-reality/evidence/A2_DEBUGGING_AND_OUTPUT.md)
- [A3 debugging preflight](../../../audits/v1-v3-product-reality/evidence/A3_DEBUGGING_PREFLIGHT.md)

## M0 — Live-Seam and Host Verification / Plan Acceptance

- [ ] Confirm package 8 remains optional and that no G5 decision depends on it.
- [ ] Verify `DebugAdapterLocator`, `ZAIDE_NETCOREDBG_PATH`, PATH lookup,
  production DI, DAP process lifecycle, build/target resolution, and current
  positive-path test seams.
- [ ] Select a pinned NetCoreDbg artifact/version, source, integrity check,
  license/provenance record, and disposable installation location.
- [ ] Verify host OS/architecture compatibility and ensure the binary is not
  installed into the repository or a real user profile.
- [ ] Define an out-of-tree disposable .NET fixture that supports breakpoint,
  continue/step, stack, scope, variables, output, termination, and cleanup.
- [ ] Define timeout/process-tree cleanup and evidence-loss behavior for runner
  or adapter crashes.
- [ ] Confirm validation-only scope. If live evidence proves a product defect,
  stop and require a separately accepted plan amendment before code changes.
- [ ] Replace command placeholders and receive explicit human M0 acceptance
  plus external-tool execution approval.

## Scope

**Goal:** Supply NetCoreDbg in an isolated host, exercise the existing positive
debug workflow through production DI, and record a truthful `A1-DB-01` result.

**Boundaries:** Default scope is host/tool validation and smoke evidence, not a
debugger rewrite. Negative adapter/build/target behavior remains regression
coverage. The NetCoreDbg binary, fixture, HOME/XDG state, and runtime outputs
remain outside the Zaide repository.

## Non-Goals

- A G5 prerequisite or blocker for V4 reconsideration.
- Committing NetCoreDbg binaries, installer state, or disposable fixtures.
- Running against a real user profile or the Zaide repo as debug target.
- Product code, package, or test changes unless a new plan amendment and
  explicit implementation approval are granted after a proven defect.
- xdtools or manual desktop smoke.

## Milestones

| Milestone | Outcome | Verification gate |
|-----------|---------|-------------------|
| M0 | Optional status, live seams, tool pin/provenance, fixture, cleanup, commands, and stop conditions are accepted | Read-only checklist + human/tool approval |
| M1 | Pinned NetCoreDbg is available in an isolated host and locator/startup checks pass | Integrity, locator, process-lifecycle checks |
| M2 | Positive breakpoint/step/stack/variables/output/termination scenario runs through production DI | Out-of-tree A3 debug producer |
| M3 | `A1-DB-01` result, limitations, cleanup, and regression status are recorded | Evidence review + docs checks |

## Verification Command Placeholders

```bash
<verify pinned NetCoreDbg artifact provenance and checksum>
ZAIDE_NETCOREDBG_PATH=<isolated-path> dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<debug locator/lifecycle filter>"
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
<out-of-tree A3 debug producer with isolated NetCoreDbg, disposable HOME/XDG/workspace/fixture>
git diff --check
```

M0 must replace placeholders with pinned, reviewable commands. Any network,
download, or external-tool execution requires the then-applicable explicit
authorization.

## Exit Conditions

- [ ] M0 and activity-specific approvals are recorded.
- [ ] Tool provenance, integrity, compatibility, and cleanup are evidenced.
- [ ] The positive debug path has an observed A3 result through production DI.
- [ ] Failures remain truthful; a runner crash is evidence loss, not misconduct
  or product success.
- [ ] No repository fixture, real-profile state, binary, or package change is
  introduced.
- [ ] Phase 22 G5 status is unchanged by this optional result.

## Rollback Note

Remove the isolated host/fixture directory and unset the scenario-specific
NetCoreDbg path. If a separately approved amendment later introduces repository
changes, it must define its own coherent revert commit. Never delete a shared or
user-managed debugger installation.
