# Phase 22.1: Language Intelligence / LSP — Implementation Plan

## Status and Authorization

**Planning only; not implemented.** M0 is not accepted. Implementation is not
authorized until this plan passes live-seam verification, receives explicit
human M0 acceptance, and then receives separate implementation approval.

## A4 Ownership

Phase 22.1 owns A4 package 1, findings BL-01…BL-05, and affected goal rows
`A1-FN-09`…`A1-FN-13`. It has no dependency and may proceed in parallel with
Phase 22.2.

Baseline evidence:

- [A4 package ledger](../../../audits/v1-v3-product-reality/A4_GAP_REPORT.md#9-corrective-work-required-before-v4-planning)
- [A3 language-intelligence evidence](../../../audits/v1-v3-product-reality/evidence/A3_LANGUAGE_INTELLIGENCE.md)
- [A3 consolidation](../../../audits/v1-v3-product-reality/evidence/A3_CLEAN_PROFILE_SMOKE.md#23-file-navigation-and-editing)

## M0 — Live-Seam Verification and Plan Acceptance

M0 is read-only and must be completed against the implementation-session HEAD.

- [ ] Reproduce and trace the five A3 failures without changing the historical
  evidence file.
- [ ] Verify production DI in `LanguageServiceCollectionExtensions` and the
  application/editor ownership of UI scheduling.
- [ ] Trace completion from `LanguageCompletionService` through
  `EditorLanguageInputViewModel` and `EditorView`; identify the exact
  thread-affinity boundary before selecting a fix.
- [ ] Trace hover through `LanguageHoverService` and the editor hover trigger/
  projection path; distinguish request, timeout, stale-document, and rendering
  failures.
- [ ] Trace definition and document/workspace symbol requests through
  `LanguageNavigationService`, `LanguageSymbolService`, LSP parsers, and their
  command/projection owners.
- [ ] Verify active-document, cancellation, stale-response, and no-project/
  ambiguous-project contracts remain protected.
- [ ] Inventory focused tests and the Phase 10 smoke tools; replace the
  verification placeholders below with exact filters and producer commands.
- [ ] Lock rollback boundaries and receive explicit human M0 acceptance.

Candidate seams are planning pointers, not verified-current claims. M0 must
replace stale names or assumptions with live truth.

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
| M3 | Definition plus document/workspace symbols return and project known fixture results with truthful empty/failure outcomes | Focused navigation/symbol tests |
| M4 | Regression gates pass and `A1-FN-09`…`A1-FN-13` are re-smoked under the A3 contract | Build, fast/serial gates, out-of-tree A3 smoke |

Each milestone is independently reviewable. Do not combine M1–M3 until M0
proves a shared root cause and records why one coherent implementation is safer.

## Verification Command Placeholders

M0 must replace angle-bracket placeholders with exact live filters/paths:

```bash
dotnet build Zaide.slnx
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<LanguageCompletion and editor-routing filter>"
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<LanguageHover filter>"
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<LanguageNavigation and LanguageSymbol filter>"
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
<out-of-tree A3 language-intelligence producer with disposable HOME/XDG/workspace>
git diff --check
```

Run the fast suite interactively; use serial mode if it fails or hangs. The A3
producer must follow the umbrella [re-smoke contract](../phase-22/IMPLEMENTATION_PLAN.md#re-smoke-contract).

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

Revert the smallest owning milestone commit. If a shared scheduling or LSP
lifetime change affects more than one milestone, M0 must record the pre-change
behavior and a single coherent revert boundary. Do not revert unrelated Phase
10 or V3 history.
