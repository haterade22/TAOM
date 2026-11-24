#!/usr/bin/env python3
"""tools/verify_mount_assets.py on synthetic tpacs (#1081).

Run:  python -m unittest tools.tests.test_verify_mount_assets

A clip name that is a prefix of another clip's name must not hide a lost tag or a deleted clip, an empty input must not
read as a pass, and an exemption covers one action and one clip, never the clip wherever it is bound.
"""
import io
import os
import re
import struct
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import verify_mount_assets as vma  # noqa: E402

MASTER_TYPE = bytes.fromhex("07b0faba3f7e3f45bac6e7640043112b")


def _tpac(items):
    """A package holding (type_guid, name, meta) items, in the layout tpac_items walks."""
    body = b""
    for type_guid, name, meta in items:
        raw = name.encode("utf-8")
        body += type_guid + b"\x00" * 16 + struct.pack("<I", 1) + struct.pack("<i", len(raw)) + raw
        body += struct.pack("<q", len(meta)) + meta + b"\x00" * 8 + struct.pack("<i", 0) + struct.pack("<i", 0)
    header = struct.pack("<II", vma.MAGIC, 2) + b"\x00" * 16 + struct.pack("<I", len(items)) + b"\x00" * 8
    return header + body


def _clip(name, tagged=True):
    return _tpac([(vma.CLIP_GUID, name, b"quad_movement" if tagged else b"plain")])


USAGE = ('<monster_usage_sets><monster_usage_set id="x"><monster_usage_movements>'
         '<monster_usage_movement direction="front" turn_direction="none" action="act_x_trot" />'
         '<monster_usage_movement direction="right" turn_direction="none" action="act_x_strafe" />'
         '<monster_usage_movement direction="none" turn_direction="none" action="act_x_trot_stand" />'
         '</monster_usage_movements></monster_usage_set></monster_usage_sets>')
SETS = ('<action_sets><action_set id="as_x" skeleton="x_skel">'
        '<action type="act_x_trot" animation="x_trot" />'
        '<action type="act_x_strafe" animation="x_trot_strafe" />'
        '<action type="act_x_trot_stand" animation="x_trot_stand" />'
        '</action_set><action_set id="as_x_map" skeleton="x_skel" base_set="as_x">'
        '<action type="act_map_x" animation="x_trot" />'
        '</action_set></action_sets>')
CONFIG = {
    "asset_dir": "assets",
    "action_sets_xml": "action_sets.xml",
    "usage_xml": "usage.xml",
    "action_set_ids": ["as_x", "as_x_map"],
    "usage_id": "x",
    "untagged_ok": (("act_x_trot_stand", "x_trot_stand"),),
}


class GateTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.assets = os.path.join(self.tmp.name, "assets")
        os.makedirs(self.assets)
        self._write("usage.xml", USAGE)
        self._write("action_sets.xml", SETS)
        self._package("x_geo", _tpac([(vma.SKELETON_GUID, "x_skel", b"")]))
        self._package("x_trot_anm", _clip("x_trot"))
        self._package("x_trot_strafe_anm", _clip("x_trot_strafe"))
        self._package("x_trot_stand_anm", _clip("x_trot_stand", tagged=False))

    def _write(self, name, text):
        with open(os.path.join(self.tmp.name, name), "w", encoding="utf-8") as fh:
            fh.write(text)

    def _package(self, stem, raw):
        with open(os.path.join(self.assets, stem + ".tpac"), "wb") as fh:
            fh.write(raw)

    def _run(self, config=None):
        out = io.StringIO()
        with mock.patch.dict(vma.CREATURES, {"x": config or CONFIG}), mock.patch.object(vma, "DEFAULT_GAME", self.tmp.name), \
                redirect_stdout(out):
            try:
                code = vma.main(["x"])
            except SystemExit as exc:
                code = exc.code
        return code or 0, out.getvalue()

    def test_an_intact_mount_passes(self):
        code, out = self._run()
        self.assertEqual(code, 0, out)

    def test_a_creature_with_a_body_entry_fails_on_a_capsule_finding(self):
        # the capsule tests call capsule_findings directly; this one fails when CHECK 4 is taken out of main
        config = dict(CONFIG, body={"monster_id": "x"})
        with mock.patch.object(vma, "body_findings", return_value=((0.5, (0, 0.4, 0.6), (0, -0.4, 0.6)), ["its rear end is short"])):
            code, out = self._run(config)
        self.assertEqual(code, 1, out)
        self.assertIn("body capsule of x", out)
        self.assertIn("its rear end is short", out)
        self.assertIn("body_capsule", out)
        with mock.patch.object(vma, "body_findings", return_value=((0.7, (0, 0.2, 0.9), (0, -1.9, 0.9)), [])):
            code, out = self._run(config)
        self.assertEqual(code, 0, out)
        self.assertIn("the body capsule holds the body", out)

    def test_the_pass_line_counts_only_the_clips_that_carry_the_tag(self):
        _, out = self._run()
        self.assertIn("all 2 measured-gait clips", out)
        self.assertIn("1 exempt", out)

    def test_a_lost_tag_is_not_hidden_by_a_longer_clip_name(self):
        self._package("x_trot_anm", _clip("x_trot", tagged=False))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("act_x_trot -> x_trot", out)

    def test_a_deleted_clip_is_not_hidden_by_a_longer_clip_name(self):
        os.remove(os.path.join(self.assets, "x_trot_anm.tpac"))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn('animation="x_trot"', out)

    def test_the_dire_wolf_exempts_each_in_place_twin_its_gait_plan_binds(self):
        """A pace's "none" row plays an in-place twin with no quad_movement, as vanilla's does
        (act_horse_forward_walk_stand plays gait_walkfast). #1565 gave the wolf's walk its twin, and this list
        did not know it: the gate printed "do NOT battle-test" for a row in vanilla's own shape."""
        pairs = set(vma.CREATURES["dire_wolf"]["untagged_ok"])
        self.assertIn(("act_warg_forward_walk_stand", "dire_wolf_walk_stand"), pairs)
        self.assertEqual(len(pairs), 5)

    def test_an_exempt_clip_bound_to_another_action_fails(self):
        self._write("action_sets.xml", SETS.replace('type="act_x_trot" animation="x_trot"',
                                                    'type="act_x_trot" animation="x_trot_stand"'))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("act_x_trot -> x_trot_stand", out)

    def test_an_exempt_pair_on_a_row_that_moves_fails(self):
        # the pair is exempt as a reference row only: the same action on a front, strafe or turn row is a measured gait
        for attrs in ('direction="front" turn_direction="none"', 'direction="left" turn_direction="none"',
                      'direction="none" turn_direction="right"', ''):
            with self.subTest(attrs=attrs):
                self._write("usage.xml", USAGE.replace(
                    '</monster_usage_movements>',
                    '<monster_usage_movement %s\n action="act_x_trot_stand" /></monster_usage_movements>' % attrs))
                code, out = self._run()
                self.assertEqual(code, 1, out)
                self.assertIn("act_x_trot_stand -> x_trot_stand", out)
                self.assertNotIn("exempt, no quad_movement", out)

    def test_a_usage_set_with_no_movement_rows_fails(self):
        self._write("usage.xml", '<monster_usage_sets><monster_usage_set id="x"></monster_usage_set></monster_usage_sets>')
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("usage", out)

    def test_an_absent_usage_set_fails(self):
        self._write("usage.xml", "<monster_usage_sets></monster_usage_sets>")
        code, out = self._run()
        self.assertEqual(code, 1, out)

    def test_an_absent_action_set_fails(self):
        self._write("action_sets.xml", SETS.replace('id="as_x_map"', 'id="as_x_other"'))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("as_x_map", out)

    def test_a_movement_action_with_no_binding_fails(self):
        self._write("action_sets.xml", SETS.replace('<action type="act_x_trot" animation="x_trot" />', ""))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("act_x_trot", out)

    def test_a_deleted_clip_is_not_hidden_by_a_master_of_the_same_name(self):
        os.remove(os.path.join(self.assets, "x_trot_anm.tpac"))
        self._package("x_trot_geo", _tpac([(MASTER_TYPE, "x_trot", b"")]))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn('animation="x_trot"', out)

    def test_game_flag_selects_the_install(self):
        out = io.StringIO()
        with mock.patch.dict(vma.CREATURES, {"x": CONFIG}), redirect_stdout(out):
            code = vma.main(["--game", self.tmp.name, "x"])
        self.assertEqual(code or 0, 0, out.getvalue())

    def test_an_unknown_creature_exits_2(self):
        with redirect_stdout(io.StringIO()), mock.patch("sys.stderr", io.StringIO()):
            with self.assertRaises(SystemExit) as caught:
                vma.main(["nope"])
        self.assertEqual(caught.exception.code, 2)


class BodyCapsuleTests(unittest.TestCase):
    """The body capsule is what keeps two beasts, or a soldier and a beast, out of each other. On 2026-11-23 the
    spider's was a pill around its chest, 1.9 m of a 3.6 m body, and the spiders ran into each other."""

    @staticmethod
    def _bones():
        import json
        with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures", "spider_rig.json")) as fh:
            return json.load(fh)["bones"]

    HIPS = vma.CREATURES["spider"]["body"]["hips"]
    XML = ('<Monsters><Monster id="warg"><Capsules><body_capsule radius="0.4" pos1="0, 1.15, 1.15" pos2="0, -0.57, 1.15" />'
           '</Capsules></Monster>\n<Monster\n id="spider" weight="250">\n<Capsules>\n<body_capsule radius="0.50"\n'
           ' pos1="0.0, 0.45, 0.60"\n pos2="0.0, -0.45, 0.60" />\n</Capsules></Monster></Monsters>')

    def test_the_capsule_is_read_from_the_monsters_own_row(self):
        self.assertEqual(vma.read_body_capsule(self.XML, "spider"), (0.5, (0.0, 0.45, 0.6), (0.0, -0.45, 0.6)))
        self.assertEqual(vma.read_body_capsule(self.XML, "warg")[0], 0.4)
        self.assertIsNone(vma.read_body_capsule(self.XML, "elk"))
        self.assertIsNone(vma.read_body_capsule('<Monsters><!-- <Monster id="spider"><body_capsule radius="9" pos1="0,0,0" pos2="0,0,0" /></Monster> --></Monsters>', "spider"))

    def test_the_chest_pill_of_june_leaves_the_abdomen_and_the_hips_outside(self):
        got = vma.capsule_findings((0.5, (0.0, 0.45, 0.6), (0.0, -0.45, 0.6)), self._bones(), self.HIPS)
        self.assertEqual(len(got), 2, got)
        self.assertTrue([f for f in got if "rear" in f and "joint15_m" in f and "1.71 m" in f], got)
        self.assertTrue([f for f in got if "radius 0.50" in f and "0.66" in f], got)

    def test_a_capsule_from_the_head_to_the_abdomens_tip_that_holds_the_hips_passes(self):
        self.assertEqual(vma.capsule_findings((0.7, (0.0, 0.25, 0.9), (0.0, -1.95, 0.9)), self._bones(), self.HIPS), [])

    def test_a_capsule_that_stops_behind_the_head_is_a_finding(self):
        got = vma.capsule_findings((0.7, (0.0, -0.6, 0.9), (0.0, -1.95, 0.9)), self._bones(), self.HIPS)
        self.assertEqual(len(got), 1, got)
        self.assertIn("front", got[0])
        self.assertIn("joint12_m", got[0])      # the foremost bone on the centre line, 0.57 m ahead of the origin

    def test_the_two_ends_may_be_given_in_either_order(self):
        self.assertEqual(vma.capsule_findings((0.7, (0.0, -1.95, 0.9), (0.0, 0.25, 0.9)), self._bones(), self.HIPS), [])

    def test_a_capsule_beside_above_or_tilted_through_the_body_is_a_finding(self):
        # the first version read only the length along Y and the radius: each of these three passed
        good = (0.7, (0.0, 0.25, 0.9), (0.0, -1.95, 0.9))
        beside = vma.capsule_findings((0.7, (5.0, 0.25, 0.9), (5.0, -1.95, 0.9)), self._bones(), self.HIPS)
        above = vma.capsule_findings((0.7, (0.0, 0.25, 20.0), (0.0, -1.95, 20.0)), self._bones(), self.HIPS)
        tilted = vma.capsule_findings((0.7, (0.0, 0.25, -1.0), (0.0, -1.95, 3.0)), self._bones(), self.HIPS)
        self.assertEqual(vma.capsule_findings(good, self._bones(), self.HIPS), [])
        self.assertTrue([f for f in beside if "to the side" in f], beside)
        self.assertTrue([f for f in above if "at height 20.00" in f], above)
        self.assertEqual(len([f for f in tilted if "at height" in f]), 2, tilted)

    def test_a_capsule_of_numbers_that_are_none_is_a_finding(self):
        nan = float("nan")
        for bad in ((nan, (0.0, 0.25, 0.9), (0.0, -1.95, 0.9)), (0.7, (0.0, nan, 0.9), (0.0, -1.95, 0.9)),
                    (0.0, (0.0, 0.25, 0.9), (0.0, -1.95, 0.9)), (float("inf"), (0.0, 0.25, 0.9), (0.0, -1.95, 0.9))):
            got = vma.capsule_findings(bad, self._bones(), self.HIPS)
            self.assertEqual(len(got), 1, got)
            self.assertIn("no finite number", got[0])

    def test_no_hip_named_is_a_finding(self):
        got = vma.capsule_findings((0.7, (0.0, 0.25, 0.9), (0.0, -1.95, 0.9)), self._bones(), [])
        self.assertEqual(len(got), 1, got)
        self.assertIn("radius is not checked", got[0])

    def test_body_findings_holds_the_capsule_it_read_to_the_bones_it_read(self):
        # the main test mocks body_findings and the geometry tests call capsule_findings: this one fails when
        # body_findings stops passing what it read to capsule_findings
        import tpac_skeleton_dump as tsd
        body = dict(vma.CREATURES["spider"]["body"], monster_xml="monster.xml", skeleton_tpac="skel.tpac")
        with tempfile.TemporaryDirectory() as game, \
                mock.patch.object(tsd, "find_skeleton", return_value=(None, object(), None)), \
                mock.patch.object(tsd, "read_segment_data", return_value=b""), \
                mock.patch.object(tsd, "parse_skeleton_definition", return_value={"bones": self._bones()}):
            with open(os.path.join(game, "monster.xml"), "w", encoding="utf-8") as fh:
                fh.write(self.XML)
            capsule, why = vma.body_findings(game, body)
            self.assertEqual(capsule, (0.5, (0.0, 0.45, 0.6), (0.0, -0.45, 0.6)))
            self.assertEqual(len(why), 2, why)
            with open(os.path.join(game, "monster.xml"), "w", encoding="utf-8") as fh:
                fh.write(self.XML.replace('radius="0.50"', 'radius="0.70"').replace('pos1="0.0, 0.45, 0.60"', 'pos1="0.0, 0.25, 0.90"')
                         .replace('pos2="0.0, -0.45, 0.60"', 'pos2="0.0, -1.95, 0.90"'))
            self.assertEqual(vma.body_findings(game, body)[1], [])

    def test_a_monster_file_that_is_not_there_is_a_finding_not_a_traceback(self):
        with tempfile.TemporaryDirectory() as game:
            capsule, why = vma.body_findings(game, vma.CREATURES["spider"]["body"])
        self.assertIsNone(capsule)
        self.assertEqual(len(why), 1, why)
        self.assertIn("cannot read", why[0])

    def test_a_hip_that_is_not_on_the_skeleton_is_a_finding_not_a_pass(self):
        got = vma.capsule_findings((0.7, (0.0, 0.25, 0.9), (0.0, -1.95, 0.9)), self._bones(), ["no_such_bone"])
        self.assertEqual(len(got), 1, got)
        self.assertIn("no_such_bone", got[0])


class LiveMonsterRegistrationTests(unittest.TestCase):
    """Every Monster file in the live Armory is registered in its SubModule.xml. An Armory sync that replaced
    SubModule.xml dropped lotr_monster_dire_wolf (2026-11-05): the engine then loads no dire_wolf, the items point at an
    empty placeholder Monster, and a portrait of a wolf rider throws in FillAnimationSystemData. Skipped where the
    Armory is not installed (CI, the laptop)."""

    def test_every_monster_file_is_registered(self):
        root = os.path.join(vma.DEFAULT_GAME, "Modules", "LOTRLOME_Armory")
        monsters = os.path.join(root, "ModuleData", "Monsters")
        if not os.path.isdir(monsters):
            self.skipTest("LOTRLOME_Armory is not installed here")
        with open(os.path.join(root, "SubModule.xml"), encoding="utf-8-sig") as fh:
            submodule = fh.read()
        registered = set(re.findall(r'<XmlName\s+id="Monsters"\s+path="([^"]+)"', submodule))
        files = set()
        for folder, _, names in os.walk(monsters):
            for name in names:
                if name.endswith(".xml"):
                    rel = os.path.relpath(os.path.join(folder, name[:-4]), os.path.dirname(monsters))
                    files.add(rel.replace(os.sep, "/"))
        self.assertEqual(sorted(files - registered), [], "Monster files the SubModule.xml does not load; "
                         "re-run the creature's apply_*_armory.py --apply")


if __name__ == "__main__":
    unittest.main()
