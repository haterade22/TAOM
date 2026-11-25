#!/usr/bin/env python3
"""Run the data gates for the files that changed, and say PASS, FAIL or NOT RUN for each.

Authors run one gate, see it pass and declare the work clean, while the gate they skipped was the
one that read their file. Claude's hooks run only inside Claude Code, but a clone with `core.hooksPath`
set to `.githooks` runs the commit gates on a plain `git commit` too; without it, none run. This
script owns the kind table: which gates read which files, the command, and the doc to read first.
Lens 7 of /deep-review, /verify and the artist brief send authors here.

Usage:
  python tools/data_gates.py                      # gates for files changed in git (vs HEAD, plus untracked)
  python tools/data_gates.py <file> [<file> ...]  # also the gates for these files; name the LIVE
                                                  # Armory / TAOM_Map files you edited (git cannot see them)
  python tools/data_gates.py --all                # every gate, whatever changed
  python tools/data_gates.py --list               # the table, nothing run
  python tools/data_gates.py --with-tests         # also run the dotnet test gates (never with the game
                                                  # running unless -p:ModuleId= is kept in the command)

Verdict per gate:
  PASS     exit 0, nothing skipped.
  FAIL     a non-zero exit other than 2 (1 is a finding, 3 a crash), or a timeout.
  NOT RUN  exit 2 (no install, no lxml, bad input), exit-0 output that says it checked nothing (SKIPPED,
           a SKIP line, skipped=N, lxml not installed, 0 files validated), a dotnet gate without
           --with-tests (its command is printed), or a manual step. NOT RUN has not passed.

Exit code: 1 if any gate FAILed, else 2 if any NOT RUN, else 0. A run that selects no gate exits 0. A git
failure, or a named file that does not exist or that two live modules both hold, exits 2 before any
gate runs.

The table is `GATES` below; a new gate is registered here once. Tests: tools/tests/test_data_gates.py.
"""
from __future__ import annotations

import argparse
import fnmatch
import re
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

REPO = Path(__file__).resolve().parent.parent
TIMEOUT = 900
DOTNET_TEST = ("dotnet", "test", "TAOM.Tests", "-p:DisableModuleCopy=true", "-p:ModuleId=", "--filter")
MODULEDATA_DOC = "docs/features/moduledata-validation.md"
CODES_DOC = "docs/reference/moduledata-validator-codes.md"
ARMORY_DOC = "docs/reference/armory-guide.md"


@dataclass(frozen=True)
class Gate:
    id: str
    kind: str                 # the kind of file that reads this gate, for the table
    globs: tuple              # fnmatch patterns on the lower-case, forward-slash path; start with *
    cmd: tuple                # ("python", "tools/x.py", args...) or ("dotnet", ...); () for a manual step
    read_first: tuple         # repo-relative docs to read before the gate or its finding
    dotnet: bool = False      # needs a build: NOT RUN unless --with-tests
    manual: str = ""          # a step nothing automates; printed as NOT RUN
    files: bool = False       # the command takes the matched files (all mode runs it bare)
    note: str = ""


def _dotnet(*names: str) -> tuple:
    return DOTNET_TEST + ("|".join(f"FullyQualifiedName~{n}" for n in names),)


TROOP_GLOBS = ("*/troops/troops_*.xml", "*/characters/*.xml", "*/equipmentsets/*.xml", "*/taom_spcultures.xml",
               "*/taom_partytemplates.xml", "*/named_companions/*.xml",
               "*/taom_wanderers.xml", "*/taom_education_character_templates.xml", "*/lords.xslt",
               "*/spcultures.xslt", "*tools/schemas/*.json", "*tools/validate_moduledata.py",
               "*tools/taom_schema.py")
MODULEDATA_GLOBS = ("*/_module/moduledata/*.xml", "*/_module/moduledata/*.xslt", "*tools/shipped_troop_ids.txt")
ARMORY_GLOBS = ("*/lotrlome_armory/moduledata/*","*/lotrlome_items/*.xml", "*/crafting_pieces*.xml",
                "*/weapon_descriptions*.xml")
MAP_GLOBS = ("*/taom_map/moduledata/*",)
# Everything the placeholder generator prices from: its own spec and output, the melee tools and ladder
# spec, the donors (live Armory) and the troops that carry the weapons.
PLACEHOLDER_GLOBS = ("*/placeholder_weapons/*", "*tools/placeholder_weapons.json", "*tools/melee_ladders.json",
                     "*tools/generate_placeholder_weapons.py", "*tools/melee_ladder.py", "*tools/melee_damage.py",
                     "*tools/melee_catalogue.py", "*tools/analyze_melee_ladder.py", "*tools/restat_melee_blades.py",
                     "*tools/fix_melee_ladder.py", "*/troops/troops_*.xml",
                     # The catalogue's module set and loading rules (#1375).
                     "*/_module/submodule.xml", "*tools/audit_polearm_shield_parity.py",
                     # The swap rule: melee_tier reads NOBLE_TROOPS, and fix_melee_ladder.split_generated reads the
                     # generated troop files that REGENERATABLE feeds.
                     "*tools/taom_schema.py", "*tools/generate_new_faction_kingdoms.py") + ARMORY_GLOBS
# Both armour generators write stats through rebalance_armor.calculate_stats.
ARMOUR_CURVE_GLOBS = ("*tools/rebalance_armor.py",)
# The lord armour variants (#1333): their output and spec, the generator and every tool it imports, the
# rosters its wearer check reads (TROOP_GLOBS), the Armory sources it copies and the restat map (the
# repo trace of an Armory restat, which can change a source's weight, material or share).
LORD_VARIANT_GLOBS = ("*/lord_armour/*", "*tools/lord_armour_variants.json", "*tools/generate_lord_armour_variants.py",
                      "*tools/derive_armor_tiers.py", "*tools/fix_lord_kits.py", "*tools/rebalance_troops.py",
                      "*tools/fix_upgrade_armour_regressions.py", "*tools/data/armor_roster_tiers.json",
                      "*/submodule.xml") + ARMOUR_CURVE_GLOBS + TROOP_GLOBS + ARMORY_GLOBS
# The armour band clones and upgrades (2026-11-17): their output and spec, the generator and every tool it
# imports, the sets it swaps (TROOP_GLOBS: troops, characters, equipment sets, companions; the Fords of War
# copies), the generators of whole troop files that carry the swap and the list of those files
# (generate_new_faction_kingdoms.REGENERATABLE), the enlistment generator (its rank bands), the Mallorn
# pricing (generate_lothlorien_armor.ML_BAND_UP), the Armory sources it copies, the restat map and the name
# key count its --check compares.
BAND_CLONE_GLOBS = ("*/armour_band_clones/*", "*tools/armour_band_clones.json", "*tools/generate_armour_band_clones.py",
                    "*tools/derive_armor_tiers.py", "*tools/fix_upgrade_armour_regressions.py",
                    "*tools/rebalance_troops.py", "*tools/fix_melee_ladder.py", "*/fords_of_war/*",
                    "*tools/realm_troops.py", "*tools/generate_lothlorien_troops.py", "*tools/generate_snow_orc_troops.py",
                    "*tools/generate_new_faction_kingdoms.py", "*tools/generate_lothlorien_armor.py",
                    "*tools/generate_enlistment_rosters.py", "*tools/loc_inline_backlog.json",
                    "*tools/data/armor_roster_tiers.json", "*/submodule.xml", "*tools/scan_armour_colours.py",
                    "*tools/armour_colour_overrides.json", "*tools/data/armour_colour_families.json"
                    ) + ARMOUR_CURVE_GLOBS + TROOP_GLOBS + ARMORY_GLOBS
CREATURE_GLOBS = ("*/action_sets.xml", "*/action_types.xml", "*/monster_usage_sets.xml", "*/monsters/*.xml",
                  "*/lotr_monster_*.xml")
# check_gait_stride.py and every module it imports: the stride gate of each creature runs them all.
STRIDE_TOOL_GLOBS = ("*tools/check_gait_stride.py", "*tools/set_clip_balance_name.py", "*tools/tpac_clone_metamesh.py",
                     "*tools/tpac_skeleton_dump.py", "*tools/_gamedir.py", "*tools/elephant_gait_retarget.py",
                     "*tools/elephant_write_masters.py")
# The Armory's cache entries. A stride gate and a bind check read the cook of each clip they judge, and an entry is
# named by a GUID, not by its creature. Git does not see the live Armory, so such a path selects a gate only when
# it is named on the command line.
CACHE_GLOBS = ("*/lotrlome_armory/runtimedatacache/*.rdc",)
# What the two elephant gates read beside the creature XML (masters, clip records), and the tools that write it.
ELEPHANT_GLOBS = CREATURE_GLOBS + STRIDE_TOOL_GLOBS + CACHE_GLOBS + (
    "*/creature/elephant/*.tpac", "*tools/bind_elephant_clips.py", "*tools/gen_elephant_anim_clips.ps1")
# The same for the two spider gates (#1541). gen_elephant_anim_clips.ps1 and elephant_write_masters.py write the
# spider's records and masters too, and check_gait_stride.py reads both creatures.
SPIDER_GLOBS = CREATURE_GLOBS + STRIDE_TOOL_GLOBS + CACHE_GLOBS + (
    "*/creature/spider/*.tpac", "*tools/bind_spider_clips.py", "*tools/spider_gait.py",
    "*tools/gen_elephant_anim_clips.ps1")
# The warg's clip records and masters, the Armory's cache entries (the gate reads the cook of each planned record,
# and a cache entry is named by a GUID, not by its creature), the plan that sets the records, and the tool that writes
# them (#1563, #1565).
# The check also runs creature_fit.master_item (each master's GUID) and _gamedir (the install).
GAIT_TOOL_GLOBS = ("*tools/repoint_anim_clip.py", "*tools/set_clip_balance_name.py", "*tools/creature_fit.py",
                   "*tools/_gamedir.py")
WARG_GLOBS = ("*/creature/warg/animations/*.tpac", "*/lotrlome_armory/runtimedatacache/*.rdc",
              "*tools/warg_gait_plan.json") + GAIT_TOOL_GLOBS
# What the warg's stride gate reads (#1565 item 2): the action set, every package under the warg's folder (records,
# masters, the rig) and the cache entries of the gaits. The gate does not read the plan; the plan sets the records
# the gate measures, and git does not see those, so a changed plan selects the gate as the records' writers do for
# the elephant and the spider (#1591).
WARG_STRIDE_GLOBS = CREATURE_GLOBS + STRIDE_TOOL_GLOBS + CACHE_GLOBS + (
    "*/creature/warg/*.tpac", "*tools/warg_gait_plan.json")
# The same for the dire wolf's stride gate (#1591).
DIRE_WOLF_STRIDE_GLOBS = CREATURE_GLOBS + STRIDE_TOOL_GLOBS + CACHE_GLOBS + (
    "*/creature/direwolf/*.tpac", "*tools/dire_wolf_gait_plan.json")
# What the dire wolf's plan check reads: its gait records, which its own plan sets (#1565).
DIRE_WOLF_GLOBS = ("*/creature/direwolf/animations/*.tpac", "*/lotrlome_armory/runtimedatacache/*.rdc",
                   "*tools/dire_wolf_gait_plan.json") + GAIT_TOOL_GLOBS
FBX_GLOBS = ("*/assetsources/*.fbx",)
LANG_GLOBS = ("*/languages/*.xml", "*/taom_module_strings.xml", "*/taom_xslt_strings.xml")
# Every source and output of generate_name_localization_strings.py CATEGORIES not already in TROOP_GLOBS,
# and the tool itself (#1374; test_data_gates pins it against CATEGORIES).
NAME_LOC_GLOBS = ("*/realms_titles.xml", "*/custom_battle_scenes.xml", "*/career_system/*.xml",
                  "*/taom_spkingdoms.xml", "*/taom_wanderers.xml", "*/named_companions/named_companions.xml",
                  "*/_module/moduledata/taom_*_strings.xml", "*tools/generate_name_localization_strings.py",
                  "*tools/_loc_sources.py")
# Globs tested against the file name alone: `*banner*` on a full path matches the install folder
# "Mount & Blade II Bannerlord", so every named live file would select banner-sheets.
NAME_GLOBS = ("*banner*",)

# Order matters where an author has to act in order: names are registered before loc coverage is read.
GATES = (
    Gate("xml-schemas", "any engine-registered XML, repo or live",
         ("*/moduledata/*.xml", "*/submodule.xml", "*/moduledata/*.xsl", "*/moduledata/*.xslt"),
         ("python", "tools/validate_xml_schemas.py"), ("tools/validate_xml_schemas.py",), files=True,
         note="finds each file's module; add SubModule.xml when it changed"),
    Gate("xml-schemas-live", "whole live modules (Armory, TAOM_Map)", (),
         ("python", "tools/validate_xml_schemas.py", "--live"), ("tools/validate_xml_schemas.py",)),
    Gate("moduledata", "troops, characters, rosters, cultures, clans, party templates, lords, Armory refs, "
         "TAOM_Map settlements", TROOP_GLOBS + MODULEDATA_GLOBS + ARMORY_GLOBS + MAP_GLOBS
         # The enlistment rank rule lives in the generator and prices weapons with these tools (#1290).
         + ("*tools/generate_enlistment_rosters.py", "*tools/melee_catalogue.py", "*tools/analyze_melee_ladder.py",
            "*tools/ranged_ladder.py", "*tools/audit_polearm_shield_parity.py",
            # ARMOUR_COLOUR_CLASH reads the colour table and its scanner's clash rule (2026-11-18).
            "*tools/data/armour_colour_families.json", "*tools/scan_armour_colours.py"),
         ("python", "tools/validate_moduledata.py"),
         (".claude/rules/moduledata-validation.md", ".claude/rules/troops.md", ".claude/rules/xml-data.md", CODES_DOC),
         note="read the warnings as well as the errors; settlement pins live in tools/settlement_assignments.json"),
    Gate("generator-regions", "troops, characters, rosters, cultures, lord skill sets",
         TROOP_GLOBS + ("*/taom_lord_skill_sets.xml", "*tools/apply_culture_skills_traits.py"),
         ("python", "-m", "unittest", "tools.tests.test_generator_regions_in_sync"), (".claude/rules/data-scripts.md",),
         note="a hand edit inside a generator's region fails it: change the generator's table, then --apply"),
    Gate("career-kits", "troops, characters, rosters, the career tool, the band clone spec and tool",
         # career_armour gives a career kit its light band clone through generate_armour_band_clones.armour_for_band,
         # which reads the clone spec and prices through rebalance_armor.
         TROOP_GLOBS + ("*tools/generate_career_kits.py", "*tools/armour_band_clones.json",
                        "*tools/generate_armour_band_clones.py") + ARMOUR_CURVE_GLOBS,
         ("python", "tools/generate_career_kits.py", "--verify"), ("docs/features/career-system.md",),
         note="after any troop add or delete, re-run every generator check, not only the validator"),
    Gate("name-loc", "troops, characters, language files, name generator sources", TROOP_GLOBS + LANG_GLOBS + NAME_LOC_GLOBS,
         ("python", "tools/generate_name_localization_strings.py", "--check"), ("docs/localization/TRANSLATOR_GUIDE.md",)),
    Gate("wanderers", "wanderers, wanderer strings", ("*/taom_wanderers.xml", "*/taom_wanderer_strings.xml"),
         _dotnet("WandererBackstoryCoverageTests"), (MODULEDATA_DOC,), dotnet=True),
    Gate("xslt", "XSLT anywhere", ("*.xslt",), ("python", "tools/check_external_xslt.py"), (".claude/rules/xslt.md",),
         note="then transform and diff against the vanilla file (xslt.md file map)"),
    Gate("xslt-party", "spcultures.xslt", ("*/spcultures.xslt",), _dotnet("CulturePartyTemplateTests"),
         (".claude/rules/xslt.md",), dotnet=True),
    Gate("notable-templates", "notable and headman templates, spcultures, wanderers",
         ("*/characters/npcs_*.xml", "*/taom_spcultures.xml", "*/spcultures.xslt", "*/taom_wanderers.xml"),
         _dotnet("NotableTemplateGenderTests"), (MODULEDATA_DOC,), dotnet=True),
    Gate("armory-refs", "Armory items, crafting pieces, weapon descriptions", ARMORY_GLOBS,
         ("python", "tools/audit_armory_refs.py", "--report", "-"), (ARMORY_DOC,),
         note="meshes, collision bodies, dead item ids, and the companion checks of /armory-audit"),
    Gate("polearm-shield", "Armory weapons and pieces", ARMORY_GLOBS + TROOP_GLOBS
         + ("*/placeholder_weapons/*", "*/_module/submodule.xml", "*tools/audit_polearm_shield_parity.py"),
         ("python", "tools/audit_polearm_shield_parity.py"), ("docs/features/weapon-xml-pipeline.md",)),
    Gate("couch-brace", "Armory weapon descriptions, the couch spec and its tool",
         ARMORY_GLOBS + ("*tools/couch_brace_polearms.json", "*tools/register_couch_brace_polearms.py",
                         "*tools/register_one_handed_polearms.py"),
         ("python", "tools/register_couch_brace_polearms.py", "--check"), ("docs/features/weapon-xml-pipeline.md",),
         note="the Dol Guldur warg riders' spears must fit TwoHandedPolearm_Couchable and _Bracing; exit 1 means a "
              "reinstall or sync reset the Armory's weapon_descriptions.xslt: --apply --backup-tag <tag> restores it; "
              "exit 2 is NOT RUN (no install), exit 3 a crash"),
    Gate("mesh-refs", "Armory weapons and pieces", ARMORY_GLOBS,
         ("python", "tools/validate_mesh_refs.py", "--scan-bodies"), (ARMORY_DOC,),
         note="every mesh= and body_name= resolves to a shipped asset; pending artist bo_ meshes are the expected exception"),
    Gate("armour-classes", "Armory armour, troop rosters, the lord armour variants, the armour band clones",
         ARMORY_GLOBS + TROOP_GLOBS + ("*/lord_armour/*", "*/armour_band_clones/*"),
         ("python", "tools/generate_armour_classes.py", "--check"), (ARMORY_DOC,),
         note="exit 1 stale, 2 no Armory; re-run with --apply when stale"),
    Gate("armory-names", "Armory item and piece names", ARMORY_GLOBS,
         ("python", "tools/register_armory_names.py", "--check"), ("docs/localization/TRANSLATOR_GUIDE.md",),
         note="first: a new {=KEY} name needs a Languages row; fix with --apply --prefix <your ids>"),
    Gate("ranged-ladder-items", "Armory launchers", ARMORY_GLOBS,
         ("python", "tools/generate_ranged_ladder_items.py", "--verify"), ("docs/features/ranged-ladders.md",),
         note="checks the live Armory only"),
    Gate("restat-donors", "Armory launchers", ARMORY_GLOBS,
         ("python", "tools/restat_ranged_donors.py", "--verify"), ("docs/features/ranged-ladders.md",),
         note="drift reported only in its mirror tree means the lotraom-assets mirror lags the live install"),
    Gate("starter-kit", "Armory starter kits", ARMORY_GLOBS,
         ("python", "tools/generate_starter_kit.py", "--verify"), ("docs/features/starting-equipment-tuning.md",),
         note="checks the live Armory only"),
    Gate("leg-cover-flags", "Armory leg armour", ARMORY_GLOBS + ("*tools/generate_*armor*.py",),
         ("python", "tools/audit_leg_cover_flags.py"), ("docs/modding/items-armor.md",),
         note="covers_legs is true on every greave and boot, false only on a bare-shin mesh (the Uruk-hai shoes); "
              "scans the Armory, TAOM_Map and the repo, never vanilla"),
    Gate("armour-cover-flags", "Armory armour (cover and visibility attributes), their spec and its tool",
         ARMORY_GLOBS + ("*tools/armour_cover_flags.json", "*tools/apply_armour_cover_flags.py",
                         "*tools/generate_*armor*.py"),
         ("python", "tools/apply_armour_cover_flags.py", "--check"), ("docs/reference/lotrlome-beard-cover-changes.md",),
         note="holds hair, beard, mane, covers_head, covers_body, covers_hands and covers_legs for every armour item "
              "(crowns, beards under helmets, gloves, boots); exit 1 means a reinstall, sync or art drop reset a value "
              "or a rule changed: --apply --backup-tag <tag> restores, --snapshot --expect N after a change you meant "
              "(never before --apply on a reset); a bad spec or two rules that disagree are also exit 1; "
              "exit 2 is NOT RUN (no install)"),
    Gate("dwarf-chariot-armory", "the dwarven war chariot's Armory rows, their packages and their carriers",
         ARMORY_GLOBS + ("*tools/apply_dwarf_chariot_armory.py", "*/troops/troops_erebor.xml",
                         "*/lotrlome_armory/assets/creature/chariot/*", "*/lotrlome_armory/assets/creature/ram/*"),
         ("python", "tools/apply_dwarf_chariot_armory.py", "--check"), ("docs/features/chariot.md",),
         note="exit 1 when a reinstall dropped the three items or their names, a row differs from the tool's, or a "
              "mesh or material package is gone: --apply with the Kit and the game closed puts the rows back, the "
              "Modding Kit writes the packages; exit 2 is NOT RUN (no install), exit 3 a crash"),
    Gate("placeholder-weapons", "placeholder weapons, their spec, the melee tools, donors and carriers", PLACEHOLDER_GLOBS,
         ("python", "tools/generate_placeholder_weapons.py", "--verify"), ("docs/features/placeholder-weapons.md",),
         note="exit 1 when a generated file differs from the render or a placeholder is over its carriers' tier "
              "limit, when a numbered line repeats a numeral or falls in DPS at a placeholder's step, or when a troop "
              "file a generator writes still owes a swap (run that generator with --apply); run it after "
              "restat_melee_blades.py --apply, because each damage factor is relative to its donor"),
    Gate("lord-armour-variants", "lord armour variants, their spec, the generator and its imports, rosters, Armory sources",
         LORD_VARIANT_GLOBS, ("python", "tools/generate_lord_armour_variants.py", "--check"),
         ("docs/features/armor-balance.md",),
         note="exit 1 when the variants file or its SubModule node differs from the render, a new row's source "
              "needs no variant (a shipped row stays), or a roster other than a generic lord template or a fallback or ruler roster names "
              "one; exit 2 is NOT RUN (no install); after --apply run derive_armor_tiers.py and "
              "generate_armour_classes.py --apply"),
    Gate("lord-variant-meshes", "lord armour variants", ("*/lord_armour/*",),
         ("python", "tools/validate_mesh_refs.py", "--items", "Main/_Module/ModuleData/lord_armour", "--scan-bodies"),
         (ARMORY_DOC,), note="the mesh-refs gate scans the Armory only; a variant names an Armory mesh"),
    Gate("armour-band-clones", "armour band clones and upgrades, their spec, the generator and its imports, rosters, "
         "Armory sources", BAND_CLONE_GLOBS, ("python", "tools/generate_armour_band_clones.py", "--check"),
         ("docs/features/armor-balance.md",),
         note="exit 1 when the spec, the clone file, its SubModule node or a set differs from the plan (a piece "
              "worn by a level 36+ and a level 30- troop, TOP_TIER_ARMOUR_WORN_LOW; a low set naming a source, "
              "BAND_CLONE_SOURCE_WORN_LOW), a shipped clone or piece is gone, or a step is owed (NEXT: a "
              "generator's --apply, the loc key count); exit 2 is NOT RUN (no Armory slot file), 3 a crash; after "
              "--apply run scan_armour_colours.py --apply and this --check again (a new clone or pick needs its colour "
              "row), then derive_armor_tiers.py, the restat dry run, fix_lord_kits.py --values planned, "
              "generate_armour_classes.py --apply, then derive_armor_tiers.py again (armor-balance.md, Commands)"),
    Gate("armour-colours", "armour colour families, their overrides, the scanner, the Armory art, the clone and lord variant items",
         ("*tools/scan_armour_colours.py", "*tools/armour_colour_overrides.json",
          "*tools/data/armour_colour_families.json", "*tools/audit_battle_equipment_memory.py",
          "*tools/audit_map_scene_memory.py",
          # the art the scanner measures, and the TAOM items it reads through SubModule.xml
          "*/lotrlome_armory/assetsources/*", "*/lotrlome_armory/assets/*", "*/lotrlome_armory/assetpackages/*",
          "*/armour_band_clones/*", "*/lord_armour/*", "*/_module/submodule.xml") + ARMORY_GLOBS,
         ("python", "tools/scan_armour_colours.py", "--check"), ("docs/features/armor-balance.md",),
         note="exit 1 when tools/data/armour_colour_families.json differs from the install's art and the override file "
              "(or an override row is bad); exit 2 is NOT RUN (no Armory, no texture image under the install, no numpy "
              "or Pillow), exit 3 a crash; repair: --apply, then generate_armour_band_clones.py --check"),
    Gate("armour-band-clone-meshes", "armour band clones", ("*/armour_band_clones/*",),
         ("python", "tools/validate_mesh_refs.py", "--items", "Main/_Module/ModuleData/armour_band_clones",
          "--scan-bodies"),
         (ARMORY_DOC,), note="the mesh-refs gate scans the Armory only; a clone names its source's Armory mesh"),
    Gate("lorien-armor", "Lorien armour items and their generator",
         ARMORY_GLOBS + ARMOUR_CURVE_GLOBS + ("*tools/generate_lothlorien_armor.py",),
         ("python", "tools/generate_lothlorien_armor.py", "--check"), ("docs/features/lothlorien-snow-orcs.md",),
         note="exit 1 when the live Armory differs from the render; --apply copies each changed file to a .bak-gen backup"),
    Gate("snow-orc-armor", "Snow Orc armour items and their generator",
         ARMORY_GLOBS + ARMOUR_CURVE_GLOBS + ("*tools/generate_snow_orc_armor.py",),
         ("python", "tools/generate_snow_orc_armor.py", "--check"), ("docs/features/lothlorien-snow-orcs.md",),
         note="exit 1 when the live Armory differs from the render"),
    Gate("armour-restat", "Armory armour (values and loot tables), the curve, its roster map and the two generators",
         ARMORY_GLOBS + ARMOUR_CURVE_GLOBS + ("*tools/data/armor_roster_tiers.json",
                                              "*tools/generate_lothlorien_armor.py",
                                              "*tools/generate_snow_orc_armor.py"),
         ("python", "tools/rebalance_armor.py", "--check"), ("docs/features/armor-balance.md",),
         note="exit 1 when the live Armory differs from the curve; after an Armory reinstall run the restat with "
              "--apply and a backup tag (the apply sequence in the doc), and in a branch merge trunk first; "
              "exit 2 is NOT RUN (no install)"),
    Gate("generator-refs", "XML a tools/ generator wrote", ("*tools/generate_*.py",) + ARMORY_GLOBS,
         ("python", "tools/check_generator_item_refs.py"), ("tools/README.md",),
         note="and the generator's own --verify or --check; hand-read three generated entries"),
    Gate("action-sets", "action sets, monsters, monster usage", CREATURE_GLOBS,
         ("python", "tools/audit_action_set_parity.py"), (ARMORY_DOC,)),
    Gate("mount-parity", "creature mounts", CREATURE_GLOBS,
         ("python", "tools/audit_mount_parity.py", "--strict"), ("docs/ai-includes/creature-mount-authoring.md",),
         note="--strict gates the BT-driven mounts in STRICT_MOUNTS; the report itself always exits 0"),
    Gate("troll-wiring", "troll action sets and Monsters", CREATURE_GLOBS,
         ("python", "tools/wire_hill_troll_race.py", "--check"), ("docs/features/troll-race.md",)),
    Gate("troll-tests", "troll action sets and Monsters", CREATURE_GLOBS,
         _dotnet("TrollBruteForceWiringTests", "TrollHitPointsLiveDataTests"), ("docs/features/troll-race.md",), dotnet=True),
    Gate("orc-locomotion", "orc and goblin action sets", CREATURE_GLOBS,
         ("python", "tools/bind_orc_goblin_locomotion.py", "--check"), ("docs/features/troll-race.md",),
         note="a reinstall drops their rows and parity still passes"),
    Gate("elephant-clips", "elephant and mumak action sets, clip packages and their tools", ELEPHANT_GLOBS,
         ("python", "tools/bind_elephant_clips.py", "--check"), ("docs/features/elephant.md",),
         note="a reinstall drops as_mumakil and the rebound rows, and parity still passes; exit 1 also when a bound "
              "clip has no current cache entry (start the Modding Kit once)"),
    Gate("elephant-gaits", "elephant and mumak action sets, clip packages and their tools", ELEPHANT_GLOBS,
         ("python", "tools/check_gait_stride.py"), ("docs/features/elephant.md",),
         note="each gait clip's feet cover the ground its record claims; the pace rows of as_elephant and as_mumakil "
              "carry limits and Durations the engine plays well; the standing pose is on the ground; a bound clip "
              "has a cache entry no older than its record and master (start the Modding Kit once)"),
    Gate("spider-clips", "spider action sets, usage rows, clip packages and their tools", SPIDER_GLOBS,
         ("python", "tools/bind_spider_clips.py", "--check"), ("docs/features/spider.md",),
         note="a reinstall drops the rebound rows, the nine new actions, their rider rows, the pace table and the "
              "overlay rows, and "
              "parity still passes; exit 1 also when a bound clip has no current cache entry (start the Modding Kit once)"),
    Gate("spider-gaits", "spider action sets, usage rows, clip packages and their tools", SPIDER_GLOBS,
         ("python", "tools/check_gait_stride.py", "--creature", "spider"), ("docs/features/spider.md",),
         note="each gait clip's tips cover the ground its record claims; the pace rows of as_spider carry limits and "
              "Durations the engine plays well; the standing pose is on the ground; a bound gait clip has a current cache "
              "entry (the stand, the idle, the twins and the turns: spider-clips)"),
    Gate("spider-body", "spider action sets, usage rows, clip packages and their tools", SPIDER_GLOBS + ("*tools/verify_mount_assets.py",),
         ("python", "tools/verify_mount_assets.py", "spider"), ("docs/features/spider.md",),
         note="the skeleton is in a live package, every movement clip carries its tag, no row names a clip that is "
              "not there, and the body capsule holds the body (#1560: a reinstall brings back the chest pill)"),
    Gate("warg-gaits", "warg clip records, their plan and its tool", WARG_GLOBS,
         ("python", "tools/repoint_anim_clip.py", "--plan", "tools/warg_gait_plan.json", "--under",
          "creature/warg/animations", "--check"), ("docs/reference/lotrlome-warg-gait-changes.md",),
         note="a reinstall or an art drop puts the canter back on the trot's keys, the gallop back on a travel "
              "twice what its paws cover and each pace back on one loop time, and it removes the walk's in-place "
              "twin; warg-stride sees the reverted travel and none of the rest; exit 1 also while a changed "
              "record waits for its cook (start the Modding Kit once)"),
    Gate("warg-stride", "warg action set, clip records, masters, rig and the stride gate", WARG_STRIDE_GLOBS,
         ("python", "tools/check_gait_stride.py", "--creature", "warg"),
         ("docs/reference/lotrlome-warg-gait-changes.md",),
         note="the paws cover the ground each forward gait and the backward walk claims, on the master its record "
              "names; the pace rows of as_warg carry limits the engine plays well; a bound gait's cache entry carries "
              "its record's checksum; the backward walk's feet move against the walk's; the two side steps are "
              "printed with what the node does with them, and their stance, slip, Duration, master and cook are "
              "held (#1591)"),
    Gate("dire-wolf-gaits", "dire wolf clip records, their plan and its tool", DIRE_WOLF_GLOBS,
         ("python", "tools/repoint_anim_clip.py", "--plan", "tools/dire_wolf_gait_plan.json", "--under",
          "creature/direwolf/animations", "--check"), ("docs/reference/lotrlome-dire-wolf-changes.md",),
         note="a reinstall, or a run of gen_dire_wolf_anim_clips.ps1 on a folder without the records, puts each gait "
              "back on the pack's travel and Duration (26% to 57% less ground than the paws cover) and removes the "
              "walk's in-place twin; exit 1 also while a changed record waits for its cook"),
    Gate("dire-wolf-stride", "dire wolf action set, clip records, masters, rig and the stride gate",
         DIRE_WOLF_STRIDE_GLOBS, ("python", "tools/check_gait_stride.py", "--creature", "dire_wolf"),
         ("docs/reference/lotrlome-dire-wolf-changes.md",),
         note="the paws cover the ground each forward gait and the backward walk claims, each paw's height taken "
              "from its own lowest point; the backward walk plays the walk's keys from last to first, so its feet "
              "move against the walk's (a reinstall puts them back on the forward keys); a bound gait's cache "
              "entry carries its record's checksum; the two side clips, the pack's turn clips, are printed, and "
              "their stance, slip (a limit of their own), Duration, master and cook are held"),
    Gate("dire-wolf-body", "dire wolf action sets, usage rows, clip packages and the mount asset check",
         CREATURE_GLOBS + ("*/creature/direwolf/*.tpac", "*tools/verify_mount_assets.py"),
         ("python", "tools/verify_mount_assets.py", "dire_wolf"), ("docs/features/dire-wolf.md",),
         note="every clip on a moving row carries its quad_movement tag and no row names a clip that is not there; "
              "a new in-place twin needs its exact (action, clip) pair in the tool's untagged_ok list (#1565)"),
    Gate("warg-clips", "action sets and the warg's bind tool", ("*/action_sets.xml", "*tools/bind_warg_clips.py"),
         ("python", "tools/bind_warg_clips.py", "--check"), ("docs/reference/lotrlome-warg-gait-changes.md",),
         note="a reinstall puts the walk's \"none\" row of as_warg and of as_dire_wolf back on the walk's front "
              "record, so the walk has one loop time again; the live file and the repo's snapshot must both carry "
              "both bindings"),
    Gate("dwarf-attacks", "dwarf attack actions and their as_dwarf_warrior rows", CREATURE_GLOBS,
         ("python", "tools/bind_dwarf_attack_actions.py", "--check"), ("docs/features/dwarf-attacks.md",),
         note="a reinstall drops the two declarations and rows, and parity still passes; it also fails while the "
              "rows still carry actt_kick (#1499: run it with --apply)"),
    Gate("dwarf-attacks-tests", "dwarf attack actions and their as_dwarf_warrior rows", CREATURE_GLOBS,
         _dotnet("DwarfAttacksWiringTests"), ("docs/features/dwarf-attacks.md",), dotnet=True),
    Gate("dwarf-rider-rows", "dwarf creature rider rows in as_dwarf_warrior", CREATURE_GLOBS,
         _dotnet("DwarfMountRiderRowsTests", "MountRiderFallRowsTests"), ("docs/features/war-ram.md",), dotnet=True,
         note="a reinstall or art drop drops the 232 rows (#1226; eight of them the spider's, #1541, one more #1550), and every other gate still passes"),
    Gate("race-morphs", "hill troll and dwarf rig FBX in the live Armory AssetSources", FBX_GLOBS,
         ("python", "tools/check_race_morph_channels.py"), ("tools/check_race_morph_channels.py",),
         note="exact counts: head, eye and mouth 101, hands or arms 26; a new race rig is unchecked until its meshes are in SPEC"),
    Gate("fbx-lods", "Armory FBX sources", FBX_GLOBS,
         ("python", "tools/audit_fbx_lods.py", "--check"), (ARMORY_DOC,),
         note="every mesh carries LOD0 to LOD5 with no gaps, except clo_ and bo_ meshes"),
    Gate("map-villages", "TAOM_Map settlements", MAP_GLOBS,
         ("python", "tools/add_map_villages.py", "--check"), ("docs/reference/taom-map-settlement-naming.md",)),
    Gate("map-fortifications", "TAOM_Map settlements", MAP_GLOBS,
         ("python", "tools/add_map_fortifications.py", "--check"), ("docs/reference/taom-map-settlement-naming.md",),
         note="a town or castle rename goes into its FORTS row too"),
    Gate("map-descriptions", "TAOM_Map settlements", MAP_GLOBS,
         ("python", "tools/oneoff/write_village_descriptions.py", "--check"), ("docs/reference/taom-map-settlement-naming.md",),
         note="descriptions and orphan rows, not the names; exit 1 after a reinstall of an older package"),
    Gate("map-entrances", "TAOM_Map settlements", MAP_GLOBS + ("*tools/check_settlement_entrances.py",),
         ("python", "tools/check_settlement_entrances.py", "--check"), ("docs/modding/settlements.md",),
         note="#1434: exit 1 while a 2.0.34 unreachable entrance remains, also after a reinstall of an older package"),
    Gate("banner-sheets", "banner icons, banner keys, sheets and sprites",
         ("*/bannericons/*", "*/ui_taom_bannericons/*", "*banner*"),
         ("python", "tools/pack_banner_sheets.py", "--audit"), ("docs/reference/banner-icon-generation.md",)),
    Gate("banner-rdc", "banner icon Kit imports", ("*/bannericons/*",),
         ("python", "tools/check_rdc_entries.py", "--module", "{taom_module}", "--under", "BannerIcons", "--types", "tex"),
         ("docs/reference/banner-icon-generation.md",)),
    Gate("rdc-entries", "tpac packages", ("*.tpac",), ("python", "tools/check_rdc_entries.py"),
         ("docs/reference/banner-icon-generation.md",), note="a listed package needs a Modding Kit save"),
    Gate("gui-tests", "GUI prefabs, PrefabExtension XML, sprite data",
         ("*/gauntletui/*", "*/prefabs/*.xml", "*prefabextension*", "*spritedata.xml"),
         _dotnet("PrefabExtensionBrushTests", "EveryPrefabAttribute_NamesASettableWidgetProperty",
                 "PrefabElementTypeBindingTests", "GauntletStretchInCoverRatchetTests", "PrefabCloneWidgetReferenceTests",
                 "PrefabSpriteTests"),
         (".claude/rules/gui-ui.md",), dotnet=True,
         note="PrefabCloneWidgetReferenceTests applies to a TAOM copy of a vanilla prefab"),
    Gate("gui-manual", "GUI prefabs, PrefabExtension XML", ("*/gauntletui/*", "*/prefabs/*.xml", "*prefabextension*"),
         (), (".claude/rules/gui-ui.md",),
         manual="read each GetSprite(\"X\") name in C# against TAOMSpriteData.xml (PrefabSpriteTests gates a literal "
                "Sprite=\"X\" in a prefab, not a C# call), and check that every Command.* handler resolves to a TAOM "
                "method with the right parameter count"),
    Gate("loc-orphans", "English strings files, the orphan gate's own inputs",
         ("*/_module/moduledata/*strings*.xml", "*tools/check_orphan_loc_rows.py", "*tools/_loc_sources.py",
          "*tools/loc_composed_families.json", "*tools/loc_orphan_allowlist.json"),
         ("python", "tools/check_orphan_loc_rows.py"), ("docs/localization/TRANSLATOR_GUIDE.md",),
         note="a row is also orphaned when the C# literal, XSLT or JSON that read it is removed: run it by hand after such a removal"),
    Gate("loc-coverage", "language files, {=KEY} text", LANG_GLOBS + ARMORY_GLOBS,
         ("python", "tools/check_external_loc_coverage.py"), ("docs/reference/localization-map.md",),
         note="after any name or language edit; --apply raises the untranslated count, so run its --update-baseline in the same change"),
    Gate("loc-tests", "language files, {=KEY} text", LANG_GLOBS,
         _dotnet("Infrastructure.Localization"), ("docs/reference/localization-map.md",), dotnet=True,
         note="LanguageDataXmlTests, LocalizationKeyConsistencyTests, LanguageTextIntegrityTests"),
    Gate("mbproj", "project.mbproj, action sets and types", ("*project.mbproj", "*/action_sets.xml", "*/action_types.xml"),
         ("python", "tools/audit_mbproj_registration.py"),
         ("tools/audit_mbproj_registration.py",)),
    Gate("voice", "voice definitions, module sounds", ("*/voice_definitions.xml", "*/module_sounds.xml"),
         ("python", "tools/audit_voice_clip_lengths.py"), ("tools/audit_voice_clip_lengths.py",),
         note="only the module sounds a voice binds; for the rest of module_sounds.xml, only duplicate names"),
    Gate("prefab-budget", "scenes, prefabs", ("*/scenes/*", "*.scene.xml", "*/sceneobj/*"),
         ("python", "tools/check_prefab_budget.py"), ("docs/modding/module-map.md",),
         note="counts TAOM_Map only; sum all modules"),
    Gate("siege-walls", "TAOM_Map scene files", ("*/sceneobj/*/scene.xscene",),
         ("python", "tools/check_siege_wall_scripts.py"),
         ("docs/reference/engine/scene-gameentity-scriptcomponent.md",), files=True,
         note="judges the scene files you named, or every live TAOM_Map scene under --all; "
              "a wall placed as a bare prefab instance is not judged"),
)


def norm(path: str) -> str:
    return str(path).replace("\\", "/").lower()


def matches(gate: Gate, path: str) -> bool:
    p = norm(path)
    name = p.rsplit("/", 1)[-1]
    return any(fnmatch.fnmatch(name if g in NAME_GLOBS else p, g) for g in gate.globs)


def select(gates, paths, all_mode: bool = False) -> list:
    if all_mode:
        return list(gates)
    return [g for g in gates if any(matches(g, p) for p in paths)]


def _taom_module() -> str:
    from _gamedir import game_dir
    return str(Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")) / "Modules" / "TAOM")


def command_for(gate: Gate, paths, all_mode: bool) -> list:
    """The command to run, with `python` still the first word (the runner swaps in the interpreter)."""
    argv = [a.replace("{taom_module}", _taom_module()) if "{taom_module}" in a else a for a in gate.cmd]
    if gate.files and not all_mode:
        # a deleted file still selects the gate but cannot be validated; the gate runs bare if none is left
        argv += [p for p in paths if matches(gate, p) and (REPO / p).exists()]
    return argv


# Exit-0 output that means the gate checked nothing. Case-sensitive: audit_mbproj_registration prints
# "Skipped official modules" on a normal run.
NOT_RUN = re.compile(r"\bSKIPPED\b|^SKIP\b|\(skipped=\d+\)|lxml not installed|^PASS: 0 file\(s\) validated", re.M)


def verdict(returncode: int, output: str) -> tuple:
    if returncode == 0:
        line = next((l.strip() for l in output.splitlines() if NOT_RUN.search(l)), None)
        if line:
            return "NOT RUN", f"checked nothing: {line[:200]}"
        return "PASS", ""
    if returncode == 2:
        reason = "exit 2 (no install, a missing dependency or bad input)"
        # The output of a gate that did not run is not printed, so carry the line that names the file.
        error = next((l.strip() for l in output.splitlines() if l.lstrip().startswith("ERROR")), None)
        return "NOT RUN", f"{reason}: {error[:200]}" if error else reason
    return "FAIL", f"exit {returncode}"


def run_command(argv, timeout):
    real = [sys.executable] + list(argv[1:]) if argv and argv[0] == "python" else list(argv)
    try:
        p = subprocess.run(real, capture_output=True, text=True, encoding="utf-8", errors="replace",
                           cwd=str(REPO), timeout=timeout)
    except subprocess.TimeoutExpired:
        return 124, f"timed out after {timeout}s"
    except FileNotFoundError as exc:
        return 2, f"could not start: {exc}"
    return p.returncode, (p.stdout or "") + (p.stderr or "")


def changed_files(git) -> list:
    """Tracked changes against HEAD plus untracked files; `git(*args)` returns the command's stdout."""
    names = set()
    for out in (git("diff", "--name-only", "HEAD"), git("ls-files", "--others", "--exclude-standard")):
        names |= {l.strip() for l in out.splitlines() if l.strip()}
    return sorted(names)


def _git(*args) -> str:
    p = subprocess.run(["git", "-c", "core.quotepath=off", "-C", str(REPO), *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    if p.returncode != 0:
        why = ((p.stderr or "").strip().splitlines() or [""])[0]
        raise RuntimeError(f"git {' '.join(args)} exited {p.returncode}: {why}")
    return p.stdout


def live_moduledata_dirs() -> list:
    from _gamedir import game_dir
    modules = Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")) / "Modules"
    return [modules / "LOTRLOME_Armory" / "ModuleData", modules / "TAOM_Map" / "ModuleData"]


class AmbiguousName(Exception):
    """A bare name that two live modules both hold."""


def resolve_named(name: str):
    """The absolute path of a file named on the command line, or None. A relative name is tried against the
    working directory, the repo, then the live Modules folder (`TAOM_Map/ModuleData/settlements.xml`) and the
    live Armory and TAOM_Map ModuleData folders. Raises AmbiguousName when two live files match."""
    p = Path(name)
    if p.is_absolute():
        return str(p.resolve()) if p.is_file() else None
    for c in (Path.cwd() / p, REPO / p):
        if c.is_file():
            return str(c.resolve())
    dirs = live_moduledata_dirs()
    live = {str(c.resolve()) for c in [d.parent.parent / p for d in dirs] + [d / p for d in dirs] if c.is_file()}
    if len(live) > 1:
        raise AmbiguousName(f"{name} is in more than one live module: {', '.join(sorted(live))}; "
                            "name it with its module folder, such as TAOM_Map/ModuleData/<file>")
    return next(iter(live), None)


def _shown(argv) -> str:
    return " ".join(f'"{a}"' if " " in a or "|" in a else a for a in argv)


def run(gates, paths, all_mode: bool, with_tests: bool, runner=run_command, jobs: int = 4) -> int:
    selected = select(gates, paths, all_mode)
    if not selected:
        print("no gate reads the changed files; name a live file on the command line, or use --all")
        return 0
    docs = []
    for g in selected:
        for d in g.read_first:
            if d not in docs:
                docs.append(d)
    print("Read first: " + ", ".join(docs))
    print()

    def execute(g):
        if g.manual:
            return ("NOT RUN", f"manual: {g.manual}"), ""
        if g.dotnet and not with_tests:
            return ("NOT RUN", "needs a build; run it with --with-tests"), ""
        rc, text = runner(command_for(g, paths, all_mode), TIMEOUT)
        return verdict(rc, text), text

    # dotnet builds collide when they overlap, so they run one at a time after the pool
    pooled = [g for g in selected if not g.dotnet]
    with ThreadPoolExecutor(max_workers=max(1, jobs)) as pool:
        outcomes = dict(zip((g.id for g in pooled), pool.map(execute, pooled)))
    outcomes.update((g.id, execute(g)) for g in selected if g.dotnet)

    counts = {"PASS": 0, "FAIL": 0, "NOT RUN": 0}
    for g in selected:
        (status, why), text = outcomes[g.id]
        argv = command_for(g, paths, all_mode)
        counts[status] += 1
        shown = _shown(argv) if argv else "(manual)"
        print(f"{status:<8} {g.id:<20} {shown}")
        if why:
            print(f"         {why}")
        if g.note:
            print(f"         note: {g.note}")
        if g.id == "moduledata":
            found = re.search(r"\d+ error\(s\), \d+ warning\(s\)", text)
            if found:
                print(f"         {found.group(0)}")
        if status == "FAIL":
            tail = [l for l in text.splitlines() if l.strip()][-15:]
            for l in tail:
                print(f"         | {l}")
    print()
    print(f"{counts['PASS']} PASS, {counts['FAIL']} FAIL, {counts['NOT RUN']} NOT RUN. "
          "A gate that did not run has not passed.")
    if counts["FAIL"]:
        return 1
    return 2 if counts["NOT RUN"] else 0


def list_table(gates) -> None:
    for g in gates:
        how = "manual" if g.manual else _shown(g.cmd)
        flags = " [dotnet]" if g.dotnet else " [--all only]" if not g.globs else ""
        print(f"{g.id:<20} {g.kind}{flags}")
        print(f"{'':<20} {how}")
        print(f"{'':<20} read first: {', '.join(g.read_first) or '-'}")


def main(argv=None) -> int:
    for s in (sys.stdout, sys.stderr):
        try:
            s.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("files", nargs="*", help="extra files to select gates for (live Armory / TAOM_Map files git cannot see)")
    ap.add_argument("--all", action="store_true", help="run every gate, whatever changed")
    ap.add_argument("--list", action="store_true", help="print the table and exit")
    ap.add_argument("--with-tests", action="store_true", help="also run the dotnet test gates")
    args = ap.parse_args(argv)
    if args.list:
        list_table(GATES)
        return 0
    named = []
    for name in args.files:
        try:
            found = resolve_named(name)
        except AmbiguousName as exc:
            print(f"data_gates: {exc}", file=sys.stderr)
            return 2
        if not found:
            print(f"data_gates: no such file: {name}", file=sys.stderr)
            return 2
        named.append(found)
    try:
        paths = [] if args.all else sorted(set(changed_files(_git)) | set(named))
    except RuntimeError as exc:
        print(f"data_gates: {exc}", file=sys.stderr)
        return 2
    return run(GATES, paths, args.all, args.with_tests)


if __name__ == "__main__":
    sys.exit(main())
