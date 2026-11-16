# RCA: weapons and armour far above the vanilla range (#1271, 2026-11-14)

## Top-line

Mike reported that some one handed swords hit for 140, enough to kill a 100 health troop in one
blow (#1271). The melee ladder (#631) was one sustained damage (DPS) curve for every weapon class,
with no limit on one blow, and the armour curve priced a pauldron at 0.6 of the chest, so the best
troop wore 112 on the body against vanilla's 67. Weapons had been pushed up to get through that
armour, and a weapon strong enough to hurt an elite destroyed everyone below it. The fix brings both
back to the vanilla range at once: class limits on the blow and the DPS, per-class tier targets over
ten tiers with a craft table, a tier limit per carrier, hero limits by maker, and armour judged on
what a troop wears in total (best ordinary troop body 67, lord kit at most 5 above).

The work is built in the worktree `E:/repos/taom-weapon-armour` (branch
`balance/weapon-armour-vanilla`) and dry run against the live Armory; nothing is written to the
Armory yet. Two deep review waves ran (the first, then four lenses), then a Codex review of the
uncommitted tree at `fccccd353`, then four fixers and an integrator. Codex graded its four findings
P2 (MED here); the other severities are the integrator's reading of each finding's effect.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | one DPS curve for every class and no limit on one blow: a placeholder one handed sword met its tier 10 target of 119 DPS with a 140 swing, shield spears thrust for 123 (the reported bug) | model | the ladder ranked weapons by sustained damage, which is right between weapons of one class and says nothing of one blow; a slow weapon meets a DPS target with a huge blow | `class_limits` in `melee_ladders.json` (blow and DPS per template, the vanilla maximum), `melee_ladder.max_factor`, gate `MELEE_BLOW_CEILING`; `test_a_one_handed_sword_cannot_kill_a_100_health_troop_in_one_blow`; lesson |
| 2 | HIGH | pauldrons priced at 0.6 of the chest put the best troop's body sum at 112, so a 95 point sword cut did 3 to an elite and weapons rose to compensate | model | each item was judged against its own slot cap, never the sum a troop wears on one body part, which is what a hit meets | `SLOT_CAP_RATIO` pauldron 0.28, bracer 0.5, greaves 0.45; body sum pinned at 67 in `test_kingdom_caps.py`; gates `WORN_ARMOUR_CEILING` and `WORN_ARMOUR_FLOOR` on the worn sum; lesson |
| 3 | MED | the first fits of weapons against armour left out the wielder's weapon skill multiplier, so the damage that lands was understated | model | the fit used the displayed blow, the only number the tools price | the class limit is stated as a limit on the displayed blow, and the in-game check assumes a body hit on armoured troops (`melee-damage-model.md` "Class limits"); lesson |
| 4 | MED | the first armour change kept the legacy secondary ratios and read a stale roster map | model | the change replaced the caps and kept what the caps fed | secondaries 0 below heavy and capped from heavy up (`SECONDARY_SHARE_CAP`), the roster map regenerated with `derive_armor_tiers.py` |
| 5 | MED | tournament prizes: under the planned values most light and medium armour falls under the Tierf 2 floor of the regular prize band (754 of 868 candidates) (first wave) | consumer | the prize rule read engine Tierf, which the new values move | `TournamentPrizeRules.Fits` judges a class table piece by its class; superseded by rows 18 and 22 |
| 6 | MED | blades shared by one handed and two handed weapons were priced for the one handed wearer and took the two handed twins down (an Erebor two handed axe swing from 126 to 75) | data flow | damage lives on the blade piece, and the restat priced a blade without asking which classes it backs | `mixed_blades`: such a blade is not priced by tier and is lowered only to a class or hero limit; every run lists them |
| 7 | MED | the restat anchored a blade on the first troop loaded, not its weakest carrier | ordering | not recorded in the reports the integrator holds | the blade is priced from the weakest carrier's tier and craft row and held to the tightest tier limit over every carrier |
| 8 | MED | the restat was not idempotent: a second run on its output planned more moves | tooling | not recorded in the reports the integrator holds | the primary attack's ratio drives the other, no move under 2 percent or one damage point, the tolerance judged on the written factor; `test_a_raise_that_rounds_down_to_inside_the_tolerance_is_not_planned`; a second simulated run plans 0 |
| 9 | MED | placeholders were capped at the spec's tier, not their lowest carrier's, and the gate could not see them | gate | the cap and the gate both took the spec row as the only carrier | `carrier_rows` caps over every carrier; the gate prices `taom_ph_` placeholders (`price_placeholders`) |
| 10 | MED | `fix_melee_ladder.py` picked a swap by the DPS band only, and 430 of 582 swaps landed above the carrier's tier limit | model | the band is a DPS window; a slow heavy weapon sits in it while showing a blow the tier may not carry | a swap must fit the carrier's tier limit; 189 swaps now, none above it |
| 11 | MED | hero kit assigned through `lords.xslt` battle templates was missed, so lord pieces took a full troop restat | data flow | `derive_armor_tiers.py` read the XML heroes and not the stylesheet that hands the retagged vanilla lords their sets | the xslt templates and direct `<equipment>` overrides are read (`heroWorn` 471 to 532 items) |
| 12 | MED | Mordor's named hero kit was priced on the `mordor/` folder's orc cap (36), putting Sauron's torso under a Gondor elite troop | model | the folder decided the line, and the lords' kit shares the folder with the orcs | `MORDOR_LORD_KIT_MARKERS` routes it to `mordor_numenorean` (cap 46) |
| 13 | LOW | starter armour literals sat above the culture's new light band | data | the generator holds hand-set numbers the curve never reads | each value is the smaller of the literal and the culture's light band |
| 14 | MED | the armour writer rewrote `weight` under `--keep-weights` | tooling | not recorded in the reports the integrator holds | weight untouched under `--keep-weights`; checked on a copy (no weight text changed) |
| 15 | MED | `--apply` ran without a backup unless a tag was given | tooling | the backup was opt-in on a writer of an unversioned install | `--backup-tag` is required with `--apply` (exit 2 without it) |
| 16 | MED | the apply sequence had gaps (first wave) | doc | not recorded in the reports the integrator holds | `armor-balance.md` "The apply sequence" rewritten; its armour writer run on a copy (83 files written, a second run 0) |
| 17 | MED | `CommitGateCoverageTests` matched codes in source text and missed one whose severity sat in a constant (`WORN_ARMOUR_CEILING_SEVERITY`), so a flip to ERROR without its hook line would have passed the test (second wave) | test gap | the scanner read `severity=Severity.ERROR, code=...` literally | `_severity_constant_codes` resolves the constant; `test_a_severity_constant_flipped_to_error_makes_its_code_an_enforced_code`; lesson |
| 18 | MED | the tournament call site passed a bool flag for table membership (second wave) | design | the flag was derived at the caller, with no test of the derivation | `Fits` takes `ArmourClass? tableClass` and works out the class inside; superseded in part by row 25 |
| 19 | LOW | code comments carried counts and dated incident stories that nothing computes (second wave) | doc | the counts were true when written | comments state the mechanism; the numbers live in the feature docs |
| 20 | LOW | the Tierf range sentence in the arena doc and comments was wrong for today's values (second wave) | doc | it described the planned Armory, not today's | the text says the Armory rewrite is a later step and the rule already applies to today's values |
| 21 | LOW | carried weapons ended above their tier limit after the restat: factors rounded to nearest, and the DPS cap read a whole-number damage as exact (second wave R1) | model | the cap was derived on real numbers and the engine rounds damage | `melee_ladder.floor_factor` for every written factor; the DPS cap keeps the largest whole damage inside the limit with half a point of margin; `test_a_weapon_at_the_dps_cap_cannot_round_its_damage_up_past_the_dps_limit` |
| 22 | MED | the engine draws a tournament prize from a Value sorted pool, so a tabled light piece worth 162 sat in the regular band (second wave E1) | consumer | the rule was judged on class, and the engine judges on Value | a listed light, medium or civilian piece needs Value 750 (`RegularMinTableArmourValue`) |
| 23 | MED | readers of armour `Value` and `Tier` outside the tools were not listed (second wave S5) | doc | the plan priced the items and never asked who reads the price | `armor-balance.md` "Consumers outside the tools", each with its decompile line |
| 24 | MED | wanderer templates were not read as heroes, so `sk_dg_uruk_helmet_elite_a` would rise from 41 to 43 (Codex 1) | data flow | the templates say `is_hero="false"`, and the engine makes them heroes in `CompanionsCampaignBehavior` | `derive_armor_tiers.py` reads `is_template="true"` plus occupation Wanderer as heroes, never as anchors; `test_a_wanderer_template_helmet_is_hero_kit` |
| 25 | MED | `TournamentService` read the effective class (`GetClass(id).HasValue`) as table membership, and an untabled piece gets a class from its engine tier (Codex 2) | design | the rule tests supplied the provenance themselves, so the caller's derivation was never tested | `IArmourGateService.GetTableClass` returns a class only for a table entry; `GateToPrize_UntabledBodyArmourAtTier0_StaysUnderTheTierfFloor`; lesson |
| 26 | MED | the unworn pass edited a blade that only exempt troops carry when an unworn sibling shared it (Codex 3) | tooling | the exemption was decided in one pass and not honoured in the next | `exempt_only_blade` in every pass (`lower_to_limit`); `test_an_unworn_sibling_never_moves_a_blade_that_only_exempt_troops_carry` |
| 27 | MED | the apply sequence had no step to carry roster swaps into the troop generators, so a later generator run reverts them (`cardolan_levy`) (Codex 4) | doc | the sequence listed writers of the XML, not the owners of the XML | generator steps 7, 10 and 13 after each roster repair; the second commit names the generator sources |

NOT APPLIED:
- L3, one exclusion helper for the gate and the analyzer: the gate's troop records carry no name, and `BESPOKE_RIDERS` lives in `analyze_kingdom_armour.py`, which imports `taom_schema`. A drift pin (`test_the_creature_markers_are_the_balance_analyzers`) and the shipped-data pin hold them equal.
- `CodeList` in `test_githooks_pre_commit.py` passes silently when the Claude hook file is missing; left as it is.
- The whole-point DPS cap costs 18 troops their band in the scale-only simulation (599 of 965 inside after the restat, against 617 with the old cap) and 9 when re-priced through the engine port (604 against 613); the limit is the rule and the band the target.
- The convergence review found that `GetTableClass` returned null for a named item the class table does not list, so a hero weapon could pass the prize rule by its engine tier; it now reports Named, with `GetTableClass_NamedWeaponNotInTheTable_IsNamed` and `GateToPrize_NamedWeaponOutsideTheTable_IsNeverAPrize`.

Deferred:
- FOLLOW-UP #631: blades shared by one handed and two handed weapons need cloned blades; until then the Erebor one handed axes stay above their tier limits and 8 carried weapons stay over after the restat.
- FOLLOW-UP no issue: sub-floor heavy and elite troops (42 under `WORN_ARMOUR_FLOOR` today, 203 after the apply dry run) need roster swaps, a data pass nobody has scoped.
- FOLLOW-UP no issue: 46 troops (aserai, empire, shaghana, umbar) wear no Armory kingdom armour and are not on the curve; a content decision.
- FOLLOW-UP #617: the hero ranged ceiling (Bow 90, Crossbow 105) sits above the new troop tops (65 and 84).
- FOLLOW-UP no issue: horse harness has no curve or gate; out of this scope.
- BUILT AND REVIEWED 2026-11-15, #1271: heavy, elite and lord extremities roll `leather` (down-rolls -2 and -3), not `chain` (-6 and -8), so a heavy pauldron of 9 drops to 6 at worst on a new roll, not 1 (`armor-balance.md` "Open decisions", item 1). The group also sets what a troop spawns with in a campaign battle. The live Armory write (the restat, then both generators) and the in-game check in a campaign battle are owed.
- FOLLOW-UP #631: `wm_gondor_boromir_sword`, a hero blade, is carried by ordinary Gondor t7 and t8 troops; the decided roster swap is not done.
- FOLLOW-UP no issue: couch-skipped spears (`he_spear`, `dale_winged_spear_a` and `_b`) need hand pricing, the same couch rule as #812 but not its Harad spears.
- FOLLOW-UP no issue: `characters/npcs_*.xml` fighters (soldiers, caravan guards) are read by none of derive, the restat or the worn gates; scope not decided.
- FOLLOW-UP no issue: thrown weapons are not priced by `melee_catalogue` and have no baseline.
- FOLLOW-UP no issue: warg and spider bites scale with chest armour and are not re-tuned against the new values; needs an in-game check first.
- FOLLOW-UP no issue: prices and engine item categories follow the new armour (a Gondor medium chest 13,214 to 765); not re-balanced, needs a decision.
- FOLLOW-UP no issue: the `hero_makers` grouping is the author's reading of the lore and waits for the maintainer to correct it.
- FOLLOW-UP no issue: `WORN_ARMOUR_FLOOR` stays a warning; whether it becomes an error after the apply is the maintainer's call.
- FOLLOW-UP #631: retire the legacy melee curve (`kingdom_offset`, `band`, `ceiling`) once every template has a class limit.
- FOLLOW-UP no issue: the Gondor starter greave in `LOTRLOME_items/gondor/starter_armors.xml` holds the dead modifier group `mail`, and no check proves that every live armour `modifier_group` is a defined group (`cloth_unarmoured` is its band's extremity group).
- FOLLOW-UP no issue: no gate notices a stale roster map (`tools/data/armor_roster_tiers.json`) after a troop edit, so `armour-restat` can pass on an old curve; a `derive_armor_tiers.py --check` with a `data_gates.py` row would catch it.
- FOLLOW-UP no issue: the restat writer rewrites every declared stat of a changed item and does not escape decoded values (pre-existing; the 2026-11-15 write was proven byte-safe on a copy of the live Armory).

## Root-cause patterns

- **A fit made on a model that leaves out a multiplier** (rows 1, 3, 21, 22). The ladder priced DPS
  and left out the blow; the fits priced the displayed blow and left out skill; the cap priced real
  numbers and left out the engine's rounding; the prize rule priced class and left out Value. Each
  model agreed with the engine on the case it was built from.
- **A check that shares the filter of the edit it checks** (rows 9, 17, 26). The placeholder cap and
  the gate both saw only the spec row; the coverage test read the same literal form the code had
  stopped using; the unworn pass and its own exemption check looked at one pass.
- **Several writers of one stat** (rows 6, 13, 27). A blade's factor is written for every weapon on
  it; starter armour has literals beside the curve; troop XML has a generator and a roster fixer.
  Each writer was correct alone and undid or overrode another.
- **A doc or comment that states a planned state as done** (rows 16, 19, 20, 27). The apply
  sequence, the Tierf sentence and the counts in comments described the Armory as it will be.
- **Provenance derived at the call site** (rows 18, 25). The rule was pure and well tested; the
  caller decided what to pass it, and nothing tested the caller.

## Why each reviewer found what it found and missed what it missed

- **The author** built each part against the case in the report (the 140 sword, the 112 body sum)
  and the tests checked those parts alone, so the joins between parts (blade sharing, generator
  ownership, the tournament caller) had no test.
- **The first wave** read the tools against the data and the engine and found the data flow and
  tooling rows (5 to 16). It judged the code before its own fixes existed, so it could not see what
  those fixes introduced.
- **The second wave** (four lenses) read the first wave's fixes and found rows 17 to 23, each in code
  or text those fixes wrote: the severity constant, the bool flag, the comments, the Value quarter.
- **Codex** reproduced each of its four findings on real data or with the production classes
  compiled in memory (the Dol Guldur helmet's 41 to 43, the untabled tier 0 chest, the troll blade,
  the Cardolan generator). It ran the armour and melee sequences in memory, and so found the two
  rows at the edges of the tools (24, 27). It did not run the full suites or any writer.
- **The melee fixer** found row 21's last six weapons by re-pricing the restat in memory after its
  own fix; the integrator applied the whole-point DPS cap it had tried in memory.

## Feedback memories to codify

None. The rules go to the lessons files: the one-blow limit, worn totals and engine multipliers to
`lessons/data-content-cultures.md`; the severity constant, data not yet applied and call site
provenance to `lessons/build-tooling-workflow.md`.

## Found in the review of the written Armory (2026-11-14)

Three reviews read the files the apply wrote. Each finding below carries its state.

- **The generator band defect (fixed and applied).** `generate_lothlorien_armor.py` and
  `generate_snow_orc_armor.py` priced each of their 97 pieces by the tier word in its id, while every other
  Armory piece is priced at the band of its lowest wearer. The generators run after the restat, so they
  undid it for 47 of the 79 worn pieces: `sk_ll_rg_helmet_med_b` stayed at 17 for a level 31 wearer
  (elite, 49). Both generators now price a worn piece at its lowest wearer's band, an unworn piece by its
  id word, and Mallorn one band above either; the appearance stays with the id word. Both generators were applied to
  the live Armory (`--check` passes for both, `armour_classes.xml` regenerated): 55 items changed (50
  Lorien, 5 Snow Orc), no unworn piece changed, 13 troops left `WORN_ARMOUR_FLOOR` (196 to 183), no
  troop reaches a ceiling. Three level 46 Lothlorien troops wear the Mallorn kit one band above their
  own, by design (`armor-balance.md`, generator band fix).
- **The Umbar repair (reverted by the session lead).** It gave `umbar_elite_root0000` vanilla Aserai armour
  above the Umbar curve. The parent `umbar_elite_root000` and 38 other troops wear uncurved vanilla armour.
  With the Umbar file restored, the repair's net diff is 146 changed equipment lines across 17 troops in 6
  files (`git diff --numstat` on `Main/_Module/ModuleData/troops`).
- **Enlistment kits are not regenerated.** A full `generate_enlistment_rosters.py --apply` rewrites the `enlist_<culture>_*` rosters that `promote_borrowed_cultures.py` also owns for the eight promoted cultures, and their sync checks then fail; the run was reverted. With the new armour values 14 enlistment ranks give a weaker kit than the rank below (an Erebor recruit 81, the soldier 67).
- FOLLOW-UP #1290, done 2026-11-15: not a reseed. A reseed redraws each culture's donors from today's troop trees and changes kit categories (the Erebor infantry soldier went from sword and shield to spear, shield and axe in its dry run), which the category rule forbids. The player keeps every kit, so the rule holds each kit against every kit drawn before it. On the ModuleData of 12b21d5e3, `python tools/validate_moduledata.py --moduledata <that tree> --code ENLISTMENT_KIT_RANK_REGRESSION` printed 152 kits on 2026-11-15 (16 armour, 91 weapon, 85 dropped-category and 3 lance reasons). After four hand edits (the default archer veteran and sergeant keep `lowland_longbow`; the Isengard cavalry veteran wears Isengard plate and helmet), `generate_enlistment_rosters.py --repair-ranks --apply` changed 66 slots under the maintainer's saddle rule (a kit with a Horse slot is a horse archer's, one without a foot archer's), by the source its dry run prints: 32 weapons from the culture's troop gear of the same kind, 21 from its troop gear of another kind of the same category, 9 armour pieces from its troop gear, and 4 bows from an earlier kit (`wm_isengard_bow_a02` into the Dol Guldur archer veteran, `wm_mirkwood_bow_a01` into the Mirkwood archer soldier, and the soldier kit's `woodland_longbow` into the Rohan archer veteran and sergeant). The Arnor `item_map` row for `sk_gd_pin_spear_helmet_heavy_a` now maps to `sk_ar_art_helmet_inf_elite_a`, and `promote_borrowed_cultures.py --only <c> --apply` re-derived the promoted cultures; `--check` reports no change for all eight. `python tools/validate_moduledata.py --code ENLISTMENT_KIT_RANK_REGRESSION` prints 82 kits now (2026-11-15), with no armour reason: 2 weapon reasons (the Rohan cavalry sergeant's lance after the veteran kit's spear, and its South Rhovanion clone) and 87 dropped reasons (85 categories and 2 lances), which wait for a maintainer decision per chain ([enlistment.md](../features/enlistment.md#a-ranks-kit-is-never-weaker-than-the-kit-before-it-1290)).

Deferred:

- FOLLOW-UP no issue, rule done 2026-11-16: `fix_melee_ladder.py` had no weapon category rule; it now keys candidates on `weapon_category` (dry run: 125 swaps, none across a category). The roster pass itself is still not applied. Its plan of 176 swaps replaced poleaxes with short spears, so it was not applied and the 318 `MELEE_LADDER_INVERSION` warnings stay (`melee-damage-model.md`, "A troop keeps its weapon category").
- DONE 2026-11-15 (#1271): lords and companions wore light troop chests. The maintainer decided that companions stay light, that lords wear lord pieces, and that variety outranks armour points; a part within 10 percent (or 3 points) of the reference is left alone. `tools/fix_lord_kits.py` judges a lord by his worst whole battle set on head, body, arm and leg, gives the generic lord templates different pieces of their culture's lord pool, then repairs a short part from the pool or the culture's own folder pieces (elite troop pieces, their id-stem siblings and unworn pieces), reusing a piece on a second template only when the own piece is more than 25 percent under and the reuse gains 3 points, and holds the fallback and ruler rosters to template `_a`; `LORD_ARMOUR_RANGE` reports one row per generic template and one per named roster. Lords and rosters short on head 595 to 322, body 533 to 248, arm 670 to 189, leg 616 to 275 (`python tools/fix_lord_kits.py`, 2026-11-15, live values; HEAD on a copy of ModuleData with HEAD's `equipmentsets/`). Not verified in game; the lord piece restat is a dry run that the session lead writes after the merge (armor-balance.md, "Lord kit").
- FOLLOW-UP #1333, done 2026-11-15: lord armour variants (`tools/generate_lord_armour_variants.py`, [armor-balance.md](../features/armor-balance.md#lord-armour-variants-2026-11-15-1333)) give each Harad template its own chest and helmet at the lord band (head 44 under a spec cap, body 54 or 55, arm 26 to 29, leg 36 against the reference 39/51/25/39; no part short), and bring back Mirkwood's four looks of 12b21d5e3 at 50/64 or 65/44 or 45/43 against 50/61/43/41. Each variant chest takes the arm share of the culture's reference chest, so no template is lower on any part than before.
- FOLLOW-UP #1271: Harad's body and leg stay short on all five templates (body 35 to 39 against 51, leg 12 against 39) and the head on four (34 against 39): no piece of the Harad folder that rule (b) admits raises them within the limits. Its lords need lord armour of their own line (art, or a restat of an unworn Harad piece). The infantry and archer heavy chests (27/8, the cavalry chests' values) would give the templates distinct Bodies, but troops of level 26 wear them and they share no id stem with an elite piece, so rule (b) as decided does not take them.
- FOLLOW-UP no issue: Mirkwood and Dale need more lord-grade chest variants (art, or lord copies of the militia-worn chests) before each template can have its own: all five Mirkwood templates now share `mkwd_inf3_chest_silver`, and Dale `_c` and `_d`, `_g` and `_h` share a chest each. Mirkwood done 2026-11-15 by lord copies (#1333): four chests over five templates, `_c` and `_d` sharing the palace-guard look; Dale is open, and a Dale row in `tools/lord_armour_variants.json` is the route.
- FOLLOW-UP no issue: the Khand fallback and ruler rosters (`battania`) mirror Harad `_a` while Khand's lords wear the Dunland templates, and the Blue Crag rosters mirror `bluecraig_bat_template_medium_a` while Blue Crag's lords wear the goblin templates (`fix_lord_kits.FALLBACK_SOURCES`); recorded, not changed.
- FOLLOW-UP no issue: 62 armour pieces (12 of them chests) that no female lord or female fallback roster wore at HEAD are now worn by one; the Armory has no gender usage flags, so their female fit needs an in-game look.
- FOLLOW-UP #609: 96 pairs of a lord-pool piece and a troop under level 41 who wears it (docs/reference/lord-armour-pool.md) need a hand-picked substitute for those troops before those lord pieces can be priced for lords.
- FOLLOW-UP no issue: the regular tournament armour pool fell from 756 to 222 pieces; whether that is enough variety needs a decision on the pool or on `RegularMinTableArmourValue`.
- FOLLOW-UP no issue, done 2026-11-16: `wm_mordor_set1_polearm_a01_blade` had a thrust factor of 0.00 from before the write. The engine reads a declared `<Thrust>` as a usable Pierce thrust whatever its factor (`BladeData.Deserialize`), so "[Mordor] Orc Polearm I" thrust for 0 in the hands of its ten carriers. The live factor is now 1.73, blade a02's value from the same set: thrust 27 like polearms II and III, swing unchanged at 82; `restat_melee_blades.py` plans nothing on it, and `generate_starter_kit.py --apply` regenerated the starter twin (backup tag `mordor-thrust-20261116`, mirror lotraom-assets `dbbe3d343`).
- FOLLOW-UP no issue: `restat_melee_blades.py` reads an attack with 0 damage as one no weapon can use (`within_tolerance`), and no gate reports a declared attack whose factor is 0, so the case above was invisible to every check; the Armory holds no other such blade today.
- FOLLOW-UP no issue: `analyze_armor_balance.py` mixes tier sources, so 6 of its 10 errors are false; it needs one source throughout.
- FOLLOW-UP no issue: no gate fails when a reset brings old armour or blade values back; `rebalance_armor.py` and `restat_melee_blades.py` need a check mode registered in `data_gates.py`.
- FOLLOW-UP no issue: the starter armour literals are still the old ones, because `generate_starter_armor.py` overwrites the hand fixes in `harad/starter_armors.xml` and Gondor's file needs a hand edit.
- FOLLOW-UP no issue: the 39 troops on uncurved vanilla armour (the Umbar finding above) are a content decision for the maintainer.
- FOLLOW-UP no issue: the mirror and release steps are the maintainer's: commit the 118 files to the `lotraom-assets` mirror, bump the Armory version, sweep the 102 backups, re-stage the dedicated server.
- FOLLOW-UP no issue: the armour class table links a piece to an "upgrade" with equal armour (`sk_ll_ml_pauld_med_a` to `sk_ll_rg_pauld_light_a`, both body 9 and arm 8), because `generate_armour_classes.pick_next` accepts a candidate that is not better on any value.
- FOLLOW-UP no issue: the material type of the generator pieces follows the price band while the rest of the Armory kept its material; it changes hit sounds only.

The rule behind the fixer finding goes to `lessons/data-content-cultures.md` ("A tool that fits a troop
to a number must hold the troop's weapon category fixed").
