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
| Elite Emissary | `elite_emissary_config.xml` (key settlement `town_L1`, six offers) and `merchant_cost` rows in `troop_resource_costs.xml`, Elven Wine | `special_resources/` and `elite_emissary/` |
| Armour ladder | `taom_lords_material_lothlorien`, its Deep Seam and Armourer's Commission rows, no `armour_from` | `armour_acquisition/` and `lotr_issues/taom_lotr_issues.xml` |
| Lord and ruler battle rosters (a new ruler, a rebel lord, a woman who comes of age as a lord) | hand-ported from `lothlorien_bat_template_medium_a`; never regenerate (issue #637) | `equipmentsets/taom_lord_template_equipment.xml` |

Entry troops: `lorien_scout` (the culture's basic troop), `lorien_gl_warrior` (its elite basic troop) and
`lorien_mw_sentry` (the Marchwardens of Talas Duiren, `castle_L2`). The Snow Orcs enter at
`gundabad_snow_recruit` through the Mount Gram pool (`town_G2`), the Grishuk clan pool (`clan_gundabad_6`)
and a weight-2 culture fallback entry. In the live map `town_G2` belongs to `clan_gundabad_2` and
Grishuk holds `castle_G6`.

## Changing this tree safely

Every step below is a command that was run to make this tree work; the order matters.

1. Edit `tools/generate_lothlorien_troops.py` and run it.
2. When the bow ladder line or its tiers changed: `python tools/generate_ranged_ladder_items.py --apply`, then `--verify`, then `python tools/rebalance_ranged_ladders.py --apply`. `rebalance_troops.py` refuses to run before that.
3. `python tools/rebalance_troops.py --fix-monotonicity --restat <ids>` for the skills.
4. `python tools/generate_enlistment_rosters.py --culture lothlorien --reseed --apply`. Never run it without `--culture`.
5. `python tools/generate_career_kits.py --verify` and `python tools/generate_armour_classes.py --check`.
6. For armour, `python tools/generate_lothlorien_armor.py --check`, then `--apply`. The generator owns every `sk_ll_` value: `rebalance_armor.py` skips them (`is_generator_owned`). Every leg piece is `covers_legs="true"`, and `python tools/audit_leg_cover_flags.py` checks it.
7. If you change `lothlorien_bat_template_medium_a`, hand-port its five armour slots into the four Lorien lord and ruler battle rosters of `taom_lord_template_equipment.xml`. Do not regenerate that file (issue #637).
8. `python tools/data_gates.py`, `python tools/validate_moduledata.py` and `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` from the checkout you changed.
9. A full game restart and a new campaign before you call any of it working.

## What went wrong in the first pass

The review of 2026-11-05 and the follow-up of 2026-11-06 got several things wrong before they got them right. The list is in [the RCA](../reviews/rca-lorien-snow-orcs-2026-11-05.md), round 2. The ones that change how to work on this tree: the tree loaded and passed every gate while seven party templates, the kits, the villager and eight lords still belonged to Rivendell; the generator wrote `covers_legs="false"` on all leg pieces; and the enlistment generator was run out of order and unscoped.

## Art cache entries

The shipping client draws a mesh package only when `RuntimeDataCache/<package GUID>.rdc` exists (observed; material
packages need none). The `lorien_armors` packages arrived in the live Armory without those entries, so the armour
would have drawn nothing and left no log line. On 2026-11-05 the 53 missing entries were copied from the
lotraom-assets mirror (`tools/check_rdc_entries.py --under lorien_armors` now reports
`without-rdc=0`; the 9 material packages need none). Check this again after any Armory sync.

## What is shared on purpose, and known limits

- Caravans, townsfolk and notables still use Rivendell templates. The villager is Lorien's own:
  `villager_lothlorien` (`npcs_lothlorien.xml`, party template `villager_lothlorien_template`) upgrades into
  `lorien_scout`.
- Lothlorien has an armour ladder of its own since 2026-11-06 (Mike). It keeps no `armour_from` donor, so its markets, the lord's gear ladder, the lord harness and the culture item pool use the Lorien kit. It has a lord's material (`taom_lords_material_lothlorien`, named "Lórien Mallorn-silver" as a placeholder, wearing Mirkwood's bar `lord_material_06` until the artist clones a fourteenth), its own Deep Seam row, an Armourer's Commission row (reward `sk_ll_gl_chest_heavy_a`), Galadriel's sword on the weapon rung, and elite pieces in every armour rung. The head rung offers the two Marchwarden lord helmets.
- The character-creation retainer and mercenary kits (`taom_char_creation_equipment.xml`) still wear
  `starter_rivendell_*` items.
- Every Lorien lord's authored battle set is Lorien armour (Galadriel's greaves became `sk_ll_gl_grvs_heavy_a` on 2026-11-06): Galadriel, Celeborn and Thandirion have their own sets, and the other eight share
  `lothlorien_bat_template_medium_a` to `_e` (`taom_equipment_sets_lothlorien.xml`). Galadion (age 10) is created with
  child equipment, so his authored set is not his first outfit. The civilian sets still use Rivendell items. One
  lord-grade piece, `sk_ll_ml_helmet_elite_b`, is worn by nobody. Lord kit is saved hero state, so it reaches new
  campaigns only. The generated lord and ruler battle rosters follow `medium_a` (see the table above).
- Clan default party templates (Grishuk, the Wardens) and lord skills and kit are saved state: they
  reach new campaigns only.
- The Lorien bows are copies of existing Rivendell and Mirkwood bows (`donor_by_band` in `tools/ranged_ladders.json`),
  tiers 2 to 9, named "[Lórien] Longbow <numeral>" through the line's `names` override. The live Armory holds the
  eight items; the lotraom-assets mirror still lacks `ladder_lothlorien_bow_t2` and `_t3`.
- `tools/rebalance_armor.py` skips every `sk_ll_` item (`is_generator_owned`). `generate_lothlorien_armor.py` owns
  those numbers, including the one-band price step for Mallorn pieces, so a restat cannot undo them.
- The Snow Orc armour items are in the live Armory and in the lotraom-assets mirror (commit `274f7c18f`, byte-equal at the 2026-11-06 check). `tools/generate_snow_orc_armor.py --apply` regenerates the live files, and the generator owns their values (Mike, 2026-11-06): `rebalance_armor.py` skips `sk_gb_orc_` items as it skips `sk_ll_` ones.
- The mirror holds none of the 2026-11-06 Armory edits: the Lorien head, body and leg files (lord-only and gender
  flags, `covers_legs`), `ranged_ladder.xml` and `loc_lothlorien.xml` (names and the t2 and t3 bows), and the Isengard
  and Mordor leg files. A sync from the mirror reverts them.
- Lorien lord-grade pieces (`_lord_` helmets, Mallorn `_elite_` chests and helmets) are `is_merchandise="false"`, so no armoury sells them. 169 of the 171 lord-class pieces in the game are merchandise; Lorien is the recorded exception (Mike, 2026-11-06), and the lord's gear ladder awards them (`docs/features/armour-acquisition.md`).

## Tests

- `TAOM.Tests/Features/TroopProgression/LothlorienOwnTreeDataTests.cs`: Lothlorien's troop and militia
  slots name only its own troops, its seven military party templates are its own and field only its
  troops, the clan templates field only its troops, every troop from level 31 has a weight row, the villager
  is its own and upgrades into the basic troop, and the lord battle templates wear `sk_ll_` armour.
- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs`: the Lorien and Gundabad pools.
- `TAOM.Tests/Features/TroopProgression/TroopUpgradeSkillMonotonicityTests.cs`: `lorien` joins the militia cultures.
- `tools/tests/test_ranged_ladder.py`, `test_militia_elite_bonus.py`, `test_upgrade_skill_monotonicity.py`,
  `test_armor_curve_invariant.py`: counts and invariants for the new line, militia and generators.
- `tools/tests/test_generate_lothlorien_armor.py` (the eight leg pieces), `test_audit_leg_cover_flags.py` (the leg cover
  rule and a live scan), `test_generated_files.py` (the shared writer) and the every-mode skip test in
  `test_kingdom_caps.py`.

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
9. Encyclopedia, Heroes: the Lorien lords wear `sk_ll` armour in battle kit, except Galadriel's greaves and Galadion, who starts with child equipment. Galadriel, Celeborn and Thandirion have their own sets, the other eight share `lothlorien_bat_template_medium_a` to `_e`.
10. Any Lorien greave or boot: no bare shin shows through it. The Uruk-hai shoes (`sk_uruk_hai_shoes_a1`) are meant to show the shin.
11. An inventory screen shows the Lorien bows as "[Lórien] Longbow II" to "IX".
12. Let a Lorien ruler die, or start a Lorien rebellion, or make a female Lorien companion a lord: the new lord wears `sk_ll_gl` heavy armour, not Rivendell gold.
13. Look at the Lorien light boots (`sk_ll_rg_boots_light_a`, worn by the villager and `lorien_scout`) from close: no gap shows between the boot and the trousers, because `covers_legs="true"` hides the leg skin under them.
14. A militia archer (`sk_ll_rg_chest_light_b`): the skirt draws. The FBX audit lists `SK_LL_RG_Chest_Light_B.skirt` with LODs but no LOD0, an art question for KEYforce.
15. Helmets: `sk_ll_ml_helmet_elite_a` on a long-haired elf shows no hair through it. Lorien helmets mix `hair_cover_type` type2 (19) and all (10), where Rivendell and Mirkwood use all or none.
16. Caras Galadhon (`town_L1`), as its owner: the Elite Emissary sells six Lorien elites for Elven Wine (three at 28, three at 18).
17. As a Lorien player: a village headman offers The Deep Seam and pays Lorien Mallorn-silver; an artisan offers the Armourer's Commission and pays a Galadhrim heavy chest; the lord's gear ladder offers Lorien pieces for each rung and Galadriel's sword for the weapon rung. The material's icon is Mirkwood's bar until a fourteenth is cloned.
