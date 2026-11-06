#!/usr/bin/env python3
"""tools/audit_leg_cover_flags.py: every leg piece covers the leg, except shoes on a bare-shin mesh.

The two defects it exists for are cases here: the Lorien generator writing false on greaves, and boots that draw the
Uruk-hai shoe mesh being flipped to true because a script selected items by id. Synthetic folders only, except the
one live scan, which skips without the install.

Run: python -m unittest tools.tests.test_audit_leg_cover_flags
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import audit_leg_cover_flags as audit_mod  # noqa: E402
from _gamedir import DEFAULT_GAME_ROOT, game_modules  # noqa: E402

SHOE = "sk_uruk_hai_shoes_a1"


def item(item_id, mesh, covers, item_type="LegArmor"):
    attr = "" if covers is None else f' covers_legs="{covers}"'
    return (f'<Item id="{item_id}" mesh="{mesh}" Type="{item_type}"><ItemComponent>'
            f'<Armor leg_armor="10"{attr} /></ItemComponent></Item>')


def items_file(*items):
    return "<Items>" + "".join(items) + "</Items>"


def legs_of(*items):
    path = Path(tempfile.mkdtemp()) / "leg_armors.xml"
    path.write_text(items_file(*items), encoding="utf-8")
    legs, error = audit_mod.read_legs(path, "test/leg_armors.xml")
    assert error is None, error
    return legs


class AuditRulesTests(unittest.TestCase):
    def test_a_greave_that_covers_the_leg_passes(self):
        self.assertEqual(audit_mod.audit(legs_of(item("g1", "greave_a", "true"))), [])

    def test_a_greave_that_does_not_cover_the_leg_is_a_violation(self):
        # The Lorien generator wrote false on all eight leg pieces.
        problems = audit_mod.audit(legs_of(item("sk_ll_gl_grvs_heavy_a", "sk_ll_gl_grvs_heavy_a", "false")))
        self.assertEqual(len(problems), 1)
        self.assertIn("not a bare-shin mesh", problems[0])

    def test_a_leg_piece_without_the_attribute_is_a_violation(self):
        problems = audit_mod.audit(legs_of(item("g1", "greave_a", None)))
        self.assertEqual(len(problems), 1)
        self.assertIn("no covers_legs attribute", problems[0])

    def test_the_uruk_hai_shoes_pass_as_false(self):
        self.assertEqual(audit_mod.audit(legs_of(item("shoes", SHOE, "false"))), [])

    def test_a_boot_that_draws_the_shoe_mesh_must_be_false(self):
        # The Isengard starter boots were flipped to true by a script that selected items by id.
        legs = legs_of(item("shoes", SHOE, "false"), item("starter_infantry_isengard_leg_a", SHOE, "true"))
        problems = audit_mod.audit(legs)
        self.assertTrue(any("starter_infantry_isengard_leg_a" in p and "covers_legs=true" in p for p in problems),
                        problems)
        self.assertTrue(any(p.startswith("mesh " + SHOE) and "disagree" in p for p in problems), problems)

    def test_items_that_share_a_mesh_must_agree(self):
        problems = audit_mod.audit(legs_of(item("a", "same_mesh", "true"), item("b", "same_mesh", "false")))
        self.assertTrue(any("same_mesh" in p and "disagree" in p for p in problems), problems)

    def test_items_that_are_not_leg_armour_are_ignored(self):
        legs = legs_of(item("chest", "chest_a", "false", item_type="BodyArmor"), item("g1", "greave_a", "true"))
        self.assertEqual([leg.item_id for leg in legs], ["g1"])
        self.assertEqual(audit_mod.audit(legs), [])


class ScanTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.modules = self.root / "Modules"
        self.repo = self.root / "repo"
        (self.modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items" / "x").mkdir(parents=True)
        (self.modules / "TAOM_Map" / "ModuleData").mkdir(parents=True)
        (self.modules / "SandBoxCore" / "ModuleData").mkdir(parents=True)
        self.repo.mkdir()

    def tearDown(self):
        self._tmp.cleanup()

    def _write(self, rel, text):
        path = self.modules / rel
        path.write_text(text, encoding="utf-8")
        return path

    def test_scan_reads_our_modules_and_skips_vanilla_backups_and_language_files(self):
        folder = "LOTRLOME_Armory/ModuleData/LOTRLOME_items/x/"
        self._write(folder + "leg_armors.xml", items_file(item("g1", "greave_a", "true")))
        self._write(folder + "leg_armors.xml.bak-gen-20261106-000000", items_file(item("old", "o", "false")))
        self._write("SandBoxCore/ModuleData/leg_armors.xml", items_file(item("vanilla_shoe", "v", "false")))
        legs, errors = audit_mod.scan(self.modules, self.repo)
        self.assertEqual([leg.item_id for leg in legs], ["g1"])
        self.assertEqual(errors, [])

    def test_a_leg_file_that_does_not_parse_is_reported_not_skipped(self):
        self._write("TAOM_Map/ModuleData/leg_armors.xml", '<Items><Item id="x" Type="LegArmor"></Items>')
        legs, errors = audit_mod.scan(self.modules, self.repo)
        self.assertEqual(legs, [])
        self.assertEqual(len(errors), 1)
        self.assertIn("does not parse", errors[0])

    def test_an_empty_scan_exits_2_not_0(self):
        with mock.patch.object(audit_mod, "REPO", self.repo):
            self.assertEqual(audit_mod.main(["--modules", str(self.modules)]), 2)

    def test_main_exits_1_on_a_violation_and_0_when_clean(self):
        folder = "LOTRLOME_Armory/ModuleData/LOTRLOME_items/x/leg_armors.xml"
        with mock.patch.object(audit_mod, "REPO", self.repo):
            self._write(folder, items_file(item("g1", "greave_a", "false")))
            self.assertEqual(audit_mod.main(["--modules", str(self.modules)]), 1)
            self._write(folder, items_file(item("g1", "greave_a", "true")))
            self.assertEqual(audit_mod.main(["--modules", str(self.modules)]), 0)


MODULES = Path(game_modules(DEFAULT_GAME_ROOT))


@unittest.skipUnless((MODULES / "LOTRLOME_Armory" / "ModuleData").is_dir(), "needs the live Armory")
class LiveArmoryTests(unittest.TestCase):
    def test_the_live_modules_pass(self):
        legs, errors = audit_mod.scan(MODULES)
        self.assertGreater(len(legs), 300, "the live scan found too few leg items to mean anything")
        self.assertEqual(errors + audit_mod.audit(legs), [])


if __name__ == "__main__":
    unittest.main()
