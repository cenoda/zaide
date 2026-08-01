# Phase 22.1: Language Intelligence / LSP — Implementation Plan

## Status and Authorization

**M0 accepted at `8856bdf7`; M1 complete.** Implementation authorized for
accepted milestones only.

## A4 Ownership

Phase 22.1 owns A4 package 1, findings BL-01…BL-05, and affected goal rows
`A1-FN-09`…`A1-FN-13`. It has no dependency and may proceed in parallel with
Phase 22.2.

Baseline evidence:

- [A4 package ledger](../../../audits/v1-v3-product-reality/A4_GAP_REPORT.md#9-corrective-work-required-before-v4-planning)
- [A3 language-intelligence evidence](../../../audits/v1-v3-product-reality/evidence/A3_LANGUAGE_INTELLIGENCE.md)
- [A3 consolidation](../../../audits/v1-v3-product-reality/evidence/A3_CLEAN_PROFILE_SMOKE.md#23-file-navigation-and-editing)

## M0 — Live-Seam Verification and Plan Acceptance

M0 was completed read-only against `master` at
`938227b2c2fd743ac8f4d84b30ffdfac0500f6c1`. `HEAD` and freshly fetched
`origin/master` were equal before documentation edits. Detailed findings are in
[M0 live-seam verification](./M0_SEAM_VERIFICATION.md).

- [x] Reconcile and trace the five historical A3 failures against live code
  without re-running A3 or changing the historical evidence file.
- [x] Verify production DI in `LanguageServiceCollectionExtensions` and the
  application/editor ownership of UI scheduling.
- [x] Trace completion from `LanguageCompletionService` through
  `EditorLanguageInputViewModel` and `EditorView`; identify the exact
  thread-affinity boundary before selecting a fix.
- [x] Trace hover through `LanguageHoverService` and the editor hover trigger/
  projection path; distinguish request, timeout, stale-document, and rendering
  failures.
- [x] Trace definition and document/workspace symbol requests through
  `LanguageNavigationService`, `LanguageSymbolService`, LSP parsers, and their
  command/projection owners.
- [x] Verify active-document, cancellation, stale-response, and no-project/
  ambiguous-project contracts remain protected.
- [x] Inventory focused tests and the Phase 10 smoke tools; replace the
  verification placeholders below with exact filters and producer commands.
- [x] Lock milestone and rollback boundaries.
- [x] Receive explicit human G2 / M0 plan acceptance.

The named production seams are verified current. No production class name was
stale; the corrected assumption is that the temporary A3 runner is not retained
in the repository or `/tmp` and must be recreated out-of-tree before M4 smoke.

## Scope

**Goal:** Restore user-observable completion, hover, Go to Definition, document
symbols, and workspace symbols for the affected A3 scenarios.

**Boundaries:** Preserve diagnostics, formatting, format-on-save, editor undo/
dirty/caret behavior, workspace authority, cancellation, and truthful failure
states. Fix the smallest proven runtime seams; do not redesign the language
subsystem or change LSP/provider policy without M0 evidence.

## Non-Goals

- New language features, languages, servers, or package dependencies.
- UI redesign unrelated to the five affected rows.
- Phase 22.2–22.5 work.
- Reclassifying A3 rows from unit tests or source inspection alone.
- Rewriting Phase 10 or A3 historical evidence.

## Milestones

| Milestone | Outcome | Verification gate |
|-----------|---------|-------------------|
| M0 | Live seams, failure mechanisms, commands, boundaries, and rollback are verified; plan accepted | Read-only seam checklist + human acceptance |
| M1 | Completion returns and projects usable items on the correct UI ownership path without stale/cancelled mutation | Focused completion/editor routing tests |
| M2 | Hover reaches a terminal truthful state and shows known-symbol content | Focused hover and editor projection tests |
| M3 | Definition reaches a bounded terminal result; definition plus document/workspace symbols project known fixture results with truthful empty/failure outcomes | Focused navigation/symbol tests |
| M4 | Regression gates pass and `A1-FN-09`…`A1-FN-13` are re-smoked under the A3 contract | Build, fast/serial gates, out-of-tree A3 smoke |

Each milestone is independently reviewable and revertible. M0 proved a shared
unscheduled-observer failure family, but also proved distinct failure points and
presentation contracts, so M1-M3 remain separate.

## Verification Commands

M0 validated the focused filters with `--no-build --list-tests` against the
existing baseline test assembly. Later approved implementation uses these exact
commands.

### M0 DI and composition

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.App.Composition.LanguageRegistrationModuleTests|FullyQualifiedName~Zaide.Tests.Features.Language.DI.LanguageSessionServiceDiTests"
```

### M1 completion and editor routing

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Language.Application.LanguageCompletionTests|FullyQualifiedName~Zaide.Tests.Features.Editor.Presentation.EditorLanguageInputRoutingTests"
```

### M2 hover and editor routing

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Language.Application.LanguageHoverTests|FullyQualifiedName~Zaide.Tests.Features.Editor.Presentation.EditorLanguageInputRoutingTests"
```

### M3 definition, symbols, and editor routing

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Language.Application.LanguageNavigationTests|FullyQualifiedName~Zaide.Tests.Features.Language.Application.LanguageSymbolTests|FullyQualifiedName~Zaide.Tests.Features.Editor.Presentation.EditorLanguageInputRoutingTests"
```

### Preservation and common gates

```bash
dotnet build Zaide.slnx
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Language.Application.LanguageCommandAvailabilityTests|FullyQualifiedName~Zaide.Tests.Features.Language.Application.LanguageDocumentSyncTests|FullyQualifiedName~Zaide.Tests.Features.Language.Application.LanguageDiagnosticsServiceTests|FullyQualifiedName~Zaide.Tests.Features.Language.Application.LanguageFormattingTests"
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
git diff --check
```

Run the fast suite interactively; use serial mode if it fails or hangs. The A3
producer must follow the umbrella [re-smoke contract](../phase-22/IMPLEMENTATION_PLAN.md#re-smoke-contract).

### Retained exploratory service tools

These commands are optional implementation diagnostics only. They do not
compose `EditorView` and cannot close A3 rows.

```bash
dotnet run --project tools/Phase10M4CompletionHoverSmoke/Phase10M4CompletionHoverSmoke.csproj --no-build
dotnet run --project tools/Phase10M5NavigationSymbolsSmoke/Phase10M5NavigationSymbolsSmoke.csproj --no-build
```

### M4 A3 producer

The historical runner source/output was intentionally disposable and is absent
at M0. M4 must first recreate and publish the approved out-of-tree runner to
`/tmp/zaide-a3-lang/out/Release/net10.0/Zaide.Tests.dll`, with its fixture at
`/tmp/zaide-a3-lang/fixtures/workspace`. The exact producer command is then:

```bash
test -f /tmp/zaide-a3-lang/runner/Zaide.Tests.csproj
dotnet restore /tmp/zaide-a3-lang/runner/Zaide.Tests.csproj
dotnet publish /tmp/zaide-a3-lang/runner/Zaide.Tests.csproj --no-restore -c Release -o /tmp/zaide-a3-lang/out/Release/net10.0
test -f /tmp/zaide-a3-lang/out/Release/net10.0/Zaide.Tests.dll
test -d /tmp/zaide-a3-lang/fixtures/workspace
mkdir -p /tmp/zaide-a3-lang/evidence
for scenario in A1-FN-09 A1-FN-10 A1-FN-11 A1-FN-12 A1-FN-13; do
  profile_root="$(mktemp -d /tmp/zaide-a3-lang-profile-XXXXXXXX)"
  mkdir -p "$profile_root/home" "$profile_root/config" "$profile_root/data" "$profile_root/state" "$profile_root/cache"
  cp -a /tmp/zaide-a3-lang/fixtures/workspace "$profile_root/workspace"
  env HOME="$profile_root/home" \
    XDG_CONFIG_HOME="$profile_root/config" \
    XDG_DATA_HOME="$profile_root/data" \
    XDG_STATE_HOME="$profile_root/state" \
    XDG_CACHE_HOME="$profile_root/cache" \
    PATH="/home/cenoda/.dotnet/tools:$PATH" \
    dotnet restore "$profile_root/workspace/LanguageIntel.csproj"
  env HOME="$profile_root/home" \
    XDG_CONFIG_HOME="$profile_root/config" \
    XDG_DATA_HOME="$profile_root/data" \
    XDG_STATE_HOME="$profile_root/state" \
    XDG_CACHE_HOME="$profile_root/cache" \
    PATH="/home/cenoda/.dotnet/tools:$PATH" \
    dotnet /tmp/zaide-a3-lang/out/Release/net10.0/Zaide.Tests.dll \
    --scenario "$scenario" \
    --profile "$profile_root" \
    --evidence "/tmp/zaide-a3-lang/evidence/$scenario.json" \
    --repo-head "$(git rev-parse HEAD)" \
    --workspace "$profile_root/workspace"
done
```

This command is locked for later M4 preparation; it was not run in M0. M4 must
retain the resulting evidence before deleting disposable profile state.

## Exit Conditions

- [ ] M0 and implementation approvals are recorded separately.
- [ ] The five affected positive paths reach truthful terminal results.
- [ ] Cancellation, stale response, unsupported/no-result, and failure paths
  leave the editor consistent.
- [ ] Focused, build, fast-suite, and serial-fallback requirements pass.
- [ ] `A1-FN-09`…`A1-FN-13` have current isolated re-smoke evidence.
- [ ] Documentation and `TOFIX.md` reflect observed results without rewriting
  audit history.

## Rollback Note

Use one independently revertible commit per accepted milestone:

- M1 owns only completion dispatch/projection and its tests/docs.
- M2 owns only hover dwell/terminal dispatch and its tests/docs.
- M3 owns definition plus document/workspace symbol dispatch/projection and its
  tests/docs.
- M4 owns regression/re-smoke evidence and closeout documentation; it does not
  retroactively combine M1-M3 code.

Revert only the owning milestone commit. Do not change or revert the global
`IScheduler` registration, LSP request shapes/parsers, provider policy, packages,
unrelated Phase 10/V3 history, or another milestone to roll back one correction.
If later implementation proves a cross-cutting seam is unavoidable, stop and
record a renewed human-approved commit/rollback boundary before editing it.
