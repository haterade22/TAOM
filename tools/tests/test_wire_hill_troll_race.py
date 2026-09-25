#!/usr/bin/env python3
"""Unit tests for tools/wire_hill_troll_race.py (synthetic XML, no install needed).

Run:  python -m unittest tools.tests.test_wire_hill_troll_race
Pins: every hill_troll skin gets the troll skeleton and meshes and the adult male's bald / clean-shaven / brow-less
lists; face textures outside comments point at the troll head material; nothing outside the race changes; the
Monster gets the measured sizes, CanRide off, and its variants the <race>_<suffix> names the engine looks up;
the warrior action set becomes standalone on the troll skeleton; a second run changes nothing; a missing anchor is
refused; --check also fails an empty set, a set the binder never bound and one without the Brute Force binding.
"""
import contextlib
import io
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import wire_hill_troll_race as w  # noqa: E402

SKINS = """<?xml version="1.0" encoding="utf-8"?>
<skins>
\t<race id="dwarf">
\t\t<skin gender="0" name="man" skeleton="dwarf_skeleton_a" body_meta_mesh="d" body_meta_mesh_shoulders="d_sh" legs_mesh="d_l" hands_mesh="d_h" face_meta_mesh="d_f" underwear_bottom_mesh="" underwear_top_mesh="">
\t\t\t<face_textures group_id="1"><face_texture name="m_dwarf" lod_material="m_dwarf" /></face_textures>
\t\t</skin>
\t</race>
\t<race id="hill_troll">
\t\t<skin gender="0" name="man" min_scale="1.09" skeleton="troll_skeleton"
\t\t\tbody_meta_mesh="mordor_hill_troll" body_meta_mesh_shoulders="body_male_a_sh" body_meta_mesh_upperbody="box_a"
\t\t\tlegs_mesh="mordor_hill_troll_feet" hands_mesh="mordor_hill_troll_hands" face_meta_mesh="mordor_hill_troll_head"
\t\t\tunderwear_bottom_mesh="" underwear_top_mesh="">
\t\t\t<hair_meshes group_id="7"><hair_mesh><style_tags><style_tag name="Bald" /></style_tags></hair_mesh></hair_meshes>
\t\t\t<eyebrow_meshes><eyebrow_mesh name="" /></eyebrow_meshes>
\t\t\t<beard_meshes><beard_mesh><style_tags><style_tag name="Cleanshaven" /></style_tags></beard_mesh></beard_meshes>
\t\t\t<face_textures group_id="1">
\t\t\t\t<face_texture name="mordor_hill_troll_head" lod_material="mordor_hill_troll_head" tags="t1" />
\t\t\t\t<!-- <face_texture name="head_male_e" lod_material="head_male_e" tags="t2" /> -->
\t\t\t</face_textures>
\t\t\t<mouth_textures group_id="4">
\t\t\t\t<mouth_texture
\t\t\t\t\tname="t_hilltroll_mouth"
\t\t\t\t\tlod_material="t_hilltroll_mouth"
\t\t\t\t\tcolor="0xFFFFFFFF"
\t\t\t\t\ttags="mouth_texture1,mouth_texture2"></mouth_texture>
\t\t\t\t<!-- <mouth_texture name="mouth_mat" lod_material="mouth_mat" tags="mouth_texture3" /> -->
\t\t\t</mouth_textures>
\t\t</skin>
\t\t<skin gender="1" name="kid_2_female" skeleton="human_skeleton" body_meta_mesh="body_female_a" body_meta_mesh_shoulders="body_female_a_sh" legs_mesh="feet_female_a" hands_mesh="hands_female_a" face_meta_mesh="head_female_a" underwear_bottom_mesh="underwear_female" underwear_top_mesh="underwear_female_top">
\t\t\t<hair_meshes group_id="3"><hair_mesh name="female_hair_l" /><hair_mesh name="female_hair_m" /></hair_meshes>
\t\t\t<eyebrow_meshes><eyebrow_mesh name="female_eyebrow_2" /></eyebrow_meshes>
\t\t\t<face_textures group_id="1"><face_texture name="head_female_a" lod_material="head_female_a.lod" tags="t1" /></face_textures>
\t\t\t<mouth_textures group_id="4"><mouth_texture name="mouth_mat_kid_b" lod_material="mouth_mat_kid_b" color="0xFFFFFFFF" tags="mouth_texture1" /></mouth_textures>
\t\t</skin>
\t\t<skin gender="0" name="toddler_male" skeleton="human_skeleton" body_meta_mesh="body_female_a_kid3" body_meta_mesh_shoulders="body_male_a_sh" legs_mesh="feet_female_kid3" hands_mesh="hands_female_kid3" face_meta_mesh="head_male_a" underwear_bottom_mesh="underwear_baby" underwear_top_mesh="">
\t\t\t<default_hair_meshes
\t\t\t\tcover_type1="hair_female_a"
\t\t\t\tcover_type2="hair_female_b" />
\t\t\t<hair_meshes group_id="9"><hair_mesh name="hair_female_a" /></hair_meshes>
\t\t\t<eyebrow_meshes><eyebrow_mesh name="male_eyebrow_1" /></eyebrow_meshes>
\t\t\t<default_beard_meshes
\t\t\t\tcover_type1="beards_c_a" />
\t\t\t<beard_meshes />
\t\t</skin>
\t\t<skin gender="1" name="toddler_female" skeleton="human_skeleton" body_meta_mesh="body_female_a_kid4" body_meta_mesh_shoulders="body_male_a_sh" legs_mesh="feet_female_kid3" hands_mesh="hands_female_kid3" face_meta_mesh="head_female_a" underwear_bottom_mesh="underwear_baby" underwear_top_mesh="">
\t\t\t<beard_meshes
\t\t\t\tzero_probability="5"></beard_meshes>
\t\t</skin>
\t</race>
</skins>
"""

MONSTERS = """<?xml version="1.0" encoding="utf-8"?>
<Monsters>
\t<Monster id="hill_troll"
\t\t\t   action_set="as_hill_troll_warrior"
\t\t\t   standing_eye_height="1.70"
\t\t\t   crouch_eye_height="1.10"
\t\t\t   eye_offset_wrt_head="0.13, 0.1, 0.0"
\t\t\t   first_person_camera_offset_wrt_head="0.136, 0.1, 0.0"
\t\t\t   arm_length="0.9"
\t\t\t   main_hand_item_bone="r_finger0">
\t\t<Capsules>
\t\t\t<body_capsule radius="0.37" pos1="0.0, 0.0, 1.55" pos2="0.0, 0, 0.8" />
\t\t\t<crouched_body_capsule radius="0.37" pos1="0.0, 0.0, 1.55" pos2="0.0, 0, 0.6" />
\t\t</Capsules>
\t\t<Flags CanAttack="true" CanRide="true" CanCrouch="true" />
\t</Monster>
\t<Monster id="troll_child" base_monster="hill_troll" action_set="as_hill_troll_child" standing_eye_height="1.20" crouch_eye_height="0.70" arm_length="0.6" />
\t<Monster id="troll_settlement" base_monster="hill_troll" hit_points="40">
\t\t<Flags CanAttack="true" CanRide="true" />
\t</Monster>
\t<Monster id="troll_settlement_slow" base_monster="hill_troll" walking_speed_limit="1.1" />
\t<Monster id="troll_settlement_fast" base_monster="hill_troll" walking_speed_limit="1.6" />
\t<Monster id="cave_troll" action_set="as_cave_troll_warrior" standing_eye_height="1.70">
\t\t<Flags CanRide="true" />
\t</Monster>
\t<Monster id="cave_troll_child" base_monster="cave_troll" />
</Monsters>
"""

ACTION_SETS = """<?xml version="1.0" encoding="utf-8"?>
<action_sets>
\t<action_set id="as_hill_troll_warrior"  base_set="as_human_warrior">
\t</action_set>
\t<action_set id="as_hill_troll_female_warrior" base_set="as_hill_troll_warrior" />
\t<action_set id="as_cave_troll_warrior"  base_set="as_human_warrior">
\t</action_set>
</action_sets>
"""


def _skin(root, name):
    race = [r for r in root.iter("race") if r.get("id") == "hill_troll"][0]
    return [s for s in race.iter("skin") if s.get("name") == name][0]


class SkinTests(unittest.TestCase):
    def setUp(self):
        self.out, self.report = w.edit_skins(SKINS)
        self.root = ET.fromstring(self.out.encode("utf-8"))

    def test_every_skin_gets_the_troll_skeleton_and_meshes(self):
        for name in ("man", "kid_2_female"):
            s = _skin(self.root, name)
            self.assertEqual(s.get("skeleton"), "troll_skeleton_a")
            self.assertEqual(s.get("body_meta_mesh"), "hill_troll_a_body")
            self.assertEqual(s.get("body_meta_mesh_shoulders"), "hill_troll_a_shoulder")
            self.assertEqual(s.get("legs_mesh"), "hill_troll_a_legs")
            self.assertEqual(s.get("hands_mesh"), "hill_troll_a_hands")
            self.assertEqual(s.get("face_meta_mesh"), "hill_troll_a_head")
            self.assertEqual((s.get("underwear_bottom_mesh"), s.get("underwear_top_mesh")), ("", ""))

    def test_other_skins_take_the_adult_males_bald_lists(self):
        s = _skin(self.root, "kid_2_female")
        self.assertEqual(s.find("hair_meshes").get("group_id"), "3", "the skin keeps its own group id")
        self.assertEqual([t.get("name") for t in s.find("hair_meshes").iter("style_tag")], ["Bald"])
        self.assertEqual([m.get("name") for m in s.find("eyebrow_meshes")], [""])
        self.assertIsNone(s.find("beard_meshes"), "no beard list is added where none was")

    def test_helmet_hair_defaults_go_and_an_empty_beard_list_stays(self):
        s = _skin(self.root, "toddler_male")
        self.assertIsNone(s.find("default_hair_meshes"))
        self.assertIsNone(s.find("default_beard_meshes"))
        self.assertEqual(list(s.find("beard_meshes")), [], "the empty self-closing list is left alone")
        self.assertEqual([t.get("name") for t in s.find("hair_meshes").iter("style_tag")], ["Bald"])
        f = _skin(self.root, "toddler_female").find("beard_meshes")
        self.assertEqual(f.get("zero_probability"), "5")
        self.assertEqual([t.get("name") for t in f.iter("style_tag")], ["Cleanshaven"])

    def test_face_textures_point_at_the_troll_head_outside_comments(self):
        for name in ("man", "kid_2_female"):
            for t in _skin(self.root, name).iter("face_texture"):
                self.assertEqual((t.get("name"), t.get("lod_material")), ("t_tr_hill_troll_head_a",) * 2)
        self.assertIn('<!-- <face_texture name="head_male_e" lod_material="head_male_e"', self.out)

    def test_mouth_textures_point_at_the_troll_head_too(self):
        # the engine puts the skin's mouth material on the head's face_mouth_mesh; the old t_hilltroll_mouth never
        # existed in the Kit ("Unable to find material" every session) and the kids carried the human mouth_mat
        for name in ("man", "kid_2_female"):
            tags = list(_skin(self.root, name).iter("mouth_texture"))
            self.assertTrue(tags)
            for t in tags:
                self.assertEqual((t.get("name"), t.get("lod_material")), ("t_tr_hill_troll_head_a",) * 2)
        self.assertNotIn('name="t_hilltroll_mouth"', self.out.replace('<!-- <mouth_texture name="mouth_mat"', ""))
        self.assertIn('<!-- <mouth_texture name="mouth_mat" lod_material="mouth_mat"', self.out)

    def test_nothing_outside_the_race_changes(self):
        dwarf_before = SKINS.split('<race id="hill_troll">')[0]
        self.assertEqual(self.out.split('<race id="hill_troll">')[0], dwarf_before)

    def test_a_second_run_changes_nothing(self):
        again, report = w.edit_skins(self.out)
        self.assertEqual(again, self.out)
        self.assertEqual(report["changes"], 0)

    def test_a_missing_race_is_refused(self):
        with self.assertRaises(w.Refused):
            w.edit_skins(SKINS.replace('id="hill_troll"', 'id="hill_trol"'))

    def test_deleting_a_helmet_default_keeps_every_crlf_line_ending(self):
        crlf = SKINS.replace("\n", "\r\n")
        out, _ = w.edit_skins(crlf)
        self.assertNotIn("\r\r\n", out)
        self.assertNotIn("\n", out.replace("\r\n", ""), "no bare LF and no lone CR either")
        self.assertEqual(out.replace("\r\n", "\n"), self.out, "the same edit as on the LF file")

    def test_a_helmet_default_sharing_its_line_loses_only_the_tag(self):
        inline = SKINS.replace("\t\t\t<default_beard_meshes\n\t\t\t\tcover_type1=\"beards_c_a\" />\n",
                               "\t\t\t<beard_x /><default_beard_meshes cover_type1=\"beards_c_a\" />\n")
        out, _ = w.edit_skins(inline)
        self.assertIn("\t\t\t<beard_x />\n", out)
        self.assertNotIn("default_beard_meshes", out)


class MonsterTests(unittest.TestCase):
    def setUp(self):
        self.out, self.report = w.edit_monsters(MONSTERS)
        self.root = ET.fromstring(self.out.encode("utf-8"))
        self.by_id = {m.get("id"): m for m in self.root.iter("Monster")}

    def test_the_monster_gets_the_measured_sizes(self):
        m = self.by_id["hill_troll"]
        for attr, value in w.MONSTER_ATTRS.items():
            self.assertEqual(m.get(attr), value, attr)
        self.assertEqual(m.find("Capsules/body_capsule").get("radius"), w.CAPSULES["body_capsule"]["radius"])
        self.assertEqual(m.get("main_hand_item_bone"), "r_finger0", "untouched")

    def test_riding_is_off_on_the_troll_only(self):
        self.assertEqual(self.by_id["hill_troll"].find("Flags").get("CanRide"), "false")
        self.assertEqual(self.by_id["hill_troll_settlement"].find("Flags").get("CanRide"), "false")
        self.assertEqual(self.by_id["cave_troll"].find("Flags").get("CanRide"), "true")

    def test_variants_take_the_names_the_engine_looks_up(self):
        for new in ("hill_troll_child", "hill_troll_settlement", "hill_troll_settlement_slow",
                    "hill_troll_settlement_fast"):
            self.assertIn(new, self.by_id)
        self.assertNotIn("troll_child", self.by_id)
        self.assertIn("cave_troll_child", self.by_id)
        self.assertEqual(self.by_id["hill_troll_child"].get("standing_eye_height"),
                         w.CHILD_ATTRS["standing_eye_height"])

    def test_a_second_run_changes_nothing(self):
        again, report = w.edit_monsters(self.out)
        self.assertEqual(again, self.out)
        self.assertEqual(report["changes"], 0)

    def test_an_old_and_a_new_variant_id_side_by_side_are_refused(self):
        both = MONSTERS.replace("</Monsters>", '\t<Monster id="hill_troll_child" base_monster="hill_troll" />\n</Monsters>')
        with self.assertRaises(w.Refused):
            w.edit_monsters(both)


class ActionSetTests(unittest.TestCase):
    def test_the_warrior_set_becomes_standalone_on_the_troll_skeleton(self):
        out, report = w.edit_action_sets(ACTION_SETS)
        sets = {s.get("id"): s for s in ET.fromstring(out.encode("utf-8")).iter("action_set")}
        self.assertEqual(sets["as_hill_troll_warrior"].get("skeleton"), "troll_skeleton_a")
        self.assertIsNone(sets["as_hill_troll_warrior"].get("base_set"))
        self.assertEqual(sets["as_cave_troll_warrior"].get("base_set"), "as_human_warrior")
        again, report = w.edit_action_sets(out)
        self.assertEqual((again, report["changes"]), (out, 0))


class CheckModeTests(unittest.TestCase):
    """--check is the reinstall gate: the Armory is unversioned, so a reinstall silently puts the old
    hill troll (human skeleton meshes, the inheriting set) back, and nothing else would notice."""

    BOUND = ('\t\t<action type="act_walk_forward_unarmed" animation="anim_hill_troll_walk1" />\n'
             '\t\t<action type="act_swim_idle" animation="swim_idle" />\n'
             '\t\t<action type="act_troll_brute_force" animation="anim_hill_troll_attack1" />\n')

    def _armory(self, d, wired, body=BOUND):
        texts = {"skins.xml": SKINS, "monsters.xml": MONSTERS, "action_sets.xml": ACTION_SETS}
        if wired:
            sets = w.edit_action_sets(ACTION_SETS)[0]
            sets = sets.replace(w.ACTION_SET_HEADER + "\n", w.ACTION_SET_HEADER + "\n" + body, 1)
            texts = {"skins.xml": w.edit_skins(SKINS)[0], "monsters.xml": w.edit_monsters(MONSTERS)[0],
                     "action_sets.xml": sets}
        for name, text in texts.items():
            with open(os.path.join(d, name), "wb") as fh:
                fh.write(text.encode("utf-8"))

    def _check(self, **kw):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            self._armory(d, **kw)
            with contextlib.redirect_stdout(io.StringIO()) as out:
                rc = w.main(["--armory", d, "--check"])
        return rc, out.getvalue()

    def test_check_passes_on_a_wired_armory(self):
        rc, out = self._check(wired=True)
        self.assertEqual(rc, 0, out)
        self.assertIn("OK:", out)

    def test_check_fails_on_a_wired_but_empty_set(self):
        rc, out = self._check(wired=True, body="")
        self.assertEqual(rc, 1)
        self.assertIn("no actions", out)

    def test_check_fails_when_the_binder_never_ran(self):
        # the parity tool's fill: every code on its human clip, no troll clip, no Brute Force binding
        rc, out = self._check(wired=True, body='\t\t<action type="act_swim_idle" animation="swim_idle" />\n')
        self.assertEqual(rc, 1)
        self.assertIn("no anim_hill_troll_* clip", out)
        self.assertIn("act_troll_brute_force is bound 0 times", out)

    def test_check_fails_without_the_brute_force_binding(self):
        body = self.BOUND.replace('\t\t<action type="act_troll_brute_force" animation="anim_hill_troll_attack1" />\n', "")
        rc, out = self._check(wired=True, body=body)
        self.assertEqual(rc, 1)
        self.assertIn("bound 0 times", out)

    def test_check_fails_when_a_reinstall_reverted_the_race(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            self._armory(d, wired=False)
            before = {n: open(os.path.join(d, n), "rb").read() for n in os.listdir(d)}
            self.assertEqual(w.main(["--armory", d, "--check"]), 1)
            after = {n: open(os.path.join(d, n), "rb").read() for n in os.listdir(d)}
            self.assertEqual(before, after, "--check must never write")

    def test_check_on_a_folder_without_the_files_does_not_pass(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            self.assertEqual(w.main(["--armory", d, "--check"]), 2)


if __name__ == "__main__":
    unittest.main()
