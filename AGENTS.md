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

Before planning or implementing a phase, read `docs-rules.md`. Keep these
boundaries explicit:

- A phase is a roadmap-level outcome and normally contains at least three
  milestones.
- A sub-phase is a planning/ownership subdivision inside a phase; it is not a
  milestone and must not be used as a synonym for one.
- A milestone is a bounded, independently verifiable unit normally owned by
  one agent session. If it is too large, split it into milestone slices such
  as `M2a`/`M2b`, not into an incorrectly named sub-phase.
- M0 is mandatory and comes first in every independently implemented phase or
  sub-phase. It is the planning gate for live-code verification, scope,
  boundaries, dependencies, and concrete test commands. From Phase 8.3
  onward, every sub-phase owns its own independent `M0`; do not use the
  umbrella phase's M0 as a substitute.
- Prefer one clear commit at each milestone or milestone-slice boundary.
- Independently implemented refactors also require their own M0. Decimal
  refactor-family members such as `6.1`/`6.2`/`6.3` are independent refactors,
  not milestones or feature sub-phases; each owns its own plan and rollback
  boundary. An umbrella brief does not authorize implementation.

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
