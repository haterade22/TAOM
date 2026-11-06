#!/usr/bin/env python
"""The Long Defeat's own item file (Main/_Module/ModuleData/long_defeat/long_defeat_items.xml): the Elven Volley's arrow (phase3-plan.md OQ3, Slice 6).

validate_moduledata.py reads nothing under long_defeat/, so this is the check that the meshes the arrow names exist in the installed modules (the plan's LiveInstall test).

Run:  python -m unittest tools.tests.test_long_defeat_items
"""
import os
import re
import subprocess
import sys
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
ITEMS_DIR = REPO / "Main" / "_Module" / "ModuleData" / "long_defeat"
GAME_DIR = Path(os.environ.get("TAOM_GAME_DIR", r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))


class ElvenArrowItemTests(unittest.TestCase):
    def test_the_item_file_holds_the_one_arrow_the_shop_names(self):
        text = (ITEMS_DIR / "long_defeat_items.xml").read_text(encoding="utf-8")
        shop = (ITEMS_DIR / "shop.json").read_text(encoding="utf-8")

        self.assertEqual(re.findall(r'<Item\s+id="([^"]+)"', text), ["ld_elven_arrow"])
        self.assertIn('"item": "ld_elven_arrow"', shop)

    @unittest.skipUnless((GAME_DIR / "Modules").is_dir(), "no installed game to read the meshes from (a laptop with a partial install, or no game)")
    def test_the_meshes_the_arrow_names_exist_in_the_installed_modules(self):
        done = subprocess.run(
            [sys.executable, "-I", str(REPO / "tools" / "validate_mesh_refs.py"), "--items", str(ITEMS_DIR), "--game", str(GAME_DIR), "--no-rgl-log", "--tpac-modules", "LOTRLOME_Armory", "Native"],
            capture_output=True, text=True, timeout=600)

        self.assertEqual(done.returncode, 0, done.stdout + done.stderr)
        self.assertIn("Tier B missing visual meshes: 0", done.stdout)
        self.assertIn("Tier C missing collision bodies: 0", done.stdout)
        self.assertRegex(done.stdout, r"unique visual meshes present \(Tier B\): 3/3")


if __name__ == "__main__":
    unittest.main()
