---
name: lord-skills
description: Give TAOM lords lore-driven skill values and traits via the TAOM SkillSet system. Use when a canonical lord has wrong stats or a culture roster needs a balance pass.
argument-hint: [lord-name|culture]
---

# Lord Skills + Traits Authoring

Assign lore-driven skill values + personality traits to TAOM lords. **Read the authoritative guide:** [docs/ai-includes/lord-skills-authoring.md](../../../docs/ai-includes/lord-skills-authoring.md) (modeled on the 16-culture / ~880-NPC sweep, #228–#245).

## When to invoke
- A canonical Tolkien lord (Boromir, Galadriel, Théoden…) has wrong in-game stats.
- A new TAOM lord needs skills/traits, or a whole culture roster needs a balance pass.

## The one critical fact (the recurring bug)
The SkillSet named by `skill_template="SkillSet.taom_..."` is the source of truth for a hero's skills, and the NPCCharacter's inline `<skills>` block must mirror it exactly. Through v1.4.8 the engine ignored that block beside a template (Boromir showed OneHanded=145 instead of 295 because someone hand-edited `<skills>`); **since v1.5.2 it lays the block over the template, so a hand-edited row now WINS in game.** Either way the edit is wrong: `SKILL_TEMPLATE_MISMATCH` (`validate_moduledata.py`, run by the commit hook) and `LordInlineSkillParityTests` reject any row that differs from its SkillSet (#626). To change a hero's skills, edit the SkillSet (or repoint `skill_template`) through the generator below, which writes both; after a direct SkillSet edit, run `python tools/sync_lord_inline_skills.py --apply`.

## Workflow
1. **Identify the NPC** — grep `characters/lords.xml`, `lords.xslt`, `heroes.xslt` for the name → NPCCharacter id. Classify the layer: `lords.xml` (TAOM-new, wins at runtime) > `lords.xslt` (vanilla override, loses to lords.xml on same id) > `heroes.xslt` (bio text, stat-irrelevant).
2. **Edit the source of truth** — the hand-edited `CULTURES` dict in [tools/apply_culture_skills_traits.py](../../../tools/apply_culture_skills_traits.py).
3. **Generate** — `python tools/apply_culture_skills_traits.py --all-cultures --apply` emits 3 outputs: `taom_lord_skill_sets.xml`, updated `lords.xml` + `lords.xslt` (`skill_template` swaps + populated blocks).
4. **Verify**: 3 XML files well-formed; `python tools/sync_lord_inline_skills.py` reports 0 drifted (`python tools/validate_moduledata.py` shows no `SKILL_TEMPLATE_MISMATCH`); then in-game Encyclopedia spot-check (e.g. Boromir OneHanded ≈ 295 + level growth ≈ 302).
5. **Ship** — `/commit-split` (data vs tool separate); one GitHub issue per culture via `tools/generate_culture_issue_drafts.py`.

## Gotchas
- **Saves bake values** — XML edits affect NEW campaigns + un-spawned heroes only; existing saves keep locked-in stats.
- **Last-loaded wins** — if an id exists in both `lords.xml` and `lords.xslt`, the lords.xml version is live; the XSLT one is dead code.
- After any rename, grep ALL `Main/_Module/ModuleData/**/*.xml` for the OLD name (lore flavor text goes stale silently — memory `feedback_rename_grep_all_moduledata.md`).
