# Deferred Findings

This area records things noticed during testing, smoke checks, or exploratory
work that should be fixed deeply later but are intentionally not being worked
on now.

Deferred findings are deliberately lighter than issues:

- `TOFIX.md` is phase/refactor-scoped review work.
- `docs/issues/open/` is for active bugs or problems that need investigation,
  especially after two unsuccessful fix attempts.
- `docs/deferred/open/` is for useful observations that should not be forgotten
  but are outside the current scope.

## Workflow

1. Copy `templates/FINDING-template.md` into `open/`.
2. Assign the next `DF-###` identifier.
3. Add a one-line entry to `INDEX.md`.
4. Capture concrete evidence: test name, reproduction notes, screenshot,
   output, or relevant code path.
5. When revisiting, either fix it, promote it to `docs/issues/open/`, or close
   it as deliberately dropped/superseded.

Do not guess at a root cause while merely recording an observation. Mark it as
“Unknown — not investigated yet” and leave the investigation for the later
work item.
