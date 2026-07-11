# Phase 8.1.7: Settings and Provider Follow-up — Implementation Plan

## Purpose

Resolve the manual verification findings discovered after the Phase 8.1 M6
closeout without reopening M6, changing the Phase 8.1 completion claim, or
starting Phase 8.2 or Phase 8.3.

This is a post-closeout follow-up umbrella. It does not replace or repurpose
the Phase 8.1 M6 closeout gate. Production changes begin only when one child
plan is explicitly authorized.

## Baseline and Status Boundary

- The accepted Phase 8.1 baseline is M6 closeout on `2026-07-11`.
- The baseline verification remains `dotnet build Zaide.slnx --no-restore`
  with 0 warnings and 0 errors, `dotnet test Zaide.slnx --no-build` with
  895 passed, 0 failed, and 0 skipped, and clean `git diff --check`.
- The Phase 8.1 parent plan, `README.md`, `docs/roadmap/V2.md`,
  `docs/phases/README.md`, and `docs/architecture/OVERVIEW.md` must continue
  to describe Phase 8.1 as complete while this follow-up is in progress.
- Manual verification limitations carried by M4/M5 remain limitations of the
  accepted baseline until Phase 8.1.7.3 closes them with evidence.

## Child Plans

| Child plan | Responsibility | Completion boundary |
|-----------|----------------|---------------------|
| [`8.1.7.1`](phase-8.1.7.1/IMPLEMENTATION_PLAN.md) | Provider compatibility diagnosis and the smallest confirmed compatibility or error-reporting fix. | The configured provider failure is classified with secret-safe evidence and covered by focused tests. |
| [`8.1.7.2`](phase-8.1.7.2/IMPLEMENTATION_PLAN.md) | Settings panel UX expansion over the existing immutable settings contract. | Editor, Terminal, and LLM settings are usable, labelled, validated, and tested without changing settings ownership. |
| [`8.1.7.3`](phase-8.1.7.3/IMPLEMENTATION_PLAN.md) | Sequential regression, desktop verification, screenshot evidence, and documentation truth-sync. | The accepted follow-up state is verified and all affected documentation agrees with the live result. |

## Shared Scope Rules

- Preserve the existing `SettingsModel`, `ISettingsService`, `ISecretStore`,
  `ApplyAsync(expected, next)`, and transient `SettingsViewModel` ownership
  boundaries unless a child plan proves a narrowly necessary correction.
- Keep API-key values inside `ISecretStore`. Do not serialize, log, screenshot,
  or include secret values in diagnostics.
- Keep the provider implementation non-streaming and single-endpoint. Do not
  add a provider registry, retries, tool calling, LSP, or broad multi-provider
  abstraction.
- Do not add command registry or keybinding editing, menu bar, command palette,
  project discovery, project selection, or project unload behavior.
- Do not reopen M1–M5 implementation work unrelated to the two findings.
- Do not claim a child plan is complete from automated tests alone when its
  exit conditions require desktop evidence.

## Dependency Order

1. Complete `8.1.7.1` first so the provider result is classified before any
   end-to-end acceptance claim is made.
2. Complete `8.1.7.2` independently over the existing settings foundation.
3. Run `8.1.7.3` only after the accepted changes from `.1` and `.2` are
   available in one checkout.

## Umbrella Exit Conditions

- [ ] `8.1.7.1` is complete with a documented provider diagnosis and focused
      automated coverage.
- [ ] `8.1.7.2` is complete with focused UI/ViewModel coverage and no settings
      ownership or schema drift.
- [ ] `8.1.7.3` is complete with sequential build/test verification, desktop
      evidence, and truth-synced documentation.
- [ ] Phase 8.2 and Phase 8.3 behavior remains out of scope.

## Rollback Plan

Revert only the accepted child-plan changes that regress the Phase 8.1 M6
baseline. Preserve the Phase 8.1 completion record and restore the last green
baseline before continuing another child plan.
