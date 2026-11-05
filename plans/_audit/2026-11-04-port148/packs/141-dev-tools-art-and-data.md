# Input pack: WP 141 dev-tools-art-and-data

Title (draft): Developer tools: Armory mirror, artist port safety, map and village tools, creature fit, graphify, ranged ladders
Wave: 5   Lane: F   Draft priority: P2   Groups: 13   Port commits: 23
Depends on (WP ids): 137

## Orchestrator notes for this WP (binding)
- K2: 4b207f951's add_map_villages --check widening (no Village rows); b904e02a3's civilian-set skip in rebalance_troops.troop_weapon_classes. Never run any tool with --apply.
- Tools for 1.5.x-only content stay out (keep only what serves data that exists on 1.4.x).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/K2-skip-other-critic.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Port groups owned by this WP (from the commit classification)

### group `issue-616-spider-variants`  P3  track=dev-tools  commits=4  sim: clean=0 conflict=4
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/tests/test_tpac_clone_metamesh.py, tools/tpac_clone_metamesh.py
- `e1a940ac9` 2026-09-17 PORT-PARTIAL P3 pick=conflict :: feat(spider): v2.0.29 - Brown and Pale Spider mounts (#616)
  - why: The Brown and Pale Spider items and rosters are content, but tools/tpac_clone_metamesh.py is a generic metamesh-cloning dev tool, and 1.4.x already hosts that tool family (tpac_skeleton_*, rename_anim_clip_tpac.py).
  - port notes: Take tools/tpac_clone_metamesh.py, tools/tests/test_tpac_clone_metamesh.py and the tools/README.md row, together with 426f0dc88 (hash recompute) and 5ccde2771 (a Kit save is required for the RDC entry), since this first cut renders invisible on its own. Leave spider_creature.xml, the catalogue rows and the spider doc and ledger lines. Optional; low value unless tpac authoring happens on the 1.4.x line.
  - issues: 616
- `426f0dc88` 2026-09-17 PORT-PARTIAL P3 pick=conflict :: fix(spider): v2.0.29 - clone tool recomputes the tpac hashes (#616)
  - why: Fixes the clone tool to recompute the xxHash64 segment and item checksums and corrects warg-ledger and authoring-doc rows, present on 1.4.x, that called the checksum unknown.
  - port notes: Take the tool and test changes plus the lotrlome-warg-changes.md and creature-mount-authoring.md corrections (the xxHash64 formulas). The lesson this commit appends to lessons/animation-skeleton.md was rewritten by 5ccde2771, so port the final wording. Leave the spider doc and ledger lines.
  - prereqs: e1a940ac9
  - conflicts: tools/tests/test_tpac_clone_metamesh.py, tools/tpac_clone_metamesh.py
  - issues: 616
- `5ccde2771` 2026-09-17 PORT-PARTIAL P3 pick=conflict :: docs(spider): v2.0.29 - the invisible clones were a missing RDC entry (#616)
  - why: It records that a hand-built tpac without a RuntimeDataCache/<GUID>.rdc entry is skipped by the client and that only the editor writes one, which matches 1.4.x's own audit (shipping client reads RDC at native-commit-audit-2026-08.md:146, editor requires it at :259).
  - port notes: Take the rewritten lesson in docs/reviews/lessons/animation-skeleton.md, the creature-mount-authoring.md row, the tools/README.md row and the tool docstring (with e1a940ac9 and 426f0dc88). Leave spider.md and the spider change-ledger lines. The skip-without-RDC behaviour was measured on the v1.5.3 client: UNVERIFIED on v1.4.8, though consistent with the audit.
  - prereqs: e1a940ac9, 426f0dc88
  - conflicts: tools/tpac_clone_metamesh.py
  - issues: 616
- `525f67fc9` 2026-09-17 PORT P3 pick=conflict :: docs: v2.0.29 - CLAUDE.md trap: a tpac the Kit never saved is invisible (#616)
  - why: A CLAUDE.md trap row for an engine behaviour that cost a day and logs nothing, and its premise (the client reads RDC, only the editor writes it) is already settled on 1.4.x in native-commit-audit-2026-08.md.
  - port notes: Port the final row wording from 3c5e57e71, which names `python tools/check_rdc_entries.py` (take that script from 4f7ae42ef). Keep the row within 400 chars and mind the 1.4.x CLAUDE.md size budget enforced by lint_docs. The skip behaviour itself was measured on v1.5.3.
  - prereqs: 4f7ae42ef
  - issues: 616
skipped in this group: `cabfe3a99` SKIP-CONTENT feat(spider): v2.0.29 - Spider Rider line, three rungs by mount size (; `0919ae064` SKIP-CONTENT balance(spider): v2.0.29 - the rider line sits at 21, 26 and 31 (#616)

### group `tools-cave-troll-clip-pipeline`  P3  track=dev-tools  commits=2  sim: clean=0 conflict=2
- `4f7ae42ef` 2026-09-18 PORT-PARTIAL P3 pick=conflict :: feat(tools): v2.0.29 - Fab cave troll clip pipeline on human_skeleton
  - why: Eleven art-pipeline tools, of which check_rdc_entries.py (missing-RDC gate) and tpac_fix_item_checksums.py are generic tpac diagnostics while the rest is a pipeline for the Fab cave troll pack.
  - port notes: Take tools/check_rdc_entries.py and tools/tpac_fix_item_checksums.py with their tools/README.md rows; check_rdc_entries.py is read-only and pairs with the 525f67fc9 trap row. Take the cave-troll pipeline (oneoff/ue_export_cave_troll.py, blender/retarget_mannequin_to_human.py, blender/reskin_to_human_skeleton.py, blender/measure_fab_clip_roots.py, gen_troll_anim_clips.ps1, bind_troll_action_set.py, the fab_* json and human_skeleton_engine.json) only if troll animation work will happen on the 1.4.x line; bind_troll_action_set.py writes into the live Armory. No tests ship with these tools.
- `3c5e57e71` 2026-09-18 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.29 - cave troll clips: engine frames, rest frame 0, Kit facts
  - why: It records measured Modding Kit import facts (bone locals stored verbatim, root turned RotZ(180), root track relative to frame 0, master GUIDs kept across reimport) in engine docs that all exist on 1.4.x, alongside troll content notes.
  - port notes: Take bannerlord-skeleton-authoring.md, bannerlord-animation-clip-flags.md, ue-to-bannerlord-asset-pipeline.md, the three community creature guides, recipe-add-a-race-or-creature.md, development-machines.md, lessons/animation-skeleton.md with LESSONS-LEARNED.md, the doc-lookup and INDEX rows, and both CLAUDE.md trap-row edits (the skeleton row cites retarget_mannequin_to_human.py, so reword it if 4f7ae42ef's troll pipeline stays behind). Leave the troll-race.md content notes, the lotrlome-armory-snapshot action_sets.xml and the provenance-register Fab row (licence pending). The Kit facts were measured on 2026-09-18 with the maintainer's 1.5.x-era Kit: UNVERIFIED against the v1.4.8 Kit.
  - prereqs: 4f7ae42ef, 525f67fc9

### group `issue-617-ranged-ladder`  P3  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/ranged_ladder.py, tools/rebalance_troops.py, tools/restat_ranged_donors.py, tools/tests/test_ranged_ladder.py, tools/tests/test_restat_ranged_donors.py
- `8465cc984` 2026-09-18 PORT-PARTIAL P3 pick=conflict :: fix(tools): v2.0.29 - #617 review: translations, shared ladder rules
  - why: Review fixes for the 1.5.x-only #617 per-tier ranged ladder tools, carrying two small generic fixes to tools that also exist on the 1.4.x line.
  - port notes: Take only (1) tools/fix_upgrade_armour_regressions.py write_changes: build and parse every file before writing any, plus its test; (2) rebalance_troops.MILITIA_BINDING_RE accepting single quotes like taom_schema's validator regex (the 1.4.x tip still has the double-quote-only form at rebalance_troops.py:402-404), plus the test_militia_elite_bonus.py hunk. Leave sync_ranged_ladder_translations.py, restat_ranged_donors.py, the ranged_ladder.py template and RANGED_DAMAGE_CEILING roster changes and the rebalance_troops ladder-cell logic: all build on #617 (b37fc22de, 697cbdb62), which 1.4.x does not have. b904e02a3 later makes rebalance_troops reuse taom_schema's regex object; optional.
  - conflicts: tools/ranged_ladder.py, tools/rebalance_troops.py, tools/restat_ranged_donors.py, tools/tests/test_ranged_ladder.py, tools/tests/test_restat_ranged_donors.py
  - issues: 617
skipped in this group: `8800aa4cc` SKIP-BOOKKEEPING docs(ranged): v2.0.29 - #617 review RCA, lessons, handbook examples; `b904e02a3` SKIP-1.5-ONLY-FIX fix(ranged): v2.0.30 - #617 second review: guards, cache, crossbow rul; `449d80a83` SKIP-BOOKKEEPING docs(ranged): v2.0.30 - #617 second review records, RCA, lessons

### group `armory-lod-pass`  P3  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/ModuleData/taom_troop_name_strings.xml, tools/data/armor_roster_tiers.json
- `e63128528` 2026-09-25 PORT-PARTIAL P3 pick=conflict :: Add unit tests for FBX LODs and LOD fill batch functionality
  - why: The subject is mislabeled: the commit records the 1.5.x Armory distance-LOD art pass, done in the unversioned live Armory, and adds the FBX LOD audit, fill and Blender tools with 88 unit tests; the tools are portable, the art and roster data are not.
  - port notes: Take:
- tools/audit_fbx_lods.py, tools/lod_fill_batch.py, tools/blender/add_mesh_lods.py.
- tools/lod_defect_fixes.json and tools/lod_material_fixes.json.
- tools/tests/test_audit_fbx_lods.py, test_lod_fill_batch.py and test_add_mesh_lods.py.
- The tools/README rows, the armory-guide.md LOD section and the two lessons.

Leave:
- tools/data/armor_roster_tiers.json and taom_troop_name_strings.xml (content; the latter is absent on 1.4.5).
- docs/reference/troop-rosters.html.
- docs/research/creature-bandits-roadmap.md: research for a 1.5.x feature, read from the v1.5.3 decompile.
- tools/oneoff/blender_goblin_head_fix.py and the skins.xml snapshot (art and content).

lod-audit.md describes the 1.5.x desktop Armory. Regenerate it against the 1.4.8 Armory instead of copying it.

Whether the 1.4.8 Armory gets the LOD pass is Mike's art decision and is not part of this port. It needs FBX AssetSources and a Kit re-import, and this laptop has cooked tpacs only. audit_fbx_lods.py --check only means something once the pass is done.
  - conflicts: Main/_Module/ModuleData/taom_troop_name_strings.xml, tools/data/armor_roster_tiers.json

### group `issue-677-graphify-code-graph`  P3  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .agents/skills/taom-build/SKILL.md, tools/test_hooks.sh
- `b8833474e` 2026-09-26 PORT P3 pick=conflict :: feat(tooling): v2.0.30 - wire the graphify code graph into the workflow
  - why: Wires a graphify C# code graph into the workflow (tools/graphify_taom.py, a PreToolUse gate on raw graphify writes, a session-start freshness line, ADR-012), so the callers of a type are listed before it is changed, reviewed or debugged.
  - port notes: Take tools/graphify_taom.py, tools/tests/test_graphify_taom.py, .claude/hooks/check-graphify-usage.sh, the session-start freshness block, ADR-012, docs/features/graphify-code-graph.md and the skill and agent wiring. The settings.json registration is the maintainer's to make (config-protection) and goes into the folded Bash|PowerShell group if plan 027 lands first. Needs graphify installed and TAOM_GRAPHIFY_OUT set on the laptop (the default E:\graphify\TAOM is a desktop path). Expect conflicts in .agents/skills/taom-build/SKILL.md and tools/test_hooks.sh. Later 1.5.x follow-ups: e6745a492 (interface and member callers) and a one-line gate change in 9cfcadd07.
  - conflicts: .agents/skills/taom-build/SKILL.md, tools/test_hooks.sh
  - issues: 677

### group `tools-creature-fit`  P3  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/read_anim_keyframes_tpac.ps1
- `a8fc5c848` 2026-09-30 PORT-PARTIAL P3 pick=conflict :: Add unit tests for creature fitting, animation repointing, and geometry calculations
  - why: Creature-fitting and clip-repointing tools plus engine-research docs. They count as developer tooling under the brief's rules, but they serve 1.5.x creature content (the hill troll) and the docs were verified only on v1.5.3.
  - port notes: Optional: only worth taking if creature authoring will happen on the 1.4.x line. Take tools/creature_fit.py, creature_fit_math.py, repoint_anim_clip.py, tools/blender/creature_fit_*.py, their tests, and the -MasterDir option in tools/read_anim_keyframes_tpac.ps1 (it conflicts with the 1.4.x copy). Re-verify the engine claims in docs/reference/scripted-melee-strikes.md and head-mesh-and-groom-authoring.md against v1.4.8 before porting them. The docs this commit edits (bannerlord-animation-system-map.md, race-face-and-hand-morphs.md and the upper-mesh investigation) are absent on 1.4.x. Leave the hill troll fit specs unless the troll is in scope.
  - conflicts: tools/read_anim_keyframes_tpac.ps1

### group `issue-798-harad-repair`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml, Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml, Main/_Module/ModuleData/lotr_issues/taom_lotr_issues.xml, Main/_Module/ModuleData/taom_character_name_strings.xml, Main/_Module/ModuleData/taom_troop_name_strings.xml, Main/_Module/ModuleData/troops/troops_harad.xml, TAOM.Tests/Features/CultureConversion/GarrisonCultureCoverageTests.cs, tools/generate_name_localization_strings.py, tools/tests/test_generate_enlistment_rosters.py, tools/tests/test_generate_name_localization_strings.py
- `44e9edd48` 2026-10-15 PORT-PARTIAL P2 pick=conflict :: fix(data): v2.0.34 - Harad pools, kits and names after the tree rework
  - why: The data repair targets the 2026-10-10 Harad tree rework (08b30bfc4), which is on neither the 1.4.x tip nor the public build branch, but two generator bugs it fixes also exist on 1.4.x.
  - port notes: Take generate_enlistment_rosters.py's SEED_MARKER: 1.4.x line 936 writes '--seed-missing' inside an XML comment, which XML forbids, so every --seed-missing run produces malformed XML. Bring the SeedMarkerTests case from tools/tests/test_generate_enlistment_rosters.py with it. Take tools/oneoff/generate_tavern_mercenaries.py's mistymountainorcs pick changed to goblin_fighter: 1.4.x lines 61-62 still name mistymountainorcs_fighter, a troop absent since f577bd547 (on both lines). Confirm the 1.4.x taom_spcultures tavern binding uses goblin_fighter_merc first. Leave all Harad data, volunteer pools, kits and names, the abanissa alias and the shaghana/abanissa merc picks (they use 1.5.x rework troop ids), generate_name_localization_strings.py (absent on 1.4.x) and the insert_new_faction_careers comment.
  - conflicts: Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml, Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml, Main/_Module/ModuleData/lotr_issues/taom_lotr_issues.xml, Main/_Module/ModuleData/taom_character_name_strings.xml, Main/_Module/ModuleData/taom_troop_name_strings.xml, Main/_Module/ModuleData/troops/troops_harad.xml, TAOM.Tests/Features/CultureConversion/GarrisonCultureCoverageTests.cs, tools/generate_name_localization_strings.py, tools/tests/test_generate_enlistment_rosters.py, tools/tests/test_generate_name_localization_strings.py
  - issues: 798
skipped in this group: `caefaa1f3` SKIP-BOOKKEEPING docs(reviews): v2.0.34 - Harad repair RCA and lessons

### group `tools-artist-port-safety`  P2  track=dev-tools  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml, Main/_Module/ModuleData/taom_partyTemplates.xml, TAOM.Tests/Features/ArmourAcquisition/HaradWeaponsLiveEditTests.cs, tools/analyze_melee_ladder.py, tools/armory-unregistered-names-baseline.txt, tools/generate_enlistment_rosters.py, tools/generate_starter_kit.py, tools/merge_live_file.py, tools/oneoff/convert_tripo_prop_textures.py, tools/register_armory_names.py, tools/taom_schema.py, tools/tests/test_generate_enlistment_rosters.py, tools/tests/test_generate_starter_kit.py, tools/tests/test_generate_tavern_mercenaries.py, tools/tests/test_merge_live_file.py, tools/tests/test_register_armory_names.py, tools/tests/test_translate_caps_and_seed.py, tools/tests/test_validate_moduledata.py, tools/translate_with_claude.py, tools/validate_moduledata.py
- `a9f2d7eda` 2026-10-16 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.34 - safer port tools, artist name and merge helpers
  - why: Several tool fixes apply to 1.4.x as is: translate_with_claude.py still treats --max-entries 0 as no cap on paid calls (origin/bannerlord-1.4.5 line 930), generate_starter_kit.py can delete another author's rows inside its marker block, and generate_tavern_mercenaries.py writes with no dry run.
  - port notes: Take: translate_with_claude.py (max_entries is not None, the help text, --seed-only, since --sync-ids exists on 1.4.x) with tools/tests/test_translate_caps_and_seed.py and the LanguageFileCoverageTests message; the generate_starter_kit.py foreign-row guard (block_re, apply_xslt, revert_xslt all exist on 1.4.x) with its tests; generate_tavern_mercenaries.py dry run unless --apply with its test. Optional: merge_live_file.py and register_armory_names.py (its 611-name baseline is the 1.5.x live Armory's; regenerate for 1.4.x). Skip: the taom_partyTemplates.xml Shaghana stacks, the retired Harad armour names, generate_enlistment_rosters --reseed and the Harad bow table, and the validate_moduledata.py/taom_schema.py melee-ladder and armour-class hunks (those passes do not exist on 1.4.x). Take 576's follow-ups with it.
  - conflicts: Main/_Module/ModuleData/taom_partyTemplates.xml, tools/analyze_melee_ladder.py, tools/taom_schema.py, tools/tests/test_generate_enlistment_rosters.py, tools/tests/test_validate_moduledata.py, tools/validate_moduledata.py
  - issues: 804, 812, 813
- `71fea53c8` 2026-10-16 PORT-PARTIAL P2 pick=conflict :: Add painter scripts and texture sets for Carn Dum project
  - why: A mislabelled 68-file sweep: beside painter scripts and Erebor content it carries convergence fixes to 573's tools that apply to 1.4.x, including a stale-offset bug in generate_tavern_mercenaries.py's culture repoint loop that the 1.4.x repoint_cultures still has.
  - port notes: Take: generate_starter_kit.py (guard covers the pieces block, all-or-nothing writes that parse every file first, --verify reports foreign blocks) with test_generate_starter_kit.py; the tavern-mercenary reverse-iteration fix and its test; the localize SKILL.md --seed-only line; the three general lessons at the end of build-tooling-workflow.md (stage a shared file by hunk, a refusing guard runs before the first write, a cap of zero on paid calls); check_generator_item_refs.py's new spec adapted to DEFAULT_ROSTER_ITEMS only (ISSUE_LAUNCHER is absent from the 1.4.x generate_enlistment_rosters.py; verify that table's row shape); merge_live_file.py and register_armory_names.py follow-ups and the moduledata-validation.md rows only if those tools are taken. Skip: painter and blender scripts, the Carn Dum icon, map-icons.md, the prep-tripo-prop skill and its CLAUDE.md row, the Erebor npcs/troops/child-equipment data and DwarfDressWearerTests, taom_schema.py melee pricing, HaradWeaponsLiveEditTests (ArmourAcquisition) and the Carn Dum painter lessons.
  - prereqs: a9f2d7eda
  - conflicts: Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml, TAOM.Tests/Features/ArmourAcquisition/HaradWeaponsLiveEditTests.cs, tools/armory-unregistered-names-baseline.txt, tools/generate_enlistment_rosters.py, tools/generate_starter_kit.py, tools/merge_live_file.py, tools/oneoff/convert_tripo_prop_textures.py, tools/register_armory_names.py, tools/taom_schema.py, tools/tests/test_generate_enlistment_rosters.py, tools/tests/test_generate_starter_kit.py, tools/tests/test_generate_tavern_mercenaries.py, tools/tests/test_merge_live_file.py, tools/tests/test_register_armory_names.py, tools/tests/test_translate_caps_and_seed.py, tools/tests/test_validate_moduledata.py, tools/translate_with_claude.py, tools/validate_moduledata.py

### group `tools-tripo-map-icon`  P3  track=dev-tools  commits=3  sim: clean=0 conflict=3
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/blender/probe_fbx.py, tools/blender/unwrap_texture_sets.py, tools/oneoff/blender_build_carn_dum_icon.py, tools/oneoff/blender_prep_tripo_prop.py, tools/oneoff/convert_tripo_prop_textures.py, tools/painter/painter_export.py, tools/painter/painter_new_project.py, tools/painter/painter_run.py, tools/painter/painter_stacks.py
- `041808e5f` 2026-10-16 PORT-PARTIAL P3 pick=conflict :: fix(tools): v2.0.34 - map icon tooling review fixes and shared job file
  - why: Review fixes for the Tripo, Blender and Painter map-icon tooling, mostly new on 1.5.x, that also harden convert_tripo_prop_textures.py, which on 1.4.x still defaults --dst into the live TAOM_Map textures folder and overwrites without backup.
  - port notes: Minimum: the convert_tripo_prop_textures.py hardening (required --src and --dst, --force, every texture set) and the blender_prep_tripo_prop.py report clearing through tools/blender/blender_job.py, with test_convert_tripo_prop_textures.py and test_blender_job.py; on origin/bannerlord-1.4.5 the converter defaults to DEFAULT_DST under Modules\TAOM_Map\AssetSources\Scenes\Mordor\textures (tools/oneoff/convert_tripo_prop_textures.py:40,79). Both files diverged on 1.5.x through c79a58521 and 71fea53c8, so expect a hand merge. The Carn Dum builder, unwrap, painter_* and render scripts need the whole 71cc4c6d2/71fea53c8 tooling; take them only if the maintainer wants the map-icon pipeline on 1.4.x (the art lands in the shared unversioned modules either way). 71fea53c8 also carries content (Erebor dress, KEYforce Harad javelins): never take those parts.
  - prereqs: 71fea53c8
  - conflicts: tools/blender/probe_fbx.py, tools/blender/unwrap_texture_sets.py, tools/oneoff/blender_build_carn_dum_icon.py, tools/oneoff/blender_prep_tripo_prop.py, tools/oneoff/convert_tripo_prop_textures.py, tools/painter/painter_export.py, tools/painter/painter_new_project.py, tools/painter/painter_run.py, tools/painter/painter_stacks.py
- `0daece880` 2026-10-16 PORT-PARTIAL P3 pick=conflict :: docs(kitbash): v2.0.34 - Carn Dum icon RCA, lessons and workflow docs
  - why: Docs and lessons for the map-icon tooling review, of which the lessons and one harness fact are generic.
  - port notes: Take the build-tooling-workflow.md lessons (one shared completion-file wrapper, no default output into a live module, a one-item step must refuse many, estimates labelled as estimates) and the docs/reference/mcp-servers.md note that ~/.claude.json keys local MCP servers by the folder's spelling, case included. Take the tools/README rows and substance-painter-mcp-setup.md edits only for tools ported with 041808e5f. Leave the Carn Dum RCA, docs/kitbash/map-icons.md and docs/features/animalia-elk-moose.md (absent on 1.4.x).
  - prereqs: 041808e5f
- `a69cd39df` 2026-10-16 PORT-PARTIAL P3 pick=conflict :: chore(skills): v2.0.34 - prep-tripo-prop covers the rebuild route
  - why: Extends the /prep-tripo-prop skill, which exists only on 1.5.x (created by 71cc4c6d2), with the rebuild-from-solids route.
  - port notes: Only together with the map-icon tooling port: bring the skill as of this commit (71cc4c6d2 plus this one) and add a 1.4.x CLAUDE.md routing row instead of applying the 1.5.x row edit. Otherwise skip it.
  - prereqs: 71cc4c6d2, 041808e5f

### group `issue-930-clan-heraldry-eol`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
- `b75fbab07` 2026-10-19 PORT P2 pick=conflict :: fix(tools): v2.0.35 - clan heraldry tool keeps the file's line endings
  - why: generate_clan_heraldry.py writes bare LF into a CRLF checkout of spclans.xslt and its spec tests could pass with an empty spec folder; the 1.4.x tool and test are identical to the fix's parent.
  - port notes: Only tools/README.md conflicts (the 1.4.x README differs widely); tools/generate_clan_heraldry.py and tools/tests/test_clan_heraldry_specs.py apply cleanly.
  - issues: 930

### group `tools-armory-mirror-default`  P2  track=dev-tools  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/_gamedir.py, tools/generate_ranged_ladder_items.py, tools/generate_starter_kit.py, tools/tests/test_generate_starter_kit.py, tools/tests/test_ranged_ladder.py
- `798e4d0bf` 2026-10-23 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - item generators leave the assets mirror alone
  - why: The ranged-ladder and starter-kit generators default to also writing and verifying the lotraom-assets mirror, so --apply edits it and --verify reports its sync state as drift; 1.4.x has the same defaults (generate_ranged_ladder_items.py:300-321, generate_starter_kit.py:1085-1114, mirror E:\repos\lotraom-assets\v1.4).
  - port notes: First confirm the maintainer's 2026-09-22 rule (sessions never touch the lotraom-assets mirror unless asked) also covers the v1.4 mirror. 1.4.x has no _gamedir.armory_trees (its tools/_gamedir.py defines only game_dir, game_modules and ensure_exists; armory_trees came with b904e02a3, part of the 1.5.x per-tier ladder rework), so re-implement in each 1.4.x generator: default --asset-repo to None and add the mirror only when it is named; keep --no-asset-repo as an accepted no-op in generate_starter_kit.py. Port the matching test changes and the ranged-ladders.md, starting-equipment-tuning.md and tools/README.md wording.
  - conflicts: tools/_gamedir.py, tools/generate_ranged_ladder_items.py, tools/generate_starter_kit.py, tools/tests/test_generate_starter_kit.py, tools/tests/test_ranged_ladder.py
- `72cad16b1` 2026-10-23 PORT-PARTIAL P3 pick=conflict :: docs(coop): v2.0.35 - dedicated server operator page, stale docs fixed
  - why: Mostly the new co-op dedicated-server operator page, but it also corrects stale text present on 1.4.x: the item generators' mirror wording and the tpac_skeleton_extract.py deprecation reason (origin/bannerlord-1.4.5 tools/README.md:239, docs/features/starting-equipment-tuning.md:190).
  - port notes: After 798e4d0bf, take only: the generator mirror wording in tools/README.md and docs/features/starting-equipment-tuning.md (keep the 1.4.x v1.4 mirror path), and the corrected tpac_skeleton_extract.py reason (package GUID reused from the skeleton item GUID, TOC size 0) in tools/README.md, docs/tools/spider-skeleton-tpac-tools.md and docs/ai-includes/creature-mount-authoring.md, verified against the tool source, with every DsAssetPackages and server-only sentence removed. Leave coop-dedicated-server.md, the server boot text, armory-ref-audit.md and the INDEX, feature-map and doc-lookup rows.
  - prereqs: 798e4d0bf

### group `tools-graphify-code-graph`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .agents/skills/taom-build/SKILL.md, tools/graphify_taom.py, tools/tests/test_graphify_taom.py
- `e6745a492` 2026-10-24 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.35 - graph finds interface and member callers
  - why: graphify_taom.py's affected query gains interface and member-caller blast radius, plus a tests-filter command and a detached refresh; dev tooling in scope, but the wrapper (from b8833474e) is absent on 1.4.x.
  - port notes: Port b8833474e first. Then take graphify_taom.py, its tests, graphify-code-graph.md, the adopt-codegraph record, the provenance row, the evidence-over-claims and agent-manual pointers, the feature-builder and refactoring-specialist lines and the .agents taom-build line. The session-start background refresh needs ss_probe_budget (407570ece). Drop or adapt the one-line edits to files absent on 1.4.x: .claude/agents/implementer.md (fdec324f8), improve/workflows/plans.js (1198881f0), docs/reviews/adopt-harness2-2026-10-21.md. 9cfcadd07 touches graphify_taom.py again later.
  - prereqs: b8833474e, 407570ece
  - conflicts: .agents/skills/taom-build/SKILL.md, tools/graphify_taom.py, tools/tests/test_graphify_taom.py
  - issues: 995

### group `map-tools-hardening`  P2  track=dev-tools  commits=3  sim: clean=0 conflict=3
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/Apply-MapVillageNames.py, tools/add_map_fortifications.py, tools/add_map_villages.py, tools/external-loc-coverage-baseline.json, tools/generate_new_faction_settlements.py, tools/oneoff/reset_stale_village_loc_rows.py, tools/oneoff/write_village_descriptions.py, tools/tests/test_add_map_fortifications.py, tools/tests/test_add_map_villages.py, tools/tests/test_generate_new_faction_settlements.py, tools/tests/test_live_map_writers_main.py, tools/tests/test_live_map_writers_name_tags.py, tools/tests/test_reset_stale_village_loc_rows.py, tools/tests/test_write_village_descriptions.py, tools/translation_cache/br.json, tools/translation_cache/cns.json, tools/translation_cache/cnt.json, tools/translation_cache/de.json, tools/translation_cache/fr.json, tools/translation_cache/it.json, tools/translation_cache/jp.json, tools/translation_cache/ko.json, tools/translation_cache/pl.json, tools/translation_cache/ru.json, tools/translation_cache/sp.json, tools/translation_cache/tr.json, tools/translation_name_tags.json
- `244c15d27` 2026-10-27 PORT-PARTIAL P2 pick=conflict :: Add one-off scripts to reset stale village names and descriptions
  - why: Under a one-off-scripts message it also rewrites Apply-MapVillageNames.py, which on 1.4.x rewrites every table id in all 12 language files on each run with no dry run, backup or cache eviction, so a rename wipes later translations and the translator restores old names.
  - port notes: Take the Apply-MapVillageNames.py rewrite (renamed ids only, dry run by default, --apply with backups, every output parsed before any write, cache eviction) and the add_map_villages.py evict_cached_names helper and duplicate-name refusal, with their test_add_map_villages.py tests. Adapt: 1.4.x has no tools/name_tags.py (BR/IT name grammar, a 1.5.x localization feature), so drop carry_tag or port it; add_map_villages.py trails the pre-image by +183/-13 from 1.5.x content commits, so hand-apply. Leave tools/oneoff/reset_stale_village_loc_rows.py and write_village_descriptions.py, the translation_cache deletions (they evict 1.5.x-renamed rows), the generate_new_faction_settlements and name-tag tests, add_map_fortifications.py (new on 1.5.x) and the Lindon docs. Continued by 87ffea9b2 and af2b198e4.
  - conflicts: tools/Apply-MapVillageNames.py, tools/add_map_fortifications.py, tools/add_map_villages.py, tools/external-loc-coverage-baseline.json, tools/tests/test_add_map_fortifications.py, tools/tests/test_live_map_writers_name_tags.py, tools/translation_cache/br.json, tools/translation_cache/cns.json, tools/translation_cache/cnt.json, tools/translation_cache/de.json, tools/translation_cache/fr.json, tools/translation_cache/it.json, tools/translation_cache/jp.json, tools/translation_cache/ko.json, tools/translation_cache/pl.json, tools/translation_cache/ru.json, tools/translation_cache/sp.json, tools/translation_cache/tr.json, tools/translation_name_tags.json
- `87ffea9b2` 2026-10-28 PORT-PARTIAL P2 pick=conflict :: fix(map): v2.0.36 - Dunland names, 7 village descriptions, safer tools
  - why: Further hardens the map renamer (cache written before the master so a failed run is finished by rerunning it, refusal of a name already on the map, exit 2 on the wrong file) next to a Dunland and village-description repair made in the live TAOM_Map.
  - port notes: Take the Apply-MapVillageNames.py changes and the tests covering them (test_add_map_villages.py additions; test_live_map_writers_main.py and test_live_map_writers_name_tags.py are new on 1.5.x, so port only cases that do not need name_tags), and the per-channel repair check in release-process.md if 1.4.x packaging uses it. Leave the one-offs and their --rerun-retired gates, the generate_new_faction_settlements.py retirement, the translation_cache deletions and the Lindon/#1042 docs. The Dunland repair is live TAOM_Map content outside git; the laptop's installed 1.4.8 TAOM_Map still has the defect (Languages/DE/loc_settlements.xml:43 has town_EN1 = Epicrotea while settlements.xml names it Galtrev; Epicrotea appears in BR, DE, FR, IT, SP and TR), so the maintainer may want the same repair on the 1.4.8 package (content, his call).
  - prereqs: 244c15d27
  - conflicts: tools/Apply-MapVillageNames.py, tools/add_map_villages.py, tools/generate_new_faction_settlements.py, tools/oneoff/reset_stale_village_loc_rows.py, tools/oneoff/write_village_descriptions.py, tools/tests/test_add_map_fortifications.py, tools/tests/test_add_map_villages.py, tools/tests/test_generate_new_faction_settlements.py, tools/tests/test_live_map_writers_main.py, tools/tests/test_live_map_writers_name_tags.py, tools/tests/test_reset_stale_village_loc_rows.py, tools/tests/test_write_village_descriptions.py, tools/translation_cache/br.json, tools/translation_cache/cns.json, tools/translation_cache/cnt.json, tools/translation_cache/de.json, tools/translation_cache/fr.json, tools/translation_cache/it.json, tools/translation_cache/jp.json, tools/translation_cache/ko.json
  - issues: 1042, 1043, 763
- `af2b198e4` 2026-10-28 PORT-PARTIAL P3 pick=conflict :: docs(map): v2.0.36 - #1042 and Lindon docs match the shipped tools
  - why: Doc audit that restores the map renamer's targeted-write, cache-eviction and name-check description and notes that translated names change at the next game start or language switch, mixed with Lindon counts and v2.0.36 release-note fixes.
  - port notes: Only with the 244c15d27 and 87ffea9b2 tool hardening: take the tool-behaviour hunks of docs/reference/taom-map-settlement-naming.md, docs/modding/settlements.md, module-map.md and file-catalogue.md and the docstrings in Apply-MapVillageNames.py and add_map_villages.py; verify the module-map claim about when translated names change against v1.4.8 before porting it. Leave the Lindon counts, docs/releases/v2.0.36-discord.md, the RCAs, name-grammar-tags.md and add_map_fortifications.py.
  - prereqs: 244c15d27, 87ffea9b2
  - conflicts: tools/add_map_fortifications.py, tools/add_map_villages.py
  - issues: 1039, 1041, 1042, 763

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 2, batch 3, batch 8, batch 9, batch 12, batch 17, batch 18, batch 19, batch 20, batch 23, batch 26, batch 29
