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
- M0 is mandatory and comes first. It is the planning gate for live-code
  verification, scope, boundaries, dependencies, and concrete test commands.
- Prefer one clear commit at each milestone or milestone-slice boundary.

## Code

- See `docs/CONVENTIONS.md` for naming, MVVM, async, and style rules.
- See `docs/DESIGN.md` for UI/aesthetic rules.
- One class per file. File name = class name.
- MVVM: Views never in Services. Services never in ViewModels.

## Planning

- Before coding a phase, create `docs/phases/vN/phase-N/IMPLEMENTATION_PLAN.md` in the current roadmap version.
- Verify against live code before claiming something exists.
- YAGNI: build for this phase only.
