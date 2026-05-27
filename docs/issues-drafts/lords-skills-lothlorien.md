# feat(lords-skills): Lothlórien — lore-driven skills + traits for 3 adult NPCs

## Motivation

Following the Gondor Lord Review session (2026-05-26) and Gondor skills+traits pass
(2026-05-27), Lothlórien adult NPCs in TAOM had empty `<skills/>` blocks or vanilla-default
values — boring, lore-inconsistent stats. This issue covers the Lothlórien portion of the
sweep to give every adult lord skill values + trait alignment grounded in canonical
Tolkien lore (where applicable) or sensible archetypes (for TAOM-invented characters).

Parent CHANGELOG entry: `feat(all-cultures-lords): lore-driven skills + traits for every culture (16 cultures, ~880 adult NPCs)`.

## Design

**Archetypes applied** (race=`elf`):
- All adult Lothlórien NPCs receive one of the shared TAOM archetype templates (10 base
  archetypes + elf-specific variants).
- 2 canonical Tolkien characters receive explicit
  per-NPC overrides on top of the archetype.

**Canonical highlights:**
Galadriel (Lady of Lothlórien, ~8000+ yrs Noldor, Ring-bearer — capped at Charm 300, Leadership 300, Tactics 295, Steward 295, Medicine 290, Crafting 290; only character to hit Charm 300), Celeborn (Lord of Lothlórien, ancient Sindar — OneHanded 290, Polearm 295, Leadership 290), 1 placeholder lord (TAOM-invented)

**Auto-archetype inference**: remaining NPCs are matched to an archetype via keyword scan
against the `heroes.xslt` bio text (e.g., "captain" → `knight`, "wife of" → `lady`,
"ranger" → `ranger`), with gender + age fallback.

**Power thresholds** (consistent across the sweep):
- Most adults cap 200-270 in their specialty skill.
- Canonical Tolkien peak heroes push 280-295.
- Only Galadriel + Denethor hit 300 in any skill (justified by lore).
- Leadership >275 reserved for canonical army-leading heroes (matters for the in-game
  +1 party-size perk threshold).

**Race-aware defaults**: `default_archetype()` branches on `culture_data.race`. Elves
never get "young_lord" archetype from low age (immortal — TAOM age fields are placeholders).
Dwarves stay combat-focused regardless of age (250-yr lifespan).

## Implementation

**Touched**: 3 NPCs (lords.xml + lords.xslt). Children (age <14) skipped.

**Files modified:**
- `Main/_Module/ModuleData/characters/lords.xml` — per-NPC `<skills>` + `<Traits>` blocks
- `Main/_Module/ModuleData/lords.xslt` — same for canonical Tolkien NPCs whose templates
  live in lords.xslt (transformed vanilla NPCs)
- `tools/apply_culture_skills_traits.py` — generalized generator with this culture's
  entry under `CULTURES['lothlorien']`

**Apply command**: `python tools/apply_culture_skills_traits.py --culture lothlorien --apply`

**Idempotency**: rerunning the script with the same archetype data produces no diff —
the script reads current values and only writes when they differ.

**Notes:** Galadriel + Denethor are the only two characters hitting 300 in any skill. Justified: Galadriel is one of the most powerful Elves in Middle-earth. Marchwardens (Haldir, Rúmil, Orophin) are in Mirkwood not Lothlórien per TAOM's data.

## Testing

**Automated** (already passed):
- XML well-formedness: `python -c "import xml.etree.ElementTree as ET; ET.parse('Main/_Module/ModuleData/characters/lords.xml')"`
- Coverage check: confirmed all Lothlórien adult NPCs now have populated `<skills>` blocks.
- Build + tests not re-run (Bannerlord game lock on `0Harmony.dll` during the session;
  XML data changes don't depend on C# build).

**Required before ship (in-game smoke):**
1. Launch a TAOM campaign.
2. Open Encyclopedia → Heroes → filter Culture = Lothlórien.
3. For each canonical Tolkien hero, verify their skills match the archetype/override
   intent (e.g., Boromir 295 OneHanded → top of Gondor combat list; Galadriel 300 Charm
   → top of all charm lists).
4. Spot-check a few archetype-driven NPCs: confirm gender bias (ladies high Charm/Steward,
   lords high combat) and age bias (elders high Tactics/Steward, young lords lower across
   the board).
5. For the +1 party-size threshold (Leadership ≥275 perk): confirm only canonical peak
   characters in this culture have that perk available.

**Save compatibility**: hero skills are baked into the save file at hero creation. This
change affects NEW campaigns and any hero not yet realised at session start. Wandering
companions + reserved NPCs that haven't spawned yet will use new values.

## Research

Tolkien sources used:
- [Tolkien Gateway — Galadriel](https://tolkiengateway.net/wiki/Galadriel)
- [Tolkien Gateway — Celeborn](https://tolkiengateway.net/wiki/Celeborn)

---
🤖 Generated as part of the multi-culture lords skills+traits sweep — see
`CHANGELOG.md` 2026-05-27 entry.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/issues-drafts/INDEX.md](./INDEX.md)

<!-- backlinks-end -->
