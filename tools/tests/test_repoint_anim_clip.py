"""repoint_anim_clip.py: point a clip at another master and frame range, every other byte kept.

Clip packages come from test_set_clip_balance_name's builders (the engine's metadata order); master packages from
test_creature_fit's (the Kit's import record, then the SkeletalAnimation).
"""
import os
import struct
import sys
import tempfile
import unittest
import uuid

try:
    import xxhash  # noqa: F401
except ImportError as exc:  # CI's python-tests job installs no packages: skip, never error
    raise unittest.SkipTest("the packages these tests build need xxhash (%s)" % exc)

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import repoint_anim_clip as rp  # noqa: E402
import set_clip_balance_name as scb  # noqa: E402
import test_creature_fit as tcf  # noqa: E402
import test_set_clip_balance_name as tsb  # noqa: E402


def clip(name="anim_x_release_overswing_2h", guid=None, src=(2.0, 111.0), duration=1.1):
    return tsb.package(name, m=tcf.timed_meta(duration, src[0], src[1], guid or uuid.uuid4()))


def read(path):
    with open(path, "rb") as fh:
        return fh.read()


class RepointTests(unittest.TestCase):
    def test_only_the_master_the_range_and_the_checksum_change(self):
        old = clip()
        g = uuid.uuid4()
        new = rp.repoint(old, g, 27.0, 59.0)
        a, b = scb.parse(old), scb.parse(new)
        self.assertEqual(len(new), len(old))
        self.assertEqual((a.name, a.flags, a.field, a.blends_with_action), (b.name, b.flags, b.field, b.blends_with_action))
        meta = rp.timing(new)
        self.assertEqual(meta["master_guid"], str(g))
        self.assertEqual((meta["source1"], meta["source2"]), (27.0, 59.0))
        self.assertAlmostEqual(meta["duration"], 1.1, places=5)          # kept when not given
        self.assertTrue(scb.checksum_ok(new))
        diff = [i for i in range(len(old)) if old[i] != new[i]]
        p = a.m0 + 4
        allowed = set(range(p + 4, p + 12)) | set(range(p + 28, p + 44)) | set(range(a.ck_at, a.ck_at + 8))
        self.assertTrue(set(diff) <= allowed, "bytes outside the fields changed: %r" % sorted(set(diff) - allowed)[:8])

    def test_a_range_may_run_backwards_for_a_blocked_clip(self):
        new = rp.repoint(clip(), uuid.uuid4(), 59.0, 27.0, duration=1.0)
        meta = rp.timing(new)
        self.assertEqual((meta["source1"], meta["source2"]), (59.0, 27.0))
        self.assertAlmostEqual(meta["duration"], 1.0, places=5)

    def test_plan_and_apply_through_a_folder(self):
        g = uuid.uuid4()
        with tempfile.TemporaryDirectory() as d:
            with open(os.path.join(d, "anim_x_release_overswing_2h_anm.tpac"), "wb") as fh:
                fh.write(clip())
            with open(os.path.join(d, "anim_x_attack1_geo.tpac"), "wb") as fh:
                fh.write(tcf.master_package("anim_x_attack1", g))
            rows = rp.plan([{"clip": "anim_x_release_overswing_2h", "master": "anim_x_attack1",
                             "source1": 27, "source2": 59}], d)
            self.assertTrue(rows[0].changed)
            before = read(os.path.join(d, "anim_x_release_overswing_2h_anm.tpac"))
            rp.apply(rows, stamp="t")
            after = read(os.path.join(d, "anim_x_release_overswing_2h_anm.tpac"))
            self.assertEqual(rp.timing(after)["master_guid"], str(g))
            self.assertEqual(read(os.path.join(d, "anim_x_release_overswing_2h_anm.tpac.bak-repoint-t")), before)
            again = rp.plan([{"clip": "anim_x_release_overswing_2h", "master": "anim_x_attack1",
                              "source1": 27, "source2": 59}], d)
            self.assertFalse(again[0].changed)                       # idempotent

    def test_a_missing_master_is_refused(self):
        with tempfile.TemporaryDirectory() as d:
            with open(os.path.join(d, "anim_x_release_overswing_2h_anm.tpac"), "wb") as fh:
                fh.write(clip())
            with self.assertRaises(rp.RepointError):
                rp.plan([{"clip": "anim_x_release_overswing_2h", "master": "anim_x_nope", "source1": 1,
                          "source2": 9}], d)

    def test_a_range_outside_the_frames_is_refused(self):
        with self.assertRaises(rp.RepointError):
            rp.repoint(clip(), uuid.uuid4(), 0.0, 9.0)                # frame 0 is the rest frame, never played


if __name__ == "__main__":
    unittest.main()
