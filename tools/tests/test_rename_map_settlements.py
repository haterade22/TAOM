#!/usr/bin/env python3
"""Unit tests for tools/rename_map_settlements.py (#597).

Run:  python -m unittest tools.tests.test_rename_map_settlements
  or:  python tools/tests/test_rename_map_settlements.py

Pure stdlib over synthetic text; no game install needed. Each test pins one part of the contract:
  - a rename rewrites the settlement id, its name/text keys, its component id, its bound and its
    position, and nothing outside its own block
  - the old id may not survive anywhere in the renamed block (a half-rename fails loud)
  - a removal takes the whole block including its line terminator, and refuses a fortification
    that still has bound villages
  - loc renames keep the translated text; loc removals take the row and its terminator
  - the repo JSON rename is exact-token, so village_X_1 never touches village_X_10
  - the repo reference sweep finds an id the script would otherwise leave dangling
  - id_state distinguishes pending / done / conflict / missing
  - --check names a missing new row, a surviving old id, a missing entity, a drifted position
"""
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import rename_map_settlements as rms  # noqa: E402

NL = "\r\n"
MASTER = NL.join([
    '<?xml version="1.0" encoding="utf-8"?>',
    '<Settlements>',
    '  <Settlement id="castle_EW7" name="{=Settlements.Settlement.name.castle_EW7}Bar-en-Siril" owner="Faction.clan_empire_west_8" posX="599.329" posY="673.457" culture="Culture.gondor" gate_posX="602.2825" gate_posY="674.326">',
    '    <Components>',
    '      <Town id="castle_comp_EW7" is_castle="true" background_crop_position="0.0" background_mesh="menu_empire_1" wait_mesh="wait_empire_town" prosperity="980" />',
    '    </Components>',
    '  </Settlement>',
    '  <Settlement id="castle_village_EW7_3" name="{=Settlements.Settlement.name.castle_village_EW7_3}West Bridge Farm" posX="538.163" posY="626.14" culture="Culture.gondor" text="{=Settlements.Settlement.text.castle_village_EW7_3}A farm.">',
    '    <Components>',
    '      <Village id="castle_village_comp_EW7_3" village_type="VillageType.cattle_farm" hearth="603" bound="Settlement.castle_EW7" background_crop_position="0.0" background_mesh="gui_bg_village_empire" wait_mesh="wait_empire_village" castle_background_mesh="gui_bg_castle_empire" />',
    '    </Components>',
    '    <Locations complex_template="LocationComplexTemplate.village_complex">',
    '      <Location id="village_center" scene_name="empire_village_j" />',
    '    </Locations>',
    '  </Settlement>',
    '  <Settlement id="castle_village_EW7_30" name="{=Settlements.Settlement.name.castle_village_EW7_30}Decoy" posX="1.0" posY="2.0" culture="Culture.gondor">',
    '    <Components>',
    '      <Village id="castle_village_comp_EW7_30" village_type="VillageType.vineyard" hearth="603" bound="Settlement.castle_EW7" background_crop_position="0.0" background_mesh="gui_bg_village_empire" wait_mesh="wait_empire_village" castle_background_mesh="gui_bg_castle_empire" />',
    '    </Components>',
    '  </Settlement>',
    '  <Settlement id="town_EW10" name="{=Settlements.Settlement.name.town_EW10}Serelond" owner="Faction.clan_empire_west_8" posX="601.13" posY="639.68" culture="Culture.gondor" gate_posX="601.1554" gate_posY="640.6436">',
    '    <Components>',
    '      <Town id="town_comp_EW10" is_castle="false" background_crop_position="0.0" background_mesh="menu_empire_3" wait_mesh="wait_empire_town" prosperity="3500" />',
    '    </Components>',
    '  </Settlement>',
    '  <Settlement id="hideout_desert_34" name="{=Settlements.Settlement.name.hideout_desert_34}Haradrim Raider\'s Camp" type="Hideout" posX="806.846" posY="581.391" culture="Culture.harad_raiders">',
    '    <Components>',
    '      <Hideout id="hideout_desert_34" map_icon="bandit_hideout_a" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_desert" gate_rotation="0.0" />',
    '    </Components>',
    '    <Locations complex_template="LocationComplexTemplate.hideout_complex">',
    '      <Location id="hideout_center" scene_name="desert_hideout_002_sv" />',
    '    </Locations>',
    '  </Settlement>',
    '  <Settlement id="hideout_desert_35" name="{=Settlements.Settlement.name.hideout_desert_35}Haradrim Raider\'s Camp" type="Hideout" posX="824.0" posY="588.993" culture="Culture.harad_raiders">',
    '    <Components>',
    '      <Hideout id="hideout_desert_35" map_icon="bandit_hideout_a" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_desert" gate_rotation="0.0" />',
    '    </Components>',
    '  </Settlement>',
    '</Settlements>',
    '',
])

LNL = "\r\r\n"
LOC = LNL.join([
    '<?xml version="1.0" encoding="utf-8"?>',
    '<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" type="string">',
    '  <strings>',
    '    <string id="Settlements.Settlement.name.castle_EW7" text="Bar-en-Siril" />',
    '    <string id="Settlements.Settlement.name.castle_village_EW7_3" text="Westbrücken-Hof" />',
    '    <string id="Settlements.Settlement.text.castle_village_EW7_3" text="Ein Hof." />',
    '    <string id="Settlements.Settlement.name.castle_village_EW7_30" text="Decoy" />',
    '    <string id="Settlements.Settlement.name.hideout_desert_34" text="Lager der Haradrim-Räuber" />',
    '    <string id="Settlements.Settlement.name.hideout_desert_35" text="Lager der Haradrim-Räuber" />',
    '  </strings>',
    '</base>',
    '',
])

SCENE = (
    '<scene>\n'
    '\t\t<game_entity name="village_EW10_1" old_prefab_name="" mobility="1">\n'
    '\t\t\t<tags>\n\t\t\t\t<tag name="village"/>\n\t\t\t</tags>\n'
    '\t\t\t<transform position="539.713, 622.817, 50.404" rotation_euler="0.000, 0.000, 2.182" scale="1.000, 1.000, 1.250"/>\n'
    '\t\t</game_entity>\n'
    '\t\t<game_entity name="hideout_desert_35" old_prefab_name="" mobility="1">\n'
    '\t\t\t<transform position="824.000, 588.993, 47.856" rotation_euler="0, 0, 0" scale="1, 1, 1"/>\n'
    '\t\t</game_entity>\n'
    '</scene>\n'
)

JSON = '{"settlements": ["castle_EW7", "castle_village_EW7_3", "castle_village_EW7_30"]}\n'

RENAME = rms.Rename("castle_village_EW7_3", "village_EW10_1", "town_EW10")


class BlockSpanTests(unittest.TestCase):
    def test_span_covers_open_tag_to_close_tag_and_its_terminator(self):
        start, end = rms.settlement_block_span(MASTER, "castle_village_EW7_3")
        block = MASTER[start:end]
        self.assertTrue(block.startswith('  <Settlement id="castle_village_EW7_3"'))
        self.assertTrue(block.endswith("</Settlement>" + NL))
        self.assertNotIn("castle_village_EW7_30", block)
        self.assertNotIn("town_EW10", block)

    def test_span_does_not_prefix_match(self):
        start, end = rms.settlement_block_span(MASTER, "castle_village_EW7_30")
        self.assertIn('id="castle_village_comp_EW7_30"', MASTER[start:end])
        self.assertNotIn('id="castle_village_comp_EW7_3"', MASTER[start:end])

    def test_span_missing_is_none(self):
        self.assertIsNone(rms.settlement_block_span(MASTER, "castle_village_EW7_4"))


class RenameTests(unittest.TestCase):
    def test_rename_rewrites_every_id_bearing_attribute_and_the_bound(self):
        out = rms.rename_in_master(MASTER, RENAME, ("539.713", "622.817"))
        ET.fromstring(out)
        start, end = rms.settlement_block_span(out, "village_EW10_1")
        block = out[start:end]
        self.assertIn('name="{=Settlements.Settlement.name.village_EW10_1}West Bridge Farm"', block)
        self.assertIn('text="{=Settlements.Settlement.text.village_EW10_1}A farm."', block)
        self.assertIn('<Village id="village_comp_EW10_1"', block)
        self.assertIn('bound="Settlement.town_EW10"', block)
        self.assertIn('posX="539.713" posY="622.817"', block)
        self.assertIn('village_type="VillageType.cattle_farm" hearth="603"', block)
        self.assertNotIn("castle_village_EW7_3", block)
        self.assertNotIn("castle_EW7", block)

    def test_rename_leaves_every_other_block_byte_identical(self):
        out = rms.rename_in_master(MASTER, RENAME, ("539.713", "622.817"))
        for sid in ("castle_EW7", "castle_village_EW7_30", "town_EW10", "hideout_desert_34", "hideout_desert_35"):
            s0, e0 = rms.settlement_block_span(MASTER, sid)
            s1, e1 = rms.settlement_block_span(out, sid)
            self.assertEqual(MASTER[s0:e0], out[s1:e1], sid)
        self.assertIsNone(rms.settlement_block_span(out, "castle_village_EW7_3"))

    def test_rename_keeps_position_when_no_scene_position_is_given(self):
        out = rms.rename_in_master(MASTER, RENAME, None)
        start, end = rms.settlement_block_span(out, "village_EW10_1")
        self.assertIn('posX="538.163" posY="626.14"', out[start:end])

    def test_rename_keeps_the_files_newline(self):
        out = rms.rename_in_master(MASTER, RENAME, ("539.713", "622.817"))
        self.assertNotIn("\n", out.replace(NL, ""))

    def test_rename_of_a_missing_id_raises(self):
        with self.assertRaises(ValueError):
            rms.rename_in_master(MASTER, rms.Rename("castle_village_EW7_4", "village_EW10_9", "town_EW10"), None)

    def test_rename_refuses_a_block_that_still_carries_the_old_id(self):
        # A settlement whose block mentions its own id somewhere the rewrite does not know about
        # must fail loud rather than ship a half-rename the engine would read as two settlements.
        odd = MASTER.replace('scene_name="empire_village_j"', 'scene_name="castle_village_EW7_3_scene"')
        with self.assertRaises(ValueError):
            rms.rename_in_master(odd, RENAME, None)


class RemovalTests(unittest.TestCase):
    def test_remove_takes_the_whole_block_and_its_terminator(self):
        out = rms.remove_from_master(MASTER, "hideout_desert_34")
        ET.fromstring(out)
        self.assertNotIn("hideout_desert_34", out)
        self.assertIn('id="hideout_desert_35"', out)
        self.assertEqual(out.count("<Settlement "), MASTER.count("<Settlement ") - 1)
        # the block before and the block after now touch, with exactly one terminator between them
        self.assertIn("</Settlement>" + NL + '  <Settlement id="hideout_desert_35"', out)

    def test_remove_of_a_missing_id_is_identity(self):
        self.assertEqual(rms.remove_from_master(MASTER, "hideout_desert_99"), MASTER)

    def test_bound_dependents_lists_villages_of_a_fortification(self):
        self.assertEqual(rms.bound_dependents(MASTER, "castle_EW7"),
                         ["castle_village_EW7_3", "castle_village_EW7_30"])
        self.assertEqual(rms.bound_dependents(MASTER, "town_EW10"), [])
        self.assertEqual(rms.bound_dependents(MASTER, "hideout_desert_34"), [])

    def test_remove_refuses_a_fortification_with_bound_villages(self):
        with self.assertRaises(ValueError):
            rms.remove_from_master(MASTER, "castle_EW7")


class LocTests(unittest.TestCase):
    def test_loc_rename_keeps_translation_and_newline(self):
        out = rms.loc_rename(LOC, "castle_village_EW7_3", "village_EW10_1")
        ET.fromstring(out)
        self.assertIn('<string id="Settlements.Settlement.name.village_EW10_1" text="Westbrücken-Hof" />', out)
        self.assertIn('<string id="Settlements.Settlement.text.village_EW10_1" text="Ein Hof." />', out)
        self.assertNotIn("castle_village_EW7_3\"", out)
        self.assertIn('name.castle_village_EW7_30" text="Decoy"', out)
        self.assertNotIn("\n", out.replace(LNL, ""))

    def test_loc_remove_takes_the_row_and_its_terminator(self):
        out = rms.loc_remove(LOC, "hideout_desert_34")
        ET.fromstring(out)
        self.assertNotIn("hideout_desert_34", out)
        self.assertIn("hideout_desert_35", out)
        self.assertEqual(out.count(LNL), LOC.count(LNL) - 1)

    def test_loc_remove_of_a_missing_id_is_identity(self):
        self.assertEqual(rms.loc_remove(LOC, "hideout_desert_99"), LOC)


class RepoTests(unittest.TestCase):
    def test_json_rename_is_exact_token(self):
        out, n = rms.rename_json_ids(JSON, "castle_village_EW7_3", "village_EW10_1")
        self.assertEqual(n, 1)
        self.assertIn('"village_EW10_1"', out)
        self.assertIn('"castle_village_EW7_30"', out)
        self.assertNotIn('"castle_village_EW7_3"', out)

    def test_repo_references_finds_ids_in_cs_and_json_by_exact_token(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "Main" / "Features").mkdir(parents=True)
            (root / "Main" / "_Module" / "ModuleData" / "pools").mkdir(parents=True)
            (root / "Main" / "Features" / "A.cs").write_text('var x = "hideout_desert_34";\n', encoding="utf-8")
            (root / "Main" / "Features" / "B.cs").write_text('var y = "hideout_desert_340";\n', encoding="utf-8")
            (root / "Main" / "_Module" / "ModuleData" / "pools" / "p.json").write_text('["castle_village_EW7_3"]', encoding="utf-8")
            (root / "Main" / "_Module" / "ModuleData" / "settlements.xml").write_text('<Settlement id="hideout_desert_34"/>', encoding="utf-8")
            hits = rms.repo_references(root, ["hideout_desert_34", "castle_village_EW7_3", "castle_village_EW7_2"])
        self.assertEqual(hits["hideout_desert_34"], ["Main/Features/A.cs"])
        self.assertEqual(hits["castle_village_EW7_3"], ["Main/_Module/ModuleData/pools/p.json"])
        self.assertEqual(hits["castle_village_EW7_2"], [])


class StateAndCheckTests(unittest.TestCase):
    def test_id_state(self):
        self.assertEqual(rms.id_state(MASTER, "castle_village_EW7_3", "village_EW10_1"), "pending")
        done = rms.rename_in_master(MASTER, RENAME, None)
        self.assertEqual(rms.id_state(done, "castle_village_EW7_3", "village_EW10_1"), "done")
        self.assertEqual(rms.id_state(MASTER, "castle_village_EW7_3", "castle_village_EW7_30"), "conflict")
        self.assertEqual(rms.id_state(MASTER, "castle_village_EW7_4", "village_EW10_9"), "missing")

    def _done_world(self):
        master = rms.rename_in_master(MASTER, RENAME, ("539.713", "622.817"))
        master = rms.remove_from_master(master, "hideout_desert_34")
        loc = rms.loc_remove(rms.loc_rename(LOC, "castle_village_EW7_3", "village_EW10_1"), "hideout_desert_34")
        return master, loc

    def test_check_clean(self):
        master, loc = self._done_world()
        self.assertEqual(rms.check([RENAME], ["hideout_desert_34"], master, SCENE, {"DE": loc}), [])

    def test_check_names_a_pending_rename(self):
        findings = rms.check([RENAME], [], MASTER, SCENE, {"DE": LOC})
        self.assertTrue(any("village_EW10_1" in f and "settlements.xml" in f for f in findings))
        self.assertTrue(any("castle_village_EW7_3" in f and "still" in f for f in findings))

    def test_check_names_a_surviving_loc_key_and_a_missing_new_loc_row(self):
        master, loc = self._done_world()
        findings = rms.check([RENAME], [], master, SCENE, {"DE": loc, "FR": LOC})
        self.assertTrue(any("FR" in f and "village_EW10_1" in f for f in findings))
        self.assertTrue(any("FR" in f and "castle_village_EW7_3" in f for f in findings))
        self.assertFalse(any("DE" in f for f in findings))

    def test_check_names_a_missing_entity_and_a_drifted_position(self):
        master, loc = self._done_world()
        no_entity = SCENE.replace('name="village_EW10_1"', 'name="village_EW10_1_gone"')
        self.assertTrue(any("scene.xscene" in f for f in rms.check([RENAME], [], master, no_entity, {"DE": loc})))
        drifted = master.replace('posX="539.713" posY="622.817"', 'posX="500.000" posY="622.817"')
        self.assertTrue(any("539.713" in f for f in rms.check([RENAME], [], drifted, SCENE, {"DE": loc})))

    def test_check_names_a_surviving_removal_in_master_loc_or_scene(self):
        master, loc = self._done_world()
        scene_with = SCENE.replace('name="hideout_desert_35"', 'name="hideout_desert_34"')
        findings = rms.check([], ["hideout_desert_34"], MASTER, scene_with, {"DE": LOC})
        self.assertTrue(any("settlements.xml" in f for f in findings))
        self.assertTrue(any("DE" in f for f in findings))
        self.assertTrue(any("scene.xscene" in f for f in findings))
        self.assertEqual(rms.check([], ["hideout_desert_34"], master, SCENE, {"DE": loc}), [])


class ShippedTableTests(unittest.TestCase):
    def test_tables_are_well_formed(self):
        olds = [r.old for r in rms.RENAMES]
        news = [r.new for r in rms.RENAMES]
        self.assertEqual(len(set(olds)), len(olds))
        self.assertEqual(len(set(news)), len(news))
        self.assertFalse(set(olds) & set(news))
        self.assertFalse(set(rms.REMOVALS) & (set(olds) | set(news)))
        for r in rms.RENAMES:
            self.assertTrue(r.new.startswith(("village_", "castle_village_")), r)
            self.assertTrue(r.bound.startswith(("town_", "castle_")), r)


if __name__ == "__main__":
    unittest.main()
