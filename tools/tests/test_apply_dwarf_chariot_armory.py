#!/usr/bin/env python3
"""Unit tests for the dwarven war chariot Armory writer (tools/apply_dwarf_chariot_armory.py).

Pure stdlib, synthetic game dir (--game-dir points the whole script at a temp tree). Every case builds its own tree,
because the script changes files in place. One more case reads the real install when it is there.
"""
import io
import os
import sys
import tempfile
import unittest
import uuid
from contextlib import redirect_stdout
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import apply_dwarf_chariot_armory as dc  # noqa: E402

BOM = b"\xef\xbb\xbf"
HORSES = (
    "<Items>\n"
    '    <Item\n        id="taom_chariot_armour_imperial"\n        Type="HorseHarness">\n'
    # a real item holds an <ItemComponent>: the search for the next <Item must not stop at it
    '        <ItemComponent>\n            <Armor body_armor="45" family_type="4" />\n        </ItemComponent>\n'
    '        <Flags Civilian="true" />\n    </Item>\n'
    "\n\t<!-- next block -->\n"
    '    <Item id="later_item" Type="Horse" />\n'
    "</Items>\n"
)
LOC = ('<base><strings>\r\n    <string id="taom_chariot_a" text="Wainrider War Chariot"/>\r\n'
       '    <string id="later_row" text="Later"/>\r\n</strings></base>\r\n')


def run(root, *flags):
    out = io.StringIO()
    with redirect_stdout(out), mock.patch.object(dc, "game_or_kit_running", return_value=False):
        code = dc.main(["--game-dir", root, *flags])
    return code, out.getvalue()


class Tree(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = self._tmp.name
        self.armory = os.path.join(self.root, "Modules", "LOTRLOME_Armory")
        self.horses = os.path.join(self.armory, "ModuleData", "LOTRLOME_items", "LOTRAOM_horses.xml")
        self.loc = os.path.join(self.armory, "ModuleData", "Languages", "loc_LOTRAOM_horses.xml")
        for path, data in ((self.horses, BOM + HORSES.encode("utf-8")), (self.loc, LOC.encode("utf-8"))):
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "wb") as fh:
                fh.write(data)
        os.makedirs(os.path.join(self.armory, "Assets", "creature", "chariot"), exist_ok=True)
        os.makedirs(os.path.join(self.armory, "RuntimeDataCache"), exist_ok=True)

    def tearDown(self):
        self._tmp.cleanup()

    def package(self, file_name, holds, cache=True):
        """A stand-in package: 8 bytes, a GUID, then the names it holds. cache adds its RuntimeDataCache entry."""
        guid = uuid.uuid4()
        path = os.path.join(self.armory, "Assets", "creature", "chariot", file_name)
        with open(path, "wb") as fh:
            fh.write(b"TPAC\x02\x00\x00\x00" + guid.bytes_le + b"\x00".join(h.encode("ascii") for h in holds))
        if cache:
            open(os.path.join(self.armory, "RuntimeDataCache", str(guid).upper() + ".rdc"), "wb").close()
        return path

    def cook_all(self):
        for mesh in dc.MESHES:
            self.package(mesh + "_geo.tpac", [mesh])
        for material in dc.MATERIALS:
            self.package(material + "_mtl.tpac", [material], cache=False)

    def bytes_of(self, path):
        with open(path, "rb") as fh:
            return fh.read()


class CheckAndApplyTests(Tree):
    def test_check_on_an_armory_without_the_rows_is_red(self):
        self.cook_all()                                         # so the package guard is not what turns it red
        before = self.bytes_of(self.horses), self.bytes_of(self.loc)
        code, out = run(self.root, "--check")
        self.assertEqual(code, 1)
        self.assertIn("LOTRAOM_horses.xml: add 3 items", out)
        self.assertIn("loc_LOTRAOM_horses.xml: add 3 rows", out)
        self.assertIn("CHECK FAILED", out)
        self.assertEqual(before, (self.bytes_of(self.horses), self.bytes_of(self.loc)))

    def test_check_is_red_on_a_document_that_does_not_parse_even_when_the_rows_are_in(self):
        self.cook_all()
        run(self.root, "--apply")
        data = self.bytes_of(self.horses)
        with open(self.horses, "wb") as fh:
            fh.write(data.replace(b"</Items>", b""))            # the rows are still there byte for byte
        code, out = run(self.root, "--check")
        self.assertEqual(code, 1)
        self.assertIn("PARSE FAILED", out)

    def test_a_dry_run_writes_nothing(self):
        self.cook_all()
        before = self.bytes_of(self.horses), self.bytes_of(self.loc)
        code, out = run(self.root)
        self.assertEqual(code, 0)
        self.assertIn("dry run", out)
        self.assertEqual(before, (self.bytes_of(self.horses), self.bytes_of(self.loc)))

    def test_apply_adds_the_rows_after_their_anchors_and_keeps_the_bytes(self):
        self.cook_all()
        code, out = run(self.root, "--apply")
        self.assertEqual(code, 0, out)
        horses = self.bytes_of(self.horses)
        self.assertTrue(horses.startswith(BOM))
        self.assertNotIn(b"\r\n", horses)                       # the file was LF and stays LF
        text = horses.decode("utf-8")
        self.assertLess(text.index('id="taom_chariot_armour_imperial"'), text.index('id="taom_ram_chariot_a"'))
        self.assertLess(text.index('id="taom_chariot_armour_dwarf_b"'), text.index("<!-- next block -->"))
        loc = self.bytes_of(self.loc)
        self.assertEqual(loc.count(b"\n"), loc.count(b"\r\n"))  # the file was CRLF and stays CRLF
        lt = loc.decode("utf-8")
        self.assertLess(lt.index('id="taom_chariot_a"'), lt.index('id="taom_ram_chariot_a"'))
        self.assertLess(lt.index('id="taom_chariot_armour_dwarf_b"'), lt.index('id="later_row"'))

    def test_after_apply_the_check_is_green_and_a_second_apply_writes_nothing(self):
        self.cook_all()
        run(self.root, "--apply")
        after = self.bytes_of(self.horses), self.bytes_of(self.loc)
        self.assertEqual(run(self.root, "--check")[0], 0)
        code, out = run(self.root, "--apply")
        self.assertEqual(code, 0)
        self.assertIn("nothing to write", out)
        self.assertEqual(after, (self.bytes_of(self.horses), self.bytes_of(self.loc)))

    def test_the_ram_team_row_carries_no_skeleton_scale(self):
        # The engine applies a scale by bone index of horse_skeleton, which lands on the wrong bones of the chariot rig.
        self.assertNotIn("skeleton_scale", dc.ITEMS_BLOCK)

    def test_an_unknown_flag_is_refused(self):
        self.cook_all()
        before = self.bytes_of(self.horses)
        with self.assertRaises(SystemExit) as stop, redirect_stdout(io.StringIO()), mock.patch("sys.stderr", io.StringIO()):
            dc.main(["--game-dir", self.root, "--aply"])
        self.assertEqual(stop.exception.code, 2)
        self.assertEqual(before, self.bytes_of(self.horses))    # a mistyped --apply must not become a silent dry run

    def test_a_file_that_cannot_be_read_as_text_is_a_crash_not_a_finding(self):
        with open(self.horses, "wb") as fh:
            fh.write(b"\xff\xfe<Items/>")
        code, out = run(self.root, "--check")
        self.assertEqual(code, 3, out)                          # tools/data_gates.py: 1 a finding, 3 a crash
        self.assertIn("CRASH", out)

    def test_apply_is_refused_while_the_game_or_the_kit_runs(self):
        self.cook_all()
        out = io.StringIO()
        with redirect_stdout(out), mock.patch.object(dc, "game_or_kit_running", return_value=True):
            code = dc.main(["--game-dir", self.root, "--apply"])
        self.assertEqual(code, 2)
        self.assertNotIn(b"taom_ram_chariot_a", self.bytes_of(self.horses))


class CookedPackageGuardTests(Tree):
    """An item row that names a mesh with no cooked package can hang the game at load, so the writer refuses."""

    def refused(self):
        code, out = run(self.root, "--apply")
        self.assertEqual(code, 1, out)
        self.assertNotIn(b"taom_ram_chariot_a", self.bytes_of(self.horses))
        return out

    def test_apply_without_any_package_is_refused(self):
        self.assertIn("PACKAGE", self.refused())

    def test_a_longer_package_name_does_not_stand_in_for_a_mesh(self):
        # dwarf_war_chariot_b_geo.tpac holds the bytes "dwarf_war_chariot" too: only the exact file name counts.
        self.cook_all()
        os.remove(os.path.join(self.armory, "Assets", "creature", "chariot", "dwarf_war_chariot_geo.tpac"))
        self.assertIn("dwarf_war_chariot_geo.tpac", self.refused())

    def test_a_texture_package_does_not_stand_in_for_a_material(self):
        self.cook_all()
        os.remove(os.path.join(self.armory, "Assets", "creature", "chariot", "dwarf_war_chariot_iron_mtl.tpac"))
        self.package("dwarf_war_chariot_iron_d_tex.tpac", ["dwarf_war_chariot_iron"])
        self.assertIn("dwarf_war_chariot_iron_mtl.tpac", self.refused())

    def test_a_mesh_package_without_a_cache_entry_is_refused(self):
        self.cook_all()
        os.remove(os.path.join(self.armory, "Assets", "creature", "chariot", "dwarf_chariot_rams_geo.tpac"))
        self.package("dwarf_chariot_rams_geo.tpac", ["dwarf_chariot_rams"], cache=False)
        self.assertIn("RuntimeDataCache", self.refused())

    def test_a_material_package_needs_no_cache_entry(self):
        self.cook_all()                                         # cook_all makes the materials without one
        self.assertEqual(run(self.root, "--apply")[0], 0)

    def test_a_package_file_name_is_matched_without_case(self):
        self.cook_all()
        folder = os.path.join(self.armory, "Assets", "creature", "chariot")
        os.rename(os.path.join(folder, "m_eb_ram_barding_a1_mtl.tpac"), os.path.join(folder, "M_EB_Ram_Barding_A1_mtl.tpac"))
        self.assertEqual(run(self.root, "--apply")[0], 0)

    def test_a_package_that_is_under_assets_twice_is_refused(self):
        self.cook_all()
        other = os.path.join(self.armory, "Assets", "other")
        os.makedirs(other)
        with open(os.path.join(other, "dwarf_war_chariot_geo.tpac"), "wb") as fh:
            fh.write(b"TPAC\x02\x00\x00\x00" + uuid.uuid4().bytes_le + b"dwarf_war_chariot")
        self.assertIn("twice", self.refused())

    def test_a_file_that_is_no_package_is_refused(self):
        self.cook_all()
        with open(os.path.join(self.armory, "Assets", "creature", "chariot", "dwarf_chariot_rams_geo.tpac"), "wb") as fh:
            fh.write(b"dwarf_chariot_rams")                     # shorter than a package header
        self.assertIn("not a package", self.refused())

    def test_check_is_red_when_the_rows_are_in_and_a_package_is_gone(self):
        self.cook_all()
        run(self.root, "--apply")
        os.remove(os.path.join(self.armory, "Assets", "creature", "chariot", "dwarf_war_chariot_b_geo.tpac"))
        code, out = run(self.root, "--check")
        self.assertEqual(code, 1)
        self.assertIn("dwarf_war_chariot_b_geo.tpac", out)


class DriftAndRemoveTests(Tree):
    def applied(self):
        self.cook_all()
        self.assertEqual(run(self.root, "--apply")[0], 0)

    def edit(self, path, old, new):
        data = self.bytes_of(path)
        self.assertEqual(data.count(old), 1)
        with open(path, "wb") as fh:
            fh.write(data.replace(old, new))

    def test_remove_gives_back_the_first_bytes(self):
        first = self.bytes_of(self.horses), self.bytes_of(self.loc)
        self.applied()
        code, out = run(self.root, "--remove")
        self.assertEqual(code, 0, out)
        self.assertEqual(first, (self.bytes_of(self.horses), self.bytes_of(self.loc)))

    def test_remove_keeps_an_edit_somebody_made_after_the_apply(self):
        self.applied()
        self.edit(self.horses, b'<Item id="later_item" Type="Horse" />', b'<Item id="later_item" Type="Horse" weight="9" />')
        self.assertEqual(run(self.root, "--remove")[0], 0)
        data = self.bytes_of(self.horses)
        self.assertIn(b'weight="9"', data)
        self.assertNotIn(b"taom_ram_chariot_a", data)

    def test_a_changed_row_is_drift_for_the_check_and_stops_apply_and_remove(self):
        self.applied()
        self.edit(self.horses, b'body_armor="54"', b'body_armor="99"')
        changed = self.bytes_of(self.horses)
        code, out = run(self.root, "--check")
        self.assertEqual(code, 1)
        self.assertIn("differ", out)
        for flag in ("--apply", "--remove"):
            self.assertEqual(run(self.root, flag)[0], 1, flag)
            self.assertEqual(changed, self.bytes_of(self.horses), flag)

    def test_a_block_that_is_in_the_file_twice_is_drift(self):
        self.applied()
        data = self.bytes_of(self.horses)
        block = ("\n" + dc.ITEMS_BLOCK).encode("utf-8")
        self.assertEqual(data.count(block), 1)
        with open(self.horses, "wb") as fh:
            fh.write(data.replace(block, block + block))        # a hand paste
        twice = self.bytes_of(self.horses)
        code, out = run(self.root, "--check")
        self.assertEqual(code, 1)
        self.assertIn("more than one time", out)                # the doubled-id guard itself, not a later one
        self.assertEqual(run(self.root, "--remove")[0], 1)
        self.assertEqual(twice, self.bytes_of(self.horses))

    def test_rows_with_other_quotes_and_an_edit_are_drift_not_absent(self):
        # id='x' is the same XML as id="x": the rows are there, so a second block must not be added.
        self.applied()
        data = self.bytes_of(self.horses)
        for item in dc.ITEM_NAMES:
            data = data.replace(('id="%s"' % item).encode(), ("id='%s'" % item).encode())
        with open(self.horses, "wb") as fh:
            fh.write(data.replace(b'body_armor="54"', b'body_armor="99"'))
        edited = self.bytes_of(self.horses)
        code, out = run(self.root, "--apply")
        self.assertEqual(code, 1, out)
        self.assertIn("DRIFT", out)
        self.assertEqual(edited, self.bytes_of(self.horses))

    def test_a_name_anchor_that_is_only_inside_a_comment_is_refused(self):
        self.cook_all()
        data = self.bytes_of(self.loc)
        row = b'    <string id="taom_chariot_a" text="Wainrider War Chariot"/>\r\n'
        self.assertEqual(data.count(row), 1)
        with open(self.loc, "wb") as fh:
            fh.write(data.replace(row, b"    <!--\r\n" + row + b"    -->\r\n"))
        commented = self.bytes_of(self.loc)
        code, out = run(self.root, "--apply")
        self.assertEqual(code, 1, out)
        self.assertIn("ANCHOR", out)
        self.assertEqual(commented, self.bytes_of(self.loc))
        self.assertNotIn(b"taom_ram_chariot_a", self.bytes_of(self.horses))   # nothing is written when one file fails

    def test_rows_that_are_partly_present_are_refused(self):
        self.applied()
        self.edit(self.loc, b'    <string id="taom_chariot_armour_dwarf_b" text="[Erebor] War Chariot Steel Plating"/>\r\n', b"")
        code, out = run(self.root, "--apply")
        self.assertEqual(code, 1)
        self.assertIn("partly present", out)

    def test_a_missing_anchor_is_refused(self):
        self.cook_all()
        self.edit(self.horses, b'id="taom_chariot_armour_imperial"', b'id="some_other_item"')
        code, out = run(self.root, "--apply")
        self.assertEqual(code, 1)
        self.assertIn("ANCHOR", out)

    def test_a_missing_armory_is_exit_2(self):
        err = io.StringIO()
        with self.assertRaises(SystemExit) as stop, redirect_stdout(io.StringIO()), mock.patch("sys.stderr", err):
            dc.main(["--game-dir", os.path.join(self.root, "nowhere"), "--check"])
        self.assertEqual(stop.exception.code, 2)
        self.assertIn("not found", err.getvalue())              # the tool's own message, not argparse's exit 2


class LiveArmoryTests(unittest.TestCase):
    """The live, unversioned Armory holds exactly the rows of this file's spec (skipped where no install is found)."""

    def setUp(self):
        self.live = dc.resolve_paths(dc.game_dir(dc._DEFAULT_GAME_DIR))
        if not os.path.isfile(self.live["horses"]):
            self.skipTest("no Armory install on this machine")

    def test_the_live_armory_matches_the_spec(self):
        out = io.StringIO()
        with redirect_stdout(out):
            code = dc.main(["--game-dir", str(dc.game_dir(dc._DEFAULT_GAME_DIR)), "--check"])
        self.assertEqual(code, 0, out.getvalue())

    def test_remove_then_apply_on_a_copy_of_the_live_files_gives_the_live_bytes_back(self):
        # The restore after a reinstall, on the real documents: their anchor item holds an <ItemComponent>.
        with tempfile.TemporaryDirectory() as root:
            copy = dc.resolve_paths(root)
            for key in ("horses", "loc"):
                os.makedirs(os.path.dirname(copy[key]), exist_ok=True)
                with open(self.live[key], "rb") as src, open(copy[key], "wb") as dst:
                    dst.write(src.read())
            with mock.patch.object(dc, "missing_packages", return_value=[]):
                self.assertEqual(run(root, "--remove")[0], 0)
                for key in ("horses", "loc"):
                    with open(copy[key], "rb") as fh:
                        self.assertNotIn(b"taom_ram_chariot_a", fh.read(), key)
                code, out = run(root, "--apply")
            self.assertEqual(code, 0, out)
            for key in ("horses", "loc"):
                with open(self.live[key], "rb") as a, open(copy[key], "rb") as b:
                    self.assertEqual(a.read(), b.read(), key)


if __name__ == "__main__":
    unittest.main()
