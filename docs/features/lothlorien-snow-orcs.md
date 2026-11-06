# Lothlorien and Snow Orc troop trees

## Overview

Lothlorien fields its own three troop trees and Gundabad fields a regional Snow Orc tree. Before this,
Lothlorien recruited Rivendell's whole line (`imladris_*`). Now a Lothlorien player recruits, enlists,
garrisons, patrols and crowns with Lothlorien troops and Lothlorien armour. The trees, the
armour art and the first version of the generators came from the outside contributor KEYforce (git
author "Erkam"); the wiring around them was finished in a review pass on 2026-11-05.

## Why This Exists

- **Vanilla behavior:** none. This is TAOM content.
- **TAOM requirement:** the Lorien armour pack in the Armory (`LOTRLOME_Armory/Assets/lorien_armors`)
  and the Snow Orc pack (`gundabad_armors/snow_orc`) had no wearers, and Lothlorien looked like a
  Rivendell reskin in every recruitment, patrol and enlistment screen.
- **Without this feature:** Lothlorien armies are Rivendell armies with another banner.

## Architecture

### Design Challenge

A troop tree is more than its XML. A culture's troops reach about a dozen consumers (volunteer pools,
party templates, garrison conversion, enlistment kits, career kits, troop weights, crown guard,
tavern mercenaries), and most of them fail quietly: a template left on Rivendell gives Rivendell
troops with no error. The review of 2026-11-05 found seven such leftovers after the tree itself
loaded correctly (`docs/reviews/rca-lorien-snow-orcs-2026-11-05.md`).

### Solution Approach

Data plus recruitment pools, no new `Main/Features/` module. Every generated file has a generator, so
the whole tree can be regenerated and checked:

| Piece | Source | Writes |
|---|---|---|
| Lorien troops (33: regular T4 to T7, Marchwardens T5 to T8, Galadhrim and Mallorn T6 to T9, 4 militia at levels 11 and 16, 1 tavern mercenary copy) | `tools/generate_lothlorien_troops.py`, then `tools/rebalance_troops.py --fix-monotonicity --restat <ids>` and `tools/rebalance_ranged_ladders.py --apply` for the skills | `Main/_Module/ModuleData/troops/troops_lothlorien.xml` |
| Snow Orc troops (13, levels 6 to 26) | `tools/generate_snow_orc_troops.py` | `troops/troops_gundabad_snow.xml` |
| Armour items (77 `sk_ll_*`, 20 `sk_gb_orc*`) | `tools/generate_lothlorien_armor.py`, `tools/generate_snow_orc_armor.py` (`--check` reports drift, `--apply` backs up what it changes) | the live, unversioned `LOTRLOME_Armory/ModuleData/LOTRLOME_items/lothlorien` and `gundabad_snow` |
| Bow ladder | `tools/ranged_ladders.json` line `lothlorien` (tiers 2 to 9), `tools/generate_ranged_ladder_items.py` | live `lothlorien/ranged_ladder.xml` |
| Volunteer pools | `VolunteerRecruitmentService.Lothlorien.cs`, `VolunteerRecruitmentService.OrcKingdoms.cs` | in code |
| Culture bindings and 7 military party templates (default army, 3 patrols, militia, rebels, vassal reward) | `taom_spcultures.xml`, `taom_partyTemplates.xml` | by hand, sized with `tools/rebalance_party_template_maxes.py` |
| Enlistment kits | `tools/generate_enlistment_rosters.py --culture lothlorien --reseed --apply` | `equipmentsets/taom_enlistment_equipment.xml` |
| Career kits | `tools/generate_career_kits.py` (Lothlorien is no longer in `KIT_SOURCE`) | `equipmentsets/taom_career_starting_equipment.xml` |
| Troop weights | level rule at the top of the file: 3.0 from level 41, 2.0 from level 31 | `TroopWeights/troop_weights.xml` |
| Crown guard | `lorien_gl_mallorn_defender` | `CrownGuardMap.cs` and `crown_guard/crown_guard_cultures.json` |

Entry troops: `lorien_scout` (the culture's basic troop), `lorien_gl_warrior` (its elite basic troop) and
`lorien_mw_sentry` (the Marchwardens of Talas Duiren, `castle_L2`). The Snow Orcs enter at
`gundabad_snow_recruit` through the Mount Gram pool (`town_G2`), the Grishuk clan pool (`clan_gundabad_6`)
and a weight-2 culture fallback entry. In the live map `town_G2` belongs to `clan_gundabad_2` and
Grishuk holds `castle_G6`.

## Changing this tree safely

Every step below is a command that was run to make this tree work; the order matters.

1. Edit `tools/generate_lothlorien_troops.py`, run it, then `tools/rebalance_troops.py --fix-monotonicity --restat <ids>` and `tools/rebalance_ranged_ladders.py --apply`.
2. `python tools/generate_enlistment_rosters.py --culture lothlorien --reseed --apply`. Never run it without `--culture`.
3. `python tools/generate_career_kits.py --verify` and `python tools/generate_armour_classes.py --check`.
4. For armour, `python tools/generate_lothlorien_armor.py --check`, then `--apply`. The generator owns every `sk_ll_` value: `rebalance_armor.py` skips them (`is_generator_owned`). Every leg piece is `covers_legs="true"`.
5. `python tools/validate_moduledata.py` and `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` from the checkout you changed.
6. A full game restart and a new campaign before you call any of it working.

## What went wrong in the first pass

The review of 2026-11-05 and the follow-up of 2026-11-06 got several things wrong before they got them right. The list is in [the RCA](../reviews/rca-lorien-snow-orcs-2026-11-05.md), round 2. The ones that change how to work on this tree: the tree loaded and passed every gate while seven party templates, the kits, the villager and eight lords still belonged to Rivendell; the generator wrote `covers_legs="false"` on all leg pieces; and the enlistment generator was run out of order and unscoped.

## Art cache entries

The shipping client draws a package only when `RuntimeDataCache/<package GUID>.rdc` exists. The
`lorien_armors` packages arrived in the live Armory without those entries, so the armour would have
drawn nothing and left no log line. On 2026-11-05 the 53 missing entries were copied from the
lotraom-assets mirror (`tools/check_rdc_entries.py --under lorien_armors` now reports
`without-rdc=0`; the 9 material packages need none). Check this again after any Armory sync.

## What is shared on purpose, and known limits

- Caravans, townsfolk and notables still use Rivendell templates. The villager is Lorien's own:
  `villager_lothlorien` (`npcs_lothlorien.xml`, party template `villager_lothlorien_template`) upgrades into
  `lorien_scout`.
- `culture_marketplace_config.xml` keeps `armour_from="rivendell"` for Lothlorien, because the armour
  acquisition shipped-data tests require it. Lorien markets stock Rivendell armour beside their own.
- The character-creation retainer and mercenary kits (`taom_char_creation_equipment.xml`) still wear
  `starter_rivendell_*` items.
- Every Lorien lord wears Lorien kit in battle: Galadriel, Celeborn and Thandirion have their own sets, and the
  other eight share `lothlorien_bat_template_medium_a` to `_e` (`taom_equipment_sets_lothlorien.xml`). Their
  civilian sets still use Rivendell items. One lord-grade piece, `sk_ll_ml_helmet_elite_b`, is worn by nobody.
  Lord kit is saved hero state, so it reaches new campaigns only.
- Clan default party templates (Grishuk, the Wardens) and lord skills and kit are saved state: they
  reach new campaigns only.
- The Lorien bows are copies of existing Rivendell and Mirkwood bows (`donor_by_band` in `tools/ranged_ladders.json`),
  tiers 2 to 9, named "[Lórien] Longbow <numeral>" through the line's `names` override. The live Armory holds the
  eight items; the lotraom-assets mirror still lacks `ladder_lothlorien_bow_t2` and `_t3`.
- `tools/rebalance_armor.py` skips every `sk_ll_` item (`is_generator_owned`). `generate_lothlorien_armor.py` owns
  those numbers, including the one-band price step for Mallorn pieces, so a restat cannot undo them.
- The Snow Orc armour items exist only in the live Armory. `tools/generate_snow_orc_armor.py --apply`
  regenerates them.

## Tests

- `TAOM.Tests/Features/TroopProgression/LothlorienOwnTreeDataTests.cs`: Lothlorien's troop and militia
  slots name only its own troops, its seven military party templates are its own and field only its
  troops, the clan templates field only its troops, every troop from level 31 has a weight row, the villager
  is its own and upgrades into the basic troop, and the lord battle templates wear `sk_ll_` armour.
- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs`: the Lorien and Gundabad pools.
- `TAOM.Tests/Features/TroopProgression/TroopUpgradeSkillMonotonicityTests.cs`: `lorien` joins the militia cultures.
- `tools/tests/test_ranged_ladder.py`, `test_militia_elite_bonus.py`, `test_upgrade_skill_monotonicity.py`,
  `test_armor_curve_invariant.py`: counts and invariants for the new line, militia and generators.

## How to verify in game

**Preconditions:** a full game restart (new item files and cache entries load only at launch) and a
new campaign (clan templates, lord skills and kit are saved state). TAOM, TAOM_Map and LOTRLOME_Armory
enabled.

1. Encyclopedia, Units: open `lorien_scout`, `lorien_mw_sentry`, `lorien_gl_warrior`, the three T9 troops and
   the four militia. Each shows its `sk_ll` armour, not a bare body.
2. Encyclopedia, Heroes: Galadriel, Celeborn and Thandirion wear Lorien armour.
3. Field battle with a Lorien party that fields outriders or dawnriders: they fire while mounted.
4. Enlist under a Lorien lord as an archer: the kit holds `ladder_lothlorien_bow_t2`.
5. Join Lothlorien as a vassal: the reward is Lorien troops. A Lorien castle patrol and a Lorien
   militia party carry Lorien troops.
6. Custom battle: Gundabad with Snow Orcs. Armour draws, wolf riders sit on `fell_warg`, and the Snow
   Spearman fights with spear and shield.
7. A coronation under a Lorien ruler (`taom.play_coronation`): the guard is a Mallorn Defender.
8. A Lorien village's villager party: the villagers wear Lorien light armour, and recruiting one promotes into `lorien_scout`.
9. Encyclopedia, Heroes: all 11 Lorien lords wear `sk_ll` armour in battle kit. Galadriel, Celeborn and Thandirion have their own sets, the other eight share `lothlorien_bat_template_medium_a` to `_e`.
10. Any Lorien greave or boot: no bare shin shows through it. The Uruk-hai shoes (`sk_uruk_hai_shoes_a1`) are meant to show the shin.
11. An inventory screen shows the Lorien bows as "[Lórien] Longbow II" to "IX".
