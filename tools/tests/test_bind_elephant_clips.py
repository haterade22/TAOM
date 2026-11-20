#!/usr/bin/env python3
"""Tests for tools/bind_elephant_clips.py (#1474): the text change to action_sets.xml and the cache gate.

Run:  python -m unittest tools.tests.test_bind_elephant_clips
"""
import os
import struct
import sys
import tempfile
import unittest
import uuid
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import bind_elephant_clips as b  # noqa: E402

def _sets(extra_rows=30):
    rows = ['\t\t<action type="act_elephant_stand_for_movement_data" animation="elephant_stand_for_movement_data" />',
            '\t\t<action type="act_elephant_stand_1" animation="elephant_stand_2" alternative_group="g" />',
            '\t\t<action type="act_elephant_stand_2" animation="elephant_stand_2" alternative_group="g" />',
            '\t\t<action type="act_elephant_stand_3" animation="elephant_stand_3" alternative_group="g" />',
            '\t\t<action type="act_elephant_forward_walk_stand" animation="elephant_stand_2" />',
            '\t\t<action type="act_elephant_forward_walk" animation="elephant_walk" />',
            '\t\t<action type="act_elephant_forward_trot" animation="elephant_walk" />',
            '\t\t<action type="act_elephant_trot_turn_left_head" animation="elephant_walk" />',
            '\t\t<action type="act_elephant_forward_canter" animation="elephant_canter" />',
            '\t\t<action type="act_elephant_canter_turn_left_head" animation="elephant_canter_left" />',
            '\t\t<action type="act_elephant_forward_gallop_left_foot" animation="elephant_gallop" />',
            '\t\t<action type="act_elephant_backward_walk" animation="elephant_walk_backwards" />',
            '\t\t<action type="act_elephant_turn_left" animation="elephant_turn_left" />',
            '\t\t<action type="act_elephant_turn_right" animation="elephant_turn_right" />',
            '\t\t<action type="act_elephant_attack_1"',
            '\t\t\tanimation="elephant_attack_1" />']
    rows += ['\t\t<action type="act_elephant_strafe_%d" animation="elephant_walk" />' % i for i in range(extra_rows)]
    lines = (['﻿<?xml version="1.0" encoding="utf-8"?>', "<action_sets>",
              '\t<action_set id="as_other" skeleton="x"><action type="act_x" animation="elephant_walk" /></action_set>',
              '\t<action_set id="as_elephant" skeleton="elephant_skeleton" movement_system="quadrupedal">'] + rows
             + ["\t</action_set>",
                '\t<action_set id="as_elephant_town_and_village" skeleton="elephant_skeleton" base_set="as_elephant">',
                '\t\t<action type="act_elephant_stand_1" animation="elephant_stand_3"/>', "\t</action_set>",
                '\t<action_set id="as_elephant_map" skeleton="elephant_skeleton" base_set="as_elephant">',
                '\t\t<action type="act_map_mount_attack_1h" animation="elephant_stand_3"/>', "\t</action_set>",
                "\t<!-- /ELEPHANT -->", "</action_sets>", ""])
    return "\r\n".join(lines)


def _tree(text):
    root = ET.fromstring(text.encode("utf-8").lstrip(b"\xef\xbb\xbf"))
    return {s.get("id"): {a.get("type"): a.get("animation") for a in s} for s in root.iter("action_set")}


class RoleTests(unittest.TestCase):
    def test_a_trot_action_on_the_walk_clip_takes_the_trot(self):
        self.assertEqual(b.role_of("act_elephant_forward_trot_strafe_left", "elephant_walk"), "trot")
        self.assertEqual(b.role_of("act_elephant_trot_turn_left_head", "elephant_walk"), "trot")
        self.assertEqual(b.role_of("act_elephant_forward_walk", "elephant_walk"), "walk")
        self.assertEqual(b.role_of("act_elephant_forward_walk_strafe_left", "elephant_walk"), "walk")

    def test_the_two_standing_strafe_actions_take_the_strafe_clip(self):
        # the engine plays a standing-pace strafe clip in 0.7 to 1.3 times its Duration, so it needs its own record
        for action in ("act_elephant_strafe_right", "act_elephant_strafe_left"):
            self.assertEqual(b.role_of(action, "elephant_walk"), "strafe")
            self.assertEqual(b.role_of(action, "mumakil_walk"), "strafe")      # bound on the walk on 2026-11-19
            self.assertEqual(b.role_of(action, "war_elephant_strafe"), "strafe")
        row = '<action type="act_elephant_strafe_right" animation="mumakil_walk" />'
        self.assertIn('animation="mumakil_strafe"', b.rebind(row, "mumakil_"))

    def test_only_the_first_two_idle_actions_take_an_idle(self):
        self.assertEqual(b.role_of("act_elephant_stand_1", "elephant_stand_2"), "idle_1")
        self.assertEqual(b.role_of("act_elephant_stand_2", "elephant_stand_2"), "idle_2")
        self.assertIsNone(b.role_of("act_elephant_stand_3", "elephant_stand_3"))
        self.assertIsNone(b.role_of("act_elephant_forward_walk_stand", "elephant_stand_2"))

    def test_rows_this_tool_does_not_own_are_left_alone(self):
        for action, clip in (("act_elephant_attack_1", "elephant_attack_1"), ("act_elephant_fall_left", "elephant_death"),
                             ("act_elephant_canter_turn_left_head", "elephant_canter_left"),
                             ("act_elephant_quick_stop", "elephant_stand_for_movement_data")):
            self.assertIsNone(b.role_of(action, clip))

    def test_a_bound_row_keeps_its_role(self):
        self.assertEqual(b.role_of("act_elephant_forward_trot", "war_elephant_trot"), "trot")
        self.assertEqual(b.role_of("act_elephant_forward_walk", "mumakil_walk"), "walk")


class PlanTests(unittest.TestCase):
    def setUp(self):
        self.old = _sets()
        self.new, self.info = b.plan_action_sets(self.old)
        self.sets = _tree(self.new)

    def test_the_elephant_takes_its_own_clips_and_keeps_every_other_row(self):
        e = self.sets["as_elephant"]
        self.assertEqual(e["act_elephant_forward_walk"], "war_elephant_walk")
        self.assertEqual(e["act_elephant_forward_trot"], "war_elephant_trot")
        self.assertEqual(e["act_elephant_forward_canter"], "war_elephant_canter")
        self.assertEqual(e["act_elephant_forward_gallop_left_foot"], "war_elephant_gallop")
        self.assertEqual(e["act_elephant_backward_walk"], "war_elephant_walk_backwards")
        self.assertEqual((e["act_elephant_turn_left"], e["act_elephant_turn_right"]), ("war_elephant_turn_left", "war_elephant_turn_right"))
        self.assertEqual((e["act_elephant_stand_1"], e["act_elephant_stand_2"]), ("war_elephant_idle_1", "war_elephant_idle_2"))
        self.assertEqual(e["act_elephant_stand_3"], "elephant_stand_3")
        self.assertEqual(e["act_elephant_forward_walk_stand"], "elephant_stand_2")
        self.assertEqual(e["act_elephant_attack_1"], "elephant_attack_1")
        self.assertEqual(e["act_elephant_canter_turn_left_head"], "elephant_canter_left")

    def test_the_mumak_set_is_the_elephant_set_on_mumak_clips(self):
        e, m = self.sets["as_elephant"], self.sets["as_mumakil"]
        self.assertEqual(list(e), list(m))
        for action, clip in e.items():
            self.assertEqual(m[action], clip.replace("war_elephant_", "mumakil_"))
        root = ET.fromstring(self.new.encode("utf-8").lstrip(b"\xef\xbb\xbf"))
        mum = [s for s in root.iter("action_set") if s.get("id") == "as_mumakil"][0]
        self.assertEqual((mum.get("movement_system"), mum.get("base_set")), ("quadrupedal", None))

    def test_the_mumak_gets_both_child_sets_on_its_own_root(self):
        root = ET.fromstring(self.new.encode("utf-8").lstrip(b"\xef\xbb\xbf"))
        by_id = {s.get("id"): s for s in root.iter("action_set")}
        for child in ("as_mumakil_town_and_village", "as_mumakil_map"):
            self.assertEqual(by_id[child].get("base_set"), "as_mumakil")
        self.assertEqual(self.sets["as_mumakil_town_and_village"], self.sets["as_elephant_town_and_village"])
        self.assertEqual(by_id["as_elephant_map"].get("base_set"), "as_elephant")

    def test_other_sets_bytes_line_ends_and_bom_are_kept(self):
        self.assertEqual(self.sets["as_other"], {"act_x": "elephant_walk"})
        self.assertEqual(self.new.count("\r\n"), self.new.count("\n"))
        self.assertEqual(self.new[0], "﻿")
        self.assertTrue(self.new.endswith("\t<!-- /ELEPHANT -->\r\n</action_sets>\r\n"))

    def test_a_second_run_changes_nothing(self):
        again, info = b.plan_action_sets(self.new)
        self.assertEqual(again, self.new)
        self.assertFalse(info["changed"])
        b.check_tree(again)

    def test_a_later_change_to_the_elephant_set_reaches_the_mumak_set(self):
        edited = self.new.replace('<action type="act_elephant_attack_1"', '<action type="act_elephant_new" animation="elephant_hit" />\r\n\t\t<action type="act_elephant_attack_1"', 1)
        again, _ = b.plan_action_sets(edited)
        self.assertEqual(_tree(again)["as_mumakil"]["act_elephant_new"], "elephant_hit")
        self.assertEqual(again.count('id="as_mumakil"'), 1)
        b.check_tree(again)

    def test_the_bound_clips_are_the_eighteen_new_names_in_use(self):
        clips = b.clips_bound(self.new)
        self.assertEqual(len(clips), 18)
        self.assertIn("mumakil_idle_2", clips)
        self.assertNotIn("elephant_walk", clips)

    def test_refuses_a_file_without_the_elephant_sets_or_with_too_few_rows(self):
        with self.assertRaises(b.BindError):
            b.plan_action_sets(self.old.replace('id="as_elephant_map"', 'id="as_gone"'))
        with self.assertRaises(b.BindError):
            b.plan_action_sets(_sets(extra_rows=2))

    def test_check_tree_refuses_a_root_level_action(self):
        bad = self.new.replace("</action_sets>", '<action type="act_x" animation="y" /></action_sets>')
        with self.assertRaises(b.BindError):
            b.check_tree(bad)


class MonsterTests(unittest.TestCase):
    def test_the_mumak_monster_takes_its_own_set_and_the_comment_keeps_its_words(self):
        old = '<!-- shares action_set="as_elephant" -->\r\n<Monsters>\r\n\t<Monster\r\n\t\tid="taom_mumakil"\r\n\t\taction_set="as_elephant"\r\n\t\tmonster_usage="elephant" />\r\n</Monsters>'
        new = b.plan_monster(old)
        self.assertIn('id="taom_mumakil"\r\n\t\taction_set="as_mumakil"', new)
        self.assertIn('<!-- shares action_set="as_elephant" -->', new)
        self.assertEqual(b.plan_monster(new), new)

    def test_refuses_a_monster_file_that_names_neither_set(self):
        with self.assertRaises(b.BindError):
            b.plan_monster('<Monsters><Monster id="taom_mumakil" action_set="as_horse" /></Monsters>')


class ClipGateTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.clips = os.path.join(self.dir.name, *b.CLIPS.split("/"))
        os.makedirs(self.clips)
        os.makedirs(os.path.join(self.dir.name, "RuntimeDataCache"))

    def _clip(self, name, cooked):
        guid = uuid.uuid4()
        anm = os.path.join(self.clips, name + "_anm.tpac")
        with open(anm, "wb") as fh:
            fh.write(b"TPAC" + struct.pack("<I", 2) + guid.bytes_le)
        geo = os.path.join(self.clips, name + "_geo.tpac")
        with open(geo, "wb") as fh:
            fh.write(b"master")
        os.utime(anm, (1_000_000, 1_000_000))
        os.utime(geo, (1_000_000, 1_000_000))
        if cooked:
            entry = os.path.join(self.dir.name, "RuntimeDataCache", str(guid) + ".rdc")
            with open(entry, "wb") as fh:
                fh.write(b"RDC0")
            os.utime(entry, (cooked, cooked))

    def test_a_cooked_clip_passes_and_the_three_ways_to_fail_are_named(self):
        self._clip("mumakil_walk", 2_000_000)
        self._clip("mumakil_trot", None)
        self._clip("mumakil_canter", 500_000)
        got = b.clip_problems(self.dir.name, ["mumakil_walk", "mumakil_trot", "mumakil_canter", "mumakil_gallop"])
        self.assertEqual(len(got), 3)
        self.assertIn("mumakil_trot: it has no cache entry", got[0])
        self.assertIn("older than its master", got[1])
        self.assertIn("mumakil_gallop: no clip record or no master", got[2])


if __name__ == "__main__":
    unittest.main()
