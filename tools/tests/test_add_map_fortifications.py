#!/usr/bin/env python3
"""Unit tests for tools/add_map_fortifications.py.

Run:  python -m unittest tools.tests.test_add_map_fortifications

Pure stdlib over synthetic text; no game install needed. Each test pins one part of the contract:
  - the gate is the tagged child's position composed through every ancestor frame, rotation in
    Z, X, Y order with the scale on the columns (the order that reproduces the live map's 230
    recorded gates; a different order is wrong by up to 4.8 map units)
  - an entity with no tagged child, or no entity at all, has no gate
  - a rendered town and castle parse and carry each culture's own scenes, menus and gate rotation
  - an unknown culture raises before anything is written
  - --check names a missing row, a missing loc row, a missing entity, a drifted position and a
    drifted gate
"""
import math
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import add_map_fortifications as amf  # noqa: E402


def entity(name, pos, euler="0, 0, 0", scale="1, 1, 1", tags=(), children=""):
    tag_xml = "".join(f'<tag name="{t}"/>' for t in tags)
    return (f'<game_entity name="{name}" old_prefab_name="" mobility="1">'
            f'<tags>{tag_xml}</tags>'
            f'<transform position="{pos}" rotation_euler="{euler}" scale="{scale}"/>'
            f'<children>{children}</children></game_entity>')


GATE = lambda pos: entity("gate", pos, tags=("main_map_city_gate",))  # noqa: E731


class GateMathsTests(unittest.TestCase):
    def test_identity_parent_puts_the_gate_at_parent_plus_offset(self):
        scene = entity("town_T1", "100, 200, 0", tags=("town",), children=GATE("3, 4, 0"))
        x, y = amf.scene_gate(scene, "town_T1")
        self.assertAlmostEqual(x, 103.0)
        self.assertAlmostEqual(y, 204.0)

    def test_yaw_rotates_and_scale_stretches_the_offset(self):
        # z = +90 degrees turns local +x into world +y; scale 2 doubles the distance.
        scene = entity("castle_C1", "10, 10, 0", euler=f"0, 0, {math.pi / 2}", scale="2, 2, 2",
                       children=GATE("1, 0, 0"))
        x, y = amf.scene_gate(scene, "castle_C1")
        self.assertAlmostEqual(x, 10.0, places=6)
        self.assertAlmostEqual(y, 12.0, places=6)

    def test_pitch_is_applied_after_yaw(self):
        # Z then X: yaw 90 then pitch 90 sends local +y to world +z, so the gate keeps the parent's
        # x/y. The X, Y, Z order would instead move it off the parent on the map plane.
        scene = entity("town_T2", "0, 0, 0", euler=f"{math.pi / 2}, 0, {math.pi / 2}",
                       children=GATE("0, 1, 0"))
        x, y = amf.scene_gate(scene, "town_T2")
        self.assertAlmostEqual(x, 0.0, places=6)
        self.assertAlmostEqual(y, 0.0, places=6)

    def test_a_gate_nested_two_levels_down_composes_both_frames(self):
        inner = entity("wall", "1, 0, 0", children=GATE("1, 0, 0"))
        scene = entity("town_T3", "50, 50, 0", euler=f"0, 0, {math.pi}", children=inner)
        x, y = amf.scene_gate(scene, "town_T3")
        self.assertAlmostEqual(x, 48.0, places=6)
        self.assertAlmostEqual(y, 50.0, places=6)

    def test_no_gate_child_and_no_entity_both_give_none(self):
        scene = entity("castle_C2", "1, 1, 0", children=entity("banner_pos", "0, 1, 0"))
        self.assertIsNone(amf.scene_gate(scene, "castle_C2"))
        self.assertIsNone(amf.scene_gate(scene, "castle_C9"))

    def test_the_last_gate_child_wins_like_the_editor(self):
        # SettlementPositionScript walks GetChildrenRecursive (depth-first, pre-order) and keeps
        # overwriting the gate, so the last tagged child in that order is the one the game uses.
        scene = entity("town_T4", "0, 0, 0", children=GATE("1, 0, 0") + GATE("5, 0, 0"))
        self.assertAlmostEqual(amf.scene_gate(scene, "town_T4")[0], 5.0)

    def test_the_root_entity_itself_is_never_its_own_gate(self):
        scene = entity("town_T4", "5, 5, 0", tags=("main_map_city_gate",))
        self.assertIsNone(amf.scene_gate(scene, "town_T4"))


class BlockTests(unittest.TestCase):
    def render(self, fort):
        xml = amf.fort_block(fort, ("10.5", "20.25"), (11.0, 21.0), "\n")
        return ET.fromstring(xml)

    def test_an_arthedain_town_carries_its_culture_scenes_menus_and_gate(self):
        f = amf.Fort("town_X1", "Testford", "clan_x_1", "arthedain", "town", "empire_town_c", 3000, "a")
        s = self.render(f)
        self.assertEqual(s.get("owner"), "Faction.clan_x_1")
        self.assertEqual(s.get("culture"), "Culture.arthedain")
        self.assertEqual(s.get("gate_posX"), "11.0000")
        town = s.find("Components/Town")
        self.assertEqual(town.get("id"), "town_comp_X1")
        self.assertEqual(town.get("is_castle"), "false")
        self.assertEqual(town.get("background_mesh"), "menu_empire_3")
        self.assertEqual(town.get("gate_rotation"), "0.808")
        self.assertEqual(town.get("prosperity"), "3000")
        locs = {l.get("id"): l for l in s.find("Locations")}
        self.assertEqual(locs["center"].get("scene_name"), "empire_town_c")
        self.assertEqual(locs["arena"].get("scene_name"), "arena_empire_a")
        self.assertIsNotNone(s.find("CommonAreas"))

    def test_a_bluecraig_town_uses_sturgia_scenes_and_its_own_building_levels(self):
        f = amf.Fort("town_X2", "Gob", "clan_y_2", "bluecraig", "town", "sturgia_town_e", 4800, "a")
        s = self.render(f)
        town = s.find("Components/Town")
        self.assertEqual(town.get("background_mesh"), "gui_bg_town_sturgia")
        self.assertEqual(town.get("gate_rotation"), "0.378")
        walls = [b for b in town.find("Buildings") if b.get("id") == "building_settlement_fortifications"]
        self.assertEqual(walls[0].get("level"), "2")
        locs = {l.get("id"): l for l in s.find("Locations")}
        self.assertEqual(locs["tavern"].get("scene_name"), "sturgia_house_b_interior_tavern")

    def test_a_castle_has_no_arena_and_no_common_areas(self):
        f = amf.Fort("castle_X3", "Keep", "clan_x_1", "arthedain", "castle", "taom_gondor_castle_001_forceatmo", 800, "a")
        s = self.render(f)
        self.assertEqual(s.find("Components/Town").get("id"), "castle_comp_X3")
        self.assertEqual(s.find("Components/Town").get("is_castle"), "true")
        self.assertEqual({l.get("id") for l in s.find("Locations")}, {"center", "lordshall", "prison"})
        self.assertIsNone(s.find("CommonAreas"))

    def test_an_unknown_culture_raises(self):
        f = amf.Fort("town_X4", "Nowhere", "clan_z", "nobody", "town", "empire_town_c", 1, "a")
        with self.assertRaises(ValueError):
            amf.fort_block(f, ("0", "0"), (0.0, 0.0), "\n")


class CheckTests(unittest.TestCase):
    FORT = amf.Fort("town_X5", "Checkton", "clan_x_1", "arthedain", "town", "empire_town_c", 3000, "a")

    def setUp(self):
        self.scene = entity("town_X5", "10, 20, 0", children=GATE("1, 0, 0"))
        self.master = amf.fort_block(self.FORT, ("10", "20"), (11.0, 20.0), "\n")
        self.loc = {"DE": '<string id="Settlements.Settlement.name.town_X5" text="Checkton" />'}

    def test_a_consistent_row_has_no_findings(self):
        self.assertEqual(amf.check([self.FORT], self.master, self.scene, self.loc), [])

    def test_a_missing_row_and_loc_row_are_named(self):
        found = amf.check([self.FORT], "", self.scene, {"DE": ""})
        self.assertTrue(any("no <Settlement> row" in f for f in found))
        self.assertTrue(any("no loc row" in f for f in found))

    def test_a_missing_entity_is_named(self):
        found = amf.check([self.FORT], self.master, "", self.loc)
        self.assertTrue(any("no entity in scene.xscene" in f for f in found))

    def test_a_drifted_position_and_gate_are_named(self):
        moved = entity("town_X5", "15, 20, 0", children=GATE("1, 0, 0"))
        found = amf.check([self.FORT], self.master, moved, self.loc)
        self.assertTrue(any("posX=10" in f for f in found))
        self.assertTrue(any("gate in settlements.xml" in f for f in found))


def row(sid, common=False):
    areas = "<CommonAreas><Area type=\"Backstreet\" name=\"x\" /></CommonAreas>" if common else ""
    return f'  <Settlement id="{sid}" posX="1" posY="2">{areas}</Settlement>\n'


def master(*rows):
    return '<Settlements>\n' + "".join(rows) + '</Settlements>'


class SaveOrderTests(unittest.TestCase):
    """A save made before a batch still loads settlements.xml: the first new row with
    <CommonAreas> throws inside Settlement.Deserialize and MBObjectManager.LoadXML swallows it,
    so every row after it is skipped. New rows must therefore be the file's tail, headed by a
    town or village (a castle carries no <CommonAreas> and would load ownerless)."""

    def test_new_rows_forming_the_tail_under_a_town_are_clean(self):
        text = master(row("town_A1", True), row("town_N1", True), row("castle_N2"), row("village_N1_1", True))
        self.assertEqual(amf.amv.save_order_findings(text, {"town_N1", "castle_N2", "village_N1_1"}), [])

    def test_an_existing_row_after_a_new_one_is_named(self):
        text = master(row("town_N1", True), row("village_A1_1", True), row("castle_N2"))
        found = amf.amv.save_order_findings(text, {"town_N1", "castle_N2"})
        self.assertEqual(len(found), 1)
        self.assertIn("village_A1_1", found[0])

    def test_a_castle_heading_the_tail_is_named(self):
        text = master(row("town_A1", True), row("castle_N2"), row("town_N1", True))
        found = amf.amv.save_order_findings(text, {"town_N1", "castle_N2"})
        self.assertEqual(len(found), 1)
        self.assertIn("castle_N2", found[0])

    def test_a_settlement_inside_a_comment_is_not_a_row(self):
        # The engine skips comment nodes (MBObjectManager.LoadXml), so a commented-out row is no row.
        text = master(row("town_N1", True), '  <!-- <Settlement id="village_A9_9" /> -->\n', row("castle_N2"))
        self.assertEqual(amf.amv.save_order_findings(text, {"town_N1", "castle_N2"}), [])

    def test_no_new_rows_means_no_findings(self):
        self.assertEqual(amf.amv.save_order_findings(master(row("town_A1", True)), set()), [])

    def test_the_batch_is_the_unreleased_forts_and_the_villages_bound_to_them(self):
        forts = [amf.Fort("town_N1", "n", "c", "arthedain", "town", "s", 1, "a"),
                 amf.Fort("castle_O1", "o", "c", "arthedain", "castle", "s", 1, "a")]
        villages = [amf.amv.Village("village_N1_1", "v", "town_N1", "trapper", "arthedain", "s"),
                    amf.amv.Village("village_O1_1", "w", "castle_O1", "trapper", "arthedain", "s"),
                    amf.amv.Village("village_A1_1", "x", "town_A1", "trapper", "arthedain", "s")]
        self.assertEqual(amf.batch_ids(forts, villages, released={"castle_O1"}), {"town_N1", "village_N1_1"})

    def test_towns_are_appended_first_then_table_order(self):
        forts = [amf.Fort(i, i, "c", "arthedain", k, "s", 1, "a")
                 for i, k in (("castle_N1", "castle"), ("town_N2", "town"), ("castle_N3", "castle"), ("town_N4", "town"))]
        self.assertEqual([f.id for f in amf.append_order(forts)], ["town_N2", "town_N4", "castle_N1", "castle_N3"])

    def test_check_reports_the_save_order(self):
        fort = amf.Fort("town_X6", "Late", "clan_x_1", "arthedain", "town", "empire_town_c", 3000, "a")
        scene = entity("town_X6", "10, 20, 0", children=GATE("1, 0, 0"))
        block = amf.fort_block(fort, ("10", "20"), (11.0, 20.0), "\n")
        loc = {"DE": '<string id="Settlements.Settlement.name.town_X6" text="Late" />'}
        late = master(block, row("village_A1_1", True))
        self.assertTrue(any("village_A1_1" in f for f in amf.check([fort], late, scene, loc)))
        self.assertEqual(amf.check([fort], master(row("village_A1_1", True), block), scene, loc), [])


if __name__ == "__main__":
    unittest.main()
