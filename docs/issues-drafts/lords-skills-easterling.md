# feat(lords-skills): Easterlings of Rhûn — lore-driven skills + traits for 71 adult NPCs

## Motivation

Following the Gondor Lord Review session (2026-05-26) and Gondor skills+traits pass
(2026-05-27), Easterlings of Rhûn adult NPCs in TAOM had empty `<skills/>` blocks or vanilla-default
values — boring, lore-inconsistent stats. This issue covers the Easterlings of Rhûn portion of the
sweep to give every adult lord skill values + trait alignment grounded in canonical
Tolkien lore (where applicable) or sensible archetypes (for TAOM-invented characters).

Parent CHANGELOG entry: `feat(all-cultures-lords): lore-driven skills + traits for every culture (16 cultures, ~880 adult NPCs)`.

## Design

**Archetypes applied** (race=`man`):
- All adult Easterlings of Rhûn NPCs receive one of the shared TAOM archetype templates (10 base
  archetypes + man-specific variants).
- 0 canonical Tolkien characters receive explicit
  per-NPC overrides on top of the archetype.

**Canonical highlights:**
TAOM Easterlings: Mongol-named wives + heirs + chieftains. Auto-assigned to `easterling_lord` / `easterling_archer` / `easterling_lady` archetypes via bio scan. Boronchar (advisor) + Valathmir Mashakian (chieftain) get explicit lord-tier stats.

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

**Touched**: 71 NPCs (lords.xml + lords.xslt). Children (age <14) skipped.

**Files modified:**
- `Main/_Module/ModuleData/characters/lords.xml` — per-NPC `<skills>` + `<Traits>` blocks
- `Main/_Module/ModuleData/lords.xslt` — same for canonical Tolkien NPCs whose templates
  live in lords.xslt (transformed vanilla NPCs)
- `tools/apply_culture_skills_traits.py` — generalized generator with this culture's
  entry under `CULTURES['easterling']`

**Apply command**: `python tools/apply_culture_skills_traits.py --culture easterling --apply`

**Idempotency**: rerunning the script with the same archetype data produces no diff —
the script reads current values and only writes when they differ.

**Notes:** Wainrider / horse-archer culture — Bow 260-275, Riding 265-275, Tactics 235. Khamûl ("Shadow of the East", Easterling who became a Nazgûl) would be a special entry if TAOM exposes him as a player-meetable character (currently not in lords.xml under khuzait — likely in Dolguldur roster).

## Testing

**Automated** (already passed):
- XML well-formedness: `python -c "import xml.etree.ElementTree as ET; ET.parse('Main/_Module/ModuleData/characters/lords.xml')"`
- Coverage check: confirmed all Easterlings of Rhûn adult NPCs now have populated `<skills>` blocks.
- Build + tests not re-run (Bannerlord game lock on `0Harmony.dll` during the session;
  XML data changes don't depend on C# build).

**Required before ship (in-game smoke):**
1. Launch a TAOM campaign.
2. Open Encyclopedia → Heroes → filter Culture = Easterlings of Rhûn.
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
- [Tolkien Gateway — Easterlings](https://tolkiengateway.net/wiki/Easterlings)
- [Tolkien Gateway — Balchoth](https://tolkiengateway.net/wiki/Balchoth)

---
🤖 Generated as part of the multi-culture lords skills+traits sweep — see
`CHANGELOG.md` 2026-05-27 entry.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/issues-drafts/INDEX.md](./INDEX.md)

<!-- backlinks-end -->
