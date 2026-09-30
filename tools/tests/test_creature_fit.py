"""creature_fit.py: the clip index (which master a creature clip plays, over which frames, with which hand poses).

Clip packages come from test_set_clip_balance_name's byte builders (the engine's metadata order); a master
package is built here with the Kit's two items (the import record, then the SkeletalAnimation).
"""
import os
import struct
import sys
import tempfile
import unittest
import uuid

try:
    import xxhash  # noqa: F401  (the clip builder hashes its metadata region)
except ImportError as exc:  # CI's python-tests job installs no packages: skip, never error
    raise unittest.SkipTest("the clip packages these tests build need xxhash (%s)" % exc)

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import creature_fit as cf  # noqa: E402
import test_set_clip_balance_name as clipbuild  # noqa: E402

SKELETAL_ANIMATION = bytes.fromhex("07b0faba3f7e3f45bac6e7640043112b")
IMPORT_RECORD = bytes.fromhex("7936ba3ebdde7a4c8634f121f6325e33")


def master_package(name, guid):
    def item(type_guid, item_guid, item_name):
        meta = b"\x00" * 12
        return (type_guid + item_guid.bytes_le + struct.pack("<I", 0) + clipbuild.sstr(item_name)
                + struct.pack("<q", len(meta)) + meta + b"\x00" * 8
                + struct.pack("<i", 1) + b"\xaa" * 69 + struct.pack("<i", 0))
    body = item(IMPORT_RECORD, uuid.uuid4(), name + ".fbx") + item(SKELETAL_ANIMATION, guid, name)
    return b"TPAC" + struct.pack("<I", 2) + clipbuild.PKG + struct.pack("<I", 2) + struct.pack("<Q", len(body)) + body


def timed_meta(duration, src1, src2, guid, hands=(3, 3), flags=("client_prediction",)):
    """test_set_clip_balance_name.meta with the timing floats, master guid and hand poses set."""
    m = bytearray(clipbuild.meta(flags=flags))
    struct.pack_into("<3f", m, 4, duration, src1, src2)
    m[32:48] = guid.bytes_le
    at = 4 + 28 + 16 + 16
    for _ in range(5):
        at += 4 + struct.unpack_from("<i", m, at)[0]
    struct.pack_into("<ii", m, at, *hands)
    return bytes(m)


class JoinWeightTests(unittest.TestCase):
    def test_short_accepted_stretches_are_dropped(self):
        ok = [False] * 5 + [True] * 4 + [False] * 5
        self.assertEqual(float(sum(cf.join_weights(ok, min_run=6))), 0.0)

    def test_a_hold_eases_on_and_off_inside_its_stretch(self):
        ok = [False] * 3 + [True] * 20 + [False] * 3
        w = cf.join_weights(ok, min_run=6, ramp=5)
        self.assertEqual(w[2], 0.0)
        self.assertGreater(w[3], 0.0)
        self.assertLess(w[3], 0.2)
        self.assertEqual(w[12], 1.0)
        self.assertEqual(w[23], 0.0)

    def test_small_gaps_inside_a_hold_are_filled(self):
        ok = [True] * 10 + [False] * 2 + [True] * 10
        w = cf.join_weights(ok, min_run=6, gap=3, ramp=3)
        self.assertEqual(w[10], 1.0)

    def test_runs_lists_true_stretches(self):
        self.assertEqual(cf.runs([False, True, True, False, True]), [(1, 3), (4, 5)])


class HandPoseChannelTests(unittest.TestCase):
    def test_the_pair_selects_5l_plus_r_plus_1(self):
        self.assertEqual(cf.hand_pose_channel(0, 0), 1)
        self.assertEqual(cf.hand_pose_channel(3, 3), 19)          # every troll clip: shape_20
        self.assertEqual(cf.hand_pose_channel(4, 4), 25)

    def test_a_pose_outside_0_to_4_is_refused(self):
        with self.assertRaises(ValueError):
            cf.hand_pose_channel(5, 0)


class ClipMetaTests(unittest.TestCase):
    def test_reads_timing_master_hand_poses_and_flags(self):
        g = uuid.uuid4()
        buf = clipbuild.package("anim_x_release_overswing_2h",
                                m=timed_meta(1.1, 2.0, 111.0, g, hands=(2, 4), flags=("enable_left_hand_ik",)))
        meta = cf.read_clip_meta(buf)
        self.assertEqual(meta["name"], "anim_x_release_overswing_2h")
        self.assertAlmostEqual(meta["duration"], 1.1, places=5)
        self.assertEqual((meta["source1"], meta["source2"]), (2.0, 111.0))
        self.assertEqual(meta["master_guid"], str(g))
        self.assertEqual(meta["hand_poses"], [2, 4])
        self.assertEqual(meta["flags"], ["enable_left_hand_ik"])

    def test_the_played_frames_run_either_way(self):
        g = uuid.uuid4()
        meta = cf.read_clip_meta(clipbuild.package("anim_x_blocked", m=timed_meta(1.0, 111.0, 2.0, g)))
        self.assertEqual(cf.played_frames(meta), list(range(111, 1, -1)))

    def test_master_item_is_the_skeletal_animation_not_the_import_record(self):
        g = uuid.uuid4()
        self.assertEqual(cf.master_item(master_package("anim_x_stand", g)), ("anim_x_stand", str(g)))

    def test_index_joins_clips_to_their_masters_by_guid(self):
        g = uuid.uuid4()
        with tempfile.TemporaryDirectory() as d:
            with open(os.path.join(d, "anim_x_stand_geo.tpac"), "wb") as fh:
                fh.write(master_package("anim_x_stand", g))
            with open(os.path.join(d, "anim_x_stand_2h_anm.tpac"), "wb") as fh:
                fh.write(clipbuild.package("anim_x_stand_2h", m=timed_meta(5.1, 2.0, 156.0, g)))
            with open(os.path.join(d, "anim_x_orphan_anm.tpac"), "wb") as fh:
                fh.write(clipbuild.package("anim_x_orphan", m=timed_meta(1.0, 1.0, 9.0, uuid.uuid4())))
            idx = cf.build_index(d, "anim_x_")
        self.assertEqual(idx["clips"]["anim_x_stand_2h"]["master"], "anim_x_stand")
        self.assertEqual(idx["clips"]["anim_x_stand_2h"]["source2"], 156.0)
        self.assertIsNone(idx["clips"]["anim_x_orphan"]["master"])      # reported, never guessed
        self.assertEqual(idx["orphans"], ["anim_x_orphan"])


if __name__ == "__main__":
    unittest.main()
