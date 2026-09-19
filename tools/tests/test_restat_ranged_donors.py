#!/usr/bin/env python3
"""Unit tests for tools/restat_ranged_donors.py (#617): the Armory's own bows, crossbows and ammo
take the spec's donor_stats and ammo_stats, byte-faithfully, in the live Armory and the mirror.

Run:  python -m unittest discover -s tools/tests -p "test_restat_ranged_donors.py"
"""
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import restat_ranged_donors as rd  # noqa: E402

BOM = b"\xef\xbb\xbf"

# The Armory's own layout: one attribute per line, LF, no BOM.
WEAPONS = """<?xml version="1.0" encoding="utf-8"?>
<Items>
    <Item
        id="elf_bow"
        name="{=k}[Elf] Longbow IV"
        Type="Bow">
        <ItemComponent>
            <Weapon
                weapon_class="Bow"
                missile_speed="100"
                accuracy="100"
                thrust_damage="115"
                item_usage="bow">
                <WeaponFlags RangedWeapon="true" />
            </Weapon>
        </ItemComponent>
    </Item>

    <Item
        id="elf_arrow"
        name="Elven Arrow"
        Type="Arrows">
        <ItemComponent>
            <Weapon
                weapon_class="Arrow"
                thrust_damage="5"
                stack_amount="30" />
        </ItemComponent>
    </Item>

    <Item
        id="elf_bow_b"
        Type="Bow">
        <ItemComponent>
            <Weapon weapon_class="Bow" accuracy="95" thrust_damage="90" />
        </ItemComponent>
    </Item>
</Items>
"""


class RestatTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.modules = root / "Modules"
        self.items = self.modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items"
        self.items.mkdir(parents=True)
        self.weapons = self.items / "LOTRAOM_weapons.xml"
        self.weapons.write_bytes(WEAPONS.encode())
        vanilla = self.modules / "SandBoxCore" / "ModuleData" / "items"
        vanilla.mkdir(parents=True)
        (vanilla / "weapons.xml").write_bytes(
            b'<Items><Item id="noble_long_bow" Type="Bow"><ItemComponent><Weapon weapon_class="Bow" '
            b'thrust_damage="95" accuracy="98"/></ItemComponent></Item></Items>')
        self.mirror = root / "assets" / "LOTRLOME_Armory"
        (self.mirror / "ModuleData" / "LOTRLOME_items").mkdir(parents=True)
        self.mirror_weapons = self.mirror / "ModuleData" / "LOTRLOME_items" / "LOTRAOM_weapons.xml"
        # The mirror copy is CRLF with a BOM: both must survive.
        self.mirror_weapons.write_bytes(BOM + WEAPONS.replace("\n", "\r\n").encode())
        self.spec = root / "spec.json"
        self._spec({"elf_bow": {"damage": 90, "accuracy": 98}}, {"elf_arrow": 4})

    def tearDown(self):
        self._tmp.cleanup()

    def _spec(self, donors, ammo):
        self.spec.write_text(json.dumps({"donor_stats": donors, "ammo_stats": ammo}), encoding="utf-8")

    def _run(self, *extra):
        return rd.main(["--game-modules", str(self.modules), "--asset-repo", str(self.mirror),
                        "--spec", str(self.spec), *extra])

    def test_dry_run_writes_nothing(self):
        live, mirror = self.weapons.read_bytes(), self.mirror_weapons.read_bytes()
        self.assertEqual(self._run(), 0)
        self.assertEqual(self.weapons.read_bytes(), live)
        self.assertEqual(self.mirror_weapons.read_bytes(), mirror)
        self.assertFalse(self.weapons.with_name(self.weapons.name + rd.BACKUP_SUFFIX).exists())

    def test_apply_changes_only_the_listed_attributes(self):
        self.assertEqual(self._run("--apply"), 0)
        expected = (WEAPONS.replace('accuracy="100"', 'accuracy="98"')
                    .replace('thrust_damage="115"', 'thrust_damage="90"')
                    .replace('thrust_damage="5"', 'thrust_damage="4"'))
        self.assertEqual(self.weapons.read_bytes(), expected.encode())                 # nothing else moved
        mirror = self.mirror_weapons.read_bytes()
        self.assertTrue(mirror.startswith(BOM))
        self.assertEqual(mirror, BOM + expected.replace("\n", "\r\n").encode())       # CRLF kept
        self.assertEqual(self.weapons.with_name(self.weapons.name + rd.BACKUP_SUFFIX).read_bytes(), WEAPONS.encode())
        self.assertIn(b'thrust_damage="90" />', self.weapons.read_bytes())            # elf_bow_b untouched
        # The mirror is a git repo whose history is the backup; a sidecar there is an untracked file
        # a broad add would ship (second review, #617).
        self.assertFalse(self.mirror_weapons.with_name(self.mirror_weapons.name + rd.BACKUP_SUFFIX).exists())

    def test_apply_is_idempotent_and_verify_sees_drift(self):
        self.assertEqual(self._run("--verify"), 1)
        self.assertEqual(self._run("--apply"), 0)
        first = self.weapons.read_bytes()
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(self.weapons.read_bytes(), first)
        self.assertEqual(self._run("--verify"), 0)
        self.mirror_weapons.write_bytes(self.mirror_weapons.read_bytes().replace(b'thrust_damage="4"', b'thrust_damage="5"'))
        self.assertEqual(self._run("--verify"), 1)
        backup = self.weapons.with_name(self.weapons.name + rd.BACKUP_SUFFIX)
        self.assertEqual(backup.read_bytes(), WEAPONS.encode())                     # the sidecar keeps the original

    def test_an_unknown_id_writes_nothing(self):
        self._spec({"elf_bow": {"damage": 90}, "no_such_bow": {"damage": 50}}, {})
        live = self.weapons.read_bytes()
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.weapons.read_bytes(), live)

    def test_a_vanilla_id_is_refused(self):
        self._spec({"noble_long_bow": {"damage": 80}}, {})
        live = self.weapons.read_bytes()
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.weapons.read_bytes(), live)

    def test_an_id_defined_twice_is_refused(self):
        (self.items / "copy.xml").write_bytes(WEAPONS.encode())
        live = self.weapons.read_bytes()
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.weapons.read_bytes(), live)

    def test_a_weapon_without_the_attribute_is_refused(self):
        self.weapons.write_bytes(WEAPONS.replace('                accuracy="100"\n', "").encode())
        live = self.weapons.read_bytes()
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.weapons.read_bytes(), live)

    def test_the_ammo_class_is_matched_not_any_weapon(self):
        self._spec({}, {"elf_bow": 4})          # a bow listed as ammo has no Arrow/Bolt weapon
        self.assertEqual(self._run("--apply"), 2)

    def test_an_id_inside_a_comment_is_neither_a_definition_nor_edited(self):
        # A retired item kept as a comment must not count as a second definition (which aborted
        # the whole run) and must never be the thing edited (a false "Wrote" on dead text).
        commented = WEAPONS.replace(
            "<Items>\n",
            "<Items>\n    <!-- retired:\n    <Item id=\"elf_bow\" Type=\"Bow\"><ItemComponent><Weapon weapon_class=\"Bow\" "
            "accuracy=\"55\" thrust_damage=\"55\" /></ItemComponent></Item>\n    -->\n", 1)
        self.weapons.write_bytes(commented.encode())
        self.assertEqual(self._run("--apply"), 0)
        text = self.weapons.read_bytes().decode()
        self.assertIn('accuracy="55" thrust_damage="55"', text)               # the comment is untouched
        self.assertIn('thrust_damage="90"', text)                              # the live item is restatted
        self.weapons.write_bytes(WEAPONS.replace(
            "    <Item\n        id=\"elf_arrow\"", "    <!-- <Item id=\"elf_arrow\"> -->\n    <Item\n        id=\"elf_arrow_x\"").encode())
        self.assertEqual(self._run("--apply"), 2)                              # only a commented copy: unknown

    def test_a_donor_row_with_no_known_stat_is_refused(self):
        # Second review (#617): a row with misspelled keys built an empty attribute map, so the
        # tool matched the weapon, set nothing and --verify reported OK while checking nothing.
        self._spec({"elf_bow": {"dmg": 50, "acc": 50}}, {})
        self.assertEqual(self._run("--verify"), 2)
        self._spec({"elf_bow": {}}, {"elf_arrow": -1})
        self.assertEqual(self._run("--verify"), 2)
        self._spec({"elf_bow": 50}, {})          # not a container: refused, not a traceback (convergence pass)
        self.assertEqual(self._run("--verify"), 2)

    def test_an_id_in_both_tables_is_refused(self):
        self._spec({"elf_bow": {"damage": 50}}, {"elf_bow": 4})
        live = self.weapons.read_bytes()
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.weapons.read_bytes(), live)

    def test_missing_mirror_is_a_warning(self):
        self.assertEqual(self._run("--apply", "--asset-repo", str(self.mirror / "nope")), 0)
        self.assertIn(b'thrust_damage="90"', self.weapons.read_bytes())

    def test_the_repo_spec_lists_only_armory_items(self):
        import ranged_ladder as rl
        spec = rl.load_spec()
        want = rd.targets(spec)
        self.assertEqual(len(spec["donor_stats"]), 38)
        self.assertTrue(all(k.startswith(("wm_", "sk_", "sm_", "highelf_", "gondor_", "dale_")) for k in want))


if __name__ == "__main__":
    unittest.main()
