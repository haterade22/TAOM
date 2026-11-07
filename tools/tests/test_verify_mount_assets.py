#!/usr/bin/env python3
"""tools/verify_mount_assets.py on synthetic tpacs (#1081).

Run:  python -m unittest tools.tests.test_verify_mount_assets

A clip name that is a prefix of another clip's name must not hide a lost tag or a deleted clip, an empty input must not
read as a pass, and an exemption covers one action and one clip, never the clip wherever it is bound.
"""
import io
import os
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
         '<monster_usage_movement action="act_x_trot" />'
         '<monster_usage_movement action="act_x_strafe" />'
         '<monster_usage_movement action="act_x_trot_stand" />'
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

    def test_an_exempt_clip_bound_to_another_action_fails(self):
        self._write("action_sets.xml", SETS.replace('type="act_x_trot" animation="x_trot"',
                                                    'type="act_x_trot" animation="x_trot_stand"'))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("act_x_trot -> x_trot_stand", out)

    def test_a_usage_set_with_no_movement_rows_fails(self):
        self._write("usage.xml", '<monster_usage_sets><monster_usage_set id="x"></monster_usage_set></monster_usage_sets>')
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("usage", out)

    def test_no_tpac_at_all_fails_and_does_not_blame_the_usage_set(self):
        for name in os.listdir(self.assets):
            os.remove(os.path.join(self.assets, name))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertNotIn("no movement row that binds a clip", out)
        self.assertIn("none of the 3 bound measured-gait clips was checked: 3 not found in a live tpac", out)

    def test_every_bound_clip_missing_from_present_tpacs_fails_with_the_same_cause(self):
        for stem in ("x_trot_anm", "x_trot_strafe_anm", "x_trot_stand_anm"):
            os.remove(os.path.join(self.assets, stem + ".tpac"))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertNotIn("no movement row that binds a clip", out)
        self.assertIn("3 not found in a live tpac", out)

    def test_every_counted_clip_exempt_fails_and_names_the_exemption(self):
        os.remove(os.path.join(self.assets, "x_trot_anm.tpac"))
        os.remove(os.path.join(self.assets, "x_trot_strafe_anm.tpac"))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertNotIn("no movement row that binds a clip", out)
        self.assertIn("1 exempt, 2 not found in a live tpac", out)

    def test_a_set_that_binds_no_movement_row_keeps_the_usage_message(self):
        self._write("usage.xml", USAGE.replace('act_x_trot"', 'act_x_a"').replace('act_x_strafe"', 'act_x_b"')
                    .replace('act_x_trot_stand"', 'act_x_c"'))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("has no movement row that binds a clip", out)

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


if __name__ == "__main__":
    unittest.main()
