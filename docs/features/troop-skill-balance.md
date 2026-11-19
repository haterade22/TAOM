# Troop Skill Balance

## Overview

TAOM sets every troop's 8 combat skills (Athletics, Riding, OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing) with one deterministic rule, the **role table** (#343, 2026-11-15): the troop's tier gives a number N on a curve in vanilla's range, its kit says which skill is its main weapon, and its culture adds a share of N. Two tools own this: `tools/rebalance_troops.py` *writes* the skills, and `tools/analyze_troop_balance.py` *reads* the current state into a per-culture balance overview (HTML, markdown and JSON) without touching anything. The realm generators (`tools/realm_troops.py`) call the same function, so a generated troop and a restated troop are the same numbers.

## Why This Exists

- **Vanilla / ad-hoc behavior:** Troop skills are hand-authored per `NPCCharacter`, so same-tier units drift apart between cultures and across authoring sessions. Content added after a balance pass ships off the curve.
- **TAOM requirement:** cross-culture parity at each tier, shifted only by the culture's deliberate identity, with the elves above the line and the orcs below it, and the whole curve inside the range the engine was tuned for.
- **Without this:** the old per-group tables ran to 390 at tier 10 (vanilla's top troop is about 260), so the AI level saturated at tier 8 (block 0.99, parry 0.95 for tiers 8 to 10 alike) and same-tier two-handers killed in one blow at tiers 9 and 10. The design and its numbers: [the RCA](../reviews/rca-troop-skill-curve-2026-11-15.md).

## Architecture

### The role table (#343)

`tools/rebalance_troops.py` `calculate_skills(culture, level, troop_id, kit, ladder, mounted, mount_difficulty)` is the only statement of the rule. N is `TIER_CURVE[tier]`, the tier being the engine's (`ceil((level - 5) / 5)`, 1 for a level 1 to 5 troop):

| Tier | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| N | 20 | 40 | 70 | 100 | 130 | 156 | 182 | 208 | 234 | 260 |

Tiers 1 to 5 are vanilla's Soldier medians (20, 40, 70, 100, 130); tiers 6 to 10 go on in even steps of 26. pct is the culture's row in `CULTURE_PCT`, in percent.

| Skill | Value | Vanilla basis (median ratio) |
|---|---|---|
| Main carried weapon (the Item0 class, `troop_purpose.main_class`) | N x (1 + pct) | the grid |
| Another carried weapon (a sidearm) | 0.9 N x (1 + pct) | 0.929 |
| A melee class it does not carry | 0.5 N | 0.45 to 0.5 |
| A launcher or Throwing it does not carry | 0.4 N | 0.385, 0.45 |
| A carried launcher with a ranged ladder line | the ladder cell ([ranged-ladders.md](ranged-ladders.md)) | |
| An archer's (launcher main) other weapons | round down to 5 of 0.9 x its own launcher value; a spare launcher at most the launcher | 0.81 to 1.0 |
| Throwing, for a troop in `THROWN_PRIMARY_TROOP_IDS` that carries it | N x (1 + pct) | |
| Athletics | N x (1 + pct) on foot, 0.5 N x (1 + pct) mounted | 1.0, 0.5 |
| Riding | mounted: N x (1 + pct), and never under the highest `difficulty` of its horses; on foot: 0.4 N | 1.0, 0.43 |
| Militia | the tier 4 row (N = 100) whatever the level; the elite slots add `MILITIA_ELITE_BONUS` (10) on every skill but the archer's derived ones | |

Values round to the nearest 5, a half up, computed in exact fractions; an archer's other weapons round down so they stay under the launcher. Then the purpose rule reorders the melee and launcher values to the kit and ties alternative weapons (below), and the main class is raised to the top weapon value if a sidearm's culture share put one over it. After the formula, the upgrade clamp and the purpose raise run over the whole roster (`settle_purpose`).

- **Mounted** is the ranged ladder's reading (`ranged_ladder.load_ranged_troops`): a Horse slot in any battle set, or a `Cavalry` or `HorseArcher` group. The horse's difficulty comes from `taom_schema.build_mount_difficulty_registry`, the XML `difficulty` of each `Type="Horse"` item over the install and the repo. The engine checks Riding against that difficulty when an agent mounts (`Agent.CheckSkillForMounting`, v1.5.7 `Agent.cs` 3645 to 3651): a rider under it still spawns on its horse but cannot mount again once off it. The floor lifts the Rohan tier 1 recruits on difficulty-80 horses from 70 to 80.
- **No name is read.** The old name shifts (a "Spearman" took 15 points onto Polearm) and the Bow and Crossbow swap are gone: the kit decides the main weapon and every other value follows from it.
- **A kit with no weapon has no formula** (the troop is reported SKIPPED and keeps its values). The writer hard-fails without the game install (`--game-modules`, default resolved through `tools/_gamedir.py`), because the kit and the horses are read from the install's item files. The read-only analyzer warns instead and leaves every troop without a formula.
- **`CULTURE_PCT`** has a row for every culture `detect_culture` returns; a missing row raises `MissingCultureRow` instead of putting the faction on the bare curve (the Lindon lesson, #522). `detect_culture` routes `iron_hills_*` ids (in the erebor file) to `iron_hills`, `rhun_new` to `rhun`, `gundabad_snow` to `gundabad`, `mordor_uruk*` to `mordor_uruk`, `mordor_num*` to `mordor_numenorean` and `orthanc*` to `isengard_orthanc`. A troop whose culture has no row stops `--apply` and the troops `--restat` names; `--fix-monotonicity` and `--fix-purpose` report it `SKIPPED (no culture row)` and go on, because they read what is on disk (`ClampOnlyRunTests`).
- **Villagers.** The `villager_*` characters in `characters/npcs_*.xml` (level 1) upgrade into the tier 1 recruits. Each with a `<skills>` block takes the tier 1 row from its own kit (a farm tool and throwing stones) and its npcs file's culture row, and `--apply` writes only those skill values into the npcs file. An npcs file that `tools/promote_borrowed_cultures.py` copies from another culture (Rhudaur, Arthedain and Cardolan from Gondor, Ered Luin from Erebor, Lindon from Rivendell, Blue Craig from the goblins, and four more whose villagers have no `<skills>` block; `rebalance_troops.promoted_npcs_sources`) gives its villager the source's row, because the promoter owns that file and its `--check` holds only while the copy equals the source (`test_a_promoted_villager_equals_its_source_villager`); the two with no `<skills>` block (`villager_rohan`, `villager_southrhovanion`) stay read-only sources. The clamp still lifts 17 recruit values from them, on the farm tool or the stone a recruit does not carry (`python tools/rebalance_troops.py --apply --dry-run` lists the edges, 2026-11-15).
- **The Fords of War copies.** `--apply` reads `fords_of_war/fords_troops.xml` (`troop_purpose.EXTRA_TROOP_FILES`) as a second root. A copy follows its campaign source's rule: a copy `fow_<id>` takes its own level and kit, and campaign troop `<id>`'s culture row, ranged ladder line, militia row (the tier 4 row, elite +10) and thrown-primary status (`copy_source`, `bare_id`; 25 copies are militia, 10 of them elite, so each equals its campaign twin). The two copies whose id names no campaign troop (`fow_rohan_westfold_spearman`, `fow_imladris_spearman`) take the troop file their culture attribute points to. A copy of a troop in `SKIP_TROOP_IDS` is skipped and keeps its hand-set values (`fow_cave_troll`, `fow_hill_troll`, `fow_goblin_spider_rider`, `fow_harad_elephant_rider`, `fow_harad_mumakil_rider`). Only skill values are written; a copy with no `<skills>` block keeps none, and its slots still follow `--main-weapon-slots`. `--fix-purpose` changes no equipment line in this file (trunk's `--fix-purpose` never read it); only `--main-weapon-slots` plans a copy's Item0. No upgrade edge between the two roots is a clamp edge, in either direction, so a campaign troop is never raised for a copy. The Long Defeat shop upgrades copies into campaign troops and back (`long_defeat/shop.json`, 9 edges on 2026-11-15); the clamp does not read it, and `tools/tests/test_long_defeat_shop_skills.py` checks that no shop upgrade lowers the main weapon skill. Since the restat (2026-11-15: 209 of the 214 copies on the curve, 1608 values that differ from trunk's file, the 5 skipped copies equal to trunk's) every copy is a fixed point of the writer too.
- **`SKIP_TROOP_IDS`**: troops excluded from the formula (trolls, the elephant and mumakil riders, the spider and wolf riders). They take part in the clamp, which only raises, so a skip protects a hand-tune without letting it read backwards in the tree. The restat lifted eight of them on uncarried skills only (Crossbow, Throwing), where their parents' new 0.4 N is higher.
- **The ranged ladder** anchors on the same curve: `tools/ranged_ladders.json` `stats.skill.anchor` is `TIER_CURVE` and the step is 0.04 N (1 to 10), so a rank-1 line (Mirkwood) is N + 6 steps and the rank-11 line (Dunland) N - 4 steps.

Contract tests: `tools/tests/test_troop_skill_curve.py` (the table, the culture rows and their order, the archer and militia rules, and the shipped roster as a fixed point of the writer: every value on disk, villagers and Fords copies included, is what `--apply` plans, so a hand edit or a drifted rule fails it), `tools/tests/test_long_defeat_shop_skills.py` (the shop's upgrades), `tools/tests/test_rebalance_equipment.py` (the kit, never the name), `tools/tests/test_militia_elite_bonus.py`.

### The culture table (percent of N)

| Culture | Ath | Rid | 1H | 2H | Pol | Bow | Xbow | Thr |
|---|---|---|---|---|---|---|---|---|
| rivendell, lindon | +18 | +12 | +18 | +20 | +20 | +20 | +20 | +20 |
| mirkwood | +23 | +3 | +20 | +15 | +15 | +25 | +25 | +25 |
| lothlorien | +18 | +10 | +15 | +13 | +15 | +18 | +18 | +18 |
| erebor, eredluin | +5 | -10 | +8 | +12 | +8 | 0 | 0 | +5 |
| iron_hills | +5 | -3 | +10 | +12 | +12 | 0 | +3 | +5 |
| gondor | +3 | +3 | +5 | +3 | +3 | 0 | 0 | -5 |
| rohan | -3 | +15 | 0 | 0 | +5 | -3 | -5 | +1 |
| dale | +3 | -5 | +3 | +6 | +13 | +6 | +6 | -3 |
| dunland | +10 | -3 | +3 | +3 | 0 | 0 | 0 | +8 |
| rhun | +3 | +9 | 0 | 0 | +8 | -5 | -5 | -3 |
| umbar | +5 | -8 | +5 | +3 | 0 | 0 | 0 | 0 |
| harad | 0 | +8 | +3 | -5 | -3 | +5 | 0 | 0 |
| arthedain | +5 | +2 | +3 | +2 | +2 | +5 | 0 | -3 |
| cardolan | 0 | +5 | +3 | 0 | +5 | 0 | 0 | -3 |
| rhudaur | +5 | 0 | +2 | +5 | 0 | +2 | 0 | +5 |
| anduinvale | +8 | -10 | 0 | +8 | 0 | +3 | 0 | 0 |
| angaladh | +5 | -5 | 0 | +3 | +3 | +5 | 0 | 0 |
| dorwinion | 0 | +3 | 0 | 0 | +3 | +5 | 0 | 0 |
| southrhovanion | 0 | +8 | 0 | 0 | +5 | +3 | 0 | 0 |
| mordor_numenorean | +3 | +3 | +5 | +5 | +5 | 0 | 0 | 0 |
| isengard | +5 | +3 | +5 | +5 | +5 | 0 | +5 | +5 |
| isengard_orthanc | +9 | +3 | +6 | +8 | +8 | 0 | +6 | +6 |
| mordor_uruk | +5 | -3 | +5 | +6 | +5 | 0 | 0 | +3 |
| dolguldur | +6 | -3 | +5 | +6 | +6 | -3 | -3 | +5 |
| mordor | -3 | -3 | 0 | 0 | -3 | -3 | -3 | +3 |
| gundabad, mistymountainorcs | +2 | -3 | 0 | 0 | +2 | -5 | -5 | +3 |
| goblin | -5 | -8 | -4 | -3 | -4 | +8 | -8 | -3 |
| feral_orcs | -6 | -3 | -3 | 0 | -6 | -3 | -3 | +3 |

Each row started as the old point modifier divided by 200 (the harmonic mean of the carried main skill over tiers 4 to 10 before the change), so a culture kept the share of an average skill it had. The maintainer then set the order (2026-11-15): the Elves best, the Dwarves next, then Men, the orcs under Men, and each people keeps its strength. `CultureTableTests` pins the order, not the numbers: on OneHanded, TwoHanded and Polearm the mean of the Elf rows is over the Dwarf rows' and the Dwarf rows' over the mannish rows'; no plain orc row (mordor, gundabad, mistymountainorcs, goblin, feral_orcs) is over the mannish mean on a melee skill, and no orc row, the elite uruks included, is over any Dwarf row; Rohan holds the top Riding, Mirkwood the top Bow, Dale the top mannish Polearm, and Erebor and Iron Hills the top TwoHanded outside the Elves. The test compares the plain orcs with the mannish mean, because Harad's own TwoHanded (-5) sits under every plain orc row and its Polearm (-3) under Gundabad's (+2). The Black Numenoreans (`mordor_num_*`) are Men inside the mordor file and have their own row; no other hand-tuned line gets a route (`gondor_loss_noble*`, the `ironpass_ram_*` riders and the `*_bolgs_ironfang` troops take their culture's row). The Bow and Crossbow columns change a written value only on a carried launcher with no ranged ladder line: every other archer takes its ladder cell, and an uncarried launcher takes 0.4 N without the culture. One troop reads them today, `feral_orcs_archer`. `CultureTableTests.test_the_feature_doc_prints_the_table_the_code_holds` keeps this table equal to `CULTURE_PCT`. The nine realms that had no modifier (anduinvale to southrhovanion, eredluin, feral_orcs) got rows from their kits and lore; Harad's is its old row scaled, which lifted its 53 troops from about half the curve onto it. No troop route returns `mistymountainorcs`; only `villager_mistymountainorcs` reads it, from its npcs file's name.

### Damage under the curve

From the design harness (the engine blow chain and armour formula, TAOM's kingdom armour caps and weapon tiers; it leaves out attack speed, blocking, perks and crush-through). Same-tier body hit, gondor attacker into gondor defender, 100 HP; "worst pair" is the lowest over the 625 kingdom pairs. A is the old curve, C this one.

| 1H sword | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| vanilla | 51 | 49 | 46 | 35 | 26 | 18 |  |  |  |  |
| A | 46 | 51 | 43 | 27 | 33 | 22 | 26 | 30 | 34 | 38 |
| C | 45 | 49 | 41 | 26 | 31 | 19 | 21 | 24 | 28 | 31 |
| C worst pair | 37 | 41 | 32 | 16 | 20 | 6 | 9 | 11 | 14 | 16 |

| 2H sword | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| vanilla | - | 84 | 81 | 59 | 52 | 33 |  |  |  |  |
| A | 70 | 77 | 68 | 50 | 57 | 46 | 52 | 58 | 62 | 71 |
| C | 67 | 74 | 65 | 47 | 53 | 40 | 44 | 49 | 54 | 59 |
| C worst pair | 57 | 63 | 53 | 34 | 39 | 24 | 27 | 31 | 35 | 39 |

| Spear thrust | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| vanilla | 23 | 29 | 22 | 19 | 13 | 8 |  |  |  |  |
| A | 29 | 32 | 26 | 17 | 19 | 12 | 14 | 16 | 17 | 19 |
| C | 28 | 31 | 25 | 16 | 18 | 11 | 12 | 13 | 15 | 16 |

| Bow (ladder cell at rank 7) | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| vanilla | - | 40 | 39 | 29 | 23 | 20 |  |  |  |  |
| A | 35 | 37 | 31 | 21 | 24 | 15 | 18 | 22 | 23 | 25 |
| C | 35 | 37 | 30 | 20 | 22 | 14 | 16 | 17 | 19 | 21 |

Crossbow skill adds no damage in the engine, so the crossbow rows do not change. One-blow body kills at a tier gap of 0 or 1: 8 under A (two-handers at tiers 9 and 10), 0 under C. Accepted: 44 kingdom pairs at tier 6 and 14 at tier 7 (of 625 each) where a same-tier one-handed body hit is under 10. Every one is a blade of sword craft 0.82 to 0.88 into the four cultures with the highest armour cap (erebor, iron_hills, rivendell, lindon); vanilla's median tier 6 sword into its best tier 6 armour does 11. A sword of craft 0.93 or more never does single digits at the same tier (lowest 12, at tier 6).

The player against the troops (a skill-250 player in gondor tier 10 armour): a gondor tier 10 troop hits for 31 instead of 38 (4 hits instead of 3); its AI block and parry fall from 0.99 and 0.95 to 0.958 and 0.893, so tiers 8 to 10 no longer defend alike; and the player now gets crush-through on tier 6 troops (0.122 side, 0.244 overhead on gondor, measured with the old skill window of 30 and 200; [combat-mechanics.md](combat-mechanics.md) scales the window to 20 and 135, which raises both).

Reproduce: the design harness in the #343 session scratchpad, `python curve_sim.py A,B,C` then `python render.py` (writes `data/curve_results.md`). It is not in the repo ([the RCA](../reviews/rca-troop-skill-curve-2026-11-15.md) has the follow-up).

### Purpose: the kit decides the order (#343, 2026-11-15)

The old curve was keyed by formation group, not by kit. Its Infantry and Cavalry tables put Polearm at or above OneHanded at most levels, so a swordsman with a shield came out a better spearman than swordsman, and a Ranged elf with a two-handed sword had its best melee number on a one-handed sword it does not carry. The role table now reads the kit too, and the purpose rule still settles the order after the clamp, and `tools/troop_purpose.py` is its only statement: the writer, the realm generators, the validator and the analyzer call the same functions.

- **P1, the main class:** the weapon class in Item0, over the battle sets (civilian sets never count). `Equipment.GetInitialWeaponIndicesToEquip` wields the first weapon slot that is not held in the off hand, so Item0 is the weapon in hand at spawn. When sets disagree, the class most sets open with wins; a tie goes to the class the troop is more skilled in, and the tie rule below lifts the other to it. A thrown weapon counts: a javelin in Item0 is a thrower's main weapon, as in vanilla's skirmisher factions (`hidden_hand`, `beni_zilal`), and the Marshal's Word thrower trial passed with `gondor_har_javelineer` holding it.
- **P2, Item0 is a real weapon:** never a shield or ammunition while the same set holds a weapon. The engine puts a shield in the off hand and still wields the weapon, so the fault is in the rule's input: it reads no main class from Item0. `--fix-purpose` exchanges Item0 with one other weapon slot of that set; no item is added, removed or replaced. No battle set breaks it today. A troop that carries a launcher in every battle set and is `Ranged` or `HorseArcher` should open with the launcher, but vanilla ships the other layout too, so that is a warning reason no tool repairs: a designer exchanges the slots by hand.
- **P3, skills follow the kit:** the troop's own melee values are re-dealt to the melee classes in purpose order (main first, then the other carried classes by how many sets carry them, then the classes it does not carry), and the two launcher values the same way. The reorder keeps the troop's own total; the tie, the clamp and the purpose raise are what move it. Throwing, Athletics and Riding are never touched, and a ranged ladder cell keeps its number. A thrower's melee values are ordered the same way; its Throwing keeps its value on disk.
- **Ties, the raise before the clamp:** in vanilla, the two troops whose battle sets hold alternative weapons for one role (`imperial_recruit`, `khuzait_nomad`, OneHanded and Polearm) hold both at one value. Two classes of one kind that share a role (Item0, or a side slot) in the same number of battle sets, and never in the same set, are alternatives: one set carries a two-handed sword where the other carries a one-handed sword (the Lorien Marchwarden line), or one set opens with a spear and the other with a two-hander (the Dale lake town line). Neither is "the" weapon, so both take the higher of the two values after the reorder. A tie for Item0 makes both main classes, so a sidearm stays below them. A 3 to 1 split is no tie, and neither is a pair carried together. The tie runs before the clamp, in the formula, the realm generators and `--fix-purpose`. The only other raises are the clamp and the purpose raise after it.
- **After the clamp:** the clamp only raises, and an inherited value can land above the child's own main weapon. The purpose raise then lifts, never lowers: each class to the highest class after it in its kind, and the main class to tie any rival the clamp itself lifted. A gap between kinds that was on disk before the clamp is reported, never filled with a borrowed number.

**The size of the change** (the troop files on commit `078b86761` against the #343 result, measured 2026-11-15): skills changed on 369 troops. On 297 of them the reorder kept the 8-skill total. On the other 72 the total moved by +1518 net: 21 by the tie rule (+712), 33 by the clamp cascade below a reordered parent (+601), and 18 by the regeneration of the Arthedain, Cardolan and Rhudaur realm files (+205; four of those went down). No troop's top weapon skill rose except through a tie. The RCA lists the troops whose Item0 class rose over a class they carry in every set ([rca](../reviews/rca-troop-skill-purpose-2026-11-15.md)).

### Main weapon in Item0: lance cavalry and named polearm troops (#343, 2026-11-15)

The engine wields the first weapon slot that is not held in the off hand (`Equipment.GetInitialWeaponIndicesToEquip`), so a troop starts the battle with its Item0 weapon in hand. The maintainer's rule: a troop's main weapon is in Item0. P4 in `tools/troop_purpose.py` (`main_weapon_slots`, called through `troop_main_weapon`) applies it to two kinds of troop and leaves every other kit as the designer made it.

- **Rule A, lance cavalry:** `default_group` Cavalry, a Horse in every battle set (a Horse slot inside the roster, or a troop-level Horse row laid over every roster, as the validator's `_battle_kits` reads it), and a polearm in every battle set that can be couched. Item0 holds a one handed or two handed weapon.
- **Rule B, named polearm troops:** a word in the English name or the id starts with `spear`, `pike`, `lance` or `halberd` (`names_a_polearm`; `veteranpikeman` counts, `Spiked` does not), the troop carries a polearm in every battle set, and Item0 holds a one handed or two handed weapon.
- **The change:** in each battle set the Item0 item and the polearm item trade slots. Nothing else in the set moves, and no item is added, removed or replaced. The skills then follow through the purpose rule (Polearm takes the troop's top melee value) and the roster-wide clamp.
- **Who is left:** a swordsman with a spare spear (the infantry rows of the RCA table), a horse archer, a rider whose spear cannot be couched, and any troop whose sets disagree (the polearm is missing from a set) or whose Item0 holds a launcher or a thrown weapon. The dry run lists each of them with the reason.
- **One source for "can be couched".** `melee_catalogue.couchable_ids` reads the priced usages (a usage whose features include `couch`), and `taom_schema.couchable_item_ids` prices the Armory, vanilla (SandBoxCore and SandBox) and this repo's placeholders and returns the ids. The writer (`rebalance_troops.load_couchable_ids`) and the validator (`TROOP_MAIN_WEAPON_NOT_ITEM0`) both call it, so no rule of this pass keeps a second list. Trunk's `generate_enlistment_rosters.py` still has its own inline `any('couch' in u.features for u in p.usages)` on the same priced usages (about line 841); a FOLLOW-UP in the RCA points it at `couchable_ids`. A description gives an item a usage when every piece the item uses is in its AvailablePieces (`Crafting.GenerateCraftedItem`), so the usage list is the fit. Native defines the couch as `<WeaponDescription id="TwoHandedPolearm_Couchable" weapon_class="TwoHandedPolearm" item_usage_features="polearm:couch">` (`weapon_descriptions.xml`), whose usage set `polearm_couch` carries the `requires_mount` flag (`item_usage_sets.xml`). Ordinary spears are couchable too, not only items named lance. The couch is a mode a rider switches to; the item opens in its primary usage.
- **Which files the tool reads.** The troop files `troops/troops_*.xml` and one second root, `fords_of_war/fords_troops.xml` (`troop_purpose.EXTRA_TROOP_FILES`): the Fords of War roster, whose troops are copies of campaign troops and which Long Defeat fields (`long_defeat/waves.json` names `fow_` troops). `--fix-purpose` and `--main-weapon-slots` read it for the slot rule only, so only a troop the rule changes has its skills reordered there, and only `--main-weapon-slots` plans a copy's Item0 (`--fix-purpose` changes no equipment line in it, as on trunk, which never read it in that mode); `--apply` puts it on the curve ([the Fords of War copies](#the-role-table-343)). Every other file with an `<NPCCharacter>` is not read: lords, notables, wanderers, companions, creature files, the education templates and the obsolete vanilla overrides are not troops that fight in a mode of their own. A new fighting roster file goes into `EXTRA_TROOP_FILES` and into the tests.
- **Mode:** `python tools/rebalance_troops.py --main-weapon-slots` is a dry run that lists, for every troop it changes, each roster's slot order before and after and the skills before and after, then the troops in scope that stay and why; add `--apply` to write. A second run plans nothing, and `--fix-purpose` plans nothing after it.
- **Validator:** `TROOP_MAIN_WEAPON_NOT_ITEM0` (warning) calls the same function for a troop the rule would still change. It reads the same two roots, needs the install for couchability, and is skipped without it.

**Result of the first run (2026-11-15).** 13 troops, 31 battle sets. Rule A: `dale_outrider`, `gondor_anf_cavalry`, `gondor_anf_vet_cavalry`, `mordor_warg_reaver`, `mordor_warg_ravager`, `mordor_warg_beastmaster`, `mordor_num_cavalry`, `mordor_num_vet_cavalry`, `mordor_num_knight`, `mordor_num_temple_knight`, and the Fords copy `fow_mordor_num_knight`. Rule B: `gondor_lin_spearman`, `gondor_lin_vet_spearman`. On the 12 troops of the troop files OneHanded and Polearm trade values (the troop's own total is unchanged) and the clamp raised nothing; the Fords copy's OneHanded and Polearm are tied at 350, so its skills did not change. Left at that run, in scope: ten horse archers (`gundabad_scout`, `gundabad_scout_merc`, `kharaghul_keshig`, the seven `rohan_wold_*`) the four Dol Guldur warg riders and their two Fords copies below, and `fow_rohan_wold_veteran_horse_archer`: 17 troops in that dry run's "left as they are" list. The six Dol Guldur riders were changed in a second run the same day (next paragraph). The RCA table has the rest ([rca](../reviews/rca-troop-skill-purpose-2026-11-15.md)).

**The Dol Guldur warg riders.** All six carry a shield in every battle set, so they can couch but cannot brace even on foot (brace needs no mount and no shield). `dg_warg_raider`, `dg_warg_skirmisher`, `dg_warg_red_fang` and `dg_fell_warg_rider` (and the Fords copies `fow_dg_warg_raider`, `fow_dg_fell_warg_rider`) carry `sm_dg_khml_spear_a` or `_b`, which could not be couched: the Armory listed their blades under `OneHandedPolearm` and `TwoHandedPolearm` only. The maintainer asked for both spears to couch and brace. `tools/register_couch_brace_polearms.py` writes that into the Armory ([pipeline](weapon-xml-pipeline.md#couch-and-brace-usages)). The Armory write and a second `--main-weapon-slots --apply` ran on 2026-11-15: all six now hold the spear in Item0, and on the four campaign riders OneHanded and Polearm traded values (`dg_fell_warg_rider` Polearm 285 to 318). On an install whose Armory lacks the registration the spear is no lance, so the rule and the validator say nothing about these six there.

**Generators.** `generate_dale_troops.py` names other weapons for `dale_outrider` (`sturgia_sword_4_t4`, `northern_spear_3_t4`, which cannot be couched), so a run does not reproduce the shipped kit and the rule would not apply to its output. `generate_gondor_troops.py` already writes a spear alone as the Linhir weapon and is stale by its own header. `apply_mordor_troop_revamp.py` inserts a new troop only when its id is missing, and `apply_black_numenorean_troops.py` stops when its marker is present, so neither rewrites the shipped kits. The realm generators go through `realm_troops.py` and own none of these troops.

**Enlistment kits.** The enlistment kit branch is on trunk (merge `7af8138ce`), and its cavalry kits hold the sword first: `enlist_mordor_cavalry_soldier` has `wm_mordor_set1_sword_a02` in Item0 and the polearm in Item1. The generator compares a kit's weapons by category (`best_by_category`) and never reads slot order, so a changed troop's new order does not reach the enlistment rosters and changes no behaviour there. A rule that moves a lance into the enlistment Item0 would read couchability from `taom_schema.couchable_item_ids`, the source above. `generate_career_kits.py --verify` and `generate_starter_kit.py --verify` pass on the new data.

**What the formula no longer does:** the Polearm and TwoHanded swap, the Bow and Crossbow swap and the name shifts (`spear`, `sword`, `axe`, `shield` and the rest) are gone (the role table, #343 curve). The kit names the main weapon, and every value follows from it.

**Three validator codes** ([codes](../reference/moduledata-validator-codes.md)):

| Code | Severity | Fires when |
|---|---|---|
| `TROOP_ITEM_ZERO_NOT_MAIN` | error | a battle set holds a shield or ammunition in Item0 beside a weapon, so the rule reads no main class from Item0. Commit-gated; no finding in today's data |
| `TROOP_MAIN_WEAPON_NOT_ITEM0` | warning | a lance rider (rule A) or a troop named for its polearm (rule B) still opens a battle set with a one or two handed weapon. Reads `troops/troops_*.xml` and the Fords roster; couchability comes from `taom_schema.couchable_item_ids`, so a rider whose spear cannot be couched is not judged |
| `TROOP_SKILL_KIT_MISMATCH` | warning | a melee or launcher class is above the main class, a class the troop does not carry is above a carried one of the same kind, or a launcher troop's battle set does not open with its launcher. It becomes an error when the count reaches zero (`TROOP_SKILL_KIT_MISMATCH_SEVERITY` in `tools/taom_schema.py`) |

Three warning reasons are left after `--fix-purpose`, and the message names each. "launcher troop that does not open with its launcher": no tool repairs it, because vanilla ships the layout; a designer exchanges the slots by hand. "melee skill above the launcher's ladder cell": an archer whose Bow or Crossbow is a ranged ladder cell and whose melee is higher; moving a value between kinds is a decision on the level of the curve, so the writer leaves them. "thrower below its melee skill": a thrower whose Throwing is under its melee; the message says whether the troop is in `THROWN_PRIMARY_TROOP_IDS`; `--apply` gives a javelin main the top of its weapons, `--fix-purpose` invents no value. After the curve restat one troop is left, on the second reason: `dg_uruk_skirmisher` (Bow cell 92, OneHanded 105 from the clamp, its parent `dg_uruk_veteran_warrior` holding 105). The other reasons count 0 ([rca](../reviews/rca-troop-skill-curve-2026-11-15.md)).

`tools/analyze_troop_balance.py` **calls the writer's own `calculate_skills`**: it never re-derives the curve, so the "ideal" and the "writer" can never disagree. It compares each troop's actual skills to it and reports the delta; its reference row per group and level is the median formula total of the group's troops there; a ranged ladder troop's Bow or Crossbow reference is its ladder cell (`rebalance_troops.ladder_cells`), the value the writer sets (#617).

### Component diagram

```
TIER_CURVE + CULTURE_PCT + calculate_skills  (rebalance_troops.py, the role table)
        |                         |                              |
   rebalance_troops.py       realm_troops.py             analyze_troop_balance.py
   (WRITES skills via regex, (the realm generators       (READS, emits the HTML, MD
    keeps all formatting)     call the same function)     and JSON overview)
        |                         |                              |
  troops_*.xml (27 files)    troops_<realm>.xml          tools/reports/troop-balance/
```

## The 2026-06-24 rebaseline

Goal: bring every troop back onto the curve after the post-#212 content drift, **without** retuning the baseline numbers themselves (Gondor already matches them exactly — they are the validated standard).

Tooling changes (`rebalance_troops.py`):
1. Authored 3 new modifiers (`goblin`, `mistymountainorcs`, `dale`), each independently proposed from the roster + lore and adversarially balance-checked against peer cultures.
2. Bumped `dolguldur` from near-neutral (net −5) to elite (net +65, 2H-heavy) so Sauron's uruks land at ~Isengard tier — strong enough to contest the bordering elf realms — instead of being nerfed to a weak curve.
3. Fixed the `rhun_new`→`rhun` key mismatch in `detect_culture` (the Easterling modifier had been silently un-applied).
4. Added `SKIP_TROOP_IDS` to exclude `cave_troll` + `harad_elephant_rider` from the formula.
5. Fixed a latent `UnicodeEncodeError` (a `Δ` char in the warning output crashed on Windows cp1252 stdout once enough >100 deltas appeared).

Result: **262 troops rewritten across 11 files** (the under-tuned cultures; dunland/harad/rivendell/rohan/umbar were already on-curve and untouched). The standout corrections were the #212 Erebor / Iron Hills nobles (e.g. `iron_hills_noble_ironbreaker` 580 → 1340). The mount-riders (warg/wolf) rebaselined as cavalry; the elephant rider + cave troll were preserved.

Verification: `validate_moduledata` PASS (zero broken refs); diff perfectly balanced (1,701 insertions / 1,701 deletions = skill-values-only, no structural change); overview outliers **186 → 14** (the 14 are benign Mordor partial-skill-block troops whose *present* skills are on-curve); **780 / 798 troops within ±25 of the formula** (up from 593).

## Upgrade monotonicity: no stat may go down

**The rule: no skill on an upgrade target may sit below the troop it upgrades from.** A player reads the troop tree as a ladder, so an upgrade that lowers a stat reads as a bug however it got there. Vanilla is looser (87 of its 195 edges shed a secondary skill, worst single drop -70), but TAOM had drops down to -200 and 5 edges where the total of all 8 skills fell, so the tighter rule is the one that ships.

`clamp_upgrade_monotonicity` runs after the formula and before the write, over the whole roster at once because a single topological order is what lets a raise propagate down a chain and lets formula-skipped troops lift their children. It spans two directories: all 844 edges inside `troops/` stay within their own file, but the 21 villager edges in `characters/npcs_*.xml` cross into it. It topologically orders the upgrade graph (1,064 troop definitions and 844 edges, plus 21 villager sources and their 21 edges, once the villagers in `characters/npcs_*.xml` are included; acyclicity is asserted, not assumed) and sets `child[skill] = max(child[skill], parent[skill])`. Troops the formula skipped take part using their current values: they can lift a child and be lifted, and since a clamp only raises, a hand-tune is never reverted.

**Two modes, and picking the wrong one rebaselines the roster inside a bug fix.**

| Mode | Base for the clamp | Use when |
|---|---|---|
| `--apply` | the formula result | you actually want a rebaseline, and have reviewed the per-culture deltas |
| `--fix-monotonicity` | what is on disk | you are repairing upgrade ladders and nothing else |
| `--restat <id,id>` | forces the formula for those ids in clamp-only mode | a specific troop was misclassified and needs to come back onto the curve |
| `--fix-purpose` | what is on disk, reordered to the kit | a troop's skills or its Item0 break the purpose rule (#343). A dry run unless `--apply` is added; it writes any Item0 exchange and the reordered and tied skills, then the clamp and the purpose raise; it prints every tie raise. A second run plans nothing. Not combinable with `--restat` |
| `--main-weapon-slots` | what is on disk, reordered to the kit as if the exchange were made | a lance rider or a troop named for its polearm opens with a one or two handed weapon (P4). Everything `--fix-purpose` does, plus the Item0 exchange; a dry run unless `--apply` is added. Not combinable with `--restat`. A second run plans nothing |
| (every mode) | | the clamp and the purpose raise repeat until neither moves a value (`settle_purpose`), so a `--restat` child on a respecialising edge keeps its main class on top. In the clamp-only mode a troop the clamp leaves alone is never raised |
| `--dry-run` | (modifier) | preview EITHER mode without writing. It used to be a third mutually exclusive mode, which meant the clamp-only path could only be inspected by letting it write and then reading `git diff` |

`--fix-monotonicity` repairs a ladder without a rebaseline: it moves only the values an upgrade edge needs and leaves the rest of the roster as it is on disk. Since the #343 restat no line is off the curve on purpose (`gondor_loss_noble*`, `mordor_num_*`, the dwarf ram riders and `*_bolgs_ironfang` are on it), so on a clean roster the two modes agree and both plan nothing. The tell during the 2026-08-30 pass was a per-file diff size: `troops_lindon.xml` came out at 478 lines where the clamp accounted for 30.

The cost is deliberate. A Ranged troop branching off an Infantry parent keeps the parent's Polearm, so
group specialization blurs on branch upgrades. A clamp can produce a proficiency the troop cannot use:
`mordor_uruk_crossbow` inherits Bow 130 from its bowman parent, which is exactly what vanilla does for
`imperial_trained_archer -> imperial_crossbowman`, and it is inert on a weapon the troop does not carry.

**One inherited value is no longer accepted (#554, 2026-09-06).** This paragraph used to list
`sagarun_naffatun` inheriting Crossbow 160 as the same benign case. It was not. Inert stops being
harmless when the troop also has no skill in the weapon it actually throws: that troop carries two
javelins and an axe, no bow and no crossbow, and shipped with Bow 195 / Crossbow 160 / Throwing 55.
Its Bow and Crossbow are now the uncarried value and Throwing takes the main number (`THROWN_PRIMARY_TROOP_IDS`).
Because its parent `sagarun_crossbowman` genuinely carries a crossbow at 160, flooring the child is a
real regression across a real edge, so the pair is named in `RESPECIALIZATION_EXEMPT_EDGES`, mirrored
in `rebalance_troops.py`, `taom_schema.py` and `TroopUpgradeSkillMonotonicityTests.cs`. All three must
agree: without the entry in the writer's own clamp, the run floors the value and the clamp puts it
straight back, and the pass reads as having done nothing.

### On 1.4.8 `skill_template` beat an inline `<skills>` block outright

`BasicCharacterObject.Deserialize` (v1.4.8, `BasicCharacterObject.cs:337-358`) resolves
`skill_template` first and only calls `DefaultCharacterSkills.Init(childNode)` when that reference
came back **null**:

```csharp
if (childNode.Name == "Skills" || childNode.Name == "skills")
{
    if (mBCharacterSkills == null)      // only when skill_template did NOT resolve
        DefaultCharacterSkills.Init(objectManager, childNode);
}
```

So a character carrying both is declaring two different skill sets and the engine silently takes the
template. Until 2026-08-31 that described 44 militia troops, every one pointing at a vanilla
Calradian SkillSet that SandBoxCore registers: `rivendell_militia_spearman` was authored at 850 total
and delivered 215. None of the tooling knew the attribute existed, so `rebalance_troops.py` had been
rewriting the dead half on every balance pass and `analyze_troop_balance.py` had been reporting it as
the troop's real skills. 17 prison guards in `characters/npcs_*.xml` had the same shape (#523).

The templates are gone from `troops/` and from those prison guards, so the authored values now apply.
That was the 1.4.8 loader, which ignored the inline rows whenever a template was named (a dangling id
still returned an empty placeholder). Since 1.5.2
the engine copies the template and lays the inline rows over it, so a listed row wins. The rule that
holds on both (#626, 2026-09-19): a character may declare both, and every inline value must equal its
template's. `SKILL_TEMPLATE_MISMATCH` in `validate_moduledata.py` (the commit hook runs it) and
`TAOM.Tests/Core/LordInlineSkillParityTests.cs` enforce it over every ModuleData file and `lords.xslt`;
`python tools/sync_lord_inline_skills.py --apply` repairs a drift, the SkillSet being the source of
truth. It replaced `SKILL_TEMPLATE_SHADOWS_SKILLS` and `SkillTemplate_NeverShadowsAnInlineSkillsBlock`,
which refused any character declaring both. **A troop with an empty `<skills>` block and a template is
the legitimate shape** and is left alone. The Python gate (`UPGRADE_SKILL_REGRESSION`) and both
skill writers resolve the template to the SkillSet with the inline rows laid over it
(`sync_lord_inline_skills.effective_skills`, #626); the C# gate, which runs without the install,
still skips such an edge. On 2026-11-19, 0 of 913 indexed upgrade edges had a templated side.

### The graph spans two directories

Upgrade sources are not only in `troops/`. The `villager_<culture>` entries in
`characters/npcs_*.xml` upgrade into their culture's tier-1 troops, and the engine treats any character
with a non-empty `UpgradeTargets` array as upgradeable. Gating only `troops/` once left 16 edges
unchecked, six of them regressing, while `validate_moduledata.py` reported PASS. Those villagers
declare their skills explicitly, so every gate can read the whole graph: **891 edges inside
`troops/` and 22 crossing in from 22 characters in `characters/`, 20 of them villagers that
`--apply` writes** (`rebalance_troops.load_roster` over the shipped files, 2026-11-15).
`load_external_sources()` loads them; the rest are read-only and only seed the clamp. The Fords of
War file holds no upgrade edge, and the Long Defeat shop's 9 edges are not clamp edges (the
role table section).

### Militia are a binding, not a name

TAOM militia take the **tier 4 row of the role table regardless of their actual level** so sieges and village defence stay costly (user direction, 2026-06-24; the level-21 baseline until the #343 curve), and the elite slots sit `MILITIA_ELITE_BONUS` (10 since the #343 curve, 15 before) above that on every skill (2026-09-13; before that the four slots were identical and a level-11 militia archer and a level-16 veteran were the same troop). A militia archer's launcher is its ladder cell by its own tier, and its other weapons read that cell, so on those the step is only "never lower". A militia promoting into another militia is therefore a fixed step off the curve, and the 22 militia-to-militia edges (across 11 cultures) are exempt from the rule above. The one other exemption is `RespecializationExemptEdges` in the test: `sagarun_crossbowman` to `sagarun_naffatun` for Bow and Crossbow only.

Which troops are militia comes from what a culture **binds**: the 92 ids in `militia_troop` / `melee_militia_troop` / `ranged_militia_troop` / `*_elite_militia_troop` across `taom_spcultures.xml` and `spcultures.xslt`. Both encodings have to be read, a plain attribute and an `<xsl:attribute>` element; Dale, Dunland, Harad, Rhûn and Rohan use only the second. Comments are masked first, so a commented-out `<Culture>` block cannot widen the exemption, and the 92 ids are pinned by identity in the C# gate rather than by count (the old name rule produces the same set plus one, so a count-only assertion would not have caught a revert). The kit decides the militia troop's roles, as for any troop.

Until 2026-08-30 this was a name substring (`militia` plus one of spearman/archer/veteran). It had exactly one false positive across 871 troops, and that one shipped the worst upgrade edge in the game: `gondor_ano_archer_militia` is a level-11 Anórien **line** troop, bound only in `taom_partyTemplates.xml`, so it wore level-21 stats and out-statted its own level-16 target on seven of its eight skills, -145 total (Riding was already equal on both). Same defect family as the name-based weapon detection replaced in #340/#341.

### What the analyzer still reports

`analyze_troop_balance.py` stays the read-only overview and stays total-based with a ±25 tolerance. It exempts militia-to-militia **edges** now, not militia troops: excluding the troops is how it printed "0 inversions" for two months while the -145 edge sat inside its own exclusion. Run the fixed check against the pre-fix data and it names the bug immediately.

Three within-culture level inversions are known and accepted, all Gondor Ranged: `gondor_ith_longbowman` and `gondor_mt_longbowman` (L36) out-total `gondor_brv_shadowbow` (L41) by 110, and `gondor_ith_moon_guard` (L46) out-totals `gondor_ithilien_ranger` (L51) by 45. These compare disconnected branches, and the whole gap is melee the longbowmen inherited from an infantry parent through the clamp. The higher-tier troop still leads on Bow in every case, and no player sees an upgrade get worse. Closing them means a Gondor balance pass, not a bug fix.

Some upgrade edges end up exactly flat, which the gate allows because flat is not a decrease (militia pairs are exempt anyway). On 2026-11-15 there are seven, outside the militia: `dg_goblin_slave` into `dg_goblin_harrier` and `dg_goblin_crawler`, `goblin_chosen_of_tharzog` and `gundabad_chosen_of_tharzog` into their `*_bolgs_ironfang`, `isengard_orc_grunt` into `isengard_orc_brawler`, `mordor_orc_recruit` into `mordor_orc_lackey`, and `mordor_warg_tamer` into `mordor_warg_rider` (an ElementTree comparison of each edge's eight skills). The `*_bolgs_ironfang` troops sit at level 36 (tier 7).

**Per-skill enforcement lives in two gates**, not in the analyzer: `TAOM.Tests/Features/TroopProgression/TroopUpgradeSkillMonotonicityTests.cs` (runs in CI, no game install) and the `UPGRADE_SKILL_REGRESSION` error in `tools/taom_schema.py` (so `validate_moduledata.py` fails on it). `tools/tests/test_upgrade_skill_monotonicity.py` covers the gate, the clamp and the entry insertion on synthetic data.

## Armour is a ladder too

The ladder rule has an equipment half since 2026-09-04 (#541). A troop's armour in a slot is the
average over its BATTLE sets of the item's `head + body + arm + leg` (all four, because a chest
contributes arm armour and a pauldron body armour), an unfilled slot counting as 0 since the engine
draws each slot from an independently chosen set. The total is the sum over Head, Body, Cape, Gloves
and Leg, and **no upgrade target may total less than its source.** 62 edges in 13 cultures did when
first measured: the Rhun `loke_rim_*` and `dragon_wrath_ash_*` capstones in `plate_light` over
`hplate_heavy` parents (-60 to -66), `dg_khamul_shadow_archer` in a hood over a plated disciple
(-74), the uruk skirmisher with no gloves or cape (-65).

| Piece | Where | What it does |
|---|---|---|
| The clamp | `tools/fix_upgrade_armour_regressions.py` | Topological over the whole upgrade DAG (villager sources in `characters/` read-only). Per regressing edge, per slot, in every battle set: an `OVERRIDES` entry, else the cheapest item in the target's own culture folder and slot file sharing the target's item FAMILY (id with the trailing tier tokens stripped) that meets the source's value, else the source's own item. A slot the target never fills is appended to every set. Dry-run default, `--apply` writes byte-faithfully and re-checks from disk. |
| Hero-kit demotion | `DEMOTE` in the same script | The parent is sometimes the anomaly: `imladris_recruit` (L21) wore a lord's circlet in 8 of 13 sets and a lord's torso in 7, and raising the whole elf tree to lord kit would have been the wrong fix. Items matching a pattern are swapped, on troops below the listed level, for the troop's own strongest ordinary item in that slot, before the clamp judges anything. |
| The gate | `UPGRADE_ARMOUR_REGRESSION` (WARNING) in `tools/taom_schema.py` | Same definition, item values from the install (`Registries.item_armour`); skipped, never faked, when the install is absent. Warns rather than errors because the Armory is unversioned and the commit hook cannot see it. |
| The overview | `tools/analyze_troop_balance.py` | "Upgrade armour drops" section beside the skill monotonicity one. |

Exemptions mirror the skill rule: militia-to-militia edges, and the bare-chested-by-design troops
(`_BODYLESS_BY_DESIGN`) are compared without Body and Cape, because their only Body item, where
they have one, is an 89-armour skirt that leaves the chest bare. When a parent out-armours its child because an ITEM is
mis-statted, fix the item: the two Dunland `wulf_helmet_medium_*` carried 45 (the lord row) and made
every L21 Dunlending read as a downgrade, restatted to 22.

## Key Files

| File | Purpose |
|------|---------|
| `tools/rebalance_troops.py` | Writes skills onto the curve. `TIER_CURVE` + `CULTURE_PCT` + `calculate_skills` + `troop_mount` + `SKIP_TROOP_IDS` + `detect_culture` + `militia_troop_ids` + `clamp_upgrade_monotonicity` + `insert_missing_skill_entries` + `settle_purpose` + `apply_item0_swaps` + `load_roster` (the second root, `copy_source`) + `promoted_npcs_sources`. `--dry-run` / `--apply` / `--fix-monotonicity` / `--fix-purpose` / `--main-weapon-slots`. |
| `tools/register_couch_brace_polearms.py` + `tools/couch_brace_polearms.json` | Lists the spec's spear pieces under the Armory's couch and brace weapon descriptions (dry run, `--apply --backup-tag`, `--check`); tests in `tools/tests/test_register_couch_brace_polearms.py`, gate `couch-brace` in `tools/data_gates.py`. |
| `tools/troop_purpose.py` | The purpose rule (#343): main class, Item0 check and exchange, the reorder, the tie rule (`tie_raise`), the post-clamp raise, the findings. Pure functions on plain data (P4: `names_a_polearm`, `main_weapon_slots`, `apply_slot_swaps`); tests in `tools/tests/test_troop_purpose.py`, `tools/tests/test_fix_purpose.py` and `tools/tests/test_main_weapon_slots.py`. |
| `tools/analyze_troop_balance.py` | Read-only overview generator. Imports the curve from `rebalance_troops.py`; emits HTML/MD/JSON. `--outlier-threshold N` / `--stdout`. Never writes troop XML. |
| `tools/taom_schema.py` | `UPGRADE_SKILL_REGRESSION` (ERROR), the `validate_moduledata.py` half of the gate. `TROOP_ITEM_ZERO_NOT_MAIN` (ERROR) and `TROOP_SKILL_KIT_MISMATCH` (WARNING), fed by `Registries.item_classes`. Template parity is `SKILL_TEMPLATE_MISMATCH` in `validate_moduledata.py` (#626). |
| `TAOM.Tests/Features/TroopProgression/TroopUpgradeSkillMonotonicityTests.cs` | 3 tests: no skill drops, all 8 skills declared, militia exemption pinned by identity (template parity is `TAOM.Tests/Core/LordInlineSkillParityTests.cs` since #626). Fails rather than going inconclusive when it cannot find ModuleData, since a data gate that cannot read its data has checked nothing. |
| `tools/tests/test_upgrade_skill_monotonicity.py` | 35 synthetic-data tests: the gate, the clamp, entry insertion, the fail-closed militia loader, the template resolution (a templated side judged on its SkillSet, read from the install root, one ERROR when the install holds no vanilla SkillSet file, even beside TAOM's own), the clamp refusing to raise a templated character, and that the upgrade index leaves template parity to `SKILL_TEMPLATE_MISMATCH` (`tools/tests/test_skill_template_mismatch.py`). |
| `tools/fix_upgrade_armour_regressions.py` | The armour clamp (#541): family step-up, parent fallback, slot append, hero-kit demotion. `tools/tests/test_fix_upgrade_armour_regressions.py` covers it on synthetic data. |
| `UPGRADE_ARMOUR_REGRESSION` in `tools/taom_schema.py` | The armour gate (WARNING), fed by `Registries.item_armour`; tests in `tools/tests/test_validate_moduledata.py`. |
| `Main/_Module/ModuleData/troops/troops_*.xml` (×25) | The troop definitions (the data under management). |
| `Main/_Module/ModuleData/characters/npcs_*.xml` (×31) | The 21 `villager_*` upgrade sources that feed into the tier-1 troops, read-only to the writer. |
| `.claude/hooks/check-moduledata-validation.sh` | The commit gate. Its `--code` list is an ALLOWLIST, so a new ERROR check does not block until it is named there. |
| `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` | Cross-referenced by the analyzer for weight-coverage gaps (army-composition weighting, not skills). |
| `tools/reports/troop-balance/REPORT.{html,md}` + `troop-balance.json` | Generated overview (gitignored under `tools/reports/`; regenerate any time). |

## How to review balance / run a rebaseline

> **Repairing an upgrade ladder is not a rebaseline.** If the goal is "an upgrade stopped lowering a stat", run `--fix-monotonicity` and skip the rest of this section. `--apply` is for a deliberate curve change.

1. **See where things stand (read-only):** `python tools/analyze_troop_balance.py --stdout`, then open `tools/reports/troop-balance/REPORT.html`. The heatmap parity matrix shows under-tuned (red) vs on-curve (green) vs elite (purple) per culture × tier; the data-quality section flags missing modifiers, dead entries, and excluded creatures.
2. **Adjust the curve:** edit `TIER_CURVE` (the power curve, every culture; keep `tools/ranged_ladders.json` `stats.skill` on it, `LadderSpecTests` checks) or `CULTURE_PCT[culture]` (one faction's identity) in `rebalance_troops.py`. To exclude a bespoke/creature troop, add its id to `SKIP_TROOP_IDS`.
3. **Preview:** `python tools/rebalance_troops.py --dry-run` — review the per-level deltas and the >100 warnings. Inspect **downward** changes especially (they may signal a too-weak modifier rather than over-tuned troops — that's how the Dol Guldur bump was found).
4. **Apply:** `python tools/rebalance_troops.py --apply` (regex-based — preserves all XML formatting/comments; only rewrites skill *values* already present in a `<skills>` block).
5. **Verify:** `python tools/validate_moduledata.py` (no broken refs and no `UPGRADE_SKILL_REGRESSION`), regenerate the overview (outliers should collapse), and **read `git diff --stat` per file, not just the total**. Expect a balanced insert/delete count (values-only) apart from any restored `<skill>` elements. A file that moves an order of magnitude more than you expected is the tool doing something you did not ask for: that is how the missing `CULTURAL_MODS['lindon']` entry (now `CULTURE_PCT`, where a missing row stops the run) was caught, mid-apply, at 478 lines against an expected 30.

Save-compat: troop skills are read from XML at agent spawn, so a rebaseline applies to all troops in new *and* existing saves with nothing serialized per-troop-type — no save migration needed.

## How to verify in game

The checks owed for the ten tier curve (#343, 2026-11-15). An existing save is enough: troop skills are read from the XML. A tool cannot do these; a person must.

1. Encyclopedia, Lothlórien. The Galadhrim Blademaster (`lorien_gl_blademaster`) shows Two Handed 235 above One Handed 215. The Marchwarden Archer (`lorien_mw_archer`) shows Bow 186 on top, and One Handed and Two Handed both at 165.
2. Encyclopedia, lance cavalry. The Black Numenorean Knight (`mordor_num_knight`) shows Polearm 220 above One Handed 195. The Riders of Rohan (`rohan_edoras_golden_hall_supreme_rider`) show Riding 240 and Polearm 220.
3. Encyclopedia, an archer of Men. The Ithilien Ranger (`gondor_ithilien_ranger`) shows Bow 300 and One Handed 270.
4. A campaign battle between two armies of about the same tier. Fights between same-tier troops last several blows; an elite kills a recruit in one or two. No troop group stands idle or flees at once (the AI level reads the skill).
5. A Fords of War match. The troll copies fight as before (they keep their old values), and a militia copy fights like its campaign twin, not like a recruit.
6. Crush-through. An elite against a recruit has a skill lead over 135, so it crushes through a block at the full configured chance; two troops of the same tier have a lead under 20 and do not.
7. Shield bash (Hammerhand). The chance reads the Athletics lead, and a higher tier's lead over a lower tier is larger on this curve (between foot troops one tier apart the mean chance went from 22 to 30 percent, two tiers apart from 33 to 53). An elite on foot should knock a recruit down with most bashes, and two troops of the same tier seldom do. The window is unchanged.

## Changelog

- 2026-11-15: **The Fords of War copies on the curve, promoted villagers, the shop (#343).** The
  merge of trunk's main weapon slot pass kept trunk's equipment lines and restated the four troop
  files it touched. `--apply` now puts `fords_of_war/fords_troops.xml` on the curve: each `fow_`
  copy by its own level and kit, on the rules of the campaign troop it copies (its culture row,
  militia row and thrown-primary status; a copy of an excluded troop is skipped), skill values
  only (209 copies on the curve, 5 skipped). A villager in an npcs file the promoter copies takes its source's row,
  so `promote_borrowed_cultures.py --only rhudaur --check` passes. A test holds the Long Defeat
  shop's upgrades (`tools/tests/test_long_defeat_shop_skills.py`); no clamp reads them.
- 2026-11-15: **The culture order, the Black Numenoreans, the villagers (#343, review round).** The
  culture table follows the maintainer's order (Elves, then Dwarves, then Men, the orcs under
  Men; Rohan the best riders), pinned as an order by `CultureTableTests`. `mordor_num_*` takes its
  own mannish row. The villagers go on the tier 1 row and are written into their npcs files, so
  the clamp lifts 17 recruit values from them instead of 50. A culture with no row stops only
  `--apply`. The shipped roster is pinned as a fixed point of the writer, and the analyzer measures
  against the settled values. The crush-through skill window scales with the curve
  ([combat-mechanics.md](combat-mechanics.md)).
- 2026-11-15: **The vanilla range over ten tiers (#343).** Troop skills ran to 390 on the main
  weapon at tier 10, far over vanilla's 260, and the group tables gave carried and uncarried
  weapons the same numbers. `TIER_CURVE` (20 to 260), the role table and the percent
  `CULTURE_PCT` replace `GROUP_BASELINES` and `CULTURAL_MODS`; Riding never sits under the
  troop's hardest horse; militia take the tier 4 row and the elite slots +10. The ranged ladder
  skill spec now anchors on the same curve. Applied with `python tools/rebalance_troops.py
  --apply`; a second pass and `--fix-monotonicity` and `--fix-purpose` plan nothing
  ([rca](../reviews/rca-troop-skill-curve-2026-11-15.md)).
- 2026-11-15: **A lance rider and a troop named for its polearm open with it (#343).** The maintainer: a
  troop's main weapon is in Item0. Added P4 (`troop_purpose.main_weapon_slots`) and
  `python tools/rebalance_troops.py --main-weapon-slots [--apply]`: where a troop is mounted
  Cavalry with a couchable polearm in every battle set, or is named spear, pike, lance or halberd
  with a polearm in every set, the polearm and the Item0 weapon trade slots and the skills follow.
  12 troops changed in `troops_dale.xml`, `troops_gondor.xml` and `troops_mordor.xml` and one Fords
  copy in `fords_troops.xml`; the rest of the sidearm table stays. One source says what can be
  couched (`taom_schema.couchable_item_ids`), the validator warns with `TROOP_MAIN_WEAPON_NOT_ITEM0`,
  and `register_couch_brace_polearms.py` holds the Dol Guldur spears' couch and brace usages.
- 2026-11-15: **Skills follow the kit (#343).** The curve is keyed by formation group, so a
  swordsman could top out on Polearm and an archer with a two-handed sword on OneHanded. Added
  `tools/troop_purpose.py` (the purpose rule), `--fix-purpose`, the purpose step in
  `calculate_skills` and the realm generators, and the validator codes `TROOP_ITEM_ZERO_NOT_MAIN`
  and `TROOP_SKILL_KIT_MISMATCH`. Removed the Polearm and TwoHanded swap. Throwing is a main
  class (a thrower's javelin stays in Item0), and alternative weapons of one role tie at the
  higher value. The analyzer shows each
  troop's main class and both findings. Applied with `python tools/rebalance_troops.py
  --fix-purpose --apply` after regenerating Arthedain, Cardolan and Rhudaur; the RCA holds the
  counts ([rca](../reviews/rca-troop-skill-purpose-2026-11-15.md)).

- 2026-09-04: **The Dol Guldur Uruk line sat a tier under the curve, and equipment joined the
  ladder rule (#541).** `f9942d84` had moved the ten `dg_uruk_*` levels up by five without a
  restat (an L36 Black Guard tied an L31 Khamul infantryman); nine Mordor archer, scout and warg
  troops carried the 0-seeded secondaries from the 2026-08-30 insertion. All 20 restatted with
  `--fix-monotonicity --restat`, values only. New: the armour clamp, the `UPGRADE_ARMOUR_REGRESSION`
  gate and the analyzer section above; 62 regressing edges repaired, 0 remain.
- 2026-08-31: **Upgrades can no longer lower a stat (#522, #523).** Four independent causes, all
  live at once. Militia detected by name substring (one false positive in 871 troops,
  `gondor_ano_archer_militia`, worth -145 across seven of its eight skills on its own upgrade edge);
  `dg_warg_red_fang` tagged `HorseArcher` while carrying no ranged weapon, which handed it Bow 240
  and made its Cavalry child read as -200; the Ranged curve sitting below the Infantry curve on
  Polearm/TwoHanded; and `skill_template` silently discarding the authored `<skills>` block on 44
  militia troops, which had been shipping vanilla Calradian values while every tool reported the
  authored ones (#523). Fixed by binding-driven militia detection, the retag, a roster-wide
  monotonicity clamp, and removing the 44 dead templates. Widened the graph to include the 15
  villager sources in `characters/npcs_*.xml`, six of whose edges regressed unseen. Added the
  missing `CULTURAL_MODS['lindon']` entry, without which an `--apply` would strip the high-elf
  tuning off all 30 Lindon troops. Taught the writer to insert a missing `<skill>` element,
  resolving the 34 partial-block Mordor/Morannon troops, to bound itself to its own
  `NPCCharacter` element, to round-trip bytes, and to refuse to write XML that no longer parses.
  Added `--fix-monotonicity` and `--restat`, and made `--dry-run` a modifier so the clamp-only path
  can be previewed. Landed with:
  ```
  python tools/rebalance_troops.py --fix-monotonicity --restat gondor_ano_archer_militia,dg_warg_red_fang
  ```
  222 troops changed (251 skill values, 113 previously undeclared `<skill>` elements written, 44
  dead templates removed) across all 16 files, plus 32 NPCs in `characters/`; only the two
  reclassified troops end up lower than before. Gated by 4 C# data tests, two validator ERROR codes,
  and 25 Python contract tests, with the commit hook's code allowlist corrected so any of them can
  actually block. `check_monotonicity` now exempts militia-to-militia edges instead of militia
  troops, which is what hid the bug.

- 2026-06-25 — Orthanc elite: routed the `orthanc_*` guard line (Saruman's best, sword+shield, L26–41) to a new `isengard_orthanc` modifier (net +111) — now the best NON-elf line in the game, a clear step above the regular uruk-hai (+75), still far below elves. Only `troops_isengard.xml` changed (4 troops, +36 each). Same id-routing pattern as the Black Uruks. Surfaced by the 2026-06-25 deep review.
- 2026-06-24 — Black Uruk + goblin-archer follow-up: routed the `mordor_uruk_*` Black Uruk line to a new elite `mordor_uruk` modifier (net +52, between Gundabad & Dol Guldur — they were stuck on the weak Mordor-orc curve); raised goblin `Bow −15 → +15` so goblin archers are dangerous while the melee swarm stays throwaway (Uruks > Orcs > Goblins, with the goblin-archer exception). Only goblin + mordor files changed. Added a **level-monotonicity check** to `analyze_troop_balance.py` (upgrade-path + within-culture+group, militia-excluded): 0 inversions among professional troops.
- 2026-06-24 — Added the read-only `analyze_troop_balance.py` overview generator; full-roster rebaseline (262 troops / 11 files); authored `goblin`/`mistymountainorcs`/`dale` modifiers, bumped `dolguldur` to elite, fixed the `rhun_new`→`rhun` key mismatch, added `SKIP_TROOP_IDS` (cave_troll + elephant rider), fixed a latent `Δ` stdout crash.

## Related

- [troop-progression.md](troop-progression.md) — tier cap (6→10), volunteer cap, wage/recruitment tables.
- [troop-weight-system.md](troop-weight-system.md) — army-composition weighting (orthogonal to skills).
- [battle-balance.md](battle-balance.md) — tier power, casualty rates, blunt/cut ratios (GameModel side).
- [troop-tree-revamp.md](troop-tree-revamp.md) — #212 roster changes that drove most of this pass's corrections.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/armor-balance.md](./armor-balance.md)
- [docs/features/lord-perk-review.md](./lord-perk-review.md)
- [docs/modding/balance-levers.md](../modding/balance-levers.md)
- [docs/modding/skill-sets.md](../modding/skill-sets.md)
- [docs/modding/troops.md](../modding/troops.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
