# Deep review: plan 038, battle equipment memory audit (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: plan 038, tools/audit_battle_equipment_memory.py ranks the assets a battle preloads;
         the map audit can read loose Assets trees
         (branch perf/038-battle-equipment-memory-audit, ba3f2a57..65f640fa)
Date:    2026-10-02 (review lead run 2026-10-03)

Scope:   scripts (one new offline Python tool, one option on the map audit tool, their unittest
         module), tools/README.md. No C#, no XML, no harness.
Blast radius: NOT IN SCOPE (no TAOM C# type changed)
Waves:   one wave: Standards (1), Completeness (4), Tooling correctness. Codex not run.

STANDARDS:     FAIL: 1 MEDIUM, 6 LOW
COMPATIBILITY: NOT IN SCOPE (no engine code; the engine model was checked by Tooling and the lead)
EFFICIENCY:    NOT IN SCOPE (lens not launched)
COMPLETENESS:  INCOMPLETE: no feature doc, no feature-map row, issue drafted but not filed (all
               owed by the orchestrator under the plan); 1 MEDIUM, 6 LOW
DATA FLOW:     NOT IN SCOPE (lens not launched)
DESIGN:        NOT IN SCOPE (lens not launched)
XML:           NOT IN SCOPE
TOOLING:       FAIL: 11 findings (1 HIGH, 3 MEDIUM, 7 LOW)
```

## Details

The lead re-checked every finding against the worktree before acting on it. Engine claims were
read in the installed v1.5.3 decompile (`tools/taom-src.ps1 path` with full type names):
`TaleWorlds.MountAndBlade.View.CraftedDataView`, `TaleWorlds.MountAndBlade.View.PreloadHelper`,
`TaleWorlds.Core.Equipment`, `TaleWorlds.Core.BasicCharacterObject`, `TaleWorlds.Core.ItemObject`,
`TaleWorlds.Core.CraftingTemplate`, `TaleWorlds.CampaignSystem.CharacterObject` and
`TaleWorlds.ObjectSystem.MBObjectManager`. Data claims were probed with scripts that use the map
tool's index over the testing channel and the game install. The probes and run outputs are in the
lead's scratch folder (`lead-038`: `probe_guid.py`, `probe_records.py`, `red.log`, `red_filter.py`,
`eq-testing`, `eq-battle`, `eq-live`, `map-base`, `map-head`).

The lenses overlap heavily; the table merges them into one row per defect (R numbers), with the
lens ids that raised each.

| # | Sev | Finding (lens ids) | Verdict | Action |
|---|---|---|---|---|
| R1 | HIGH | A texture whose size falls back (header formula, stub, nothing) writes no reason line; under `--loose-assets` 82 reached textures count as 0 bytes, and the plan makes an undecoded loose header a STOP (Tooling F1; Standards LOW-4) | CONFIRMED: the executor's live `assets.tsv` has 82 rows `HEADER_UNDECODED(ValueError);NO_PIXEL_DATA` at floor 0; the cause is texture metadata version 2, which `parse_texture_meta` rejects; plan 038 lines 904 and 1021 name it a STOP | Fixed: `TEXTURE_SIZE_FROM_HEADER` and `TEXTURE_SIZE_UNKNOWN` reason codes, one row per texture. The live run now logs `TEXTURE_SIZE_UNKNOWN count=82` and `TEXTURE_SIZE_FROM_HEADER count=1868`. Decoding the version 2 header is FOLLOW-UP |
| R2 | MEDIUM | Textures reached through a material resolve by guid to the `AssetPackages` stub, not the same-guid `EmAssetPackages` copy that holds the pixels: wrong pack, false `PIXELS_NOT_IN_PACKS`, `FULL_CHAIN_ONLY_IN_EMASSETPACKAGES` never emitted (Standards MEDIUM-1; Completeness F1) | CONFIRMED: `by_guid.setdefault` keeps the first copy while `add_item` swaps the name index to the pixel twin; 3,255 Native textures differ, `weapon_crafting_n` resolves to `AssetPackages/core_game.tpac` by guid and `EmAssetPackages/crafting/crafting.tpac` by name | Fixed: take the name index's entry when it carries the same guid. Testing run: 0 `PIXELS_NOT_IN_PACKS`, 338 `FULL_CHAIN_ONLY_IN_EMASSETPACKAGES`, texture bytes unchanged (3,671,987,036) |
| R3 | MEDIUM | Crafted weapons on a `use_weapon_as_holster_mesh` template count the Blade's holster mesh, which `CraftedDataView` never builds (Tooling F2; Standards LOW-3; Completeness F2a) | CONFIRMED: both holster builders return null when `Template.UseWeaponAsHolsterMesh`; Native sets it on OneHandedAxe, TwoHandedAxe, TwoHandedPolearm, Pike, Mace and TwoHandedMace; the Armory's `crafting_templates.xslt` rewrites only `UsablePieces` | Fixed: `WEAPON_AS_HOLSTER_TEMPLATES`. `dunland_caerdh_spear_a` 13,728,835 to 2,367,043 bytes; the testing floor drops 163,272 bytes (`throwing_spear_quiver_3_2`), Gondor's side the same |
| R4 | MEDIUM | An empty selection exits 0 with an empty ranking: the population compares the module name case-sensitively, a missing `SubModule.xml` is silent, only sides are checked for emptiness (Tooling F3) | CONFIRMED by reading `population()`, `load_definitions()` and `_run()` | Fixed: case-insensitive module match, `SUBMODULE_MISSING` reason code, `abort reason=NO_TROOPS_SELECTED` with exit 2 |
| R5 | MEDIUM | Skin eyebrow meshes, face and mouth textures and tattoo materials are not counted, and APPROXIMATIONS does not say so (Tooling F4; Standards LOW-3; Completeness F2c) | CONFIRMED: `_read_skins` reads only the eight body attributes, hair and beards | Fixed by naming the gap in APPROXIMATIONS. Counting them changes totals by about 159 MB on the testing population (lens figure): NEEDS MIKE |
| R6 | LOW | `--loose-assets` resolves a name in both trees to the packed copy; the Armory guide records that the engine loads the loose copy (Tooling F5) | CONFIRMED as a statement of fact; the plan chose cooked-first | Named in APPROXIMATIONS; the order is NEEDS MIKE |
| R7 | LOW | A troops file with a byte order mark, or a missing one, crashes with exit 1; the exit-2 causes are printed but never reach `run.log` (Tooling F6; Standards LOW-4; Completeness F5) | CONFIRMED by reading `_read_troop_file` and `_run` | Fixed: `utf-8-sig`; troop files read before indexing; `abort reason=TROOP_FILE_UNREADABLE`, `TROOP_UNKNOWN`, `SIDE_EMPTY` in `run.log` |
| R8 | LOW | The report says the real cost "lies between" floor and upper, which neither bound supports (Tooling F7; Completeness F3) | CONFIRMED: the floor counts every hair, beard and horse material alternative; skin assets and runtime buffers are in neither | Fixed: the totals bracket only the edit data question; the Approximations item says the same |
| R9 | LOW | Engine rules neither modelled nor named: hero sets, slot fit, last `ItemComponent` child, `Type` override, `.xsl` and per-file stylesheets, banner materials, `BuildOrders`, the whole-id `EquipmentSet` lookup (Tooling F8; Standards LOW-3; Completeness F2b, F2d) | CONFIRMED in the decompile: `CharacterObject.BattleEquipments` (hero), `Equipment.DeserializeNode` with `IsItemFitsToSlot`, `ItemObject.Deserialize`, `BasicCharacterObject.Deserialize` passes the attribute's `InnerText` to `GetObject`, a plain lookup | `EquipmentSet` ids fixed to whole-id lookup (exact, one line); the rest named in APPROXIMATIONS. Modelling slot fit is NEEDS MIKE |
| R10 | LOW | The map tool under `--loose-assets` flags every loose mesh over 1 MB as `EDITOR_STREAM_BLOAT` and still says "both pack trees" (Tooling F9; Completeness F7) | CONFIRMED by reading the flag and `summary_lines` | Fixed, for loose runs only: no bloat flag on `Assets/` packs, the summary names the loose trees. Default output byte-identical (below) |
| R11 | LOW | No test checks the report's Approximations content (Tooling F10) | CONFIRMED | Fixed: the report test asserts an Approximations sentence |
| R12 | LOW | Exit codes undocumented (Tooling F11) | CONFIRMED | Fixed: EXIT CODES section in the docstring |
| R13 | LOW | Side and battle lines are logged before the fallback lines; the docstring and the plan say after (Standards LOW-1) | CONFIRMED by reading `_run` | Fixed: fallback lines first; order test |
| R14 | LOW | The report, the README row and commit 65f640fa say the preload registers race meshes and horse materials; `PreloadCharacters` and `AddItemObject` register neither (Standards LOW-2; Completeness F2e, F2f) | CONFIRMED: `AddItemObject`'s horse branch registers only `AdditionalMeshesNameList` | Fixed in the docstring (ENGINE RULES), the report paragraph and the README row. The pushed-to-branch commit body cannot change; this report records the correction |
| R15 | LOW | Mesh records whose material guid is in no pack are dropped silently (Standards LOW-4) | CONFIRMED, but the lens's count of 4 was low: a plain check gives 820 rows on the testing population, 816 of them one misread guid (a cloth record's `uses_cloth_s...` string) | Fixed: `MATERIAL_GUID_UNRESOLVED` only for records whose counts decode; 7 rows on testing. Test proven red against a scratch copy without the filter |
| R16 | LOW | `fallback ... first=` carries the subject, not the whole first row (Standards LOW-4; Completeness F5a) | Plan 038 pins this format and its tests; every row is in `unresolved.tsv`, which the run log names, so no information is dropped | NOT APPLIED: plan versus D6 wording, NEEDS MIKE |
| R17 | LOW | The report's "Sides and battle" table has no battle row (Standards LOW-5; Completeness F6) | CONFIRMED | Fixed: a battle row and the sides' summed floor |
| R18 | LOW | Dead code: `READ_IDS`, `SIZE_FIELDS`, `Definitions.roster_modules`, `import os` in the tests (Standards LOW-6) | CONFIRMED by grep: no reader | Deleted (parity: the suite green before and after) |
| R19 | LOW | Paths no test exercises: `XML_PARSE_ERROR`, `RACE_MESH_UNRESOLVED`, pack `MODULE_MISSING`, `PACK_ERROR` from a corrupt pack, the `--report` and `--tsv-dir` refusals (Completeness F4) | CONFIRMED | Characterisation tests added for all five |
| C1 | PROCESS | No feature doc, feature-map row or filed issue (Completeness) | CONFIRMED; plan 038 lines 1029 to 1033 assign all three to the orchestrator, and DECISIONS D4 holds the issue | Orchestrator, before merge |

The orchestrator's probes, as the lead found them:

1. **Engine fidelity.** The lenses' matches hold (the gender and slim lists, reins and `_rope`, the
   three body names, crafted bodies from `InitCraftedItemObject`, set assembly, Stealth sets, game
   types). The over-counts were R3 and the slot-fit case in R9; the under-counts R1 (loose) and R5.
   APPROXIMATIONS now names every gap the review found.
2. **Byte accounting.** Unions key on (kind, lowercase name), so a shared asset cannot count twice.
   In the new battle run, 2,579,739,831 = 3,179,524,528 minus the shared 599,784,697. R2 changed no
   byte (the formula equals the pixel segment for all 338 rows); only pack and flags.
3. **Live results.** The lenses' independent table-of-contents readers matched
   `sk_mumakil_platform_a1` (render 68,625,564) and `t_gd_ano_chainmail_a1_d` (699,064). After the
   fixes the testing run still reports `troops=1657 items=2671` and
   `maxAsset=metamesh:sk_mumakil_platform_a1:68625564`; `weapon_crafting_n` now reads 44,739,280
   from `EmAssetPackages/crafting/crafting.tpac`.
4. **Map tool default output.** Base (`git show ba3f2a57`) and head on the default install (1,192
   packs): `diff -r` over all 9 output files and the stdout diff (less the `report:` and `tsv:`
   lines) are both empty. `33 passed in 0.25s`.
5. **Formats.** Fixed: line order (R13), abort lines (R7, R4), battle row (R17). The abort line is
   documented in RUN LOG and is the D6 "reason line whenever anything disables itself" for this
   offline tool.
6. **Local paths.** The only new absolute path is still `DEFAULT_RELEASE_MODULES`. The lead agrees
   with all three lenses: it follows the map tool's `DEFAULT_GAME` and `DEFAULT_OUT`,
   `--release-modules` overrides it, and a missing folder exits 2 with a message. The review's
   own additions carry no local path.
7. **Known Python failures.** Only the three base failures remain (below).

## Action items

All done on the branch except C1 and the NEEDS MIKE items.

## Improvements (Step 4)

No Efficiency (Agent 3) or Design (Agent 6) lens ran, so there were no APPLY-scoped or KEEP
proposals. The Tooling lens marked R1, R3, R4, R6, R7, R9 and R10 behaviour-changing; R1, R3, R4,
R7, R10 and the R9 `EquipmentSet` fix are defect fixes, applied test-first.

```
APPLIED:     audit_battle_equipment_memory.py material_assets, the pixel twin (R2):
               test_texture_reached_by_guid_uses_the_pixel_twin
             _texture_assets, size fallbacks (R1): test_texture_sized_from_the_header_is_a_fallback,
               test_texture_of_unknown_size_is_a_fallback
             item_asset_names, WEAPON_AS_HOLSTER_TEMPLATES (R3):
               test_weapon_as_holster_template_builds_no_blade_holster
             population, load_definitions, _run (R4): test_population_matches_the_module_name_in_any_case,
               test_module_without_submodule_xml_is_reported, test_no_troop_selected_exits_2_and_says_why
             _read_troop_file, _abort (R7): test_troop_file_with_a_byte_order_mark_resolves,
               test_unreadable_troop_file_exits_2_and_says_why,
               test_unknown_troop_and_empty_side_say_why_in_the_run_log
             _roster_id (R9): test_equipment_set_reference_uses_the_whole_id
             _run log order (R13): test_fallback_lines_come_before_side_and_battle_lines
             mesh_sizes, MATERIAL_GUID_UNRESOLVED (R15): test_record_material_in_no_pack_is_reported,
               test_misread_record_material_is_not_reported
             write_report (R8, R14, R17, R11): test_report_has_a_battle_row_and_names_its_limits
             audit_map_scene_memory.py mesh flags and summary_lines (R10):
               test_loose_mesh_is_not_flagged_as_editor_stream_bloat, test_loose_run_names_the_loose_trees
             docstring ENGINE RULES, RUN LOG, EXIT CODES, APPROXIMATIONS (R5, R6, R9, R12, R14);
               README row (R14); dead code (R18); characterisation tests (R19)
NOT APPLIED: R5 counting the skin eyebrow, face, mouth and tattoo assets: changes totals, NEEDS MIKE
             R6 loose-first precedence under --loose-assets: reverses the plan's choice, NEEDS MIKE
             R9 modelling slot fit, last ItemComponent, the Type override and BuildOrders: named in
               APPROXIMATIONS; slot fit is NEEDS MIKE, the rest have no case today
             R16 first= carrying the whole row: the plan pins the format, NEEDS MIKE
FOLLOW-UP:   (no issue filed: issues are the orchestrator's, DECISIONS D4)
             - Decode texture metadata version 2 in audit_map_scene_memory.parse_texture_meta (115
               of 2,595 loose Armory textures, lens figure); /research first
             - Content: sk_uruk_hai_skirt_a1 (BodyArmor) sits in slot="Cape" in
               Main/_Module/ModuleData/troops/troops_isengard.xml (urukhai_champion,
               urukhai_berserker), so the engine never equips it; recommend /investigate and a
               slot-fit check in validate_moduledata.py
             - audit_map_scene_memory.py opens scene.xscene without closing it (ResourceWarning)
             - audit_map_scene_memory.py raises IndexError on a truncated pack header
             - tools/README.md: a blank line leaves the check_prefab_budget.py row outside the table
             - The testing channel's Armory ModuleData ships *.xml.bak-* files
```

Step 4.6, the convergence pass: the lead cannot spawn a `deep-reviewer`. The lead read its own diff
once for parity and standards (no em or en dash, no local path in added lines, default map output
byte-identical, every changed live total explained by R2 or R3). The orchestrator should run one
convergence `deep-reviewer` over `65f640fa..` the lead's commit before merge.

VERDICT: READY FOR COMMIT (every confirmed defect fixed or named; C1 and NEEDS MIKE remain)

## Codex review

Codex not run: no adversarial review was dispatched for this item.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| none | n/a | n/a | n/a | Codex not run |

Confirmed bugs: none from Codex. False positives: none. Design questions: none from Codex. Things
Codex missed: not applicable; the Claude lenses' findings are above.

## AGENTS.md lessons (pending)

For "Look harder here" in `.ai/review-reference.md`, if the orchestrator agrees:

- An offline tool that models an engine rule from one entry point: read every callee's
  data-driven early exit (here `CraftedDataView`'s template flag), not only the entry point.
- An index that keeps two maps over the same items (by name, by guid) with different precedence:
  check every consumer resolves through the map that carries the rule.
- A plan STOP condition checked on one sampled row: run it over the whole output.

## NEEDS MIKE

1. R5: count the skin eyebrow meshes, face and mouth textures and tattoo materials (an upper bound,
   like hair), or keep them named as a gap.
2. R6: under `--loose-assets`, should a loose copy win over a packed one, as the engine does?
3. R9: model slot fit (`IsItemFitsToSlot`), or keep it named; today it affects only the uruk skirt.
4. R16: should `fallback first=` carry the whole first row, as D6 reads, over the plan's pinned
   format?
5. The uruk skirt in a Cape slot (FOLLOW-UP): move it to Body or drop it.
6. C1: file the drafted issue (D4); the orchestrator writes the feature doc and feature-map row.

## Verification

| Command | Result |
|---|---|
| `python -B -m unittest discover -s tools/tests -t .` before any edit | `Ran 3032 tests`, `FAILED (failures=3, skipped=8)` |
| the same after the fixes | `Ran 3054 tests`, `FAILED (failures=3, skipped=8)`; the three are the known base failures `test_applying_every_spec_is_a_no_op`, `test_the_committed_career_file_is_what_the_rule_derives`, `test_default_is_on_the_e_drive`; 3054 = 3032 + 22 new tests |
| `python -B -m unittest tools.tests.test_audit_battle_equipment_memory` | `Ran 92 tests`, `OK`. Before the fixes, 16 new tests and the updated `test_every_code_has_a_consequence` failed (`FAILED (failures=16, errors=1)`); `test_misread_record_material_is_not_reported` failed against a scratch copy without its filter; the 5 characterisation tests (R19) pass, and the 3 of them present in that run passed before the fixes too |
| `python -B -m pytest tools/tests/test_audit_map_scene_memory.py -q -p no:cacheprovider` | `33 passed in 0.25s` |
| `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | `Failed!  - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`; the failure is `EveryLanguage_DeclaresARowForEveryEnglishKey`, the same as the executor's base run |
| `python -B tools/lint_docs.py --fail-on-drift` | exit 0 |
| Map tool default output, base against head | 9 files identical, stdout identical |

No gate changed (no hook, validator or CI step), so no differential sweep applies.

## Convergence round 1

One convergence reviewer read `65f640fa..666d5249` and raised three LOW findings. Each was
re-checked against the code before any edit. All three are fixed in the commit that adds this
section, `fix(tools): v2.0.32 - convergence fixes for plan 038`.

| # | Severity | Finding | Verdict | Outcome |
|---|---|---|---|---|
| C-1 | LOW | The R15 filter keeps misread cloth-record guids: on the testing channel 3 of the 7 `MATERIAL_GUID_UNRESOLVED` rows are the sized string `uses_cloth_s...` (guid `15000000757365735f636c6f74685f73`) on `spear_banner_12`, `_16` and `_17`, whose misread counts (4,161,536 each) pass the plausibility window; the `mesh_sizes` docstring said such guids were left out | CONFIRMED: the reviewer's fixture probe printed the row against `666d5249`, and the new test failed with that row before the fix | Fixed: `_sized_ascii` drops a guid whose first u32 is 1 to 255 and whose other 12 bytes are printable ASCII; docstring corrected. Test `test_cloth_string_read_as_a_guid_is_not_reported` (live shape, a real material beside it) failed first, then passed. The live testing run now logs `fallback reason=MATERIAL_GUID_UNRESOLVED count=4` (the four `sturgia_infantry_shield_b` records) and `unresolved=818` (was 821); every byte total and the largest troop and asset are unchanged. R15's "7 rows on testing" above and the RCA's R15 row describe the round-0 filter; 4 rows is the corrected figure |
| C-2 | LOW | EXIT CODES omitted the argparse usage error, which exits 2 before any `run.log` exists | CONFIRMED: `--troops a.txt --culture gondor` exits 2 with `argument --culture: not allowed with argument --troops` and creates no output folder | Fixed: exit 2 now also names a command line that does not parse (no `run.log` is written), and the line notes that `--help` exits 0 writing nothing (checked: exit 0, no folder) |
| C-3 | LOW | R10 exempts loose `Assets/` meshes from EDITOR_STREAM_BLOAT, but the map report and the README row still defined the flag by the bare ratio | CONFIRMED: `audit_map_scene_memory.py` flags only non-`Assets/` packs, and the report paragraph and README row stated the ratio alone | Fixed: the report adds one sentence only when an indexed pack label starts with `Assets/`, so the default report is unchanged; the `--loose-assets` help and the README row carry the same clause. Test `test_loose_report_says_loose_meshes_are_never_flagged` failed first, then passed; it also asserts the default report lacks the sentence |

False positives: none. Not fixed: none.

**Verification for this round**

| Command | Result |
|---|---|
| `python -B -m unittest discover -s tools/tests -t .` before the edits | `Ran 3054 tests`, `FAILED (failures=3, skipped=8)` |
| the same after the fixes | `Ran 3056 tests`, `FAILED (failures=3, skipped=8)`; the three are the known base failures `test_applying_every_spec_is_a_no_op`, `test_the_committed_career_file_is_what_the_rule_derives`, `test_default_is_on_the_e_drive`; 3056 = 3054 + 2 new tests |
| `python -B -m unittest tools.tests.test_audit_battle_equipment_memory` | `Ran 94 tests`, `OK` |
| `python -B -m pytest tools/tests/test_audit_map_scene_memory.py -q -p no:cacheprovider` | `33 passed` |
| `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`, before and after | `Failed!  - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348` both times; the failure is `EveryLanguage_DeclaresARowForEveryEnglishKey`, the base's own |
| Map tool default output, `666d5249` against the fix | all 9 output files identical (`diff -r` empty), stdout identical less the `report:` and `tsv:` lines |
| Map tool with `--loose-assets` on the live install | exit 0; the report carries the new sentence |
| Battle audit on the testing channel | exit 0; `MATERIAL_GUID_UNRESOLVED count=4` |

No gate changed (no hook, validator or CI step), so no differential sweep applies.

## Convergence round 2

The convergence reviewer read `666d5249..9546c167` and found nothing. The orchestrator recorded both rounds
in the RCA ("Convergence rounds") and the REVIEW-LOG entry; the module's tests pass at `9546c167`.

## Review pass of 2026-10-03: Codex adversarial review

A Codex adversarial review of `ba3f2a57..072d46dc` (verdict ISSUES FOUND: 3 P2 and 1 P3, no P1) ran after the two
convergence rounds, so the "Codex review" section above, which says "Codex not run", describes the state at the time.
This fix pass re-read each finding against the code, the installed v1.5.3 decompile (`taom-src`:
`TaleWorlds.Core.Equipment`, `EquipmentIndex`, `ItemObject`, `WeaponComponent`, `WeaponComponentData`,
`MBEquipmentRoster`, `BasicCharacterObject`, `CraftingPiece`, `Crafting`, `TaleWorlds.Library.Debug`) and the real data
before changing anything. All four were CONFIRMED. The issue the sections above call "drafted but not filed" (C1) was
filed as #720.

| # | Source | Sev | Finding | Verdict | Action |
|---|---|---|---|---|---|
| X1 | Codex | P2 | The audit counts equipment the engine refuses from its slot: `urukhai_champion` and `urukhai_berserker` carry `sk_uruk_hai_skirt_a1`, a BodyArmor, in `Cape` | CONFIRMED: `Equipment.DeserializeNode` assigns an item only when `IsItemFitsToSlot` allows it and otherwise leaves the slot as it was (`Equipment.cs:204-223`; a BodyArmor fits `Body` only, `:482-484`), and `AddOverriddenEquipments` runs the same method for each override on a clone of each set (`MBEquipmentRoster.cs:121-136`). The new assembly over every character of Native, SandBoxCore, SandBox, the Armory and TAOM (5,143 characters, 135,359 assignments: each roster element, standalone set element and override application, civilian sets included) refuses exactly two, this skirt in the Cape slot of those two troops | The rule is modelled where the engine applies it: sets are assembled one assignment at a time (`assemble_sets`, `_place`, `item_fits_slot`), so a refused override or roster item leaves the earlier item in the slot. The type is `ItemObject.Type` as `ItemObject.Deserialize` settles it (`Type` read ignoring case, replaced by the weapon class's type for a Weapon component, Invalid with no `Type`: `ItemObject.cs:611-638`, `WeaponComponentData.cs:265-320`); an item whose Flags set `DropOnWeaponChange` or `DropOnAnyAction` fits only `ExtraWeaponSlot`. A refusal is `ITEM_SLOT_REJECTED` (reason row, fallback line, report row) and adds nothing: the item still counts for that troop if another of its slots or sets holds it. Testing channel: `items` 2,671 to 2,670, `unresolved` 818 to 820, every byte total identical; `urukhai_champion` floor 86,249,779 to 85,594,123, `urukhai_berserker` unchanged (its other items reach the same assets). The content defect (`troops_isengard.xml`) is not touched and stays owed |
| X2 | Codex | P2 | The loose-header STOP condition (Step 9.4) is unmet, and reporting the fallback does not decode the textures | CONFIRMED: `parse_texture_meta` in the map tool rejects every metadata version but 3, and a rejected loose texture has neither the pixel segment nor the AssetPackages stub segment, so `_texture_row` returns 0 bytes. A census of the live install found 115 version 2 headers among the Armory's 2,595 loose textures (2,480 are version 3), all with the same two segments (`e25b477c`, `a781b98a`), and none among TAOM's 89. The default population reaches 82 of the 115 (`TEXTURE_SIZE_UNKNOWN count=82`, every one flagged `HEADER_UNDECODED`, none an unknown format) | By the ruling there is no decoder: version 2 is a `/research` follow-up. The condition is documented as reached, and the fallback is reported in the run log (a `fallback` line whose consequence says every total that holds the texture is a lower bound), in the report (an "Incomplete totals" note with the row count whenever such a row exists, and never otherwise), in `unresolved.tsv` (one row per texture) and in the texture's own `assets.tsv` row (its flags and bytes); `items.tsv` and `troops.tsv` count it in their `unresolved` column, and `cultures.tsv` and `sides.tsv` carry no marker. APPROXIMATIONS, the battle tool's `--loose-assets` help and its README row state the version 2 gap and that a loose total is a lower bound, not a finished attribution; the map tool's `--loose-assets` help says of a loose texture only that a header the decoder rejects counts 0 bytes, flagged `HEADER_UNDECODED` |
| X3 | Codex | P2 | Reusing an output directory leaves tables from another run | CONFIRMED by test: a run without sides keeps the earlier `sides.tsv`, and an aborted run (exit 2) leaves every earlier table beside a `run.log` that reports the abort, because `RunLog` truncates only the log | `main` deletes the report and the six TSVs of an earlier run after the refusal checks and before the log opens (`_remove_previous_outputs`). `run.log` is rewritten as before and nothing else in the folder is touched, so a folder never mixes two runs and an aborted run leaves only its own `run.log` |
| X4 | Codex | P3 | `--loose-assets` resolves a name in both trees to the packed copy, against the repository's engine record | CONFIRMED as a conflict; the build followed plan 038 Step 2. `docs/reference/armory-guide.md` ("Two asset trees") records, with the engine's own log, that a module's `Assets` tree is loaded and its cooked packs are not, and `validate_mesh_refs.module_tpacs` already makes that choice. Decided (FOR-MIKE 16s): follow the engine | `AssetIndex.add_module`, shared by both tools: when `trees` names `Assets` and the module holds a loose tpac, only that tree is read, so the loose copy wins and a name only the cooked packs hold does not exist, by name or by guid; a module with no loose tpac keeps its packs; the default `trees` are untouched. Each skipped cooked tree is recorded (`skipped_trees`) and reported: `COOKED_TREE_NOT_READ` in the battle tool, a `note:` line in the map tool. On the live install only TAOM is affected (5 cooked packs against 123 loose; its 4 cooked-only meshes are map props), and every byte total is identical. The native loader's selection is not decompiled (UNVERIFIED) and the docstring says so |

NEEDS MIKE items 2 (loose over packed) and 3 (model slot fit) of the list above are settled by this pass. Items 1 (skin
eyebrow, face, mouth and tattoo assets) and 4 (`first=` carrying the whole row) are unchanged. Item 5 (the Uruk skirt in
a Cape slot) is now visible as `ITEM_SLOT_REJECTED` in every run; moving the skirt to `Body` in
`Main/_Module/ModuleData/troops/troops_isengard.xml`, and a slot-fit check in `validate_moduledata.py`, stay owed (the
plan forbids a ModuleData change). Item 6 is filed as #720.

Two of Codex's suspect dispositions were CONFIRMED only narrowly and are left as they are. Suspect 11 (the code-table
test scans source strings and cannot discover a branch that degrades without a reason code) is the limit the RCA
already records; literal end-to-end numbers carry the rest. Suspect 12 (`PieceDef.has_blade` is written and never read)
is a cleanup with no behaviour, and every changed line in this pass traces to a finding.

**Evidence.** Fixtures first: five assignments in three places put items in slots the engine refuses (a Goods item in
`Body` and another in `Leg`, a BodyArmor in `Leg`, a HeadArmor in `Body`, and the battle fixture's second troop), so they
were made engine-valid before the red run with every expected number unchanged (`Ran 94 tests`, `OK` before and after).
The new tests then ran red against the unchanged tool, `Ran 123 tests`, `FAILED (failures=16, errors=38)`, and green
after, `Ran 123 tests`, `OK`: the module went from 94 to 123 tests. `test_loose_tree_is_indexed_after_the_pack_trees_when_asked`,
which pinned the packed-first rule, became four tests of the engine's rule, and `test_loose_run_names_the_loose_trees`
took the new summary wording. 36 mutations of the new guards (the fit tables, each flag and type rule, the three
assembly points, refusal reporting, the civilian filter, the output removal and its order against the refusal checks,
the loose rule, the skipped-tree rows and the incomplete-totals note) ran on a scratch copy, each failed at least one
test, and none survived (the first run kept one equivalent mutant, the removal moved after the log opens, which became
"run.log appended"); the worktree files were never mutated. Live, base `072d46dc` against this change: the testing
channel and the live loose install move only as X1 and X4 say, a gondor and mordor battle is identical in all six
tables, and the live loose run logs `COOKED_TREE_NOT_READ count=1` (`TAOM/AssetPackages`) and `TEXTURE_SIZE_UNKNOWN
count=82` with the report note. The map tool's default output is byte-identical to the base (`diff -r` over its 9 files
and the stdout less the `report:` and `tsv:` lines, both empty). Full tools suite
`python -B -m unittest discover -s tools/tests -t .`: `Ran 3085 tests`, `FAILED (failures=3, skipped=8)`, the three known base failures
(`test_applying_every_spec_is_a_no_op`, `test_the_committed_career_file_is_what_the_rule_derives`,
`test_default_is_on_the_e_drive`), against `Ran 3056 tests`, `FAILED (failures=3, skipped=8)` before;
`python -B -m pytest tools/tests/test_audit_map_scene_memory.py -q -p no:cacheprovider`: `33 passed`;
`python -B tools/lint_docs.py --fail-on-drift`: exit 0. Python only, so no dotnet run. The "none survived" above
describes those 36 mutants, chosen per guard: the convergence pass of 2026-10-04 (next section) found that they left 27
of the 30 rows of the weapon-class table and the capitalised `False` flag unpinned.

## Convergence of the Codex pass (2026-10-04)

A convergence reviewer read the fix, `072d46dc..27d3e486`, and raised one MEDIUM and one LOW, both about the item type
rule that fix added. This pass re-read each against the code, the installed v1.5.3 decompile (`taom-src`:
`TaleWorlds.Core.BannerComponent`, `WeaponComponent`, `WeaponComponentData`, `WeaponClass`, `ItemObject` and
`TaleWorlds.MountAndBlade.View.PreloadHelper`) and the real data before editing. Both were CONFIRMED. The fix is the
commit that adds this section.

| # | Severity | Finding | Verdict | Outcome |
|---|---|---|---|---|
| Y1 | MEDIUM | Single-token mutations of the type rule survive. The weapon-class to item-type table has 30 entries and the tests drive 3 of them (OneHandedSword, Bow, Sling); deleting `.lower()` in `_flag_set` fails no test either | CONFIRMED: against `27d3e486` the reviewer's six mutants (`.lower()` deleted from `_flag_set`; LargeShield, Arrow, OneHandedPolearm, the Banner class and SmallShield sent to Invalid) each ran `Ran 123 tests`, `OK`, while the control (Bow sent to Invalid) failed. Generated per row (each of the 30 entries sent to Invalid, its key mistyped, and sent to another weapon type: 90 mutants), 82 survived, and the 8 that failed all belong to those three classes. The table itself is right: all 32 `WeaponClass` members match `GetItemTypeFromWeaponClass` (`WeaponComponentData.cs:265-320`), so the gap was in the tests. On the testing channel's 1,657-troop population the base refuses 2 rows and reaches 2,670 items; the LargeShield mutant refuses 786 rows across 585 troops and reaches 2,501 items, and the Arrow mutant 423 rows across 197 troops and 2,634 items | Tests only, the table is unchanged. `ENGINE_WEAPON_CLASS_TYPES` is a hand-written copy of the engine's switch for all 32 members (compared with the decompile: same order, no mismatch), and `test_every_weapon_class_gives_the_item_type_the_engine_gives_it` builds one `Type="Goods"` item per class and asserts `item_type` and the fitted slots against `ENGINE_SLOT_FIT`. The flag test gains `DropOnWeaponChange="False"`, and the no-Type case now asserts that `item_type` is Invalid. All 90 table mutants and the reviewer's six fail a test |
| Y2 | LOW | The type rule misses a `<Banner>` component. The engine builds a `BannerComponent`, a `WeaponComponent` subclass, so the same override applies; the tool judged only `<Weapon>` | CONFIRMED: `BannerComponent : WeaponComponent` (`BannerComponent.cs:7`, whose `Deserialize` calls the base at `:34`), `ItemObject.Deserialize` builds one for a `Banner` element (`ItemObject.cs:595-596`) and replaces the Type whenever `WeaponComponent != null` (`:629-636`). On the unchanged tool a `Type="Goods"` item with `<Banner weapon_class="Banner"/>` fits nowhere (the engine equips it as a Banner) and a `Type="Banner"` item whose `<Banner>` has no `weapon_class` fits Weapon0 to Weapon3 (the engine gives it Invalid). The three new tests ran `Ran 127 tests`, `FAILED (failures=3)` before the fix. It is latent today: the 46 Banner items of the five modules are each a lone `<Banner>` child with `Type="Banner"` and `weapon_class="Banner"`, none carries a holster-with-weapon or flying mesh, and no troop of either audited population reaches one | `_parse_item` reads a `<Banner>` element before a `<Weapon>` one, because the engine builds a fresh `BannerComponent` that replaces a component read earlier, and records it as the item's weapon component, so `item_type` applies the class override to it. The holster and flying mesh branch gates on `WeaponComponent != null` too (`PreloadHelper.cs:122-135`) and now follows through the same flag; no banner carries those meshes, so no total moves. The docstrings and comments that state the rule now say a Weapon or Banner component. Tests: a Goods banner fits Weapon0 to Weapon3, a classless Banner fits nothing, a Banner replaces a Weapon read before it, and a banner takes the weapon mesh branch |

False positives: none. Not fixed: nothing from these two findings; the sweep below names seven survivors outside them.

**Verification for this round**

| Command | Result |
|---|---|
| `python -B -m unittest tools.tests.test_audit_battle_equipment_memory` before the edits | `Ran 123 tests`, `OK` |
| the same with the new tests and the unchanged tool | `Ran 127 tests`, `FAILED (failures=3)`: the three Banner tests |
| the same after the fix | `Ran 127 tests`, `OK` |
| `python -B -m unittest discover -s tools/tests -t .` | `Ran 3089 tests`, `FAILED (failures=3, skipped=8)`; the three are the known base failures `test_applying_every_spec_is_a_no_op`, `test_the_committed_career_file_is_what_the_rule_derives`, `test_default_is_on_the_e_drive`; 3089 = 3085 + 4 new tests |
| `python -B -m pytest tools/tests/test_audit_map_scene_memory.py -q -p no:cacheprovider` | `33 passed` |
| Scratch mutants, each a copy of the tools with one change, the worktree never touched: the reviewer's set, the 90 table mutants, five others and six Banner mutants (element ignored, precedence swapped, `or` for an is-None test, component not Weapon, class not recorded, a classless Banner losing to a Weapon) | 108 mutants: 106 killed, 2 survive (the override pass labelling every refusal Battle, and the map tool's `has_loose_tree` without `.lower()`), neither in the findings and both untouched. Before the new tests the same set without the Banner mutants (102 mutants) had 92 survivors |
| A line-level sweep of 14 functions (`_flag_set`, `_parse_item`, `item_type`, `item_fits_slot`, `_place`, `assemble_sets`, `slot_rejections`, `_kept_types`, `battle_sets`, `_equipment_type`, `_assignments`, `_slot`, `_object_id`, `_bool`): operator swaps, flipped constants, deleted `.lower()` and `.strip()` | 70 mutants, 63 killed, 7 survive: the `component` assignment on the crafted branch, which nothing reads (equivalent), and six in older attribute helpers (the element filter of `_assignments` twice, `.strip()` in `_slot` and in `_object_id`, `.strip()` and `.lower()` in `_bool`). None is a type rule; they are named here, not changed |
| Battle audit on the testing channel, default and with `--include-civilian --include-heroes`, base `27d3e486` against this change | exit 0 both; in both runs (1,657 and 2,856 troops) the five TSVs are byte-identical, and `run.log` and the report differ only in the `elapsedS` value of the summary line (identical with that value normalised), as does stdout less the `report:` and `tsv:` lines; no banner item is reached by either |
| The 46 real Banner items, `item_type` and fitted slots, base against this change | identical (all `Banner`, all fitting only `ExtraWeaponSlot`); the changed tool now also reads their component and class |

No gate changed (no hook, validator or CI step), so no differential sweep applies. Python only, so no dotnet run.
