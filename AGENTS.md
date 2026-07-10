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

## Code

- See `docs/CONVENTIONS.md` for naming, MVVM, async, and style rules.
- See `docs/DESIGN.md` for UI/aesthetic rules.
- One class per file. File name = class name.
- MVVM: Views never in Services. Services never in ViewModels.

## Planning

- Before coding a phase, create `docs/phases/phase-N/IMPLEMENTATION_PLAN.md`.
- Verify against live code before claiming something exists.
- YAGNI: build for this phase only.
