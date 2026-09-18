# Ranged Ladders

> Every archer's reach, damage, accuracy and skill come from one ranked, per-tier ladder. Tier
> climbs every stat inside a kingdom; rank orders the kingdoms at each tier; one generated item per
> tier a kingdom fields. Issues #582 (2026-09-12, reach) and #617 (2026-09-18, the rest).

## Overview

Every bow and crossbow a troop carries is a generated `ladder_<line>_<bow|xbow>_t<tier>` item. Its
`missile_speed`, `thrust_damage` and `accuracy`, and the troop's own Bow or Crossbow skill, come
from `tools/ranged_ladders.json`: a curve per stat along the tiers, shifted by the kingdom's rank on
that stat. Four tools apply it (`generate_ranged_ladder_items.py`, `rebalance_ranged_ladders.py`,
`restat_ranged_donors.py`, and `rebalance_troops.py`, which agrees on the skill), and the
validator's `RANGED_LADDER_INVERSION` and `RANGED_DAMAGE_CEILING` keep it true.

## Why This Exists

### What decides an arrow (Bannerlord 1.5.3, `SandBox.dll` and `TaleWorlds.MountAndBlade.dll`)

- **Reach** is the launcher's `missile_speed` and nothing else. `Mission.OnAgentShootMissile`
  launches at the wielded bow's `GetModifiedMissileSpeedForCurrentUsage()` (the arrow's own
  `missile_speed` is a dead field); `SandboxAgentStatCalculateModel` sets `MissileSpeedMultiplier`
  to 1, and only two Throwing perks and wet weather move it. Range is native ballistics of that
  speed, gravity 9.806, `AirFrictionArrow` 0.003.
- **Damage**: `CalculateStrikeMagnitudeForMissile` returns `(v_hit / v_launch)^2 x MissileTotalDamage`,
  where the total is the bow's `thrust_damage` plus the ammo's (`OnAgentShootMissile` passes the bow's
  as `damageBonus`). `ComputeBlowMagnitudeMissile` multiplies by `GetWeaponDamageMultiplier`, which
  for a bow is `1 + 0.0011 x Bow` (`DefaultSkillEffects.BowDamage`) and for a crossbow is 1. Armour:
  `scaled = magnitude x 50 / (50 + armour)`, Pierce `0.25 x scaled + 0.75 x max(0, scaled - 0.33 x armour)`
  (`ComputeRawDamage`); head or neck x2.0 for a Pierce missile (`GetDamageMultiplierForBodyPart`); a
  troop has 100 HP. So the launcher's number is almost all of the hit: Bow 200 adds 22%.
- **Accuracy**: `GetWeaponInaccuracy` is `(100 - accuracy) x (1 - 0.0009 x Bow) x 0.001` for a bow and
  `(1 - 0.0005 x Crossbow)` for a crossbow. Accuracy 100 is no spread at any skill; Bow 300 removes
  only 27% of the rest. Mounted, `SetMountedPenaltiesOnAgent` multiplies it by
  `1 + max(0, 5 - 0.05 x Riding)` (x6 at Riding 0, x1 from Riding 100).
- **Skill** also drives the AI: `SetAiRelatedProperties` scales the lead, vertical and horizontal
  (up to 2 degrees) aim errors by `1 - aiLevel` and fire rate by `0.3 + 0.7 x aiLevel`, with
  `aiLevel = min(1, skill / 300 x 0.96)` on Realistic combat AI. `AiShooterError` is a flat 0.008 plus
  up to 30% in rain or fog and 10% at night.
- TAOM changes none of this. The Culture Doctrine aggression pass can scale the AI error terms per
  culture, but it ships off.

### What was wrong

- **#582 (2026-09-12)**: the 227 troops carrying a bow or crossbow had no order of reach: six trees
  handed a higher tier a slower launcher, seven kingdoms' militia carried vanilla `noble_long_bow`
  over their own regulars, and Dol Guldur outranged every Gondor regular. 1,741 inverted pairs.
- **#617 (2026-09-18)**: archers nearly or fully one-shot troops. The #582 generator cloned each line's
  donor and changed only `missile_speed`, so every tier of a line carried the donor's damage and
  accuracy. A Rivendell T2 militia archer held a 105-damage, accuracy-100 Noldor longbow and landed
  about 48 on a T5 chest (armour 48), where vanilla T2 bows are 40 to 61 at a median accuracy of 85.
  Mordor's Black Numenoreans carried 107, the Iron Hills crossbows 120 to 130 with +8 bolts. Militia
  archers also carried Bow 120 to 180 at T1 and T2 against 45 to 70 for regular T2 archers, so garrison
  militia out-aimed their own kingdom's T4s. The ranked, per-tier model below was approved by the
  project lead on 2026-09-18 from an editable roster (the scratch generator is not in the repo).

## Architecture

### The two rules

Per launcher class (Bow and Crossbow are never compared with each other) and per stat (speed,
damage, accuracy, skill):

1. Inside a **line**, a lower tier never beats a higher tier.
2. At the same **tier**, a better-ranked line is never worse than a worse-ranked one, on that stat's
   own rank list. Equal ranks are not compared.

`validate_spec` refuses a spec in which any line's cells fail to rise with tier, so the spec itself
cannot express a rule 1 inversion. Rule 2 across ties and across stats is a property of the formulas.

### Lines, ranks, cells

| Term | Definition |
|---|---|
| Engine tier | `clamp(ceil((level - 5) / 5), 0, 10)`, `ranged_ladder.engine_tier`, pinned to `taom_schema.Validator._troop_tier` by a test. The cap of 10 is TAOM's `TaomCharacterStatsModel.MaxCharacterTier` override (vanilla caps at 6) |
| Line | A troop file (`files`, the `troops_<culture>.xml` token) or an id prefix inside one (`prefixes`, matched first). Militia, bosses and horse archers ride their file's line |
| Ranks | Three lists per line, 1 = best, ties allowed: `overall` (drives speed and the troop's skill), `damage`, `accuracy` |
| Tiers | The tiers a line fields, per class (`tiers`). Each is a cell and a generated item; a troop at an unlisted tier is a finding |
| Band | E = T0-2, R = T3-4, V = T5-6, X = T7-8, C = T9-10. Bands now only pick the donor MESH (`donor_by_band`), so tiers in a band share a look |
| Speed | `speed.tier_base[t] + 3 x (worst overall rank - overall rank)` |
| Damage | `round(damage[cls][t] x (1 + damage.step[t] x (7 - damage rank)))` |
| Accuracy | `min(99, accuracy.top[t] - (accuracy rank - 1) (+3 for a crossbow))` |
| Skill | `max(10, skill.anchor[t] + skill.step[t] x (7 - overall rank))`, the troop's Bow or Crossbow |

The anchor rank 7 is the Gondor group, standing in for vanilla's typical culture. Vanilla's own
pattern (1.5.3 regulars) set the scale: its best archer culture, Battania, sits about 23% above the
typical cultures at T4 to T6, and the damage step is 4% per rank at T1 to T6, so Mirkwood sits 24% over
Gondor. From T7 (levels 36 to 51, where vanilla has no troops) the step grows to 5, 6, 7 and 8% per
rank, so the top of the ladder separates sharply: Mirkwood T10 hits for about 90 on a T5 chest, an
outright kill on anything at armour 36 or less. Accuracy cannot widen the same way under its ceiling
without a lower rank's T7 dropping under its T6, so accuracy steps 1 per rank at every tier and the
high-tier gap comes from damage and skill.

The ranking, position 1 best: Mirkwood 1; Rivendell and Lindon 2; Ithilien 3 (`gondor_ith_*`,
`gondor_ithilien_*`); Blackroot Vale 4 (`gondor_brv_*`); Black Numenoreans 5; Harad and Dale 6; Gondor,
Rhun, Isengard and Umbar 7; Erebor and the Black Uruks 8; Gundabad, Dol Guldur, Mordor's orcs and the
goblins 9; Rohan 10; Dunland 11. Harad ranks 3rd on accuracy (after the elves). Erebor's bows and
crossbows rank 2nd on damage (crafting) and 8th on everything else. Crossbows rank among crossbows.
Khand has no troop file, so no archers.

### Items

Every tier each line lists, per class, is a generated item, 123 in all, a pure function of the spec.
Each is a verbatim clone of the line's donor for the tier's band (elf Longbow I to IV, Ithilien I to
III, Numenorean I/II, Erebor I/II, Dale recurve to longbow, Rhun steppe to Dragon longbow) with `id`,
`name`, `missile_speed`, `thrust_damage` and `accuracy` replaced (and `item_usage` where the line
overrides it, below) and `is_merchandise="false"`. That flag is `ItemObject.NotMerchandise`: it keeps
the clones out of the shops, and vanilla `DefaultBattleRewardModel.GetRandomItem` skips every
`NotMerchandise` slot when it rolls casualty loot, so a ladder bow never drops from a fallen archer.
The player's own bows are the donors, which stay in the shops and the loot pool. Mesh, difficulty and
flags are the donor's; a vanilla donor keeps its vanilla `culture=` (harmless while
`is_merchandise="false"` keeps the clones out of `DefaultTournamentModel.GetRegularRewardItems`).

Names are `{=<id>}<donor name, its own numeral and "- Starting" / "- Horse" suffix stripped>
<tier numeral I..X>`, registered as English rows in the Armory's `Languages/loc_<folder>.xml`
between `<!-- TAOM-RANGED-LADDER:START/END -->` markers so `translate_with_claude.py --module Armory
--sync-ids` can seed the other eleven languages.

**A rider cannot draw a `long_bow`.** Native's `long_bow` usage set is `base_set="bow"` plus
`requires_no_mount` and `requires_no_shield`; a mounted AI archer holding one never fires. The spec
overrides the usage per line with `"usage": {"Bow": "bow"}` (Harad and Dunland), the Armory's own
`wm_mirkwood_bow_a02` "LongBow II - Horse" pattern; `planned_edits(barred=)` refuses a mounted troop
in a cell whose effective usage is in the install's `requires_no_mount` set, and `RANGED_MOUNT_USAGE`
names any that exists.

### The Armory's own bows and ammo

The ladder items carry the troops; the Armory's own launchers are what lords, wanderers, named
companions and the shops hand out. `donor_stats` in the spec restats all 38 of them (hero-grade, top
`highelf_longbowd` 90, the Iron Hills Heavy Crossbow II 104, accuracy capped at 98) and `ammo_stats`
caps the Armory's arrows at +4 and its bolts at +5, vanilla's ceilings (27 items: the elven and
Mirkwood arrows from +5, the Isengard and Iron Hills bolts from +8). `restat_ranged_donors.py` edits
only those attribute values inside the matching `<Weapon>` tag, in the live Armory and the v1.5
mirror, and refuses an id that vanilla defines, that the Armory defines twice, or that it does not
define. `hero_ceiling` (Bow 90, Crossbow 105) backs `RANGED_DAMAGE_CEILING`: a launcher any Lord,
Wanderer or `is_hero` character can carry (its own equipment, its battle `EquipmentSet` rosters, or a
template `lords.xslt` hands to a retagged vanilla lord) above the ceiling is a finding.

### Rosters and skills

`rebalance_ranged_ladders.py --apply` moves every `Item0..Item3` slot holding a bow or crossbow, in
every battle set, onto `ladder_<line>_<class>_t<tier>`, and sets the troop's Bow or Crossbow to its
cell through `rebalance_troops.apply_skills_via_regex` (the troop's full declared skill set with only
that value replaced). Ammo slots are never touched and a class never changes. Two guards:

- **Retired ids.** Regenerating the items removes ids the spec no longer lists (the #582 band ids
  `ladder_<line>_<cls>_<e|r|v|x|c>`, or a tier a spec change dropped) before the rosters are repointed.
  `retired_ladder_launchers` recognises a `ladder_*` id no loaded file defines by its shape and plans
  it as a launcher of the right class, so the slot is repointed rather than skipped.
- **The skill clamp.** `UPGRADE_SKILL_REGRESSION` is an error, so a child below its upgrade source on
  the written skill is raised to it (non-ladder children only, the way `rebalance_troops.py`'s clamp
  works); a ladder troop that would need raising off its cell is refused. Militia-to-militia edges and
  `RESPECIALIZATION_EXEMPT_EDGES` are skipped. The tool refuses to run when the militia bindings
  cannot be read (`rebalance_troops.militia_troop_ids` fails closed).

`rebalance_troops.py` takes a ladder troop's Bow or Crossbow from the same `skill_cell`
(`ladder_skill_override`), so a later full rebaseline never undoes the ranking. A full rebaseline is
still not part of this pass: its dry run on 2026-09-18 changed 77 troops for reasons unrelated to
archery.

First #617 run: 709 inverted pairs to 0; 227 slot edits and 186 skill values (2 clamps:
`sagarun_marine` and `sagarun_storm_forged_marine` Bow 160 to 170, under the Rhun T5 skirmisher) over
16 troop files; 123 items; 65 Armory items restatted per tree.

### Component Diagram

```
tools/ranged_ladders.json  (stats curves, lines with ranks/tiers/donors, donor_stats, ammo_stats, hero_ceiling)
        |
        v
tools/ranged_ladder.py     cell, skill_cell, rank, tiers_for, ladder_id, line_of/line_for, index_launchers,
        |                  load_ranged_troops, load_upgrade_sources, inversions, unlisted, planned_items,
        |                  planned_edits, planned_skill_edits, retired_ladder_launchers, hero_launchers
        |
        +--> tools/generate_ranged_ladder_items.py   LOTRLOME_items/<folder>/ranged_ladder.xml + loc rows
        +--> tools/restat_ranged_donors.py           LOTRAOM_weapons.xml attribute values
        |                                             (both: live Armory + lotraom-assets v1.5 mirror)
        +--> tools/rebalance_ranged_ladders.py       reports; --apply: troops/troops_*.xml slots + skills
        +--> tools/rebalance_troops.py               ladder_skill_override (the same skill cell)
        +--> tools/taom_schema.py                    RANGED_LADDER_INVERSION, RANGED_MOUNT_USAGE,
                                                      RANGED_DAMAGE_CEILING (warnings)
```

## Configuration

### Config File: `tools/ranged_ladders.json`

```json
{"bands": {"E": [0, 2], "R": [3, 4], "V": [5, 6], "X": [7, 8], "C": [9, 10]},
 "stats": {"anchor_rank": 7,
   "damage": {"Bow": {"1": 37, "...": 0, "10": 90}, "Crossbow": {"1": 68, "...": 0, "10": 118},
              "step": {"1": 0.04, "...": 0, "10": 0.08}},
   "accuracy": {"top": {"1": 85, "...": 0, "10": 99}, "rank_step": 1, "crossbow_bonus": 3, "max": 99},
   "speed": {"tier_base": {"0": 54, "...": 0, "10": 78}, "rank_step": 3},
   "skill": {"anchor": {"1": 30, "...": 0, "10": 320}, "step": {"1": 10, "...": 0, "10": 15}, "min": 10}},
 "hero_ceiling": {"Bow": 90, "Crossbow": 105},
 "lines": [
   {"id": "harad", "folder": "harad", "files": ["harad"],
    "ranks": {"overall": 6, "damage": 6, "accuracy": 3}, "tiers": {"Bow": [2, 3, 4, 5, 6]},
    "donor": {"Bow": "wm_harad_bow_a01"}, "usage": {"Bow": "bow"},
    "donor_by_band": {"Bow": {"X": "wm_harad_bow_a02", "C": "wm_harad_bow_a02"}}},
   ...],
 "donor_stats": {"highelf_longbowd": {"damage": 90, "accuracy": 98}, ...},
 "ammo_stats": {"sm_dwarf_iron_bolt_a": 5, ...}}
```

`validate_spec` refuses: a line without all three ranks as positive integers; `tiers` that are empty,
out of 0 to 10, repeated, or for a class without a donor; a listed tier some curve does not cover; a
line whose speed, damage, accuracy or skill does not rise from each listed tier to the next; a
non-numeric curve value (reported, never raised); bands that leave a tier uncovered; a `folder` the
Armory does not register (generator); a missing or wrong-class donor; a `files` token with no
`troops_<token>.xml`; prefixes overlapping across lines; a `usage` for a non-ladder class or an empty
one; `donor_stats` / `ammo_stats` / `hero_ceiling` values that are not positive integers.

### Current Values

| Line | Ranks (overall, damage, accuracy) | Class | Cells: tier speed / damage / accuracy / skill |
|---|---|---|---|
| mirkwood | 1, 1, 1 | Bow | T2 88/51/88/115; T3 90/57/91/145; T8 102/98/97/370; T9 105/114/98/390; T10 108/133/99/410 |
| rivendell | 2, 2, 2 | Bow | T2 85/49/87/105; T3 87/55/90/135; T5 91/66/93/220; T6 93/71/94/255; T7 96/81/95/320; T8 99/94/96/355; T9 102/108/97/375 |
| ithilien | 3, 3, 4 | Bow | T7 93/78/93/305; T8 96/89/94/340; T9 99/102/95/360; T10 102/119/96/380 |
| blackroot | 4, 4, 5 | Bow | T3 81/52/87/115; T4 83/57/89/160; T5 85/62/90/200; T6 87/66/91/235; T7 90/75/92/290; T8 93/85/93/325 |
| mordor_num | 5, 5, 6 | Bow | T6 84/64/90/225; T7 87/72/91/275; T8 90/81/92/310; T9 93/91/93/330 |
| harad | 6, 6, 3 | Bow | T2 73/43/86/65; T3 75/48/89/95; T4 77/53/91/140; T5 79/57/92/180; T6 81/61/93/215 |
| dale | 6, 6, 7 | Bow | T1 71/38/79/40; T3 75/48/85/95; T4 77/53/87/140; T5 79/57/88/180; T6 81/61/89/215 |
| dale | 6, 6, 7 | Crossbow | T3 75/79/88/95; T4 77/83/90/140; T5 79/88/91/180; T6 81/94/92/215 |
| gondor | 7, 7, 8 | Bow | T2 70/41/81/55; T3 72/46/84/85; T4 74/51/86/130; T5 76/55/87/170; T6 78/59/88/205; T7 81/65/89/245; T8 84/72/90/280 |
| gondor | 7, 7, 8 | Crossbow | T3 72/76/87/85; T4 74/80/89/130; T5 76/85/90/170; T6 78/90/91/205; T7 81/96/92/245; T8 84/103/93/280 |
| rhun_new | 7, 7, 8 | Bow | T2 70/41/81/55; T3 72/46/84/85; T4 74/51/86/130; T5 76/55/87/170; T6 78/59/88/205; T7 81/65/89/245; T8 84/72/90/280; T9 87/80/91/300 |
| rhun_new | 7, 7, 8 | Crossbow | T5 76/85/90/170; T6 78/90/91/205; T7 81/96/92/245 |
| isengard | 7, 7, 8 | Bow | T3 72/46/84/85; T4 74/51/86/130; T5 76/55/87/170 |
| isengard | 7, 7, 8 | Crossbow | T2 70/72/84/55; T3 72/76/87/85; T4 74/80/89/130; T5 76/85/90/170; T6 78/90/91/205 |
| umbar | 7, 7, 8 | Bow | T3 72/46/84/85; T4 74/51/86/130; T5 76/55/87/170 |
| umbar | 7, 7, 8 | Crossbow | T5 76/85/90/170 |
| erebor | 8, 2, 9 | Bow | T2 67/49/80/45; T3 69/55/83/75; T4 71/61/85/120; T5 73/66/86/160; T6 75/71/87/195 |
| erebor | 8, 2, 9 | Crossbow | T4 71/96/88/120; T5 73/102/89/160; T6 75/108/90/195 |
| mordor_uruk | 8, 8, 9 | Bow | T4 71/49/85/120; T5 73/53/86/160; T6 75/57/87/195 |
| mordor_uruk | 8, 8, 9 | Crossbow | T5 73/82/89/160; T6 75/86/90/195 |
| gundabad | 9, 9, 10 | Bow | T2 64/38/79/35; T3 66/42/82/65; T4 68/47/84/110; T5 70/51/85/150 |
| dolguldur | 9, 9, 10 | Bow | T2 64/38/79/35; T3 66/42/82/65; T4 68/47/84/110; T5 70/51/85/150; T6 72/54/86/185; T7 75/58/87/215; T8 78/63/88/250; T9 81/69/89/270 |
| mordor | 9, 9, 10 | Bow | T2 64/38/79/35; T3 66/42/82/65; T4 68/47/84/110 |
| goblin | 9, 9, 10 | Bow | T2 64/38/79/35; T3 66/42/82/65; T4 68/47/84/110; T5 70/51/85/150 |
| rohan | 10, 10, 11 | Bow | T1 59/33/75/10; T2 61/36/78/25; T3 63/40/81/55; T4 65/45/83/100; T5 67/48/84/140; T6 69/52/85/175; T7 72/55/86/200 |
| dunland | 11, 11, 12 | Bow | T2 58/34/77/15; T3 60/39/80/45; T4 62/43/82/90; T5 64/46/83/130; T6 66/50/84/165 |
| dunland | 11, 11, 12 | Crossbow | T4 62/67/85/90; T5 64/71/86/130; T6 66/76/87/165 |

## Key Files

| File | Role |
|---|---|
| `tools/ranged_ladders.json` | The spec |
| `tools/ranged_ladder.py` | Library shared by the tools and the validator (no CLI) |
| `tools/generate_ranged_ladder_items.py` | Items and English loc rows into the live Armory and the v1.5 mirror (`--apply`, `--verify` on speed, damage, accuracy and usage, `--revert`) |
| `tools/restat_ranged_donors.py` | `donor_stats` / `ammo_stats` into the Armory's own items (`--apply`, `--verify`) |
| `tools/rebalance_ranged_ladders.py` | Report to `tools/reports/ranged-ladders/` (md, html, json); `--apply` rewrites the launcher slots and the ranged skill |
| `tools/rebalance_troops.py` | `ladder_skill_override`: the ladder troops' Bow or Crossbow in a full rebaseline |
| `docs/reference/ranged-troops.html` | The tracked HTML, every ranged troop per kingdom; regenerated by the roster tool, never hand-edited |
| `tools/taom_schema.py` | `Validator._ranged_ladder_inversions` (emits `RANGED_LADDER_INVERSION`, `RANGED_MOUNT_USAGE`, `RANGED_DAMAGE_CEILING`), `_RANGED_LADDER_EXEMPT` |
| `tools/tests/test_ranged_ladder.py`, `tools/tests/test_restat_ranged_donors.py` | Synthetic-data tests over all of the above plus the shipped spec |
| `Main/_Module/ModuleData/troops/troops_*.xml` | The 227 rosters, edited through the tools only |
| `<game>/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/<folder>/ranged_ladder.xml` | 13 generated item files (unversioned; `--verify` is the reversion gate) |
| `<game>/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_weapons.xml` | The restatted donors and ammo (unversioned; `restat_ranged_donors.py --verify`) |

## Tests

`python -m pytest tools/tests/test_ranged_ladder.py tools/tests/test_restat_ranged_donors.py`: the
cell and skill formulas and their caps and floors on a fixture spec; spec validation (ranks, tier
lists, a falling curve, an uncovered tier, non-numeric values, structure, prefixes, files, donors);
the shipped spec (validates, 19 lines, the approved ranks, Mirkwood T10 108/133/99 and Bow 410, no
better damage rank below a worse one at any tier, 123 items, the donor table under the ceiling);
the rules per stat (tier, same-tier rank, different tiers never compared, the worst set judged, ties,
classes); unassigned and unlisted troops; planned items, slot edits (retired ids repointed, unlisted
tier refused, mounted refusal, class refusal) and skill edits (cells, the clamp raising a non-ladder
child, refusing a ladder one, militia and exempt edges skipped); `rebalance_troops` agreeing with the
cell; hero launchers from inline kit, rosters and `lords.xslt`, and the ceiling; the roster tool end to
end (dry run, BOM + CRLF kept, skills written and inserted, retired ids repointed, idempotent, militia
bindings unreadable refused); the generator end to end (both trees, per-stat drift, loc rows,
revert); the restat tool (dry run, only the listed attributes change, BOM + CRLF kept on the mirror,
idempotent, drift, unknown, vanilla, duplicate and attribute-less ids refused); the validator gates.

## How to change the ranking or the curves

1. Edit `tools/ranged_ladders.json`: a line's `ranks`, a curve in `stats`, a line's `tiers`, or
   `donor_stats` / `ammo_stats`.
2. `python -m pytest tools/tests/test_ranged_ladder.py` (the shipped-spec tests pin the approved
   numbers; update them with the decision).
3. `python tools/rebalance_ranged_ladders.py --stdout`: read the cells and the inversions.
4. `python tools/generate_ranged_ladder_items.py --apply`, then `--verify`; for the donor and ammo
   tables `python tools/restat_ranged_donors.py --apply`, then `--verify`.
5. `python tools/rebalance_ranged_ladders.py --apply` (it repoints ids the generator just retired);
   `python tools/validate_moduledata.py`.
6. Restart Bannerlord fully (item XML loads at process launch) and check a troop from each end of the
   ladder in a Custom Battle.
7. `python tools/translate_with_claude.py --lang <L> --module Armory --sync-ids --apply` per language
   for new item names (needs `ANTHROPIC_API_KEY`).

## Performance

Nothing at runtime: the items are ordinary `<Item>` rows and the rosters ordinary references. The
validator pass is a pairwise sweep over about 230 ranged troops for four stats plus one scan of the
character files for hero kit.

## Changelog

- 2026-09-12: created (#582). First run 1,741 inverted pairs to 0; 130 items, 258 slot edits.
  `docs/reviews/rca-ranged-ladders-2026-09-12.md`.
- 2026-09-13: the tracked HTML report and the militia veteran step (#588); Codex review fixes (the
  `long_bow` usage override, the loot claim, the worst-set rule). `docs/reviews/rca-ranged-ladders-codex-2026-09-13.md`.
- 2026-09-18: per-tier, ranked stats (#617). Damage, accuracy and the troop's skill join reach; each
  tier its own item (`_t<tier>`, 123 cells); ranks per stat; Ithilien and Blackroot Vale split; the
  Armory's own bows and ammo restatted (`restat_ranged_donors.py`); `RANGED_DAMAGE_CEILING`; the
  roster tool writes skills and repoints retired ids. 709 inverted pairs to 0.

## GitHub Issue

[#582](https://github.com/haterade22/TAOM/issues/582), [#617](https://github.com/haterade22/TAOM/issues/617)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
