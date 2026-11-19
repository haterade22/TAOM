#!/usr/bin/env python3
"""Unit tests for tools/check_prefab_meshes.py.

Run:  python -m unittest tools.tests.test_check_prefab_meshes
"""
import os
import sys
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)

import check_prefab_meshes as cpm  # noqa: E402

PREFAB = """<prefabs>
	<game_entity name="gondor_wall_deco_2" old_prefab_name="">
		<children>
			<game_entity name="gondor_wall_deco_21" old_prefab_name="">
				<components>
					<meta_mesh_component name="wm_gondor_shield_d"/>
				</components>
			</game_entity>
		</children>
	</game_entity>
	<game_entity name="other" old_prefab_name="">
		<components>
			<meta_mesh_component name="wm_gondor_sword_a02_blade.lod0" position="0, 0, 0"/>
			<meta_mesh_component name="wm_gondor_sword_a02_blade"/>
		</components>
	</game_entity>
</prefabs>
"""


class ExtractTests(unittest.TestCase):
    def test_names_lines_and_owning_entity(self):
        refs = cpm.extract_prefab_meshes(PREFAB, "gondor_environment.xml")
        self.assertEqual(refs[0].name, "wm_gondor_shield_d")
        self.assertEqual(refs[0].line, 6)
        self.assertEqual(refs[0].prefab, "gondor_wall_deco_2")
        self.assertEqual(refs[1].prefab, "other")

    def test_lod_suffix_is_stripped(self):
        refs = cpm.extract_prefab_meshes(PREFAB, "f.xml")
        self.assertEqual(refs[1].name, "wm_gondor_sword_a02_blade")

    def test_file_without_meshes(self):
        self.assertEqual(cpm.extract_prefab_meshes("<prefabs/>", "f.xml"), [])


class MissingTests(unittest.TestCase):
    def test_reports_only_absent_names_once_per_prefab(self):
        refs = cpm.extract_prefab_meshes(PREFAB, "f.xml")
        present = {"wm_gondor_sword_a02_blade"}
        missing = cpm.find_missing(refs, present)
        self.assertEqual([(m.name, m.prefab) for m in missing],
                         [("wm_gondor_shield_d", "gondor_wall_deco_2")])

    def test_present_lod_names_match_their_base(self):
        refs = cpm.extract_prefab_meshes(PREFAB, "f.xml")
        present = {"wm_gondor_shield_d.lod0", "wm_gondor_sword_a02_blade.lod0"}
        self.assertEqual(cpm.find_missing(refs, present), [])


class TopLevelTests(unittest.TestCase):
    def test_top_level_missing_mesh_is_reported_like_a_nested_one(self):
        text = PREFAB.replace("wm_gondor_sword_a02_blade.lod0", "icon_missing")
        refs = cpm.extract_prefab_meshes(text, "f.xml")
        missing = cpm.find_missing(refs, {"wm_gondor_sword_a02_blade"})
        self.assertEqual(sorted(m.name for m in missing), ["icon_missing", "wm_gondor_shield_d"])


if __name__ == "__main__":
    unittest.main()
