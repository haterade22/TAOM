# RCA: troop skills do not follow the weapons the troop carries (#343, 2026-11-15)

## Top-line

Players reported Lothlorien archers whose best melee skill was a one handed sword they do not
carry, while the two handed sword they do carry sat lower. The data confirmed it across the
roster ([roster analysis](troop-roster-analysis-2026-11-15.md), section 2): swordsmen with Polearm
on top, two handers with OneHanded on top, archers with a two handed side weapon whose melee number
was on the one handed sword.

The fix is one rule in one module, `tools/troop_purpose.py`: the weapon class in Item0 over the
battle sets is the troop's main class (a thrower's javelin counts), Item0 holds a real weapon, the
troop's melee and launcher values are re-dealt to its kit with the main class first, and
alternative weapons of one role tie at the higher value. The writer, the realm generators, the
validator and the analyzer call it. `python tools/rebalance_troops.py --fix-purpose --apply`
applied it to what was on disk after the Arthedain, Cardolan and Rhudaur generators were re-run
(those three files were regenerated from the role curve, not reordered).

Measured with `python tools/validate_moduledata.py`, the same validator on the committed data
(`--moduledata` on a `git archive HEAD` copy) and on the result: `TROOP_SKILL_KIT_MISMATCH` 430 to
97, `TROOP_ITEM_ZERO_NOT_MAIN` 0 to 0, `UPGRADE_SKILL_REGRESSION` 0 to 0, errors 0 to 0. Skills
changed on 369 troops in 22 troop files. No equipment line changed.

The size of the change, the troop files of `078b86761` against the result: the reorder kept the
8-skill total on 297 of the 369 troops. On the other 72 the total moved by +1518 net: 21 troops by
the tie rule (+712), 33 by the clamp cascade below a reordered parent (+601), and 18 by the realm
regeneration (+205; four of those went down). No troop's top weapon skill rose except through a
tie. "The totals do not move" holds only for what `--fix-purpose` does to one troop's own values.
The non-realm values on disk are exactly what `plan_fix_purpose` computes from the commit's files.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | The curve gives every troop of a formation group the same skill order whatever it carries. The Infantry and Cavalry tables put Polearm at or above OneHanded at most levels, so a swordsman with a shield was a better spearman than swordsman; the Ranged table puts OneHanded far above TwoHanded, so `arthedain_steel_bowman` had OneHanded 220 against the TwoHanded 80 of the sword he carries. | Rule keyed on the wrong input | Every check compared a troop to the curve, and the curve was the source of the order. | `troop_purpose.purpose_skills` after the curve in `calculate_skills` and in `realm_troops.clamp_skills`; `TROOP_SKILL_KIT_MISMATCH` reports what is left. |
| 2 | MEDIUM | The name shifts moved 15 points by keyword: a "Spearman" holding a sword took +15 on Polearm and -15 on OneHanded. | Name as a proxy for the kit | The equipment-driven pass of #340 replaced the Bow and Crossbow keyword and left the melee keywords. | A name shift fires only onto the main class, or a carried class when no main class is known (`detect_weapon_specialization`). Test: `test_a_troop_named_spear_with_a_sword_kit_follows_the_kit`. |
| 3 | MEDIUM | The Polearm and TwoHanded pair swap fixed one shape (a two hander with no polearm) and left the rest, for example the one handed troops with Polearm on top that #343 recorded. | Special case for a general fault | The swap made its own cases pass, and nothing counted the other shapes. | The swap is removed; the purpose order covers every kit. |
| 4 | MEDIUM | Troops whose battle sets hold two alternative weapons for one role (a two handed sword in one set where the other has a one handed sword, or a spear opening one set and a two hander the other) had one of the two far lower: `lorien_mw_archer` OneHanded 195, TwoHanded 130. A reorder cannot settle it, because neither class is the troop's weapon. Vanilla's troops with alternative weapons for one role hold both at one value (`imperial_recruit` and `khuzait_nomad`, OneHanded and Polearm 20, the only two among the 388 vanilla troops with battle sets and their own melee skills); on the roster analysis' 119 main-tree troops the median second carried class equals the first. | Rule keyed on the wrong input | The permutation of finding 1 needs one class to win, and these troops have none. | `troop_purpose.tie_raise`: both classes take the higher value. It raised 21 values on 21 troops by 712 points in total, counted from the value after the reorder (list below). |
| 5 | MEDIUM | The realm generator gave each role a fixed skill order, so `arthedain_dunadan_youth` and `cardolan_dunadan` carried a two handed sword with TwoHanded 27 and OneHanded 70. | Rule keyed on the wrong input | Same as finding 1, in a second writer. | `realm_troops.clamp_skills` reorders and ties per troop and repeats clamp and purpose raise to a fixed point. The generators now read item classes from the game install and exit 2 without it. |
| 6 | LOW | The upgrade clamp only raises, so an inherited value can land above the child's own weapon (a swordsman parent handing a two hander child OneHanded 250 above its TwoHanded 220). | Two rules applied in sequence | The clamp was checked alone. | `troop_purpose.purpose_raise` after the clamp lifts the class back to a tie, never lowers. On this roster it raised one value (`dg_orc_archer` Polearm 110 to 115, inherited from `dg_orc_scout`). |
| 7 | MEDIUM | The first version of the rule (from the task brief) treated a thrown weapon in Item0 as a fault and moved it out of 27 battle sets of eight troops. Those eight are throwers by design, the Marshal's Word `advance_throwers` trial passed in the game with `gondor_har_javelineer` holding its javelin, and vanilla's `hidden_hand_tier_1` to `_3` and `beni_zilal_tier_1` to `_3` open every set with a thrown weapon. `SeatTrialRosterTests.TheJavelineer_IsAFootSoldierWithJavelinsASwordAndAShield_AndNoHorse` failed on it. | Rule written without the designed cases | The rule was checked against vanilla's main culture trees only, and the C# suite ran after the data was written. | Throwing is a main class; P2 forbids only a shield or ammunition in Item0. The 27 sets were never written in the final run (the troop files were reset to the commit and the corrected rule re-applied), and the C# test passes unedited. |
| 8 | LOW | `test_militia_elite_bonus` expected every veteran militia exactly 15 above its basic sibling skill by skill. Six veteran spearmen carry a sword their basic sibling does not, so OneHanded and TwoHanded trade places (`erebor_militia_veteran_spearman` OneHanded 120 to 145, TwoHanded 130 to 135 over the basic). | Test pinning a layout, not a rule | It was written before the purpose rule existed. | The test compares the step per kind as sorted values, and asserts that no single melee or launcher class falls on the step up; both hold for all 48 pairs, the six spearmen included. |
| 9 | LOW | `--fix-monotonicity` and `--restat` ran the clamp without the purpose raise, so a restatted child on a respecialising edge could keep an inherited skill above its main class. The clamp's summary also counted against the curve (309 values on 348 troops on an `--apply` dry run). | Two rules applied in sequence | The settle loop was added to the two modes that rebaseline and not to the third. | `settle_purpose` runs in every mode with that mode's base; in the clamp-only mode a troop the clamp leaves alone is never raised. It returns the clamp's own count (327 values on 183 troops on the same dry run). Tests: `test_a_restat_child_on_a_respecialising_edge_keeps_its_main_class_on_top`, `test_the_clamp_only_mode_raises_no_troop_the_clamp_left_alone`. |
| 10 | LOW | The first `TROOP_ITEM_ZERO_NOT_MAIN` also made a launcher troop that does not open with its launcher an error, and told the designer to run `--fix-purpose`, which never moves that layout (vanilla ships it). | Message promising a repair | The message was written with the shield case in mind. | The launcher clause is a warning reason of `TROOP_SKILL_KIT_MISMATCH` whose message says no tool repairs it; the error keeps only a shield or ammunition in Item0. Test: `test_an_archer_opening_with_a_sword_is_a_warning_no_tool_repairs`. |

Tie raises (skill: the value after the reorder to new):

| Troop | Raise |
|---|---|
| `dale_recruit` | OneHanded 33 to 53 |
| `dale_militia` | OneHanded 67 to 95 |
| `dale_squire` | OneHanded 67 to 95 |
| `dale_lake_town_skirmisher` | TwoHanded 97 to 120 |
| `dale_lake_town_mariner` | TwoHanded 130 to 155 |
| `dale_lake_town_veteran` | TwoHanded 180 to 195 |
| `dg_goblin_crawler` | OneHanded 43 to 46 |
| `dg_goblin_harrier` | OneHanded 43 to 46 |
| `dg_goblin_hunter` | OneHanded 48 to 50 |
| `gondor_loss_noble` | Polearm 90 to 110 |
| `gondor_loss_noble_veteran` | Polearm 115 to 150 |
| `gondor_loss_noble_sergeant` | Polearm 160 to 190 |
| `gondor_loss_noble_warden` | Polearm 215 to 240 |
| `gondor_loss_noble_captain` | Polearm 255 to 290 |
| `lorien_mw_sentry`, `lorien_mw_sentry_merc` | TwoHanded 120 to 170 |
| `lorien_mw_archer` | TwoHanded 130 to 195 |
| `lorien_mw_ranger` | TwoHanded 150 to 225 |
| `lorien_mw_high_ranger` | TwoHanded 170 to 250 |
| `lorien_mw_outrider` | TwoHanded 195 to 245 |
| `lorien_mw_dawnrider` | TwoHanded 230 to 280 |

## Root cause of the bug itself

The curve is keyed by formation group, not by kit. `GROUP_BASELINES` holds one table per
`default_group`, and `calculate_skills` reads the troop's level and group, adds the culture's
modifier and stops. Two troops of the same group and level get the same order whether they hold a
sword, a spear or a two handed axe. The order on disk was right only for troops whose kit happened
to match their group's table. The keyword shifts and the pair swap were partial patches on top.

## Where the rule needed a reading

- **The tie takes the higher value after the reorder.** On most troops that is the higher of the
  two tied classes' own values. On the `gondor_loss_noble` line it is not: those troops carry no
  one handed weapon, so the reorder first moves their top value (OneHanded 110 on
  `gondor_loss_noble`) onto the main class TwoHanded and the next (90) onto Polearm, and the tie
  then lifts Polearm to 110 (80 on disk), where the two classes' own values give 90. Using 90 would leave TwoHanded above Polearm and
  break the tie. That line is a hand tune.
- **A tie for Item0 makes both classes main classes,** so a sidearm carried in more sets stays
  below them. Without that, the Dale lake town line lifted its side sword a second time after the
  tie (97 to 120 on `dale_lake_town_skirmisher`).
- **A tie counts only at the top count of its role, and only between classes never held in that
  role in the same set.** A 3 to 1 split is no tie; `erebor_oathsworn_legionary`, which carries a
  two hander and a polearm side by side in every set, is no tie either.
- **Mixed Item0 classes with a winner (18 troops in all, ties included).** The class most sets open
  with is the main class: `dg_orc_warrior` polearm 4 to one handed 1, `dg_orc_reaver` two handed 4
  to polearm 1, the two Gondolin battlemasters one handed 32 to two handed 16.

## Not verified

No game check. Owed: the Lothlorien Marchwarden and Galadhrim lines and the Dale lake town line in
the encyclopedia and in a battle, and a look at the `gondor_loss_noble` line's new values against
the hand tune. The Mounted column of the table at the end reads Horse slots inside the rosters only; the 11
`Cavalry` troops without one carry a troop-level Horse row (checked on 2026-11-15).

FOLLOW-UP #343: 89 launcher troops have a melee skill above the launcher's ladder cell and stay warnings: dunland 15, dolguldur 11, rohan 10, southrhovanion 6, eredluin 5, mordor 4, gundabad 3, two each in anduinvale, angaladh, arthedain, cardolan, dale, dorwinion, erebor, goblin, gondor, harad, isengard, lindon, lothlorien, mirkwood, rhudaur, rhun_new and rivendell, and gundabad_snow 1. Capping their melee would lower troops, so this belongs to the decision on the level of the curve.
FOLLOW-UP #343: eight throwers have Throwing below their top melee skill and stay warnings; no value was invented. In `THROWN_PRIMARY_TROOP_IDS` (the formula gives Throwing the Ranged Bow curve): `gondor_har_skirmisher` Throwing 85 under OneHanded 100, `gondor_har_vet_skirmisher` 130 under 135, `gondor_har_javelineer` 170 under 185, `sagarun_naffatun` 195 under 250, `sagarun_storm_helmed_naffatun` 235 under 290; the formula gives each the value it already has. Not in it (the formula gives Throwing its group curve, again the value on disk): `gondor_loss_skirmisher` 20 under 100, `gondor_loss_axe_thrower` 40 under 135, `gondor_loss_vet_axe_thrower` 60 under 175. Decide whether the three `gondor_loss_*` throwers join the set and whether a thrower's Throwing should lead its melee.
FOLLOW-UP #343: the level of the curve against vanilla (recommendation B of the roster analysis) moves battle results and needs the damage matrix of #1271 re-run first.
FOLLOW-UP #343: Harad sits at about half of every other culture from tier 3 up (roster analysis, section 4).
FOLLOW-UP #343: nine culture files have no `CULTURAL_MODS` row (`anduinvale`, `angaladh`, `arthedain`, `cardolan`, `dorwinion`, `eredluin`, `feral_orcs`, `rhudaur`, `southrhovanion`).
FOLLOW-UP #343: `tools/fix_melee_ladder.py` still takes swap candidates outside the troop's weapon category (lesson "A tool that fits a troop to a number must hold the troop's weapon category fixed").
DONE #343: troops whose Item0 class rose over a class carried in every battle set (table below). The maintainer's words: "Its main weapon should be in item0 slot." The decision: the polearm goes into Item0 for lance cavalry (mounted Cavalry that carries a couchable polearm in every battle set) and for troops whose own name says spear, pike, lance or halberd; every other row stays, a swordsman with a spare spear. `python tools/rebalance_troops.py --main-weapon-slots --apply` exchanged the slots of 12 troops (29 battle sets) and the purpose order re-dealt their skills: rule A `dale_outrider`, `gondor_anf_cavalry`, `gondor_anf_vet_cavalry`, `mordor_warg_reaver`, `mordor_warg_ravager`, `mordor_warg_beastmaster` and the four `mordor_num_*` riders; rule B `gondor_lin_spearman`, `gondor_lin_vet_spearman`. The table's last column says what happened to each of its 54 rows: 12 changed, 19 where Item0 already holds the polearm (the Polearm over OneHanded rows were never a fault), 17 swordsmen with a spare polearm left by the decision, 4 Dol Guldur warg riders, changed in a second run on 2026-11-15 once their `sm_dg_khml_spear_*` could be couched (the Armory registration of `tools/register_couch_brace_polearms.py`), and 2 Dale lake town troops whose battle sets carry no polearm in every set. Ten horse archers carry a polearm behind another Item0 weapon and are reported, not touched: `gundabad_scout`, `gundabad_scout_merc`, `kharaghul_keshig` and the seven `rohan_wold_*` troops. The four riders wait for the Armory: `tools/register_couch_brace_polearms.py` makes `sm_dg_khml_spear_a` and `_b` couchable and braceable (the maintainer's request), and `--main-weapon-slots` then plans them. All 24 Cavalry rows are mounted: the 11 that the table first marked "no Horse slot" carry a troop-level Horse row outside their rosters. Whether a rider now starts with the lance in hand and still draws the sword in melee has not been checked in the game.
FOLLOW-UP #343: `python tools/rebalance_troops.py --apply --dry-run` and the purpose result disagree. Of the 369 troops `--fix-purpose` wrote, `--apply` would move 131, and 45 of those sat exactly on the formula before; roster-wide it plans a change to 407 troops, against 327 on the commit before. Nothing was restatted. The gap closes with the decision on the level of the curve.
FOLLOW-UP #343: `tools/generate_gondor_troops.py` is not the source of the Gondor skill values. A run of it changes 111 of the 188 Gondor troops and puts 22 of the troops #343 wrote back to their values before it, undoing their purpose order. The Dale, Lothlorien, Rhun and snow orc generators carry their own baselines too: a run changes every troop #343 wrote in their files (19, 15, 42 and 6), none back to the old values. Either make `rebalance_troops.py` their source, or retire their skill tables.
FOLLOW-UP #343: 97 `TROOP_SKILL_KIT_MISMATCH` warnings stand (the 89 launcher and 8 thrower lines above), and the code becomes an error only at zero. The maintainer decides both: the 89 with the level of the curve, the 8 with the thrower question.
Resolved by the troop skill curve ([rca](rca-troop-skill-curve-2026-11-15.md), round 4): `--apply` puts the Fords of War copies on the curve. Read as a troop file the same way (`rebalance_troops.process_file(<fords_troops.xml>, purpose=True)` then `plan_fix_purpose`, 2026-11-15), 0 copies would be reordered and 0 have a finding; it was 64 and 18 before the restat. `--apply` applies the purpose rule to them through the role table.
FOLLOW-UP #343: `tools/generate_enlistment_rosters.py` on trunk keeps its own inline `any('couch' in u.features for u in p.usages)` (about line 841). Point it at `melee_catalogue.couchable_ids`, so the enlistment rule, the slot rule and the validator read one source.
FOLLOW-UP #343: `tools/register_one_handed_polearms.py` has the same marker pattern (`block_re`, no count of markers) and a comment-blind item scan as `register_couch_brace_polearms.py` had before its review: a lone marker or a commented-out `<CraftedItem>` is not refused. Move it to `_marker_region.replace_region` and strip comments before scanning.
FOLLOW-UP #343: `register_couch_brace_polearms.py --check` cannot see a region in a template the spec no longer names, a duplicated line outside the region, or a region made redundant because the Armory lists the pieces itself. Its `defined_pieces` does not read vanilla crafting pieces (two starter spears use them).
FOLLOW-UP #343: the troop-level guard (a slot exchange that would move a troop-level row) lives only in `rebalance_troops.troop_kit_plan`. Move it into `troop_purpose.troop_main_weapon` and feed the writer and the validator the same XML in the parity test, which asserts only `len(found) == 1` today.
FOLLOW-UP #343: `rebalance_troops.main` writes the skills of every file before the slot exchange can raise, so a failed exchange leaves a partial write (#979 reads every output before it writes any).
FOLLOW-UP #343: `melee_catalogue.couchable_ids` reads the raw usage features, not the usage set the engine builds (`GetItemUsage` drops a feature a used piece excludes). No piece in the Armory, SandBoxCore or this repo excludes `couch` today (the excluded features are `long`, `swing`, `thrust` and `widegrip`).
FOLLOW-UP #343: `taom_schema.couchable_item_ids` drops the pricing failures of the placeholders, the Armory and vanilla silently (`price_placeholders` returns its problems, the melee ladder pass reports them, this call does not).
FOLLOW-UP #343: the validator's civilian-roster test accepts `civilian="True"` where `ranged_ladder.is_civilian` accepts only `"true"`; the two readings of a battle set can differ.

Troops whose Item0 class rose over a class carried in every battle set (HEAD is `078b86761`; Mark: "name" for spear, pike, lance or halberd in the name, "mounted" for a Horse slot in every battle set):

| Troop | Name | L | Group | Mounted | Item0 class, HEAD to now | Carried in every set, HEAD to now | Mark | Result |
|---|---|---|---|---|---|---|---|---|
| `dale_outrider` | [Dale] Dalian Merchant Guard | 16 | Cavalry | yes | OneHanded 85 to 125 | Polearm 125 to 95 | mounted | changed, rule A |
| `dale_guardsman` | [Dale] Dalian Guardsman | 21 | Infantry | no | OneHanded 130 to 155 | Polearm 155 to 130 |  | left: a swordsman with a spare polearm |
| `dale_lake_town_mariner` | [Dale] Lake-Town Veteran Watchman | 21 | Infantry | no | TwoHanded 122 to 155 | OneHanded 130 to 122 |  | left: no polearm in every battle set (the pair is two melee classes) |
| `dale_lake_town_veteran` | [Dale] Lake-Town Officer of the Watch | 26 | Infantry | no | TwoHanded 167 to 195 | OneHanded 180 to 167 |  | left: no polearm in every battle set (the pair is two melee classes) |
| `dg_warg_raider` | [Dol Guldur] Warg Rider | 21 | Cavalry | yes | OneHanded 130 to 163 | Polearm 163 to 130 | mounted | changed in the second run: the spear was made couchable, and Polearm is the top skill again |
| `dg_warg_skirmisher` | [Dol Guldur] Warg Ravager | 26 | Cavalry | yes | OneHanded 185 to 213 | Polearm 213 to 185 | mounted | changed in the second run: the spear was made couchable, and Polearm is the top skill again |
| `dg_warg_red_fang` | [Dol Guldur] Warg Fang | 31 | Cavalry | yes | OneHanded 235 to 268 | Polearm 268 to 235 | mounted | changed in the second run: the spear was made couchable, and Polearm is the top skill again |
| `dg_fell_warg_rider` | [Dol Guldur] Fell Ravager | 36 | Cavalry | yes | OneHanded 285 to 318 | Polearm 318 to 285 | mounted | changed in the second run: the spear was made couchable, and Polearm is the top skill again |
| `ironpass_axeman` | [Ironpass] Axeman | 26 | Infantry | no | Polearm 180 to 190 | TwoHanded 190 to 180 |  | left: Item0 already holds the polearm |
| `erebor_noble_guard` | [Erebor] Guard | 31 | Infantry | no | OneHanded 240 to 245 | Polearm 245 to 240 |  | left: a swordsman with a spare polearm |
| `iron_hills_noble_guard` | [Iron Hills] Guard | 31 | Infantry | no | OneHanded 245 to 255 | Polearm 255 to 245 |  | left: a swordsman with a spare polearm |
| `erebor_oathsworn` | [Erebor] Oathsworn | 36 | Infantry | no | OneHanded 280 to 285 | Polearm 285 to 280 |  | left: a swordsman with a spare polearm |
| `erebor_oathsworn_merc` | [Erebor] Hired Oathsworn | 36 | Infantry | no | OneHanded 280 to 285 | Polearm 285 to 280 |  | left: a swordsman with a spare polearm |
| `iron_hills_noble_gate_warden` | [Iron Hills] Gate Warden | 41 | Infantry | no | OneHanded 325 to 330 | Polearm 330 to 325 |  | left: a swordsman with a spare polearm |
| `iron_hills_noble_royal_warden` | [Iron Hills] Royal Warden | 46 | Infantry | no | OneHanded 345 to 350 | Polearm 350 to 345 |  | left: a swordsman with a spare polearm |
| `gondor_anf_cavalry` | [Gondor] Anfalas Cavalry | 21 | Cavalry | no Horse slot | OneHanded 125 to 150 | Polearm 150 to 125 |  | changed, rule A |
| `gondor_anf_vet_cavalry` | [Gondor] Anfalas Veteran Cavalry | 26 | Cavalry | no Horse slot | OneHanded 180 to 200 | Polearm 200 to 180 |  | changed, rule A |
| `gondor_ano_infantry` | [Gondor] Anórien Infantry | 26 | Infantry | no | Polearm 175 to 185 | OneHanded 185 to 175 |  | left: Item0 already holds the polearm |
| `gondor_arn_cavalry` | [Gondor] Arndir Cavalry | 26 | Cavalry | no Horse slot | Polearm 135 to 200 | TwoHanded 200 to 180 |  | left: Item0 already holds the polearm |
| `gondor_lin_spearman` | [Gondor] Linhir Spearman | 26 | Infantry | no | OneHanded 170 to 190 | Polearm 190 to 170 | name | changed, rule B |
| `gondor_arn_knight` | [Gondor] Arndir Knight | 31 | Cavalry | no Horse slot | Polearm 135 to 255 | TwoHanded 255 to 230 |  | left: Item0 already holds the polearm |
| `gondor_lin_vet_spearman` | [Gondor] Linhir Veteran Spearman | 31 | Infantry | no | OneHanded 225 to 255 | Polearm 255 to 225 | name | changed, rule B |
| `gondor_arn_vet_knight` | [Gondor] Arndir Veteran Knight | 36 | Cavalry | no Horse slot | Polearm 135 to 305 | TwoHanded 305 to 280 |  | left: Item0 already holds the polearm |
| `gondor_arn_hill_knight` | [Gondor] Arndir Hill-Knight | 41 | Cavalry | yes | Polearm 165 to 345 | TwoHanded 345 to 320 | mounted | left: Item0 already holds the polearm |
| `harad_reg_horseman` | [Harwan] Harad Horseman | 16 | Cavalry | no Horse slot | Polearm 61 to 66 | OneHanded 66 to 61 |  | left: Item0 already holds the polearm |
| `harad_reg_warrior` | [Harwan] Harad Warrior | 16 | Infantry | no | Polearm 61 to 66 | OneHanded 66 to 61 |  | left: Item0 already holds the polearm |
| `harad_sreg_horseman` | [Shaghana] Southron Horseman | 16 | Cavalry | no Horse slot | Polearm 61 to 66 | OneHanded 66 to 61 |  | left: Item0 already holds the polearm |
| `harad_sreg_warrior` | [Shaghana] Southron Warrior | 16 | Infantry | no | Polearm 61 to 66 | OneHanded 66 to 61 |  | left: Item0 already holds the polearm |
| `harad_reg_footman` | [Harwan] Harad Footman | 21 | Infantry | no | Polearm 73 to 78 | OneHanded 78 to 73 |  | left: Item0 already holds the polearm |
| `harad_reg_scout_cav` | [Harwan] Harad Scout | 21 | Cavalry | no Horse slot | Polearm 73 to 78 | OneHanded 78 to 73 |  | left: Item0 already holds the polearm |
| `harad_sreg_footman` | [Shaghana] Southron Footman | 21 | Infantry | no | Polearm 73 to 78 | OneHanded 78 to 73 |  | left: Item0 already holds the polearm |
| `harad_sreg_scout_cav` | [Shaghana] Southron Scout | 21 | Cavalry | no Horse slot | Polearm 73 to 78 | OneHanded 78 to 73 |  | left: Item0 already holds the polearm |
| `harad_reg_cavalry` | [Harwan] Harad Cavalry | 26 | Cavalry | no Horse slot | Polearm 85 to 90 | OneHanded 90 to 85 |  | left: Item0 already holds the polearm |
| `harad_reg_infantry` | [Harwan] Harad Infantry | 26 | Infantry | no | Polearm 85 to 90 | OneHanded 90 to 85 |  | left: Item0 already holds the polearm |
| `harad_sreg_cavalry` | [Shaghana] Southron Cavalry | 26 | Cavalry | no Horse slot | Polearm 85 to 90 | OneHanded 90 to 85 |  | left: Item0 already holds the polearm |
| `harad_sreg_infantry` | [Shaghana] Southron Infantry | 26 | Infantry | no | Polearm 85 to 90 | OneHanded 90 to 85 |  | left: Item0 already holds the polearm |
| `mordor_warg_reaver` | [Mordor] Nurn Warg Reaver | 16 | Cavalry | yes | OneHanded 65 to 95 | Polearm 95 to 70 | mounted | changed, rule A |
| `mordor_warg_ravager` | [Mordor] Nurn Warg Ravager | 21 | Cavalry | yes | OneHanded 115 to 140 | Polearm 140 to 115 | mounted | changed, rule A |
| `mordor_warg_beastmaster` | [Mordor] Nurn Beast Master | 26 | Cavalry | yes | OneHanded 170 to 190 | Polearm 190 to 170 | mounted | changed, rule A |
| `mordor_num_cavalry` | [Mordor] Black Numenorean Cavalry | 31 | Cavalry | yes | OneHanded 225 to 235 | Polearm 235 to 225 | mounted | changed, rule A |
| `mordor_num_vet_cavalry` | [Mordor] Black Numenorean Veteran Cavalry | 36 | Cavalry | yes | OneHanded 270 to 295 | Polearm 295 to 270 | mounted | changed, rule A |
| `mordor_num_knight` | [Mordor] Black Numenorean Knight | 41 | Cavalry | yes | OneHanded 320 to 350 | Polearm 350 to 320 | mounted | changed, rule A |
| `mordor_num_temple_knight` | [Mordor] Black Numenorean Temple Knight | 46 | Cavalry | yes | OneHanded 350 to 380 | Polearm 380 to 350 | mounted | changed, rule A |
| `darkhun_infantry` | [Rhûn] Darkhûn Infantry | 21 | Infantry | no | OneHanded 125 to 145 | Polearm 145 to 125 |  | left: a swordsman with a spare polearm |
| `loke_rim_footman` | [Rhûn] Loke-Rim Footman | 21 | Infantry | no | OneHanded 125 to 145 | Polearm 145 to 125 |  | left: a swordsman with a spare polearm |
| `darkhun_veteran_infantry` | [Rhûn] Darkhûn Veteran Infantry | 26 | Infantry | no | OneHanded 175 to 185 | Polearm 185 to 175 |  | left: a swordsman with a spare polearm |
| `loke_rim_infantry` | [Rhûn] Loke-Rim Infantry | 26 | Infantry | no | OneHanded 175 to 185 | Polearm 185 to 175 |  | left: a swordsman with a spare polearm |
| `darkhun_guard` | [Rhûn] Darkhûn Guard | 31 | Infantry | no | OneHanded 230 to 250 | Polearm 250 to 230 |  | left: a swordsman with a spare polearm |
| `dragon_wrath_disciple` | [Rhûn] Dragon-Wrath Disciple | 31 | Infantry | no | OneHanded 230 to 250 | Polearm 250 to 230 |  | left: a swordsman with a spare polearm |
| `darkhun_ironbound` | [Rhûn] Darkhûn Ironbound | 36 | Infantry | no | OneHanded 270 to 290 | Polearm 290 to 270 |  | left: a swordsman with a spare polearm |
| `dragon_wrath_infantry` | [Rhûn] Dragon-Wrath Infantry | 36 | Infantry | no | OneHanded 270 to 290 | Polearm 290 to 270 |  | left: a swordsman with a spare polearm |
| `rohan_edoras_recruit` | [Rohan] Edoras Recruit | 6 | Infantry | no | OneHanded 28 to 38 | Polearm 38 to 28 |  | left: a swordsman with a spare polearm |
| `rohan_edoras_militia` | [Rohan] Edoras Militia | 11 | Infantry | no | OneHanded 60 to 80 | Polearm 80 to 60 |  | left: a swordsman with a spare polearm |
| `umbar_elite_root0000` | [Umbar] Naru n'Aru Royal Guard | 31 | Infantry | no | Polearm 235 to 240 | OneHanded 240 to 235 |  | left: Item0 already holds the polearm |
