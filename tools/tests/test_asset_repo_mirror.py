"""The Armory tools name the lotraom-assets mirror through _gamedir.ASSET_REPO.

Five tools once carried their own copy of the deleted v1.4 path, so they
skipped the mirror with a "not present" line and touched only the live
Armory (issue #630). They now name the v1.5 mirror, and touch it only when
it is named on the command line: sessions never touch the mirror unless
asked (Mike, 2026-09-22; _gamedir.armory_trees).
"""
import os
import sys
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import _gamedir  # noqa: E402
import generate_black_numenorean_armor  # noqa: E402
import generate_black_numenorean_weapons  # noqa: E402

TOOLS = ("apply_dead_mesh_item_swaps", "apply_rohan_spear_reforge",
         "audit_gender_variation_flags", "generate_black_numenorean_armor",
         "generate_black_numenorean_weapons")


def _source(name):
    return (Path(_gamedir.__file__).parent / (name + ".py")).read_text(encoding="utf-8")


class AssetRepoMirrorTests(unittest.TestCase):
    def test_black_numenorean_generators_name_the_shared_mirror(self):
        mirror = os.path.normcase(str(_gamedir.ASSET_REPO))
        self.assertTrue(os.path.normcase(generate_black_numenorean_armor.REPO_ARMORY).startswith(mirror))
        self.assertTrue(os.path.normcase(generate_black_numenorean_weapons.REPO_MD).startswith(mirror))

    def test_no_tool_names_the_deleted_v14_mirror(self):
        for name in TOOLS:
            text = _source(name)
            with self.subTest(tool=name):
                self.assertNotIn(r"lotraom-assets\v1.4", text)
                self.assertNotIn(r"lotraom-assets\\v1.4", text)
                self.assertNotIn('"v1.4"', text)

    def test_the_mirror_is_never_a_default_target(self):
        for name in ("apply_dead_mesh_item_swaps", "apply_rohan_spear_reforge",
                     "audit_gender_variation_flags"):
            with self.subTest(tool=name):
                self.assertIn('"--asset-repo", default=None', _source(name))
        self.assertIn("args.armory_path or [LIVE_ARMORY]\n",
                      _source("generate_black_numenorean_armor").replace("\r\n", "\n"))
        self.assertIn("args.module_data or [LIVE_MD]\n",
                      _source("generate_black_numenorean_weapons").replace("\r\n", "\n"))

    def test_armory_trees_adds_the_mirror_only_when_named(self):
        live = Path("live_md")
        self.assertEqual(_gamedir.armory_trees(live, None), [("armory", live)])
        with mock.patch.object(Path, "is_dir", return_value=True):
            trees = _gamedir.armory_trees(live, _gamedir.ASSET_REPO)
        self.assertEqual(trees, [("armory", live), ("mirror", _gamedir.ASSET_REPO / "ModuleData")])


if __name__ == "__main__":
    unittest.main()
