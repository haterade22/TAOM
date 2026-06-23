# feat(lords-skills): Mordor — lore-driven skills + traits for 97 adult NPCs

> **Erratum (2026-06-22, commit `c9434b5a`).** The 3 Nazgûl (Tainted / Shadow of Northmen / Shadow of Umbar) listed below as Mordor canonical highlights — plus their master Khamûl — were re-cultured Mordor→Dol Guldur and now belong to that draft. 21 Norse-themed Group-A lords (Astrid Bearclaw, Fenrik, Tormund…) also left for Dunland, so the "97 adult NPCs" count is now an over-count. Regenerate from the live roster before creating this issue.

## Motivation

Following the Gondor Lord Review session (2026-05-26) and Gondor skills+traits pass
(2026-05-27), Mordor adult NPCs in TAOM had empty `<skills/>` blocks or vanilla-default
values — boring, lore-inconsistent stats. This issue covers the Mordor portion of the
sweep to give every adult lord skill values + trait alignment grounded in canonical
Tolkien lore (where applicable) or sensible archetypes (for TAOM-invented characters).

Parent CHANGELOG entry: `feat(all-cultures-lords): lore-driven skills + traits for every culture (16 cultures, ~880 adult NPCs)`.

## Design

**Archetypes applied** (race=`orc`):
- All adult Mordor NPCs receive one of the shared TAOM archetype templates (10 base
  archetypes + orc-specific variants).
- 9 canonical Tolkien characters receive explicit
  per-NPC overrides on top of the archetype.

**Canonical highlights:**
3 Nazgûl (Tainted / Shadow of Northmen / Shadow of Umbar — `nazgul` archetype: everything 270+, Charm 280, Mercy -2, Authoritarian +2), Grishnâkh (Uruk captain, attacked Merry+Pippin at Amon Hen), Verina + Jonna (Black Númenórean sorceresses, `bn_sorceress` archetype), Svala Redfang (Orc captain under Gothmog), plus Pagarios + Diasca (Black Númenórean noble heirs)

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

**Touched**: 97 NPCs (lords.xml + lords.xslt). Children (age <14) skipped.

**Files modified:**
- `Main/_Module/ModuleData/characters/lords.xml` — per-NPC `<skills>` + `<Traits>` blocks
- `Main/_Module/ModuleData/lords.xslt` — same for canonical Tolkien NPCs whose templates
  live in lords.xslt (transformed vanilla NPCs)
- `tools/apply_culture_skills_traits.py` — generalized generator with this culture's
  entry under `CULTURES['mordor']`

**Apply command**: `python tools/apply_culture_skills_traits.py --culture mordor --apply`

**Idempotency**: rerunning the script with the same archetype data produces no diff —
the script reads current values and only writes when they differ.

**Notes:** Introduced orc / nazgul / Black Númenórean archetypes. Brutal trait alignment (Honor -2, Mercy -2, Authoritarian +2). Charm intentionally high on nazgul / BN sorceresses — terror + manipulation, not warmth.

## Testing

**Automated** (already passed):
- XML well-formedness: `python -c "import xml.etree.ElementTree as ET; ET.parse('Main/_Module/ModuleData/characters/lords.xml')"`
- Coverage check: confirmed all Mordor adult NPCs now have populated `<skills>` blocks.
- Build + tests not re-run (Bannerlord game lock on `0Harmony.dll` during the session;
  XML data changes don't depend on C# build).

**Required before ship (in-game smoke):**
1. Launch a TAOM campaign.
2. Open Encyclopedia → Heroes → filter Culture = Mordor.
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
- [Tolkien Gateway — Nazgûl](https://tolkiengateway.net/wiki/Nazg%C3%BBl)
- [Tolkien Gateway — Mouth of Sauron](https://tolkiengateway.net/wiki/Mouth_of_Sauron)
- [Tolkien Gateway — Grishnákh](https://tolkiengateway.net/wiki/Grishn%C3%A1kh)
- [Tolkien Gateway — Black Númenóreans](https://tolkiengateway.net/wiki/Black_N%C3%BAmen%C3%B3reans)

---
🤖 Generated as part of the multi-culture lords skills+traits sweep — see
`CHANGELOG.md` 2026-05-27 entry.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/issues-drafts/INDEX.md](./INDEX.md)

<!-- backlinks-end -->
