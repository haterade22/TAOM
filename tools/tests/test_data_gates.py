#!/usr/bin/env python3
"""tools/data_gates.py: the kind table, the selection and the PASS / FAIL / NOT RUN verdicts.

Run:  python -m unittest tools.tests.test_data_gates
"""
import io
import json
import os
import re
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = Path(TOOLS).parent
sys.path.insert(0, TOOLS)
import data_gates as dg  # noqa: E402

TROOPS = "Main/_Module/ModuleData/troops/troops_gondor.xml"
LIVE_ARMORY = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/rohan/weapons.xml"


def _ids(selected):
    return {g.id for g in selected}


class TableTests(unittest.TestCase):
    def test_ids_are_unique(self):
        ids = [g.id for g in dg.GATES]
        self.assertEqual(len(ids), len(set(ids)))

    def test_every_python_gate_names_a_script_that_exists(self):
        for g in dg.GATES:
            if g.cmd and g.cmd[0] == "python" and g.cmd[1] != "-m":
                self.assertTrue((REPO / g.cmd[1]).is_file(), f"{g.id}: {g.cmd[1]}")

    def test_every_read_first_doc_exists(self):
        for g in dg.GATES:
            for doc in g.read_first:
                self.assertTrue((REPO / doc).is_file(), f"{g.id}: {doc}")

    def test_a_gate_is_runnable_or_manual(self):
        for g in dg.GATES:
            self.assertTrue(bool(g.cmd) != bool(g.manual), g.id)

    def test_no_dash_in_the_table_text(self):
        text = repr([(g.kind, g.note, g.manual) for g in dg.GATES])
        self.assertNotIn("\u2014", text)
        self.assertNotIn("\u2013", text)

    def test_the_armory_ladder_of_the_artist_brief_is_all_present(self):
        wanted = [
            ("validate_moduledata.py",), ("generate_career_kits.py", "--verify"),
            ("generate_name_localization_strings.py", "--check"), ("generate_armour_classes.py", "--check"),
            ("audit_polearm_shield_parity.py",), ("generate_ranged_ladder_items.py", "--verify"),
            ("restat_ranged_donors.py", "--verify"), ("generate_starter_kit.py", "--verify"),
            ("audit_armory_refs.py",), ("check_rdc_entries.py",), ("register_armory_names.py", "--check"),
            ("check_external_loc_coverage.py",), ("check_generator_item_refs.py",),
            ("validate_xml_schemas.py",), ("check_external_xslt.py",),
        ]
        cmds = [(g.cmd[1].rsplit("/", 1)[-1],) + g.cmd[2:] for g in dg.GATES if g.cmd and g.cmd[0] == "python"]
        for want in wanted:
            self.assertTrue(any(c[:len(want)] == want for c in cmds), want)

    def test_register_names_runs_before_the_loc_coverage_gate(self):
        order = [g.id for g in dg.GATES]
        self.assertLess(order.index("armory-names"), order.index("loc-coverage"))


class SelectionTests(unittest.TestCase):
    def test_a_troop_file_selects_the_moduledata_gates(self):
        ids = _ids(dg.select(dg.GATES, [TROOPS]))
        self.assertIn("moduledata", ids)
        self.assertIn("xml-schemas", ids)
        self.assertNotIn("armory-refs", ids)

    def test_an_english_strings_file_selects_the_orphan_row_gate(self):
        # 561d1b308 registered two rows that the gate then called unread, and nothing ran the gate on that change.
        for path in ("Main/_Module/ModuleData/taom_module_strings.xml",
                     "Main/_Module/ModuleData/named_companions/named_companion_strings.xml",
                     "tools/check_orphan_loc_rows.py", "tools/_loc_sources.py",
                     "tools/loc_composed_families.json", "tools/loc_orphan_allowlist.json"):
            self.assertIn("loc-orphans", _ids(dg.select(dg.GATES, [path])), path)
        self.assertNotIn("loc-orphans", _ids(dg.select(dg.GATES, [TROOPS])))

    def test_every_name_generator_source_and_output_selects_the_name_loc_gate(self):
        # #1374: realms_titles.xml became a generator source, and editing it did not run the --check.
        import generate_name_localization_strings as gen
        repo = Path(gen.REPO_ROOT)
        paths = ["tools/generate_name_localization_strings.py", "tools/_loc_sources.py"]
        for spec in gen.CATEGORIES.values():
            for p in list(spec["sources"]()) + [spec["output"]]:
                paths.append(Path(p).resolve().relative_to(repo.resolve()).as_posix())
        for path in paths:
            self.assertIn("name-loc", _ids(dg.select(dg.GATES, [path])), path)

    def test_a_live_armory_file_selects_the_armory_gates(self):
        ids = _ids(dg.select(dg.GATES, [LIVE_ARMORY]))
        self.assertIn("armory-refs", ids)
        self.assertIn("polearm-shield", ids)
        # #1354: the gate reads TAOM's placeholder sheets, its SubModule.xml rows and its own code.
        for path in ("Main/_Module/ModuleData/placeholder_weapons/ph_weapon_descriptions.xslt",
                     "Main/_Module/SubModule.xml", "tools/audit_polearm_shield_parity.py"):
            self.assertIn("polearm-shield", _ids(dg.select(dg.GATES, [path])), path)

    def test_a_weapon_description_sheet_or_the_couch_spec_selects_the_couch_gate(self):
        sheet = LIVE_ARMORY.rsplit("/LOTRLOME_items", 1)[0] + "/weapon_descriptions.xslt"
        for path in (sheet, "tools/couch_brace_polearms.json", "tools/register_couch_brace_polearms.py"):
            self.assertIn("couch-brace", _ids(dg.select(dg.GATES, [path])), path)
        self.assertNotIn("couch-brace", _ids(dg.select(dg.GATES, [TROOPS])))

    def test_the_couch_gate_runs_check_and_watches_the_sibling_tool_it_imports(self):
        gate = next(g for g in dg.GATES if g.id == "couch-brace")
        self.assertEqual(gate.cmd, ("python", "tools/register_couch_brace_polearms.py", "--check"))
        self.assertIn("couch-brace", _ids(dg.select(dg.GATES, ["tools/register_one_handed_polearms.py"])))

    def test_the_action_files_the_mbproj_audit_reads_select_it(self):
        for name in ("project.mbproj", "action_sets.xml", "action_types.xml"):
            path = f"Main/_Module/ModuleData/{name}"
            self.assertIn("mbproj", _ids(dg.select(dg.GATES, [path])), path)
        self.assertNotIn("mbproj", _ids(dg.select(dg.GATES, [TROOPS])))

    def test_the_action_sets_the_mumak_monster_and_the_bind_tool_select_the_elephant_gates(self):
        armory = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/"
        for path in (armory + "action_sets.xml", armory + "Monsters/LOTR/lotr_monster_mumakil.xml",
                     "tools/bind_elephant_clips.py"):
            self.assertIn("elephant-clips", _ids(dg.select(dg.GATES, [path])), path)
        self.assertIn("elephant-gaits", _ids(dg.select(dg.GATES, [armory + "action_sets.xml"])))
        self.assertIn("elephant-gaits", _ids(dg.select(dg.GATES, ["tools/check_gait_stride.py"])))
        # the files both gates read, and the tools that write them: a master, a clip record, the three writers
        clips = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/Assets/creature/elephant/"
        for path in (clips + "mesh/adod_elephant_geo.tpac", clips + "animations/mumakil_trot_anm.tpac",
                     "tools/elephant_write_masters.py", "tools/gen_elephant_anim_clips.ps1",
                     "tools/elephant_gait_retarget.py", "tools/check_gait_stride.py", "tools/bind_elephant_clips.py"):
            got = _ids(dg.select(dg.GATES, [path]))
            self.assertIn("elephant-clips", got, path)
            self.assertIn("elephant-gaits", got, path)
        self.assertNotIn("elephant-clips", _ids(dg.select(dg.GATES, [TROOPS])))
        gate = next(g for g in dg.GATES if g.id == "elephant-clips")
        self.assertEqual(gate.cmd, ("python", "tools/bind_elephant_clips.py", "--check"))

    def test_the_action_sets_the_spider_clips_and_their_tools_select_the_spider_gates(self):
        armory = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/"
        for path in (armory + "ModuleData/action_sets.xml", armory + "ModuleData/monster_usage_sets.xml",
                     armory + "ModuleData/action_types.xml",
                     armory + "Assets/creature/spider/animations/spider_walk_anm.tpac",
                     armory + "Assets/creature/spider/animations/spider_walk_geo.tpac",
                     "tools/bind_spider_clips.py", "tools/spider_gait.py", "tools/check_gait_stride.py",
                     "tools/gen_elephant_anim_clips.ps1", "tools/elephant_write_masters.py"):
            got = _ids(dg.select(dg.GATES, [path]))
            self.assertIn("spider-clips", got, path)
            self.assertIn("spider-gaits", got, path)
        self.assertNotIn("spider-clips", _ids(dg.select(dg.GATES, [TROOPS])))
        clips = next(g for g in dg.GATES if g.id == "spider-clips")
        gaits = next(g for g in dg.GATES if g.id == "spider-gaits")
        self.assertEqual(clips.cmd, ("python", "tools/bind_spider_clips.py", "--check"))
        self.assertEqual(gaits.cmd, ("python", "tools/check_gait_stride.py", "--creature", "spider"))

    def test_the_wargs_clip_packages_the_plan_and_its_tool_select_the_warg_gate(self):
        armory = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/"
        for path in (armory + "Assets/creature/warg/animations/warg_canter_anm.tpac",
                     armory + "Assets/creature/warg/animations/Warg_Slow_run_geo.tpac",
                     armory + "RuntimeDataCache/20CCE2C2-1073-4B9A-9A57-3FFF1C8DE432.rdc",
                     "tools/warg_gait_plan.json", "tools/repoint_anim_clip.py", "tools/set_clip_balance_name.py"):
            self.assertIn("warg-gaits", _ids(dg.select(dg.GATES, [path])), path)
        for path in (TROOPS, armory + "Assets/creature/spider/animations/spider_walk_anm.tpac",
                     armory + "Assets/creature/warg/mesh/Warg_Rig_V5_geo.tpac",
                     armory + "Assets/creature/warg/animations/warg_canter_anm.tpac.bak-repoint-20261123-154054"):
            self.assertNotIn("warg-gaits", _ids(dg.select(dg.GATES, [path])), path)
        gate = next(g for g in dg.GATES if g.id == "warg-gaits")
        self.assertEqual(gate.cmd, ("python", "tools/repoint_anim_clip.py", "--plan", "tools/warg_gait_plan.json",
                                    "--under", "creature/warg/animations", "--check"))

    def test_the_wargs_rows_packages_rig_and_the_stride_tool_select_the_warg_stride_gate(self):
        """#1565 item 2. The stride gate reads the warg's action set, records, masters, rig and cache entries, and
        it runs every module check_gait_stride.py imports: a change to one of those selects it."""
        armory = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/"
        for path in (armory + "ModuleData/action_sets.xml",
                     armory + "Assets/creature/warg/animations/warg_canter_anm.tpac",
                     armory + "Assets/creature/warg/animations/Warg_Slow_run_geo.tpac",
                     armory + "Assets/creature/warg/mesh/Warg_Rig_V5_geo.tpac",
                     armory + "RuntimeDataCache/20CCE2C2-1073-4B9A-9A57-3FFF1C8DE432.rdc",
                     "tools/check_gait_stride.py", "tools/set_clip_balance_name.py", "tools/_gamedir.py",
                     "tools/tpac_clone_metamesh.py", "tools/tpac_skeleton_dump.py",
                     "tools/elephant_gait_retarget.py", "tools/elephant_write_masters.py"):
            self.assertIn("warg-stride", _ids(dg.select(dg.GATES, [path])), path)
        for path in (TROOPS, armory + "Assets/creature/spider/animations/spider_walk_anm.tpac",
                     armory + "Assets/creature/direwolf/animations/dire_wolf_trot_anm.tpac",
                     armory + "Assets/creature/warg/animations/warg_canter_anm.tpac.bak-repoint-20261123-154054"):
            self.assertNotIn("warg-stride", _ids(dg.select(dg.GATES, [path])), path)
        gate = next(g for g in dg.GATES if g.id == "warg-stride")
        self.assertEqual(gate.cmd, ("python", "tools/check_gait_stride.py", "--creature", "warg"))

    def test_a_cache_entry_selects_every_gate_that_reads_the_cook(self):
        """The elephant's and the spider's rows had no glob for the entries their checks read (the Standards lens,
        2026-11-24). An entry is named by a GUID, so it selects each creature's rows."""
        entry = ("E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/RuntimeDataCache/"
                 "20CCE2C2-1073-4B9A-9A57-3FFF1C8DE432.rdc")
        got = _ids(dg.select(dg.GATES, [entry]))
        for gate in ("elephant-gaits", "elephant-clips", "spider-gaits", "spider-clips", "warg-stride", "warg-gaits"):
            self.assertIn(gate, got)
        self.assertNotIn("elephant-gaits", _ids(dg.select(dg.GATES, [entry + ".bak-balancename-20261101-000000"])))

    def test_a_module_the_stride_gate_imports_selects_every_stride_gate(self):
        """check_gait_stride.py runs these for each creature; the elephant's and the spider's rows named only the
        tool itself (the cause of F6 in docs/reviews/rca-warg-walk-trot-2026-11-24.md, a second time)."""
        for path in ("tools/set_clip_balance_name.py", "tools/tpac_clone_metamesh.py", "tools/tpac_skeleton_dump.py",
                     "tools/_gamedir.py", "tools/elephant_gait_retarget.py"):
            got = _ids(dg.select(dg.GATES, [path]))
            for gate in ("elephant-gaits", "spider-gaits", "warg-stride"):
                self.assertIn(gate, got, path)

    def test_the_dire_wolfs_clip_packages_its_plan_and_the_tool_select_the_wolf_gate(self):
        armory = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/"
        for path in (armory + "Assets/creature/direwolf/animations/dire_wolf_trot_anm.tpac",
                     armory + "Assets/creature/direwolf/animations/adod_wolf_trot_geo.tpac",
                     armory + "RuntimeDataCache/20CCE2C2-1073-4B9A-9A57-3FFF1C8DE432.rdc",
                     "tools/dire_wolf_gait_plan.json", "tools/repoint_anim_clip.py", "tools/set_clip_balance_name.py",
                     "tools/creature_fit.py", "tools/_gamedir.py"):
            self.assertIn("dire-wolf-gaits", _ids(dg.select(dg.GATES, [path])), path)
        # the check imports these two: creature_fit reads each master's GUID, _gamedir finds the install
        for path in ("tools/creature_fit.py", "tools/_gamedir.py"):
            self.assertIn("warg-gaits", _ids(dg.select(dg.GATES, [path])), path)
        for path in (TROOPS, armory + "Assets/creature/warg/animations/warg_trot_anm.tpac", "tools/warg_gait_plan.json",
                     armory + "Assets/creature/direwolf/mesh/dire_wolf_grey_geo.tpac"):
            self.assertNotIn("dire-wolf-gaits", _ids(dg.select(dg.GATES, [path])), path)
        self.assertNotIn("warg-gaits", _ids(dg.select(dg.GATES, ["tools/dire_wolf_gait_plan.json"])))
        gate = next(g for g in dg.GATES if g.id == "dire-wolf-gaits")
        self.assertEqual(gate.cmd, ("python", "tools/repoint_anim_clip.py", "--plan", "tools/dire_wolf_gait_plan.json",
                                    "--under", "creature/direwolf/animations", "--check"))

    def test_the_dire_wolfs_rows_and_packages_select_its_mount_asset_gate(self):
        """#1565: the wolf's new walk twin failed verify_mount_assets.py dire_wolf, and no gate ran that check."""
        armory = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/"
        for path in (armory + "ModuleData/action_sets.xml", armory + "ModuleData/monster_usage_sets.xml",
                     armory + "Assets/creature/direwolf/animations/dire_wolf_walk_stand_anm.tpac",
                     "tools/verify_mount_assets.py"):
            self.assertIn("dire-wolf-body", _ids(dg.select(dg.GATES, [path])), path)
        self.assertNotIn("dire-wolf-body", _ids(dg.select(dg.GATES, [TROOPS])))
        gate = next(g for g in dg.GATES if g.id == "dire-wolf-body")
        self.assertEqual(gate.cmd, ("python", "tools/verify_mount_assets.py", "dire_wolf"))

    def test_an_action_set_file_and_the_bind_tool_select_the_wargs_binding_gate(self):
        """A reinstall puts the walk's "none" row back on the front record, and the plan check does not read XML."""
        armory = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/"
        for path in (armory + "ModuleData/action_sets.xml", "docs/reference/lotrlome-armory-snapshot/action_sets.xml",
                     "tools/bind_warg_clips.py"):
            self.assertIn("warg-clips", _ids(dg.select(dg.GATES, [path])), path)
        for path in (TROOPS, armory + "Assets/creature/warg/animations/warg_walk_stand_anm.tpac",
                     "tools/warg_gait_plan.json"):
            self.assertNotIn("warg-clips", _ids(dg.select(dg.GATES, [path])), path)
        gate = next(g for g in dg.GATES if g.id == "warg-clips")
        self.assertEqual(gate.cmd, ("python", "tools/bind_warg_clips.py", "--check"))

    def test_the_spiders_monster_file_selects_the_gate_that_reads_its_body_capsule(self):
        # #1560: the capsule is in a file of the unversioned Armory, and no gate ran the tool that reads it
        armory = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/"
        for path in (armory + "ModuleData/Monsters/LOTR/lotr_monster_spider.xml", "tools/verify_mount_assets.py",
                     armory + "ModuleData/monster_usage_sets.xml"):
            self.assertIn("spider-body", _ids(dg.select(dg.GATES, [path])), path)
        body = next(g for g in dg.GATES if g.id == "spider-body")
        self.assertEqual(body.cmd, ("python", "tools/verify_mount_assets.py", "spider"))

    def test_the_banner_sprite_folder_selects_banner_sheets(self):
        sprite = "Main/_Module/GUI/SpriteParts/ui_taom_bannericons/10000.png"
        self.assertIn("banner-sheets", [g.id for g in dg.select(dg.GATES, [sprite])])

    def test_the_install_folder_name_does_not_select_banner_sheets(self):
        """The live install is 'Mount & Blade II Bannerlord', which `*banner*` matches in a full path."""
        live = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/ModuleData/settlements.xml"
        self.assertNotIn("banner-sheets", _ids(dg.select(dg.GATES, [live, LIVE_ARMORY])))
        self.assertIn("banner-sheets", _ids(dg.select(dg.GATES, [live.replace("settlements", "banner_icons")])))

    def test_the_live_schema_gate_runs_only_under_all(self):
        self.assertNotIn("xml-schemas-live", _ids(dg.select(dg.GATES, [LIVE_ARMORY, TROOPS])))
        self.assertIn("xml-schemas-live", _ids(dg.select(dg.GATES, [], all_mode=True)))
        out = io.StringIO()
        with redirect_stdout(out):
            dg.list_table(dg.GATES)
        self.assertRegex(out.getvalue(), r"xml-schemas-live\s+whole live modules \(Armory, TAOM_Map\) \[--all only\]")

    def test_an_unrelated_file_selects_nothing(self):
        self.assertEqual(dg.select(dg.GATES, ["README.md", "Main/Features/X/Y.cs"]), [])

    def test_windows_separators_and_case_match(self):
        self.assertIn("moduledata", _ids(dg.select(dg.GATES, [TROOPS.replace("/", "\\").upper()])))

    def test_a_live_map_scene_selects_the_siege_wall_gate(self):
        scene = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/SceneObj/helms/scene.xscene"
        self.assertIn("siege-walls", _ids(dg.select(dg.GATES, [scene])))
        self.assertNotIn("siege-walls", _ids(dg.select(dg.GATES, [scene.replace("scene.xscene", "terrain.bin"), TROOPS])))

    def test_the_siege_wall_gate_judges_the_scene_that_was_named(self):
        """Run bare, the tool reads the live install, so a staged or channel scene would get the install's PASS."""
        gate = next(g for g in dg.GATES if g.id == "siege-walls")
        with tempfile.TemporaryDirectory() as tmp:
            scene = Path(tmp) / "SceneObj" / "helms" / "scene.xscene"
            scene.parent.mkdir(parents=True)
            scene.write_text("<scene/>", encoding="utf-8")
            self.assertEqual(dg.command_for(gate, [str(scene), TROOPS], all_mode=False)[2:], [str(scene)])
            self.assertEqual(dg.command_for(gate, [str(scene)], all_mode=True), list(gate.cmd))

    def test_a_file_gate_receives_only_the_files_that_match_it(self):
        g = next(g for g in dg.GATES if g.id == "xml-schemas")
        argv = dg.command_for(g, [TROOPS, "README.md"], all_mode=False)
        self.assertEqual(argv[-1], TROOPS)
        self.assertNotIn("README.md", argv)

    def test_all_mode_runs_the_file_gate_without_files(self):
        g = next(g for g in dg.GATES if g.id == "xml-schemas")
        self.assertEqual(dg.command_for(g, [], all_mode=True), ["python", "tools/validate_xml_schemas.py"])

    def test_all_selects_every_gate(self):
        self.assertEqual(len(dg.select(dg.GATES, [], all_mode=True)), len(dg.GATES))

    def test_every_tracked_non_language_moduledata_file_selects_moduledata(self):
        out = subprocess.run(["git", "-C", str(REPO), "ls-files", "Main/_Module/ModuleData"],
                             capture_output=True, text=True, encoding="utf-8").stdout
        files = [f for f in out.splitlines()
                 if f.lower().endswith((".xml", ".xslt")) and "/languages/" not in f.lower()]
        self.assertGreater(len(files), 100)
        missed = [f for f in files if "moduledata" not in _ids(dg.select(dg.GATES, [f]))]
        self.assertEqual(missed, [])

    def test_every_applies_to_glob_of_every_schema_selects_moduledata(self):
        missed = []
        for schema in sorted((REPO / "tools" / "schemas").glob("*.json")):
            for glob in json.loads(schema.read_text(encoding="utf-8"))["applies_to"]:
                path = "Main/_Module/ModuleData/" + glob.replace("*", "x")
                if "moduledata" not in _ids(dg.select(dg.GATES, [path])):
                    missed.append(path)
        self.assertEqual(missed, [])

    def test_the_troop_id_baseline_selects_moduledata(self):
        self.assertIn("moduledata", _ids(dg.select(dg.GATES, ["tools/shipped_troop_ids.txt"])))

    def test_the_tools_the_enlistment_rank_rule_reads_select_moduledata(self):
        for path in ("tools/generate_enlistment_rosters.py", "tools/melee_catalogue.py",
                     "tools/analyze_melee_ladder.py", "tools/ranged_ladder.py"):
            self.assertIn("moduledata", _ids(dg.select(dg.GATES, [path])), path)

    def test_the_lord_skill_sets_and_their_generator_select_generator_regions(self):
        for path in ("Main/_Module/ModuleData/taom_lord_skill_sets.xml", "tools/apply_culture_skills_traits.py"):
            self.assertIn("generator-regions", _ids(dg.select(dg.GATES, [path])), path)

    def test_the_placeholder_inputs_select_the_placeholder_verify_gate(self):
        """The placeholders are priced from the donors' blades, the carriers' tiers and the class
        limits, so a change to any of them can leave a generated file stale (2026-11-14, #1271)."""
        gate = next(g for g in dg.GATES if g.id == "placeholder-weapons")
        self.assertEqual(gate.cmd, ("python", "tools/generate_placeholder_weapons.py", "--verify"))
        for path in ("Main/_Module/ModuleData/placeholder_weapons/ph_items.xml",
                     "Main/_Module/ModuleData/placeholder_weapons/ph_weapon_descriptions.xslt",
                     "tools/placeholder_weapons.json", "tools/melee_ladders.json",
                     "tools/generate_placeholder_weapons.py", "tools/melee_ladder.py", "tools/melee_damage.py",
                     "tools/melee_catalogue.py", "tools/restat_melee_blades.py", "tools/fix_melee_ladder.py",
                     "Main/_Module/ModuleData/troops/troops_gondor.xml", LIVE_ARMORY):
            self.assertIn("placeholder-weapons", _ids(dg.select(dg.GATES, [path])), path)

    def test_the_lord_variant_inputs_select_its_check_the_class_table_and_the_mesh_scan(self):
        """The variants are priced from the Armory sources through the curve and judged against the
        rosters, and the class table and the mesh scan read the variant file (#1333)."""
        gate = next(g for g in dg.GATES if g.id == "lord-armour-variants")
        self.assertEqual(gate.cmd, ("python", "tools/generate_lord_armour_variants.py", "--check"))
        for path in ("Main/_Module/ModuleData/lord_armour/lord_armour_variants.xml", "tools/lord_armour_variants.json",
                     "tools/generate_lord_armour_variants.py", "tools/derive_armor_tiers.py", "tools/fix_lord_kits.py",
                     "tools/rebalance_armor.py", "tools/rebalance_troops.py", "tools/taom_schema.py",
                     "Main/_Module/SubModule.xml", "Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_harad.xml",
                     "Main/_Module/ModuleData/taom_wanderers.xml", LIVE_ARMORY,
                     # The repo trace of an Armory restat (a source's weight, material or share can change)
                     # and the item loader the generator imports for the reference pieces.
                     "tools/data/armor_roster_tiers.json", "tools/fix_upgrade_armour_regressions.py"):
            self.assertIn("lord-armour-variants", _ids(dg.select(dg.GATES, [path])), path)
        variant_file = _ids(dg.select(dg.GATES, ["Main/_Module/ModuleData/lord_armour/lord_armour_variants.xml"]))
        self.assertIn("armour-classes", variant_file)
        self.assertIn("lord-variant-meshes", variant_file)

    def test_the_band_clone_inputs_select_its_check_the_class_table_and_the_mesh_scan(self):
        """The clones and upgrades are planned from the rosters and the Armory through the curve, the whole-file
        troop generators carry the swap, and the class table and the mesh scan read the clone file (2026-11-17)."""
        gate = next(g for g in dg.GATES if g.id == "armour-band-clones")
        self.assertEqual(gate.cmd, ("python", "tools/generate_armour_band_clones.py", "--check"))
        for path in ("Main/_Module/ModuleData/armour_band_clones/armour_band_clones.xml",
                     "tools/armour_band_clones.json", "tools/generate_armour_band_clones.py",
                     "tools/derive_armor_tiers.py", "tools/rebalance_armor.py", "tools/taom_schema.py",
                     "tools/realm_troops.py", "tools/generate_lothlorien_troops.py", "tools/generate_snow_orc_troops.py",
                     "Main/_Module/ModuleData/troops/troops_mirkwood.xml",
                     "Main/_Module/ModuleData/fords_of_war/fords_troops.xml", "Main/_Module/SubModule.xml",
                     "tools/data/armor_roster_tiers.json", LIVE_ARMORY,
                     # the colour table the upgrade pick reads, its scanner and its overrides
                     "tools/scan_armour_colours.py", "tools/armour_colour_overrides.json",
                     "tools/data/armour_colour_families.json"):
            self.assertIn("armour-band-clones", _ids(dg.select(dg.GATES, [path])), path)
        clone_file = _ids(dg.select(dg.GATES, ["Main/_Module/ModuleData/armour_band_clones/armour_band_clones.xml"]))
        self.assertIn("armour-classes", clone_file)
        self.assertIn("armour-band-clone-meshes", clone_file)

    def test_the_band_clone_note_says_to_rescan_the_colours_after_apply(self):
        gate = next(g for g in dg.GATES if g.id == "armour-band-clones")
        self.assertIn("scan_armour_colours.py --apply", gate.note)
        self.assertLess(gate.note.index("scan_armour_colours.py --apply"), gate.note.index("derive_armor_tiers.py"))

    def test_the_colour_check_reads_the_art_the_clone_file_the_lord_variants_and_the_submodule(self):
        """The table is measured from the Armory's art and from the items TAOM adds (the clones, the lord variants,
        read through SubModule.xml), so each of them stales it (review 2026-11-18)."""
        gate = next(g for g in dg.GATES if g.id == "armour-colours")
        self.assertEqual(gate.cmd, ("python", "tools/scan_armour_colours.py", "--check"))
        live = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/"
        for path in ("tools/scan_armour_colours.py", "tools/armour_colour_overrides.json",
                     "tools/data/armour_colour_families.json", "tools/audit_battle_equipment_memory.py",
                     "tools/audit_map_scene_memory.py", LIVE_ARMORY, live + "AssetSources/chest/a.png",
                     live + "Assets/textures/b.tpac", live + "AssetPackages/pack0.tpac",
                     "Main/_Module/ModuleData/armour_band_clones/armour_band_clones.xml",
                     "Main/_Module/ModuleData/lord_armour/lord_armour_variants.xml", "Main/_Module/SubModule.xml"):
            self.assertIn("armour-colours", _ids(dg.select(dg.GATES, [path])), path)
        self.assertIn("exit 2", gate.note)
        self.assertNotIn("exit 0) without the install", gate.note)

    def test_the_colour_scanner_exits_are_read_as_not_run_and_a_crash_or_stale_table_as_fail(self):
        self.assertEqual(dg.verdict(2, "ERROR: no texture image under X; nothing can be measured")[0], "NOT RUN")
        self.assertEqual(dg.verdict(2, "SKIPPED: no LOTRLOME_Armory under X; nothing was checked or written")[0], "NOT RUN")
        self.assertEqual(dg.verdict(1, "rows that differ from armour_colour_families.json: 3")[0], "FAIL")
        self.assertEqual(dg.verdict(3, "scan_armour_colours.py crashed; no verdict, exit 3")[0], "FAIL")

    def test_the_validator_gate_reads_the_colour_table_and_its_scanner(self):
        for path in ("tools/data/armour_colour_families.json", "tools/scan_armour_colours.py"):
            self.assertIn("moduledata", _ids(dg.select(dg.GATES, [path])), path)

    def test_the_career_kit_check_reads_the_clone_tool_and_the_armour_curve(self):
        """generate_career_kits.career_armour reads the clone spec through generate_armour_band_clones.armour_for_band,
        whose clone values come from rebalance_armor (review 2026-11-18)."""
        for path in ("tools/generate_armour_band_clones.py", "tools/rebalance_armor.py"):
            self.assertIn("career-kits", _ids(dg.select(dg.GATES, [path])), path)

    def test_the_band_clone_check_runs_on_the_mallorn_pricing(self):
        # generate_lothlorien_armor.ML_BAND_UP prices a Mallorn clone a band up (band_values), review 2026-11-17.
        self.assertIn("armour-band-clones", _ids(dg.select(dg.GATES, ["tools/generate_lothlorien_armor.py"])))

    def test_the_band_clone_check_runs_on_the_list_of_generated_troop_files(self):
        # generate_new_faction_kingdoms.REGENERATABLE names the realm files split_generated leaves to their generators.
        self.assertIn("armour-band-clones", _ids(dg.select(dg.GATES, ["tools/generate_new_faction_kingdoms.py"])))

    def test_the_band_clone_check_runs_on_every_set_it_swaps_and_its_inputs(self):
        # Every set below the elite band takes the clone (review 2026-11-17, F1): NPCs, kits, companions.
        for path in ("Main/_Module/ModuleData/characters/npcs_mirkwood.xml",
                     "Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml",
                     "Main/_Module/ModuleData/named_companions/named_companions.xml",
                     "tools/generate_enlistment_rosters.py", "tools/loc_inline_backlog.json"):
            self.assertIn("armour-band-clones", _ids(dg.select(dg.GATES, [path])), path)

    def test_an_unrelated_tool_does_not_select_the_placeholder_verify_gate(self):
        self.assertNotIn("placeholder-weapons", _ids(dg.select(dg.GATES, ["tools/data_gates.py"])))

    def test_the_armour_curve_selects_every_generated_armour_check(self):
        """generate_lothlorien_armor.py and generate_snow_orc_armor.py write item stats through
        rebalance_armor.calculate_stats, so a change to the curve stales both live item sets."""
        selected = dg.select(dg.GATES, ["tools/rebalance_armor.py"])
        commands = [g.cmd for g in selected]
        self.assertIn(("python", "tools/generate_lothlorien_armor.py", "--check"), commands)
        self.assertIn(("python", "tools/generate_snow_orc_armor.py", "--check"), commands)

    def test_the_armour_restat_check_reads_the_curve_the_roster_map_and_any_live_armour_file(self):
        """A reinstall or a restat from a stale checkout reverts every value and loot table to the old curve,
        and no other gate compares the live Armory with it."""
        gate = next(g for g in dg.GATES if g.id == "armour-restat")
        self.assertEqual(gate.cmd, ("python", "tools/rebalance_armor.py", "--check"))
        live_arm = LIVE_ARMORY.replace("weapons.xml", "arm_armors.xml")
        for path in ("tools/rebalance_armor.py", "tools/data/armor_roster_tiers.json",
                     "tools/generate_lothlorien_armor.py", "tools/generate_snow_orc_armor.py", live_arm):
            self.assertIn("armour-restat", _ids(dg.select(dg.GATES, [path])), path)
        self.assertNotIn("armour-restat", _ids(dg.select(dg.GATES, [TROOPS])))
        self.assertIn("--apply", gate.note)
        self.assertIn("backup tag", gate.note)
        self.assertIn("merge trunk", gate.note)
        self.assertEqual(dg.verdict(2, "ERROR: SKIPPED, LOTRLOME_Armory is not installed")[0], "NOT RUN")
        self.assertEqual(dg.verdict(2, "ERROR: SKIPPED, no armour file was read; nothing was checked")[0], "NOT RUN")
        self.assertEqual(dg.verdict(1, "12 item(s) differ from the curve")[0], "FAIL")

    def test_the_armour_cover_flags_gate_reads_the_spec_the_tool_and_any_live_armour_file(self):
        """A reinstall resets the cover attributes of every armour slot, so the spec, its tool, a generator and
        a named live Armory file all select the check, and exit 2 (no install) is NOT RUN."""
        gate = next(g for g in dg.GATES if g.id == "armour-cover-flags")
        self.assertEqual(gate.cmd, ("python", "tools/apply_armour_cover_flags.py", "--check"))
        live_head = LIVE_ARMORY.replace("weapons.xml", "head_armors.xml")
        live_legs = LIVE_ARMORY.replace("weapons.xml", "leg_armors.xml")
        for path in ("tools/armour_cover_flags.json", "tools/apply_armour_cover_flags.py",
                     "tools/generate_dale_armor.py", live_head, live_legs):
            self.assertIn("armour-cover-flags", _ids(dg.select(dg.GATES, [path])), path)
        self.assertNotIn("armour-cover-flags", _ids(dg.select(dg.GATES, [TROOPS])))
        self.assertEqual(dg.verdict(2, "ERROR: SKIPPED, LOTRLOME_Armory is not installed")[0], "NOT RUN")
        self.assertEqual(dg.verdict(1, "DRIFT ...")[0], "FAIL")

    def test_a_prefab_selects_the_sprite_test(self):
        selected = dg.select(dg.GATES, ["Main/_Module/GUI/PreFabs/X.xml"])
        self.assertTrue(any("PrefabSpriteTests" in " ".join(g.cmd) for g in selected))

    def test_the_notable_template_test_is_routed_from_the_files_it_reads(self):
        for path in ("Main/_Module/ModuleData/characters/npcs_gondor.xml",
                     "Main/_Module/ModuleData/taom_spcultures.xml", "Main/_Module/ModuleData/spcultures.xslt",
                     "Main/_Module/ModuleData/taom_wanderers.xml"):
            selected = dg.select(dg.GATES, [path])
            self.assertTrue(any("NotableTemplateGenderTests" in " ".join(g.cmd) for g in selected), path)

    def test_a_deleted_path_is_dropped_from_the_file_list_but_still_selects(self):
        g = next(g for g in dg.GATES if g.id == "xml-schemas")
        gone = "Main/_Module/ModuleData/troops/troops_deleted_long_ago.xml"
        self.assertIn("xml-schemas", _ids(dg.select(dg.GATES, [gone, TROOPS])))
        argv = dg.command_for(g, [gone, TROOPS], all_mode=False)
        self.assertEqual(argv[-1], TROOPS)
        self.assertNotIn(gone, argv)

    def test_a_gate_whose_files_were_all_deleted_runs_bare(self):
        g = next(g for g in dg.GATES if g.id == "xml-schemas")
        gone = "Main/_Module/ModuleData/troops/troops_deleted_long_ago.xml"
        self.assertEqual(dg.command_for(g, [gone], all_mode=False), list(g.cmd))


class ManualStepTests(unittest.TestCase):
    def test_the_gui_manual_step_names_what_the_sprite_gate_leaves_by_hand(self):
        text = next(g.manual for g in dg.GATES if g.id == "gui-manual")
        self.assertNotIn("until the sprite gate lands", text)
        self.assertIn("GetSprite", text)

    def test_the_docstring_says_a_clone_with_hookspath_runs_the_commit_gates(self):
        self.assertIn("core.hooksPath", dg.__doc__)
        self.assertNotIn("runs none of them", " ".join(dg.__doc__.split()))


class VerdictTests(unittest.TestCase):
    def test_exit_zero_is_pass(self):
        self.assertEqual(dg.verdict(0, "ok"), ("PASS", ""))

    def test_exit_one_is_fail(self):
        self.assertEqual(dg.verdict(1, "bad")[0], "FAIL")

    def test_a_crash_is_fail(self):
        self.assertEqual(dg.verdict(3, "Traceback")[0], "FAIL")

    def test_exit_two_is_not_run(self):
        self.assertEqual(dg.verdict(2, "no install")[0], "NOT RUN")

    def test_exit_two_names_the_tools_error_line(self):
        # The output of a NOT RUN gate is not printed, so the reason must carry the file the tool names.
        status, why = dg.verdict(2, "PASS: x\nERROR: DE/loc_settlements.xml does not parse (line 3)\n")
        self.assertEqual("NOT RUN", status)
        self.assertIn("DE/loc_settlements.xml does not parse", why)

    def test_skipped_output_is_not_run_even_at_exit_zero(self):
        status, why = dg.verdict(0, "PASS\ncheck_race_morph_channels: SKIPPED, no FBX")
        self.assertEqual(status, "NOT RUN")
        self.assertIn("SKIPPED", why)

    def test_every_form_of_a_skip_is_not_run_at_exit_zero(self):
        forms = {
            "a SKIP line": "SKIP: no Bannerlord Modules folder at E:\\x\\Modules\n      Set $BANNERLORD_GAME_DIR to run this check.",
            "unittest skips": "Ran 1 test in 1.7s\n\nOK (skipped=8)",
            "no lxml": "checked 3 file(s)\nNOTE: lxml not installed, so only well-formedness was checked.",
            "no files validated": "PASS: 0 file(s) validated, 0 failed, 0 registration(s) resolve to nothing, "
                                  "0 language file(s), 1 stylesheet(s), 0 native, 0 not registered, 0 real fault(s) named above",
        }
        for name, text in forms.items():
            status, why = dg.verdict(0, text)
            self.assertEqual(status, "NOT RUN", name)
            self.assertTrue(why, name)

    def test_the_skip_forms_are_matched_case_sensitively(self):
        self.assertEqual(dg.verdict(0, "Skipped official modules: 4"), ("PASS", ""))
        self.assertEqual(dg.verdict(0, "skip list: none\nOK"), ("PASS", ""))
        self.assertEqual(dg.verdict(0, "PASS: 12 file(s) validated, 0 failed"), ("PASS", ""))

    def test_a_zero_file_validation_shows_the_bucket_line(self):
        line = "PASS: 0 file(s) validated, 0 failed, 1 stylesheet(s)"
        status, why = dg.verdict(0, "STYLESHEET lords.xslt: queued\n\n" + line)
        self.assertEqual(status, "NOT RUN")
        self.assertIn(line, why)


class RunTests(unittest.TestCase):
    def _gates(self):
        mk = dg.Gate
        return [
            mk("a", "kind", ("*.xml",), ("python", "tools/a.py"), ()),
            mk("b", "kind", ("*.xml",), ("python", "tools/b.py"), ()),
            mk("d", "kind", ("*.xml",), ("dotnet", "test", "TAOM.Tests"), (), dotnet=True),
            mk("m", "kind", ("*.xml",), (), (), manual="check by hand"),
        ]

    def _run(self, results, with_tests=False):
        def fake(argv, timeout):
            return results[argv[1]]
        out = io.StringIO()
        with redirect_stdout(out):
            code = dg.run(self._gates(), ["x.xml"], all_mode=False, with_tests=with_tests, runner=fake, jobs=1)
        return code, out.getvalue()

    def test_all_pass_but_dotnet_and_manual_is_not_run_exit_two(self):
        code, text = self._run({"tools/a.py": (0, "ok"), "tools/b.py": (0, "ok")})
        self.assertEqual(code, 2)
        self.assertIn("NOT RUN", text)
        self.assertIn("dotnet test TAOM.Tests", text)
        self.assertIn("check by hand", text)

    def test_a_failure_wins_exit_one_and_shows_the_output_tail(self):
        code, text = self._run({"tools/a.py": (1, "line1\nthe finding"), "tools/b.py": (0, "ok")})
        self.assertEqual(code, 1)
        self.assertIn("FAIL", text)
        self.assertIn("the finding", text)

    def test_nothing_selected_exits_zero_and_says_so(self):
        out = io.StringIO()
        with redirect_stdout(out):
            code = dg.run(self._gates(), ["README.md"], all_mode=False, with_tests=False,
                          runner=lambda a, t: (0, ""), jobs=1)
        self.assertEqual(code, 0)
        self.assertIn("no gate", out.getvalue())

    def test_the_summary_counts_each_status(self):
        _, text = self._run({"tools/a.py": (0, "ok"), "tools/b.py": (1, "x")})
        self.assertRegex(text, r"1 PASS, 1 FAIL, 2 NOT RUN")

    def test_a_passing_gate_still_prints_its_note_and_moduledata_its_warning_count(self):
        gates = [dg.Gate("moduledata", "kind", ("*.xml",), ("python", "tools/v.py"), (),
                         note="read the warnings as well as the errors")]
        out = io.StringIO()
        with redirect_stdout(out):
            dg.run(gates, ["x.xml"], all_mode=False, with_tests=False,
                   runner=lambda a, t: (0, "lots of lines\n  0 error(s), 1581 warning(s)\n"), jobs=1)
        text = out.getvalue()
        self.assertIn("PASS", text)
        self.assertIn("read the warnings as well as the errors", text)
        self.assertIn("0 error(s), 1581 warning(s)", text)

    def test_dotnet_gates_never_overlap(self):
        gates = [dg.Gate(f"d{i}", "kind", ("*.xml",), ("dotnet", "test", f"T{i}"), (), dotnet=True) for i in range(3)]
        gates.append(dg.Gate("p", "kind", ("*.xml",), ("python", "tools/p.py"), ()))
        lock = threading.Lock()
        state = {"live": 0, "peak": 0}

        def fake(argv, timeout):
            if argv[0] != "dotnet":
                return 0, "ok"
            with lock:
                state["live"] += 1
                state["peak"] = max(state["peak"], state["live"])
            time.sleep(0.05)
            with lock:
                state["live"] -= 1
            return 0, "ok"

        with redirect_stdout(io.StringIO()):
            dg.run(gates, ["x.xml"], all_mode=False, with_tests=True, runner=fake, jobs=4)
        self.assertEqual(state["peak"], 1)


class MainTests(unittest.TestCase):
    def _main(self, argv, git_rc=0, git_out=""):
        done = subprocess.CompletedProcess([], git_rc, stdout=git_out, stderr="fatal: not a git repository")
        out, err = io.StringIO(), io.StringIO()
        with mock.patch.object(dg.subprocess, "run", return_value=done), \
                mock.patch.object(dg, "run", return_value=0) as run, \
                redirect_stdout(out), mock.patch.object(sys, "stderr", err):
            code = dg.main(argv)
        return code, run, out.getvalue() + err.getvalue()

    def test_a_git_failure_exits_two_without_running_a_gate(self):
        code, run, text = self._main([], git_rc=128)
        self.assertEqual(code, 2)
        run.assert_not_called()
        self.assertIn("128", text)

    def test_a_named_file_that_does_not_exist_exits_two(self):
        code, run, text = self._main(["nofile.xml"])
        self.assertEqual(code, 2)
        run.assert_not_called()
        self.assertIn("nofile.xml", text)

    def test_an_existing_named_file_is_passed_on_as_an_absolute_path(self):
        code, run, _ = self._main([TROOPS])
        self.assertEqual(code, 0)
        (named,) = run.call_args[0][1]
        self.assertTrue(os.path.isabs(named), named)
        self.assertEqual(Path(named).name, "troops_gondor.xml")

    def test_a_live_file_named_relative_to_moduledata_selects_the_armory_gates(self):
        with tempfile.TemporaryDirectory() as tmp:
            live = Path(tmp) / "LOTRLOME_items" / "rohan" / "weapons.xml"
            live.parent.mkdir(parents=True)
            live.write_text("<x/>", encoding="utf-8")
            with mock.patch.object(dg, "live_moduledata_dirs", return_value=[Path(tmp)]):
                code, run, _ = self._main(["LOTRLOME_items/rohan/weapons.xml"])
        self.assertEqual(code, 0)
        paths = run.call_args[0][1]
        self.assertIn("armory-refs", _ids(dg.select(dg.GATES, paths)))


class ResolveNamedTests(unittest.TestCase):
    """A live file is named by the spelling the rules use: bare, or under its module."""

    def _modules(self, tmp, both=()):
        mods = Path(tmp)
        armory, tmap = mods / "LOTRLOME_Armory" / "ModuleData", mods / "TAOM_Map" / "ModuleData"
        for d, rel in ((tmap, "settlements.xml"), (armory, "LOTRLOME_items/w.xml")) + tuple((d, b) for b in both for d in (armory, tmap)):
            (d / rel).parent.mkdir(parents=True, exist_ok=True)
            (d / rel).write_text("<x/>", encoding="utf-8")
        return [armory, tmap]

    def test_the_trap_index_spelling_of_a_live_file_resolves(self):
        with tempfile.TemporaryDirectory() as tmp:
            dirs = self._modules(tmp)
            with mock.patch.object(dg, "live_moduledata_dirs", return_value=dirs):
                got = dg.resolve_named("TAOM_Map/ModuleData/settlements.xml")
        self.assertEqual(Path(got).parts[-3:], ("TAOM_Map", "ModuleData", "settlements.xml"))

    def test_a_bare_name_held_by_one_module_still_resolves(self):
        with tempfile.TemporaryDirectory() as tmp:
            dirs = self._modules(tmp)
            with mock.patch.object(dg, "live_moduledata_dirs", return_value=dirs):
                got = dg.resolve_named("settlements.xml")
        self.assertEqual(Path(got).parts[-3:], ("TAOM_Map", "ModuleData", "settlements.xml"))

    def test_a_bare_name_held_by_both_modules_is_ambiguous_and_names_both(self):
        with tempfile.TemporaryDirectory() as tmp:
            dirs = self._modules(tmp, both=("project.mbproj",))
            with mock.patch.object(dg, "live_moduledata_dirs", return_value=dirs):
                with self.assertRaises(dg.AmbiguousName) as ctx:
                    dg.resolve_named("project.mbproj")
        self.assertIn("LOTRLOME_Armory", str(ctx.exception))
        self.assertIn("TAOM_Map", str(ctx.exception))

    def test_main_exits_two_for_an_ambiguous_name(self):
        with tempfile.TemporaryDirectory() as tmp:
            dirs = self._modules(tmp, both=("project.mbproj",))
            err = io.StringIO()
            with mock.patch.object(dg, "live_moduledata_dirs", return_value=dirs), \
                    mock.patch.object(dg, "run", return_value=0) as run, mock.patch.object(sys, "stderr", err):
                code = dg.main(["project.mbproj"])
        self.assertEqual(code, 2)
        run.assert_not_called()
        self.assertIn("LOTRLOME_Armory", err.getvalue())
        self.assertIn("TAOM_Map", err.getvalue())


class ChangedFilesTests(unittest.TestCase):
    def test_git_output_does_not_quote_a_non_ascii_path(self):
        done = subprocess.CompletedProcess([], 0, stdout="", stderr="")
        with mock.patch.object(subprocess, "run", return_value=done) as run:
            dg._git("diff", "--name-only", "HEAD")
        self.assertIn("core.quotepath=off", run.call_args[0][0])

    def test_changed_files_unions_tracked_changes_and_untracked_files(self):
        def fake_git(*args):
            return {"diff": "a.xml\nb.xml\n", "ls-files": "c.xml\n"}[args[0]]
        self.assertEqual(sorted(dg.changed_files(fake_git)), ["a.xml", "b.xml", "c.xml"])


class DocsAgreeTests(unittest.TestCase):
    def test_the_rule_section_names_the_script(self):
        text = (REPO / ".claude" / "rules" / "moduledata-validation.md").read_text(encoding="utf-8")
        self.assertIn("## Gate per file kind", text)
        section = text.split("## Gate per file kind", 1)[1].split("\n## ", 1)[0]
        self.assertIn("tools/data_gates.py", section)
        self.assertNotIn("| Kind | Read first | Run |", section)

    def test_the_schema_tool_prints_the_script_it_points_to(self):
        src = (REPO / "tools" / "validate_xml_schemas.py").read_text(encoding="utf-8")
        self.assertIn("tools/data_gates.py", src)
        self.assertNotIn('moduledata-validation.md \\"Gate per file kind\\"', src)


if __name__ == "__main__":
    unittest.main()
