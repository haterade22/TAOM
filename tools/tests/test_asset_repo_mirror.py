"""The Armory tools name the lotraom-assets mirror through _gamedir.ASSET_REPO.

Five tools once carried their own copy of the deleted v1.4 path, so they
skipped the mirror with a "not present" line and touched only the live
Armory (issue #630).
"""
import os
import sys
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import _gamedir  # noqa: E402
import apply_dead_mesh_item_swaps  # noqa: E402
import apply_rohan_spear_reforge  # noqa: E402
import audit_gender_variation_flags  # noqa: E402
import generate_black_numenorean_armor  # noqa: E402
import generate_black_numenorean_weapons  # noqa: E402


class AssetRepoMirrorTests(unittest.TestCase):
    def test_default_asset_repo_is_the_shared_mirror(self):
        for module in (apply_dead_mesh_item_swaps, apply_rohan_spear_reforge,
                       audit_gender_variation_flags):
            with self.subTest(module=module.__name__):
                self.assertEqual(Path(module.DEFAULT_ASSET_REPO), _gamedir.ASSET_REPO)

    def test_black_numenorean_generators_write_under_the_shared_mirror(self):
        mirror = os.path.normcase(str(_gamedir.ASSET_REPO))
        self.assertTrue(os.path.normcase(generate_black_numenorean_armor.REPO_ARMORY).startswith(mirror))
        self.assertTrue(os.path.normcase(generate_black_numenorean_weapons.REPO_MD).startswith(mirror))

    def test_no_tool_names_the_deleted_v14_mirror(self):
        tools = Path(_gamedir.__file__).parent
        for name in ("apply_dead_mesh_item_swaps", "apply_rohan_spear_reforge",
                     "audit_gender_variation_flags", "generate_black_numenorean_armor",
                     "generate_black_numenorean_weapons"):
            text = (tools / (name + ".py")).read_text(encoding="utf-8")
            with self.subTest(tool=name):
                self.assertNotIn("lotraom-assets\v1.4", text)
                self.assertNotIn('"v1.4"', text)


if __name__ == "__main__":
    unittest.main()
