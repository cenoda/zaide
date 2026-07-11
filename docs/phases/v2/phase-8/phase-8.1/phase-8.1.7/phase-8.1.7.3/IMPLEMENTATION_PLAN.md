# Phase 8.1.7.3: Verification and Documentation Truth-Sync — Implementation Plan

## Purpose

Close the Phase 8.1.7 follow-up only after the provider and settings slices are
accepted, the desktop findings are verified, and every affected document tells
the same story as the live checkout.

This is a verification and documentation milestone. It must not introduce new
product behavior.

## Dependencies

- `8.1.7.1` is complete.
- `8.1.7.2` is complete.
- The checkout contains the accepted changes from both child plans.
- The Phase 8.1 M6 completion record remains intact.

## Scope

### Automated verification

- Run sequentially, in this order:
  1. `dotnet build Zaide.slnx --no-restore`
  2. `dotnet test Zaide.slnx --no-build`
  3. `git diff --check`
- Record exact warning, error, passed, failed, skipped, and total counts.
- Confirm the existing Phase 8.1 blocking test matrix remains present and
  passing.

### Desktop verification

- Verify the settings panel's Editor, Terminal, and LLM sections.
- Verify live editor/terminal application for the settings exposed by the UI.
- Verify validation, Apply, Discard, conflict, Rebase / Refresh, and close
  behavior.
- Verify provider send behavior using the configured endpoint or a documented
  equivalent.
- Capture before/after evidence for the normal settings panel, validation or
  conflict feedback, and provider send result. Screenshots must not contain
  API keys or other secrets.

### Documentation truth-sync

- Update the Phase 8.1.7 umbrella and child-plan checkboxes, status, evidence,
  and exact verification results only after the work is actually verified.
- Keep the Phase 8.1 parent plan and top-level roadmap documents truthful:
  Phase 8.1 remains complete, and this follow-up is described as post-closeout
  work until `.3` is accepted.
- If the follow-up changes a user-visible limitation or status statement,
  update the affected statements consistently in `README.md`,
  `docs/roadmap/V2.md`, `docs/phases/README.md`, and
  `docs/architecture/OVERVIEW.md` together.
- Do not update a document merely to imply desktop verification or provider
  success that was not observed.

## Out of Scope

- New production code, tests, settings schema work, or provider behavior.
- Reopening M6 or changing historical milestone commits.
- Phase 8.2 or Phase 8.3 implementation.

## Exit Conditions

- [ ] Sequential build, test, and `git diff --check` results are recorded with
      exact counts and are clean.
- [ ] Desktop verification and screenshot evidence cover the required settings
      and provider states without secrets.
- [ ] The Phase 8.1.7 parent and all completed child plans match the verified
      implementation state.
- [ ] All affected top-level documents are synchronized, or the plan records
      why no top-level status text required a change.
- [ ] Phase 8.1 remains correctly described as complete; no historical M6 claim
      was silently rewritten.
- [ ] No product behavior was added in `.3`.

## Rollback Plan

Revert only inaccurate documentation edits or remove unsupported evidence. Do
not roll back the accepted Phase 8.1 baseline merely because a manual check is
deferred; leave the limitation explicit and keep `.3` incomplete.
