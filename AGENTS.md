# Agents

Short rules for AI assistants working on Zaide.

---

## Language

- **All docs, code comments, commit messages, and PR descriptions: English only.**
- Prompts may be in any language — respond in English. 
- but if the user explicitly asks for a different language, respond in that language.
- This is open source; English keeps it accessible.

## Docs

- Docs live in `docs/`. See `docs-rules.md` for the full structure and update triggers.
- Keep docs in sync with code. If they disagree, fix the docs.
- Write docs as you go — not after.
- Treat the phase or refactor `TOFIX.md` as the current work board and normal
  work-report surface. Update a roadmap only when a phase outcome, ordering, or
  dependency changes; do not duplicate routine progress there.

Before planning or implementing a phase, read `docs-rules.md`. Keep these
boundaries explicit:

- A phase is a roadmap-level outcome. A roadmap owns phase goals, order, and
  dependencies.
- A plan owns milestone goals, implementation scope, and verification. A
  milestone is a meaningful, independently testable outcome, not a progress
  report.
- Use a sub-phase when a phase is too large to design, test, and implement as
  one coherent concern. Do not create sub-phases merely to split verification
  or documentation work.
- Prefer one reviewable commit for one coherent implementation outcome. Include
  its ordinary documentation updates in that commit; make a separate docs-only
  commit only when no implementation changes belong with it.
- Stop for a failed verification, material scope conflict, external side
  effect, destructive action, or an open decision that materially changes the
  work.

## Code

- See `docs/CONVENTIONS.md` for naming, MVVM, async, and style rules.
- See `docs/DESIGN.md` for UI/aesthetic rules.
- One class per file. File name = class name.
- MVVM: Views never in Services. Services never in ViewModels.

## Testing

- From Phase 16 onward, use `dotnet test Zaide.slnx --no-build` as the default
  fast verification command. It runs test collections with eight workers for
  implementation productivity.
- Run the fast suite in an interactive terminal. Redirected output can
  reproduce the known parallel-runner hang.
- If fast mode fails or hangs, reproduce with the opt-in serial command before
  treating the result as a regression:
  `dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings`.

## Planning

- Before coding a phase, create `docs/phases/vN/phase-N/IMPLEMENTATION_PLAN.md` in the current roadmap version.
- Before coding a refactor, create `docs/refactor/refactor-N/IMPLEMENTATION_PLAN.md` (including each decimal refactor-family member).
- Verify against live code before claiming something exists.
- YAGNI: build for this phase only.
