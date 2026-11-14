# RCA: the starter cap and the upgrade-path swaps (#1224, #631, 2026-11-13)

## Top-line

A player reported that the Isengard starter sword beat every Isengard sword in the shops (#1224).
The starter generator floored a crafted starter at its class anchor and never looked at the
culture's other weapons, so a strong tier 2 donor made a strong starter. The same session then
walked every troop tree for a higher tier that carries a weaker melee weapon than its ancestor
(#631) and fixed the cases it could with roster swaps under Mike's rule: lower tiers take vanilla
weapons, unique weapons stay with the higher tiers.

`/deep-review` ran at tier 1 (live `LOTRLOME_Armory` data) on the uncommitted working tree of
`bannerlord-1.5.x`: Lens 2 (engine), Lens 5 (data flow), Lens 7 (XML and ModuleData) and the Tooling
lens. Codex did not run: the user did not ask for it, so the pass is owed before merge. Lens 7 found
one HIGH, so a second wave ran Lens 3 (efficiency), Lens 4 (completeness) and Lens 6 (design) at
`effort: "high"`.

The first fix capped the blade factor at the weakest sibling's factor. The review showed that a
factor is not damage: the starter keeps its donor's geometry, so 15 of 44 capped starters still
out-hit a sibling, and the spears lost most of their thrust. The fix now caps in DPS, priced by
`melee_catalogue`. Row 15 repeats three earlier lessons.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | the T8 `lorien_gl_marksman` kept `he_sword` (78.3 DPS) in its second battle set while its parent, the T7 archer, now carries `wm_gf_knight_longsword` (95.9) in both, so about half of all Marksmen spawned 17.6 DPS under every Archer (Lens 7 #1) | data | the one-off walker scored each troop by its best set, and the swap planner keyed on one `(slot, item)` pair, so the second set's other sword was never a target | `generate_lothlorien_troops.py:287` gives both sets the longsword, `--apply`, `test_generated_files` green; lesson (per-set) |
| 2 | MED | `gondor_mt_fountain_guard` set 2 kept `wm_gondor_sword_a07` (44.4 DPS), 35.6 under its ancestors (Lens 7 #2) | data | as row 1 | `troops_gondor.xml:8865` is `wm_swan_knight_swordb` |
| 3 | MED | `gondor_pg_footman` sets 3 and 4 kept `wm_gondor_light_spear_pg` (28.1 DPS), 7.2 under the militia it upgrades from (Lens 7 #3) | data | as row 1 | `troops_gondor.xml:4145`, `:4154` are `wm_gondor_spear_a` |
| 4 | MED | `restat_melee_blades.py --culture anduinvale` lowered four `wm_dale_ws_*` blades that back Dale's own weapons, carried by 54 troops of four cultures; three T4 archers fell under their band and the Dale career kits lost 7 to 9 DPS (Lens 7 #4, Lens 5) | tooling | the tool labels an item with its first wearer's culture (`restat_melee_blades.py:141`) and `troops_anduinvale.xml` loads before the Dale file; the run's report listed the blades, and nobody read the list against the scope | the four factors put back from the pre-restat backup by `revert_dale_blades.py` (Armory backup `LOTRLOME_crafting_pieces.xml.bak-dalerevert-20261113-214129`), the starter kit re-applied; tool defect #1241; the doc says to read a `--culture` dry run's blade list; lesson |
| 5 | MED | the cap limited the blade factor, not the damage: a starter keeps its donor's geometry, so 15 of 44 crafted starters still out-hit or out-DPS a sibling, and spears lost thrust they never needed to lose (the Gundabad starter spear thrust for 14, the weakest Gundabad shop spear does 49) (Lens 2 F1, Lens 5 flow 2) | model | the docstring said "must not out-damage", and the code compared factors, which compare only on the same geometry | `sibling_caps` caps each stat at `factor × sibling best DPS / donor stat DPS` with prices from `melee_catalogue`; `test_caps_scale_each_factor_to_the_sibling_dps_and_round_down`, `test_starter_never_out_dps_a_weaker_same_culture_shop_sibling`; lesson |
| 6 | MED | `--verify` checked ids only, so a later restat could bring #1224 back with the gate green (Tooling F1) | gate | the gate was written when a starter's stats depended only on its donor; the sibling cap made them depend on other blades | `verify_plan` compares the starter blade block with the plan (`apply_pieces(...) != "noop"`); `test_verify_reports_a_starter_blade_whose_factor_drifted`; lesson |
| 7 | MED | the main cap test passed with `min` removed, because every fixture listed the weakest sibling last (Tooling F2) | test gap | the fixture order matched the answer | `_sibling_world` lists a stronger sibling after the weakest one |
| 8 | LOW | the cap counted siblings that no shop sells: `is_merchandise="false"` items, `_blunt` tournament weapons (the Sparring Scimitar capped three Harad starters) (Lens 2 F2, Lens 5 flow 1) | model | "same culture and template" was taken as "what the player can buy" | `shop_sibling` skips non-merchandise ids and the `_blunt`, `tournament`, `practice` markers; one decoy per filter in `test_shop_sibling_skips_each_decoy` |
| 9 | LOW | a lance capped the culture's spears to about 19.5 DPS (found in the fix's own measurement) | model | a lance's worth is the couch, which the DPS model leaves out | `NOT_COMPARABLE_ID_MARKERS = ("lance",)`, a lance decoy written RED first |
| 10 | LOW | a sibling with a 0.0 factor set a cap of 0 (`wm_mordor_set1_polearm_a01` thrust), one roster edit from a starter with no thrust (Tooling F3, Lens 5) | model | a missing attack was read as a weak one | the DPS rule skips a stat whose donor DPS is 0 and a sibling with no positive DPS; `test_a_stat_the_donor_cannot_use_is_not_capped` |
| 11 | LOW | a blade shared by donors of two cultures took the first donor's floors, so roster order decided the cap (Tooling F4, Lens 2 F8) | ordering | the blade cache predates culture-dependent caps | `merge_blade_floors` gives each shared blade the strictest floor of each stat; `test_a_shared_blade_takes_the_strictest_floors` |
| 12 | LOW | the gate prices Armory items only, so four troops on vanilla weapons above their band's top are invisible; the walker and `analyze_melee_ladder.py` treat `civilian="true"` rosters as battle sets (Lens 7 #6, Lens 5) | gate | the gate predates vanilla weapons on TAOM troops | the doc names the four troops; #1242 |
| 13 | LOW | engine claims in older text are wrong on v1.5.7: item XML reloads at every new campaign and save load, the couch mechanism, hidden-piece readers, culture and markets, a docstring's "no `value=`" (Lens 2 F3, F4, F7, F9, A1) | doc | each was written from an older dump or an incident, and none was in this change | #1243, with the in-game load check first |
| 14 | LOW | the Berserker restat raises the item's tier and price, and no doc said so (Lens 2 F6) | doc | the restat reports damage, not tier | the doc says the raise follows and is unmeasured |
| 15 | LOW | the doc called `generate_lothlorien_troops.py` a scaffold, so the first Lorien swap went into the XML only and `test_generated_files` failed (found by the build) | tooling | REPEAT of `lessons/build-tooling-workflow.md` "Before hand-editing a generated field, check whether a generator owns it", "After editing a generated file, grep `tools/` for the generator that owns it" and "Change data a generator owns through that generator": the claim came from a render without the placed skills, and no test was run first | the swap is in the generator table; the doc names the two tests that re-render each touched generator |
| 16 | LOW | the doc had wrong numbers: two Isengard troops (three lost the sword), "21 to 0 same-class" true only under the best-set score, rule 1 "inside its band" (a raise is clamped only at the top) (Lens 7 #7, Tooling F6) | doc | the numbers came from the first walker run and were not re-run after the fixes | the section names both scores and gives the walker's output of 2026-11-13 |
| 17 | MED | the #1224 body still described the factor cap and swing 1.21 / thrust 0.62 (Lens 4 #1) | doc | the issue was drafted before the review changed the design | the body is rewritten to the DPS rule and the live values |
| 18 | LOW | the "capped at" note fired for 4 starters the cap never moved: a stat already under the target still got a cap between the donor factor and the anchor (Lens 4 #3, Lens 6 P1) | report | the note tested `floors != anchor`, not whether the cap binds | `sibling_caps` caps only a stat above the target; `test_a_stat_already_under_the_target_is_not_capped`; the live dry run prints 11 notes, every file a no-op |
| 19 | LOW | the shared-blade merge inside `build_plan` had no test: going back to the first donor's floors passed every test (Lens 4 #5, Lens 6 P3) | test gap | `merge_blade_floors` was tested alone | `test_a_shared_blade_takes_the_later_donors_stricter_cap` (a Rivendell donor on the Gondor blade, listed second) |
| 20 | LOW | `load_melee_prices` read every installed module, so an id another module sorts first on (AWOIAF_Armoury) could take that module's price; it had no test of its tuple order (Lens 3 F1, Lens 4 #6) | data flow | the other five `mc.load` callers limit the modules; this one was written without the argument | `PRICE_MODULES` (the Armory and the three vanilla item modules); `test_load_melee_prices_maps_swing_then_thrust_from_the_armory_and_vanilla_only` |
| 21 | LOW | the prices loaded before the Armory check, and a `CatalogueError` ended the run in a traceback (Lens 3 F3) | error path | the load sat in `default_sources` | `main` loads them inside the `try`, after the Armory check; `load_melee_prices` raises `StarterKitError` |
| 22 | LOW | `shop_sibling` ran twice per donor, so the cap and the note could name different siblings (Lens 3 F2, Lens 6 P2) | simplicity | the note was added after the cap | `sibling_caps` takes the sibling `build_plan` found |
| 23 | LOW | the doc promised the starter's DPS "at or below the target"; 6 starters sit up to 0.19 DPS above it because the engine rounds damage to whole points; "12 blades changed by the rule" was 11 plus one restat (Lens 6 P4, Lens 4 #4) | doc | the claim was not priced after `--apply` | the doc states the margin and the split |

NOT APPLIED:
- Lens 6, the decision on rounding: an exact cap (carry the rounded damage and cap below the next whole point) is about 10 lines for a gap of 0.19 DPS that no tooltip shows. Rejected under the simplicity criterion; the doc states the margin. Undo: add the closed form in `sibling_caps`.
- Tooling F5 (wave 1): the `CraftedItem` guard in `shop_sibling` is unreachable from `build_plan`. It stays: tests and a scratch check call `shop_sibling` directly, and the guard is one condition.
- Lens 3 question 1, an index keyed on (culture, template): it saves about 20 ms per run and adds a structure.
- Lens 7 #5: five inversions under the 5 DPS tolerance (for example `cardolan_rider` 53.8 against `cardolan_lancer` 52.2). The tolerance is the gate's own; no change unless Mike wants a strict order.
- Lens 7 #6: the four vanilla picks above their band's top are already the weakest weapon in their vanilla family, so no swap can lower them.

Deferred:
- FOLLOW-UP #1241: `restat_melee_blades --culture` scope (row 4).
- FOLLOW-UP #1242: the gate's per-set scoring, vanilla pricing and `civilian="true"` (rows 1 to 3, 12).
- FOLLOW-UP #1243: the engine claims (row 13).
- FOLLOW-UP (no issue: a decision, not a defect) the Rhudaur career kits now start on `dunland_caerdh_axe_1h_b` (52.3 DPS, was 42.0), because `generate_career_kits.py` re-derived them after the Rhudaur swaps. The kit follows its rule; whether a stronger start is wanted is Mike's call, named in the report.

## Root-cause pattern

Rows 1 to 3 and 16 share one cause: the work measured a troop by its best battle set, and the
engine arms a troop from a random set per slot. A swap that changed one set passed every check
the work had.

Rows 5, 8, 9 and 10 share a second cause: the cap compared a proxy (the blade factor, or "same
culture and template") instead of the quantity the issue is about (the damage of a weapon the
player can buy). Each proxy agreed with the real quantity on the Isengard case and disagreed
elsewhere.

Rows 4 and 6 share a third: a tool's output depended on data outside the scope the operator chose
(a shared blade, a sibling blade), and the check after the run looked only inside that scope.

## Why each agent missed these

- **The author** wrote the walker and the cap, and tested each on the case in the report: the
  Isengard sword and the best set.
- **Lens 7** found rows 1 to 4, 12 and 16 by pricing every set of every troop; it did not read the
  generator's code.
- **Lens 2 and Lens 5** found row 5 from the engine formula (`Crafting.cs:374`, `:380`) and the
  measured damage, and row 8 from the merchandise readers; they did not price per set.
- **Tooling** found rows 6, 7, 10 and 11 by mutation and synthetic cases; its scope was the code,
  not the troop data.
- **The second wave** (Lenses 3, 4, 6) found rows 17 to 23. Each is in code or text the first
  wave's fixes wrote, and Lens 4 and Lens 6 found rows 18, 19 and 23 by pricing the applied result.

## Feedback memories to codify

None. The rules go to the lessons files: per-set scoring and proxy caps to
`lessons/data-content-cultures.md`, the scoped restat and the verify gate to
`lessons/build-tooling-workflow.md`.
