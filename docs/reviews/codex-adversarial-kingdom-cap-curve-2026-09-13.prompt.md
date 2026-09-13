# Codex adversarial review: the kingdom-cap armour curve (#583) and the kingdom armour overview (#581)

You are reviewing an UNCOMMITTED changeset in E:\repos\TAOM (branch bannerlord-1.4.5). It is Python tooling plus game data, no C#. The tooling has ALREADY been applied to the live Bannerlord Armory (the game's LOTRLOME_Armory module) and to its assets mirror, so a wrong rule here is a wrong number in the shipped game. Be adversarial: try to refute every claim below, and try to find the number, the item or the troop the tooling got wrong.

Other sessions have unrelated uncommitted files in the same tree. IGNORE anything under Main/Features/SmartCavalryAI, Main/Adapters, Main/_Module/ModuleData/TroopWeights, Main/_Module/ModuleData/taom_partyTemplates.xml, docs/features/black-numenorean.md, docs/features/lord-party-templates.md, docs/features/troop-weight-system.md, docs/modding/*.md, tools/wire_black_numenorean_troops.py, TAOM.Tests/**. Those are not part of this review.

## What was built (2 lines)

The armour item curve was replaced by a per-kingdom chest cap set by the maintainer (Erebor 70, Rivendell/Lindon 68, Mirkwood 63, Lothlorien 60, Gondor/Rhun/Black Numenoreans/Arnor 57, Gundabad 49, Dol Guldur/Khand 46, Isengard 45, Dale/Harad/Umbar/mercenary 44, Black Uruks 43, Rohan/Dunland 40, the shared orc kit 38, Thenn 35), with helmet 0.9, bracer 0.6, pauldron 0.6, greaves 0.5 of the cap, bands light 0.40 / medium 0.64 / heavy 0.84 / elite 1.0 / lord = elite, an item banded by its LOWEST battle-set troop wearer, hero kit excluded, and applied to all 2,826 Armory armour items (2,510 changed). A read-only overview tool and a validator gate compare the kingdoms side by side by engine tier.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid culture ID (Rohan uses "vlandia"); "dol_guldur" is NOT a valid culture ID ("dolguldur" is). BUT the Armory FOLDER names and the curve's KINGDOM_CAPS keys use their own spelling (folders rohan, dol_guldur, rhun; troop FILES troops_rohan.xml, troops_dolguldur.xml, troops_rhun_new.xml). Three namespaces: culture StringIds, Armory folders, troop-file stems. Do not report a folder or file name as a wrong culture id.

## READ FIRST

- docs/features/armor-balance.md, sections "The curve", "The kingdom-cap curve (2026-09-13, #583)", "Kingdom armour ladder: cross-culture (2026-09-12, #581)"
- docs/features/moduledata-validation.md, sections "Upgrade armour regression" and "Cross-culture armour inversion"
- docs/reviews/rca-kingdom-cap-curve-2026-09-13.md and docs/reviews/rca-kingdom-armour-2026-09-12.md (what two deep reviews already found and fixed; do not re-report those as new, but do check the fixes)
- tools/README.md rows for rebalance_armor.py, derive_armor_tiers.py, analyze_armor_balance.py, analyze_kingdom_armour.py, fix_upgrade_armour_regressions.py
- .claude/rules/moduledata-validation.md (the XML I/O convention every writer must follow)

## Files in scope

Curve and writer:
- tools/rebalance_armor.py (KINGDOM_CAPS, SLOT_CAP_RATIO, BAND_RATIO, LINE_PREFIXES, FOLDER_DEFAULT_LINE, kingdom_key, level_to_band, cap_value, calculate_stats with item_id, _roster_first_tier, process_file with keep_weights / keep_material_type / backup_tag and the secondary-stat ratio rule, apply_changes_via_regex with _inside_comment, HERO_NAME_FALSE_POSITIVE_PREFIXES, EXCLUDE_ID_SUBSTRINGS without md_num, _EXTREMITY_MODIFIER_GROUPS)
- tools/derive_armor_tiers.py (level_to_tier = ra.level_to_band, parse_rosters battle sets only and LADDER_EXEMPT_TROOPS never anchor, id_keyword_tier with a civilian branch, derive() anchor-first precedence)
- tools/analyze_armor_balance.py (check_kingdom_curve_invariant, KINGDOM_INVARIANT_PAIRS, is_excluded mirror)
- tools/data/armor_roster_tiers.json (regenerated)

Gate and overview:
- tools/taom_schema.py (Registries.item_folder, build_item_folders, item_cap_for, scale_to_reference_cap, REFERENCE_CAP, Validator._slot_armour_avg with folders, Validator._cross_culture_armour_inversions, cross_culture_armour_inversions, _ARMOUR_LADDER_EXEMPT)
- tools/analyze_kingdom_armour.py (NEW: build_records, matrices, cross_culture_inversions, troop_scaled_total, gate_cells, gate_preview, ceilings, curve_view, render_report, main)
- tools/fix_upgrade_armour_regressions.py (load_item_armour stats/type/name, load_troops name/group/has_level)
- tools/analyze_troop_balance.py (MOUNT_RIDER_MARKERS + mumak)

Data written by the ladder repair (git diff): Main/_Module/ModuleData/troops/troops_{dolguldur,erebor,goblin,gondor,gundabad,harad,isengard,lindon,mordor,rhun_new,rivendell,rohan}.xml

Tests: tools/tests/test_kingdom_caps.py, tools/tests/test_analyze_kingdom_armour.py, tools/tests/test_validate_moduledata.py (CrossCultureArmourInversionTests), tools/tests/test_fix_upgrade_armour_regressions.py (LoaderTests), tools/tests/test_armor_curve_invariant.py

Live data (read-only for you): the applied Armory at "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\<folder>\{head,body,arm,leg,shoulder}_armors.xml"; every written file has a sibling ".bak-kingdomcurve-583" holding the pre-curve bytes; the mirror at "E:\repos\lotraom-assets\v1.4\LOTRLOME_Armory\ModuleData\LOTRLOME_items" is byte-identical. The vanilla item files are under "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items".

Commands you may run (all read-only; NEVER pass --apply to anything):
- python tools/rebalance_armor.py --dry-run --tier-source roster-first --keep-weights --keep-material-type   (must print Changed: 0)
- python tools/derive_armor_tiers.py --stdout   (rewrites the committed JSON's generatedAt only; fine)
- python tools/analyze_kingdom_armour.py --stdout   (writes only under tools/reports/kingdom-armour, gitignored)
- python tools/validate_moduledata.py   (expect 0 errors; warnings: 94 INCONSISTENT_ARMOUR_SLOT, 7 CROSS_CULTURE_ARMOUR_INVERSION)
- python tools/fix_upgrade_armour_regressions.py   (dry run; expect 0 edges)
- python -m unittest discover -s tools/tests -p "test_*.py"   (expect all green, about 1,400 tests, 40 s)

## Known Suspects (CONFIRM or DISPUTE each, with the line and a computed example)

S1. Secondary stats when the primary was 0. process_file scales an item's secondaries by new_primary / old_primary and leaves them when old_primary == 0, while calculate_stats floors the new primary at 1. Is there any live armour item whose slot primary was 0 with a positive secondary (a cape with body_armor 0 and arm_armor > 0, a chest with body 0)? If so, what did the curve write and is it right? Scan both the .bak state and the live state.

S2. The legacy --tier-source roster mode now reads a map that is anchor-first. docs/features/armor-balance.md "Dale ladder repair" and "How-To" describe "rebalance_armor.py --tier-source roster --no-lower-armor --keep-materials --cultures dale" with keyword-first semantics ("id keyword wins"). Is that documented workflow now different in a way the docs do not say? Which sentences are stale?

S3. calculate_stats on the cap model returns a secondary stat in the legacy proportion (body leg_armor = 28/50 of the primary at elite) that many folders never carry (Gondor chests carry arm_armor, not leg_armor). analyze_kingdom_armour.predicted_regions sums those curve secondaries into the "predicted" regions, so the curve view's "d leg" column reads 31 to 35 under target for Gondor at every tier. The doc calls that a convention artefact. Is the curve view misleading enough to be a defect, and what is the honest prediction for a folder whose chests carry arm instead of leg?

S4. Umbar's troops wear Black Numenorean items (sk_md_num_*, sm_md_num_*), which the curve stats at the 57 cap while Umbar's own cap is 44; the gate scales per item so Umbar reads correctly, but the kingdom's T6 median (240) is above Gondor's T6 (223) although the maintainer ranked Umbar at 44. Is "a troop wearing kit above its culture's cap" a class the tooling should flag (a new WARNING, or a row in the overview), and how many troops are in it across all cultures? Compute it from the troop files and the Armory folders/prefixes.

S5. Vanilla items on TAOM troops. Harad's camel lancer wears aserai_scale_armor_on_chain (SandBoxCore, body 51) and Dunland wears 82 vanilla pieces; the curve cannot restat them and the gate scales them unscaled (cap None). Enumerate every vanilla armour item worn in troops/*.xml with its value and wearer, and say whether any of them now sits above the wearer's culture cap (a Harad chest at 51 against a 44 cap).

S6. The comment-aware item match (_inside_comment) uses rfind('<!--') and find('-->'); a "-->" inside an attribute value or a nested "<!--" would fool it. Does any live Armory file contain "<!--" or "-->" outside a real comment (in an attribute, a name)? Grep all 131 Armory XML files.

S7. The ladder repair. fix_upgrade_armour_regressions.py stepped 58 regressing children up their item family or onto their parent's item (197 swaps). Pick ten swapped slots across the 12 files (git diff) and check each new item is in the child's culture folder and slot file, is at or above the parent's value NOW (post-curve), and that the swap did not put a level-21 child into an item whose lowest wearer became the child (so the next derive would lower it). Then confirm a fresh "python tools/derive_armor_tiers.py" followed by the dry run above still plans 0 changes.

S8. The two-tier invariant on the cap model (check_kingdom_curve_invariant) judges pairs (heavy, light) and (elite, medium) with the loot table of the LOW tier and a variant step of 1, and skips (lord, heavy) because lord = elite. But items keep their CURRENT modifier_group where --keep-material-type held material_type only: does the writer actually update modifier_group on every item (read process_file), and are there live items whose modifier_group is plate on a medium-band piece that the invariant assumes rolls cloth? Count them per slot.

## REQUIRED SECTIONS in your output

1. VANILLA CODE: for the engine claims the docs make, paste the decompiled methods you checked from the installed 1.4.8 game (use the ilspy MCP or the dump under E:\Decompiled_Bannerlord): EquipmentElement.GetModifiedBodyArmor and ItemModifier.ModifyArmor (the flat, independent, num > 0 bonus the invariant assumes), ArmorComponent.Deserialize (no clamp), DefaultItemValueModel.CalculateArmorTier (tooltip tier and price), and the item_modifiers.xml legendary rows (3/5/7/9/12). State whether each claim in docs/features/armor-balance.md holds.

2. CURVE ARITHMETIC: recompute by hand, for these items, the expected primary and secondaries from the cap, slot ratio, band and the item's pre-curve (.bak) values, and compare with the live file: gondor sk_gd_mns_fount_helmet_heavy_a, gondor sk_gd_ith_chest_noble_heavy_b, dunland dunland_wulf_helmet_lord_d, rhun sk_dg_khml_plate_elite_c, mordor sm_md_num_grvs_elite_a, mordor sk_uruk_mordor_helmet_heavy_a1, mordor sk_md_orc_inf_chest_heavy_e, rivendell rivendell_boots_greaves4_silvergold, erebor sk_dwarf_erebor_helmet_plate_lord_a2, isengard urukscout_helmet (misfiled in mordor/), and one item of your choice from each of dale, harad, rohan, gundabad, thenn. For each state the lowest wearer and level from tools/data/armor_roster_tiers.json.

3. ROUTING: list every id prefix family in the mordor and rhun folders (3-token prefixes) and the cap each lands on; list every KINGDOM_CAPS key nothing can reach; say whether any Armory item lands on a cap the maintainer's table would not put it on.

4. GATE: with the item-level scaling, recompute the goblin/tier7 and umbar/tier6 cells by hand from the report's per-troop table (tools/reports/kingdom-armour/REPORT.md) and confirm the validator's seven cells and their medians. Then argue whether REFERENCE_CAP scaling per item is the right question or whether the gate should compare troops against their OWN cap's expected total instead.

5. CONFIG CROSS-REFERENCE: the 12 changed troop files reference item ids; confirm every one resolves in the Armory or vanilla (python tools/validate_moduledata.py already does; read its BROKEN_ITEM_REF output) and that no civilian set was touched by the repair.

6. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 ship-blocking / P2 wrong number or wrong doc / P3 quality), the file and line, the computed evidence, and the fix. If you find nothing at a level, say so.

## QUALITY GATES

- Every finding cites a file and line and shows the numbers you computed, not an impression.
- Distinguish a live defect (a wrong value in the applied Armory or a wrong verdict in the validator) from a latent one (a shape not present today).
- Do not report the maintainer's design (the caps, the ratios, lord = elite, goblins sharing the orc kit, Umbar in BN kit by roster) as a bug; report where the tooling fails to implement it.
- Read the two RCAs first so you do not re-report fixed findings; do check that each fix holds.

## Prior review lessons

SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. The tooling-correctness pass on #541 caught a self-closing-tag regex swallowing the next block.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections. Codex once reported a folder name as a wrong culture id (three namespaces, see the cheatsheet).

## Output

Write the review to docs/reviews/raw/codex-adversarial-kingdom-cap-curve-2026-09-13.md (the harness redirects your stdout there; just print the review).
