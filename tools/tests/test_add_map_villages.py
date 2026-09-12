#!/usr/bin/env python3
"""Unit tests for tools/add_map_villages.py.

Run:  python -m unittest tools.tests.test_add_map_villages
  or:  python tools/tests/test_add_map_villages.py

Pure stdlib over synthetic text; no game install needed. Each test pins one part of the
contract:
  - a rendered village block parses and carries the attributes the engine reads
  - the component id follows the live convention (castle_village_comp_X / village_comp_X)
  - an unknown village_type or culture family raises before anything is written
  - a position comes from the scene entity transform when the entity exists
  - a missing entity falls back to the bound settlement plus a per-parent offset, flagged
  - insertion lands right after the anchor settlement, falling back to </Settlements>
  - loc rows land after the anchor's last row and reuse the file's own newline
  - per-id idempotency: an id already present is skipped
  - --check names a missing master row, a missing loc row, a drifted position, a missing entity
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import add_map_villages as amv  # noqa: E402

SCENE = (
    '<scene>\n'
    '\t\t<game_entity name="castle_village_I2_3" old_prefab_name="" mobility="1">\n'
    '\t\t\t<tags>\n\t\t\t\t<tag name="village"/>\n\t\t\t</tags>\n'
    '\t\t\t<transform position="665.904, 812.459, 65.307" rotation_euler="0.000, 0.000, 2.182" scale="1.000, 1.000, 1.250"/>\n'
    '\t\t</game_entity>\n'
    '\t\t<game_entity name="castle_village_I2_30" old_prefab_name="" mobility="1">\n'
    '\t\t\t<transform position="1.000, 2.000, 3.000" rotation_euler="0, 0, 0" scale="1, 1, 1"/>\n'
    '\t\t</game_entity>\n'
    '</scene>\n'
)

MASTER = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    '<Settlements>\n'
    '  <Settlement id="castle_I2" name="{=Settlements.Settlement.name.castle_I2}Forthbrond" owner="Faction.clan_isengard_10" posX="650.491" posY="818.901" culture="Culture.isengard" gate_posX="651.5593" gate_posY="817.4003">\n'
    '    <Components>\n      <Town id="castle_comp_I2" is_castle="true" background_crop_position="0.0" background_mesh="menu_empire_1" wait_mesh="wait_empire_town" prosperity="950" />\n    </Components>\n'
    '  </Settlement>\n'
    '  <Settlement id="castle_village_I2_3" name="{=Settlements.Settlement.name.castle_village_I2_3}Bar-noss" posX="665.904" posY="812.459" culture="Culture.isengard">\n'
    '    <Components>\n      <Village id="castle_village_comp_I2_3" village_type="VillageType.trapper" hearth="500" bound="Settlement.castle_I2" background_crop_position="0.0" background_mesh="gui_bg_village_empire" wait_mesh="wait_empire_village" castle_background_mesh="gui_bg_castle_empire" />\n    </Components>\n'
    '  </Settlement>\n'
    '  <!-- LOTHLORIEN -->\n'
    '  <Settlement id="castle_L1" name="{=Settlements.Settlement.name.castle_L1}Cerin Amroth" posX="1.0" posY="2.0" culture="Culture.lothlorien">\n'
    '    <Components>\n      <Town id="castle_comp_L1" is_castle="true" />\n    </Components>\n'
    '  </Settlement>\n'
    '</Settlements>\n'
)

LOC = (
    '<?xml version="1.0" encoding="utf-8"?>\r\r\n'
    '<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" type="string">\r\r\n'
    '  <strings>\r\r\n'
    '    <string id="Settlements.Settlement.name.castle_I2" text="Forthbrond" />\r\r\n'
    '    <string id="Settlements.Settlement.name.castle_village_I2_3" text="Bar-noss" />\r\r\n'
    '    <string id="Settlements.Settlement.text.castle_village_I2_3" text="Ein Dorf." />\r\r\n'
    '    <string id="Settlements.Settlement.name.castle_L1" text="Cerin Amroth" />\r\r\n'
    '  </strings>\r\r\n'
    '</base>\r\r\n'
)

ROW = amv.Village("castle_village_I2_4", "Angroth", "castle_I2", "iron_mine", "isengard", "empire_village_i")
PRESENT = amv.Village("castle_village_I2_3", "Bar-noss", "castle_I2", "trapper", "isengard", "empire_village_t")


class VillageBlockTests(unittest.TestCase):
    def test_block_parses_and_carries_engine_attributes(self):
        block = amv.village_block(ROW, "628.487", "796.123", "\n")
        el = ET.fromstring(block)
        self.assertEqual(el.get("id"), "castle_village_I2_4")
        self.assertEqual(el.get("name"), "{=Settlements.Settlement.name.castle_village_I2_4}Angroth")
        self.assertEqual((el.get("posX"), el.get("posY")), ("628.487", "796.123"))
        self.assertEqual(el.get("culture"), "Culture.isengard")
        self.assertIsNone(el.get("owner"))
        self.assertIsNone(el.get("gate_posX"))
        village = el.find("Components/Village")
        self.assertEqual(village.get("id"), "castle_village_comp_I2_4")
        self.assertEqual(village.get("village_type"), "VillageType.iron_mine")
        self.assertEqual(village.get("hearth"), "500")
        self.assertEqual(village.get("bound"), "Settlement.castle_I2")
        self.assertEqual(village.get("background_crop_position"), "0.0")
        self.assertEqual(village.get("background_mesh"), "gui_bg_village_empire")
        self.assertEqual(village.get("wait_mesh"), "wait_empire_village")
        self.assertEqual(village.get("castle_background_mesh"), "gui_bg_castle_empire")
        self.assertEqual(el.find("Locations").get("complex_template"), "LocationComplexTemplate.village_complex")
        self.assertEqual(el.find("Locations/Location").get("id"), "village_center")
        self.assertEqual(el.find("Locations/Location").get("scene_name"), "empire_village_i")
        self.assertEqual([a.get("type") for a in el.findall("CommonAreas/Area")], ["Pasture", "Thicket", "Bog"])

    def test_block_uses_requested_newline(self):
        block = amv.village_block(ROW, "1", "2", "\r\n")
        self.assertIn("\r\n", block)
        self.assertNotIn("\n", block.replace("\r\n", ""))

    def test_component_id_convention(self):
        self.assertEqual(amv.comp_id("castle_village_isengard_b"), "castle_village_comp_isengard_b")
        self.assertEqual(amv.comp_id("village_isengard_b"), "village_comp_isengard_b")
        self.assertEqual(amv.comp_id("castle_village_I2_4"), "castle_village_comp_I2_4")

    def test_unknown_village_type_raises(self):
        bad = amv.Village("village_x", "X", "town_x", "cattle_range", "isengard", "empire_village_a")
        with self.assertRaises(ValueError):
            amv.village_block(bad, "1", "2", "\n")

    def test_unknown_culture_family_raises(self):
        bad = amv.Village("village_x", "X", "town_x", "wheat_farm", "nosuchculture", "empire_village_a")
        with self.assertRaises(ValueError):
            amv.village_block(bad, "1", "2", "\n")

    def test_shipped_table_is_well_formed(self):
        ids = [v.id for v in amv.VILLAGES]
        names = [v.name for v in amv.VILLAGES]
        self.assertEqual(len(ids), len(set(ids)), "duplicate id in VILLAGES")
        self.assertEqual(len(names), len(set(names)), "duplicate display name in VILLAGES")
        for v in amv.VILLAGES:
            ET.fromstring(amv.village_block(v, "1", "2", "\n"))


class PositionTests(unittest.TestCase):
    def test_scene_position_is_the_transforms_first_two_numbers(self):
        self.assertEqual(amv.scene_position(SCENE, "castle_village_I2_3"), ("665.904", "812.459"))

    def test_scene_position_does_not_prefix_match(self):
        self.assertEqual(amv.scene_position(SCENE, "castle_village_I2_30"), ("1.000", "2.000"))
        self.assertIsNone(amv.scene_position(SCENE, "castle_village_I2_"))

    def test_scene_position_missing_entity_is_none(self):
        self.assertIsNone(amv.scene_position(SCENE, "castle_village_I2_4"))

    def test_scene_position_never_borrows_the_next_entitys_transform(self):
        # An entity with no transform of its own must resolve to None, not to its neighbour's numbers.
        scene = (
            '<game_entity name="village_x" old_prefab_name="" mobility="1">\n'
            '\t<tags>\n\t\t<tag name="village"/>\n\t</tags>\n'
            '</game_entity>\n'
            '<game_entity name="village_y" old_prefab_name="" mobility="1">\n'
            '\t<transform position="100.5, 200.3, 1.0" rotation_euler="0, 0, 0" scale="1, 1, 1"/>\n'
            '</game_entity>\n'
        )
        self.assertIsNone(amv.scene_position(scene, "village_x"))
        self.assertEqual(amv.scene_position(scene, "village_y"), ("100.5", "200.3"))

    def test_scene_position_reads_the_entitys_own_transform_not_a_childs(self):
        scene = (
            '<game_entity name="castle_z" old_prefab_name="" mobility="1">\n'
            '\t<transform position="5.0, 6.0, 7.0" rotation_euler="0, 0, 0" scale="1, 1, 1"/>\n'
            '\t<children>\n'
            '\t\t<game_entity name="castle_z_gate" old_prefab_name="" mobility="1">\n'
            '\t\t\t<transform position="9.0, 9.0, 9.0" rotation_euler="0, 0, 0" scale="1, 1, 1"/>\n'
            '\t\t</game_entity>\n'
            '\t</children>\n'
            '</game_entity>\n'
        )
        self.assertEqual(amv.scene_position(scene, "castle_z"), ("5.0", "6.0"))
        self.assertEqual(amv.scene_position(scene, "castle_z_gate"), ("9.0", "9.0"))

    def test_settlement_position_reads_master(self):
        self.assertEqual(amv.settlement_position(MASTER, "castle_I2"), ("650.491", "818.901"))
        self.assertIsNone(amv.settlement_position(MASTER, "castle_I20"))

    def test_resolve_prefers_scene(self):
        px, py, source = amv.resolve_position(PRESENT, SCENE, MASTER, 0)
        self.assertEqual((px, py, source), ("665.904", "812.459", "scene"))

    def test_resolve_falls_back_to_parent_with_offset(self):
        px, py, source = amv.resolve_position(ROW, SCENE, MASTER, 2)
        self.assertEqual(source, "PLACEHOLDER")
        self.assertAlmostEqual(float(px), 650.491 + 1.5 * 3, places=3)
        self.assertAlmostEqual(float(py), 818.901 + 1.0 * 3, places=3)

    def test_resolve_with_no_parent_raises(self):
        orphan = amv.Village("village_x", "X", "castle_nowhere", "wheat_farm", "isengard", "empire_village_a")
        with self.assertRaises(ValueError):
            amv.resolve_position(orphan, SCENE, MASTER, 0)


class InsertionTests(unittest.TestCase):
    def test_master_insert_lands_after_anchor(self):
        block = amv.village_block(ROW, "1", "2", "\n")
        out = amv.insert_after_settlement(MASTER, "castle_village_I2_3", [block])
        i_anchor = out.index('id="castle_village_I2_3"')
        i_new = out.index('id="castle_village_I2_4"')
        i_next = out.index("<!-- LOTHLORIEN -->")
        self.assertTrue(i_anchor < i_new < i_next)
        ET.fromstring(out)

    def test_master_insert_falls_back_to_end(self):
        block = amv.village_block(ROW, "1", "2", "\n")
        out = amv.insert_after_settlement(MASTER, "castle_nowhere", [block])
        self.assertTrue(out.index('id="castle_village_I2_4"') > out.index('id="castle_L1"'))
        self.assertTrue(out.rstrip().endswith("</Settlements>"))
        ET.fromstring(out)

    def test_loc_insert_after_anchors_last_row_with_files_newline(self):
        out = amv.insert_loc_rows(LOC, "castle_village_I2_3", [("castle_village_I2_4", "Angroth")])
        lines = out.split("\r\r\n")
        i_text = next(i for i, l in enumerate(lines) if "text.castle_village_I2_3" in l)
        i_new = next(i for i, l in enumerate(lines) if "name.castle_village_I2_4" in l)
        self.assertEqual(i_new, i_text + 1)
        self.assertIn('    <string id="Settlements.Settlement.name.castle_village_I2_4" text="Angroth" />', out)
        self.assertNotIn("\n", out.replace("\r\r\n", ""))
        ET.fromstring(out)

    def test_loc_insert_falls_back_before_strings_close(self):
        out = amv.insert_loc_rows(LOC, "castle_nowhere", [("castle_village_I2_4", "Angroth")])
        self.assertTrue(out.index("name.castle_village_I2_4") < out.index("</strings>"))
        self.assertTrue(out.index("name.castle_village_I2_4") > out.index("name.castle_L1"))
        ET.fromstring(out)

    def test_loc_text_is_xml_escaped(self):
        out = amv.insert_loc_rows(LOC, "castle_village_I2_3", [("village_x", 'A "quoted" & name')])
        el = ET.fromstring(out)
        self.assertEqual(el.find(".//string[@id='Settlements.Settlement.name.village_x']").get("text"), 'A "quoted" & name')

    def test_detect_newline(self):
        self.assertEqual(amv.detect_newline(LOC), "\r\r\n")
        self.assertEqual(amv.detect_newline("a\r\nb\r\n"), "\r\n")
        self.assertEqual(amv.detect_newline(MASTER), "\n")

    def test_already_present_ids_are_skipped(self):
        self.assertEqual(amv.missing_ids(MASTER, [PRESENT, ROW]), [ROW])

    def test_loc_rows_are_planned_per_language_independently_of_the_master(self):
        # A master-present / loc-missing state (a crashed earlier run, a reverted language file) must be
        # repairable by a plain re-run: the plan comes from the whole table, per language.
        loc_without = LOC.replace('    <string id="Settlements.Settlement.name.castle_village_I2_3" text="Bar-noss" />\r\r\n', "")
        plan = amv.plan_loc_rows([PRESENT, ROW], {"DE": LOC, "FR": loc_without})
        self.assertEqual(plan["DE"], [("castle_village_I2_4", "Angroth")])
        self.assertEqual(plan["FR"], [("castle_village_I2_3", "Bar-noss"), ("castle_village_I2_4", "Angroth")])

    def test_nothing_to_do_only_when_master_and_every_language_are_complete(self):
        self.assertTrue(amv.nothing_to_do([PRESENT], MASTER, {"DE": LOC}))
        loc_without = LOC.replace('    <string id="Settlements.Settlement.name.castle_village_I2_3" text="Bar-noss" />\r\r\n', "")
        self.assertFalse(amv.nothing_to_do([PRESENT], MASTER, {"DE": LOC, "FR": loc_without}))
        self.assertFalse(amv.nothing_to_do([PRESENT, ROW], MASTER, {"DE": LOC}))


class CheckTests(unittest.TestCase):
    def test_check_clean(self):
        self.assertEqual(amv.check([PRESENT], MASTER, SCENE, {"DE": LOC}), [])

    def test_check_names_missing_master_row(self):
        findings = amv.check([ROW], MASTER, SCENE, {"DE": LOC})
        self.assertTrue(any("castle_village_I2_4" in f and "settlements.xml" in f for f in findings))

    def test_check_names_missing_loc_row(self):
        loc_without = LOC.replace('    <string id="Settlements.Settlement.name.castle_village_I2_3" text="Bar-noss" />\r\r\n', "")
        findings = amv.check([PRESENT], MASTER, SCENE, {"DE": LOC, "FR": loc_without})
        self.assertEqual(len(findings), 1)
        self.assertIn("FR", findings[0])

    def test_check_names_drifted_position(self):
        drifted = MASTER.replace('posX="665.904" posY="812.459"', 'posX="600.000" posY="812.459"')
        findings = amv.check([PRESENT], drifted, SCENE, {"DE": LOC})
        self.assertEqual(len(findings), 1)
        self.assertIn("665.904", findings[0])

    def test_check_tolerates_float_formatting(self):
        # The editor writes float.ToString(), so "665.904" and "665.9040" are the same position.
        reformatted = MASTER.replace('posX="665.904"', 'posX="665.9040"')
        self.assertEqual(amv.check([PRESENT], reformatted, SCENE, {"DE": LOC}), [])

    def test_check_names_entity_missing_from_scene(self):
        master = MASTER.replace("castle_village_I2_3", "castle_village_I2_4")
        loc = LOC.replace("castle_village_I2_3", "castle_village_I2_4")
        findings = amv.check([ROW], master, SCENE, {"DE": loc})
        self.assertEqual(len(findings), 1)
        self.assertIn("scene.xscene", findings[0])


if __name__ == "__main__":
    unittest.main()
