#!/usr/bin/env python3
"""Tests for tools/elephant_gait_retarget.py and tools/elephant_write_masters.py (#1474).

Run:  python -m unittest tools.tests.test_elephant_gait_tools

The retarget's claims are geometric, so they are tested as geometry: a leg's re-timing keeps the stance linear
and has no step in foot speed, the 2-bone solve keeps both bone lengths and reports a target it cannot reach.
The master writer is tested on a tpac built here by hand from the layout it relies on, so the test does not
lean on the tool's own parser for its expectations.
"""
import io
import math
import os
import struct
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from unittest import mock
import uuid

import lz4.block
import numpy as np
import xxhash

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_gait_stride as c  # noqa: E402
import elephant_gait_retarget as g  # noqa: E402
import elephant_write_masters as w  # noqa: E402
import tpac_clone_metamesh as tcm  # noqa: E402


class LegWarpTests(unittest.TestCase):
    def test_q_one_is_identity(self):
        f, duty = g.leg_warp(0.55, 1.0)
        self.assertEqual(duty, 0.55)
        for x in (0.0, 0.2, 0.55, 0.9):
            self.assertAlmostEqual(f(x), x)

    def test_stance_is_linear_and_ends_at_the_source_duty(self):
        f, d2 = g.leg_warp(0.56, 1.4)
        self.assertAlmostEqual(d2, 0.4)
        for x in np.linspace(0, d2 - 1e-6, 9):
            self.assertAlmostEqual(f(x), x * 1.4, places=9)
        self.assertAlmostEqual(f(d2 - 1e-9), 0.56, places=6)

    def test_whole_cycle_maps_to_whole_cycle_and_never_runs_back(self):
        f, _ = g.leg_warp(0.5, 1.45)
        xs = np.linspace(0, 1 - 1e-9, 4001)
        ys = np.array([f(x) for x in xs])
        self.assertTrue(np.all(np.diff(ys) > 0))
        self.assertAlmostEqual(ys[-1], 1.0, places=3)

    def test_foot_speed_has_no_step_at_lift_or_plant(self):
        f, d2 = g.leg_warp(0.54, 1.3)
        h = 1e-4
        rate = lambda x: (f(x + h) - f(x)) / h  # noqa: E731
        self.assertAlmostEqual(rate(d2 - 2 * h), rate(d2 + h), delta=0.05)     # lift
        self.assertAlmostEqual(rate(1 - 3 * h), rate(0.0), delta=0.05)         # plant

    def test_refuses_a_stance_so_short_the_swing_would_stop(self):
        with self.assertRaises(ValueError):
            g.leg_warp(0.9, 8.0)


class TwoBoneTests(unittest.TestCase):
    def test_keeps_both_lengths_and_reaches_the_target(self):
        root, end = np.array([0.0, 0.0, 2.0]), np.array([0.1, 0.5, 0.4])
        mid, end2, over = g.two_bone(root, end, 1.0, 0.8, np.array([1.0, 0, 0]), 1.0)
        self.assertAlmostEqual(np.linalg.norm(mid - root), 1.0)
        self.assertAlmostEqual(np.linalg.norm(end2 - mid), 0.8)
        self.assertEqual(over, 0.0)
        np.testing.assert_allclose(end2, end, atol=1e-9)

    def test_reports_a_target_out_of_reach_and_stops_short_on_the_line_to_it(self):
        root, end = np.zeros(3), np.array([0.0, 0.0, -3.0])
        mid, end2, over = g.two_bone(root, end, 1.0, 0.8, np.array([1.0, 0, 0]), 1.0)
        self.assertAlmostEqual(over, 3.0 - 1.8 * 0.9995)
        self.assertAlmostEqual(np.linalg.norm(end2), 1.8 * 0.9995)
        self.assertAlmostEqual(end2[0], 0.0)

    def test_side_picks_the_bend_direction(self):
        root, end, n = np.array([0.0, 0, 2.0]), np.array([0.0, 0, 0.5]), np.array([1.0, 0, 0])
        a, _, _ = g.two_bone(root, end, 1.0, 0.8, n, 1.0)
        b, _, _ = g.two_bone(root, end, 1.0, 0.8, n, -1.0)
        self.assertAlmostEqual(a[1], -b[1])
        self.assertNotAlmostEqual(a[1], 0.0)
        self.assertAlmostEqual(a[0], 0.0)      # the bend stays in the plane square to the normal


class RotationTests(unittest.TestCase):
    def test_quaternion_round_trip(self):
        rng = np.random.default_rng(3)
        for _ in range(20):
            m = g.ortho(rng.normal(size=(3, 3)))
            np.testing.assert_allclose(g.quat_to_mat(g.mat_to_quat(m)), m, atol=1e-9)

    def test_rot_between_turns_a_onto_b(self):
        a, b = np.array([1.0, 2, 3]), np.array([-2.0, 0.5, 1])
        np.testing.assert_allclose(g.rot_between(a, b) @ g.unit(a), g.unit(b), atol=1e-9)

    def test_scale_rot_half_of_ninety_is_forty_five(self):
        r = g.quat_to_mat(np.array([math.cos(math.pi / 4), 0, 0, math.sin(math.pi / 4)]))
        half = g.scale_rot(r, 0.5)
        np.testing.assert_allclose(half @ half, r, atol=1e-9)

    def test_frame_of_is_a_right_handed_rotation(self):
        f = g.frame_of(np.array([0.2, 1.0, -0.4]), np.array([1.0, 0.1, 0.0]))
        np.testing.assert_allclose(f.T @ f, np.eye(3), atol=1e-9)
        self.assertAlmostEqual(np.linalg.det(f), 1.0)


def _track(times, vals):
    return struct.pack("<IIi", 0, 16, len(times)) + struct.pack("<%df" % len(times), *times) + b"".join(
        struct.pack("<4f", *v) for v in vals)


def _anim_payload(name, rot, root):
    """Hand-built animation data: per bone a rotation track then an empty position track, then the root tracks."""
    times = [float(i + 1) for i in range(len(root))]
    out = struct.pack("<i", len(name)) + name.encode() + struct.pack("<i", len(rot))
    for keys in rot:
        out += _track(times, keys) + _track([], [])
    return out + _track(times, [(x, y, z, 0.0) for x, y, z in root]) + _track([], [])


def _master_tpac(name, payload, bones, frames):
    meta = struct.pack("<i", 1) + bytes(16) + b"\x00" + bytes(range(16)) + struct.pack("<iii", bones, frames, 0)
    blob = lz4.block.compress(payload, mode="high_compression", store_size=False)
    toc = (w.SKELETAL_ANIMATION_TYPE + bytes([7] * 16) + struct.pack("<I", 0)
           + struct.pack("<i", len(name)) + name.encode() + struct.pack("<q", len(meta)) + meta)
    toc += xxhash.xxh64(struct.pack("<q", len(meta)) + meta, seed=0).digest()[::-1]   # little-endian uint64
    toc += struct.pack("<i", 1)
    entry = (struct.pack("<QQQ", 0, len(payload), len(blob)) + bytes([9] * 16) + bytes.fromhex("6d817dd0")
             + bytes(12) + struct.pack("<Q", xxhash.xxh64_intdigest(payload)) + bytes(5))
    assert len(entry) == tcm.SEG_ENTRY_SIZE
    toc += entry + struct.pack("<i", 0)
    header = b"TPAC" + struct.pack("<I", 2) + bytes([1] * 16) + struct.pack("<I", 1) + struct.pack("<Q", len(toc))
    raw = bytearray(header + toc + blob)
    seg_at = tcm.HEADER_SIZE + len(toc) - 4 - tcm.SEG_ENTRY_SIZE
    struct.pack_into("<Q", raw, seg_at, tcm.HEADER_SIZE + len(toc))
    return bytes(raw)


def _clip(frames, bones=2, lift=0.25, loop=False):
    """A 2-bone clip in Blender rig space: the root turned 180 degrees about Z at rest, a child 1 m along +Y of it;
    in the loop the root rises by `lift` and the child turns about X by 10 degrees per frame."""
    rest_root = np.eye(4)
    rest_root[:3, 3] = [0, 2.0, 1.0]
    rest_child = np.eye(4)
    rest_child[:3, 3] = [0, 3.0, 1.0]
    rows = [[rest_root, rest_child]]
    for f in range(frames):
        a = math.radians(10 * (f + 1))
        r = np.eye(4)
        r[:3, :3] = [[1, 0, 0], [0, math.cos(a), -math.sin(a)], [0, math.sin(a), math.cos(a)]]
        root = rest_root.copy()
        root[2, 3] += lift
        child = rest_child.copy()
        child[:3, :3] = r[:3, :3]
        child[2, 3] += lift
        rows.append([root, child])
    return {"clip": "t", "frames": frames, "stride_m": 2.0, "bones": ["root", "child"], "parents": [None, "root"],
            "loop": loop, "mats": [[m.reshape(-1).tolist() for m in row] for row in rows]}


class MasterWriterTests(unittest.TestCase):
    def setUp(self):
        ident = (1.0, 0.0, 0.0, 0.0)
        self.payload = _anim_payload("t", [[ident] * 3, [ident] * 3], [(0, 0, 0)] * 3)
        self.raw = _master_tpac("t", self.payload, 2, 3)

    def test_parse_and_build_round_trip_the_hand_built_data(self):
        self.assertEqual(w.build_anim(w.parse_anim(self.payload)), self.payload)

    def test_the_hand_built_package_round_trips_through_the_parser(self):
        pkg = tcm.parse(self.raw)
        self.assertEqual(tcm.serialize(pkg.package_guid, pkg.items, pkg.version), self.raw)

    def test_keys_are_engine_space_parent_relative_and_the_root_is_offset_from_rest(self):
        rot, root = w.clip_keys(_clip(3))
        # the engine's model space is the rig turned 180 degrees about Z: a root at identity there is that turn
        np.testing.assert_allclose(np.abs(rot[0, 0]), [0, 0, 0, 1], atol=1e-9)
        # the child's rotation relative to its parent is the plain turn about X, 10 degrees on frame 1
        h = math.radians(10) / 2
        np.testing.assert_allclose(np.abs(rot[1, 0]), [math.cos(h), math.sin(h), 0, 0], atol=1e-9)
        np.testing.assert_allclose(root, [[0, 0, 0.25]] * 3, atol=1e-9)

    def test_rewrite_changes_only_the_animation_data_and_reads_back(self):
        out, info = w.rewrite(self.raw, _clip(3))
        self.assertEqual((info["frames"], info["was"]), (3, 3))
        pkg = tcm.parse(out)
        item = pkg.items[0]
        seg = item.segments[0]
        data = tcm.segment_payload(item, seg)
        self.assertEqual(tcm.segment_hash(item, seg), xxhash.xxh64_intdigest(data))
        a = w.parse_anim(data)
        self.assertEqual(a["rot"][0]["t"], [1.0, 2.0, 3.0])      # in place: the records name the master's own times
        np.testing.assert_allclose(a["root_pos"]["v"][:, 2], 0.25, atol=1e-6)
        old = tcm.parse(self.raw).items[0]
        start, end = tcm._metadata_span(item)
        self.assertEqual(bytes(item.toc[:end + 8]), bytes(old.toc[:end + 8]))   # metadata and checksum untouched

    def test_a_second_rewrite_is_a_no_op(self):
        once, _ = w.rewrite(self.raw, _clip(3))
        twice, _ = w.rewrite(once, _clip(3))
        self.assertEqual(once, twice)

    def test_refuses_a_different_frame_count_without_resize(self):
        with self.assertRaisesRegex(ValueError, "--resize"):
            w.rewrite(self.raw, _clip(5))

    def test_resize_updates_duration_sizes_and_checksum(self):
        out, info = w.rewrite(self.raw, _clip(5), resize=True)
        self.assertEqual((info["frames"], info["was"]), (5, 3))
        item = tcm.parse(out).items[0]
        start, end = tcm._metadata_span(item)
        self.assertEqual(struct.unpack_from("<iii", item.toc, end - 12), (2, 5, 0))
        self.assertEqual(bytes(item.toc[end:end + 8]), tcm.expected_checksum(item))
        self.assertEqual(item.segments[0].actual, len(tcm.segment_payload(item, item.segments[0])))


class KeyTimeTests(unittest.TestCase):
    """The editor's rule (FUN_180b8b810 in the wEditor TaleWorlds.Native.dll, 2026-11-23): a track fits a clip
    record when its first key time is at or under the lower source and its last key time at or over the upper one.
    A Kit master keys from 0, and a Kit loop's last source key repeats its first (warg_canter: keys 0..17, sources
    1..17). The first writer keyed 1..N under sources 0..N: 412 warnings for 13 clips, one held key per loop."""

    def setUp(self):
        ident = (1.0, 0.0, 0.0, 0.0)
        self.old = _anim_payload("t", [[ident] * 3, [ident] * 3], [(0, 0, 0)] * 3)      # keys 1, 2, 3: the old form

    def _keys(self, payload):
        a = w.parse_anim(payload)
        return a["rot"][1]["t"], np.asarray(a["rot"][1]["v"], dtype=float), np.asarray(a["root_pos"]["v"], dtype=float), a

    def test_a_loop_gets_a_closing_key_that_repeats_its_first(self):
        data, n, _ = w.new_payload(self.old, _clip(3, loop=True), resize=True)
        times, rot, root, a = self._keys(data)
        self.assertEqual(n, 4)
        self.assertEqual(times, [0.0, 1.0, 2.0, 3.0])
        self.assertEqual(a["root_pos"]["t"], times)
        self.assertAlmostEqual(abs(float(np.dot(rot[0], rot[3]))), 1.0, places=6)
        np.testing.assert_allclose(root[3], root[0], atol=1e-7)
        self.assertGreaterEqual(float(np.dot(rot[2], rot[3])), 0.0, "the closing key must blend the short way from the key before it")

    def test_a_clip_that_does_not_say_is_a_loop(self):
        clip = _clip(3)
        del clip["loop"]            # the #1474 clip files carry no such field, and every one of them loops
        self.assertEqual(w.new_payload(self.old, clip, resize=True)[1], 4)

    def test_a_one_shot_keys_from_zero_to_its_last_frame(self):
        data, n, _ = w.new_payload(self.old, _clip(3, loop=False))
        self.assertEqual((n, self._keys(data)[0]), (3, [0.0, 1.0, 2.0]))

    def test_an_in_place_rewrite_keeps_the_times_its_records_address(self):
        # a master whose record plays keys 10 to 12: moving them to 0, 1, 2 leaves the record on keys that are gone
        ident = (1.0, 0.0, 0.0, 0.0)
        a = w.parse_anim(_anim_payload("t", [[ident] * 3, [ident] * 3], [(0, 0, 0)] * 3))
        for tr in a["rot"] + [a["root_pos"]]:
            tr["t"] = [10.0, 11.0, 12.0]
        out, _ = w.rewrite(_master_tpac("t", w.build_anim(a), 2, 3), _clip(3, loop=False))
        item = tcm.parse(out).items[0]
        self.assertEqual(w.parse_anim(tcm.segment_payload(item, item.segments[0]))["rot"][0]["t"], [10.0, 11.0, 12.0])

    def test_rekey_migrates_only_a_master_one_record_plays_whole(self):
        shot = w.new_payload(self.old, _clip(3, loop=False))[0]
        old_form = _master_tpac("t", self._shift_payload(shot, 1.0), 2, 3)
        for sources in ((1.0, 3.0), (0.0, 2.0), (10.0, 12.0)):
            with self.assertRaisesRegex(ValueError, "sources 0 to N"):
                w.rekey(old_form, loop=False, sources=sources)
        for master in (old_form, _master_tpac("t", shot, 2, 3)):                         # migrated or not: never without them
            with self.assertRaisesRegex(ValueError, "not given"):
                w.rekey(master, loop=False)
        long_master = _master_tpac("t", self._shift_payload(shot, 10.0), 2, 3)          # keys 10, 11, 12
        with self.assertRaisesRegex(ValueError, "several clips"):
            w.rekey(long_master, loop=False, sources=(0.0, 3.0))

    def test_rekey_refuses_a_zero_based_master_whose_record_plays_past_its_keys(self):
        # poses A, B, A at 0, 1, 2 read as a closed loop; a record that plays 0 to 3 says it is not this tool's
        ident, other = (1.0, 0.0, 0.0, 0.0), (0.9, 0.3, 0.1, 0.2)
        a = w.parse_anim(_anim_payload("t", [[ident, other, ident], [other, ident, other]], [(0, 0, 0)] * 3))
        for tr in a["rot"] + [a["root_pos"]]:
            tr["t"] = [0.0, 1.0, 2.0]
        a["rot"][1]["v"] = np.array([other, ident, other], dtype="<f4")
        aba = _master_tpac("t", w.build_anim(a), 2, 3)
        self.assertEqual(w.rekey(aba, loop=True, sources=(0.0, 2.0))[0], aba)
        with self.assertRaisesRegex(ValueError, "past its keys"):
            w.rekey(aba, loop=True, sources=(0.0, 3.0))

    def test_rekey_closes_an_old_loop_and_a_second_run_changes_nothing(self):
        raw = _master_tpac("t", w.new_payload(self.old, _clip(3, loop=False))[0], 2, 3)
        old_form = _master_tpac("t", self._shift(raw, 1.0), 2, 3)
        out, info = w.rekey(old_form, loop=True, sources=(0.0, 3.0))
        item = tcm.parse(out).items[0]
        times, rot, root, _ = self._keys(tcm.segment_payload(item, item.segments[0]))
        self.assertEqual(times, [0.0, 1.0, 2.0, 3.0])
        self.assertAlmostEqual(abs(float(np.dot(rot[0], rot[3]))), 1.0, places=6)
        self.assertEqual((info["keys"], info["was"]), (4, 3))
        start, end = tcm._metadata_span(item)
        self.assertEqual(struct.unpack_from("<iii", item.toc, end - 12), (2, 4, 0))
        self.assertEqual(bytes(item.toc[end:end + 8]), tcm.expected_checksum(item))
        again, info2 = w.rekey(out, loop=True, sources=(0.0, 3.0))
        self.assertEqual(again, out)
        self.assertEqual(info2["keys"], info2["was"])

    def test_rekey_gives_the_keys_the_writer_gives(self):
        loop = _clip(3, loop=True)
        want = w.new_payload(self.old, loop, resize=True)[0]
        open_keys = w.new_payload(self.old, dict(loop, loop=False))[0]
        out, _ = w.rekey(_master_tpac("t", self._shift_payload(open_keys, 1.0), 2, 3), loop=True, sources=(0.0, 3.0))
        item = tcm.parse(out).items[0]
        self.assertEqual(tcm.segment_payload(item, item.segments[0]), want)

    def test_rekey_moves_a_one_shot_to_zero_and_keeps_its_keys(self):
        shot = w.new_payload(self.old, _clip(3, loop=False))[0]
        out, info = w.rekey(_master_tpac("t", self._shift_payload(shot, 1.0), 2, 3), loop=False, sources=(0.0, 3.0))
        item = tcm.parse(out).items[0]
        self.assertEqual(tcm.segment_payload(item, item.segments[0]), shot)
        self.assertEqual((info["keys"], info["was"]), (3, 3))

    def test_rekey_gives_a_one_key_pose_two_keys(self):
        ident = (1.0, 0.0, 0.0, 0.0)
        still = _master_tpac("t", _anim_payload("t", [[ident], [ident]], [(0, 0, 0.5)]), 2, 1)
        out, info = w.rekey(still, loop=True, sources=(0.0, 1.0))
        item = tcm.parse(out).items[0]
        times, rot, root, _ = self._keys(tcm.segment_payload(item, item.segments[0]))
        self.assertEqual(times, [0.0, 1.0])
        np.testing.assert_allclose(root[0], root[1])
        self.assertEqual(w.rekey(out, loop=True, sources=(0.0, 1.0))[0], out)

    def test_rekey_knows_a_closed_loop_whose_rotations_are_not_unit_length(self):
        # the elephant's retargeted keys are a few 1e-7 off unit length: a test by the dot product of the first and
        # the last key took such a closed loop for an open one, and a second run added a second closing key
        near = (0.9995, 0.01, 0.02, 0.0)
        other = (0.9, 0.3, 0.1, 0.2)
        old_form = _master_tpac("t", _anim_payload("t", [[near, other, other], [other, near, near]], [(0, 0, 0)] * 3), 2, 3)
        once, info = w.rekey(old_form, loop=True, sources=(0.0, 3.0))
        self.assertEqual(info["keys"], 4)
        twice, info2 = w.rekey(once, loop=True, sources=(0.0, 3.0))
        self.assertEqual(twice, once)
        self.assertEqual(info2["keys"], 4)

    def test_rekey_refuses_to_close_a_master_that_keys_from_zero_and_is_not_closed(self):
        # only a master of the old form (keys from 1) is this migration's: a one-shot already at 0..n-1, passed as
        # a loop by mistake, would gain a key and fall out of step with its record, and no gate would see it
        shot = _master_tpac("t", w.new_payload(self.old, _clip(3, loop=False))[0], 2, 3)
        with self.assertRaisesRegex(ValueError, "keys from 0"):
            w.rekey(shot, loop=True, sources=(0.0, 2.0))
        self.assertEqual(w.rekey(shot, loop=False, sources=(0.0, 2.0))[0], shot)

    def test_a_resize_never_keeps_the_old_times_even_when_the_key_count_is_the_same(self):
        # an old master of 4 keys at 1 to 4, written over with a loop of 3 frames: 4 keys again, and the records
        # are written anew for 0 to 3, so the old 1 to 4 must not stay
        ident = (1.0, 0.0, 0.0, 0.0)
        old4 = _master_tpac("t", _anim_payload("t", [[ident] * 4, [ident] * 4], [(0, 0, 0)] * 4), 2, 4)
        out, info = w.rewrite(old4, _clip(3, loop=True), resize=True)
        item = tcm.parse(out).items[0]
        self.assertEqual(w.parse_anim(tcm.segment_payload(item, item.segments[0]))["rot"][0]["t"], [0.0, 1.0, 2.0, 3.0])

    def test_rekey_apply_refuses_a_backup_inside_a_module_and_leaves_no_half_written_file(self):
        shot = w.new_payload(self.old, _clip(3, loop=False))[0]
        old_form = _master_tpac("t", self._shift_payload(shot, 1.0), 2, 3)
        with tempfile.TemporaryDirectory() as tmp, mock.patch.object(w, "game_or_kit_running", return_value=[]), \
                redirect_stdout(io.StringIO()) as said:
            folder = os.path.join(tmp, "Modules", "X", "Assets")
            os.makedirs(folder)
            master = os.path.join(folder, "t_geo.tpac")
            with open(master, "wb") as fh:
                fh.write(old_form)
            args = ["--rekey", "shot", "--sources", "0:3", "--master", master, "--apply", "--backup-dir"]
            code = w.main(args + [os.path.join(tmp, "Modules", "bak")])
            self.assertEqual(code, 2)
            self.assertIn("inside a module", said.getvalue())
            with open(master, "rb") as fh:
                self.assertEqual(fh.read(), old_form)
            free = os.path.join(tmp, "free")                    # a master outside any module: its own folder is no backup
            os.makedirs(free)
            beside = os.path.join(free, "t_geo.tpac")
            with open(beside, "wb") as fh:
                fh.write(old_form)
            code = w.main(["--rekey", "shot", "--sources", "0:3", "--master", beside, "--apply", "--backup-dir", free])
            self.assertEqual(code, 2)
            self.assertIn("own folder", said.getvalue())
            with open(beside, "rb") as fh:
                self.assertEqual(fh.read(), old_form)
            self.assertEqual(w.main(["--rekey", "shot", "--master", master]), 1)        # no --sources: refused, nothing written
            code = w.main(args + [os.path.join(tmp, "bak")])
            self.assertEqual(code, 0)
            with open(os.path.join(tmp, "bak", "t_geo.tpac"), "rb") as fh:
                self.assertEqual(fh.read(), old_form)
            self.assertEqual(sorted(os.listdir(folder)), ["t_geo.tpac"])
            with open(master, "rb") as fh:
                self.assertNotEqual(fh.read(), old_form)

    def test_rekey_refuses_keys_that_are_not_one_run(self):
        a = w.parse_anim(self.old)
        for tr in a["rot"] + [a["root_pos"]]:
            tr["t"] = [1.0, 2.0, 5.0]
        with self.assertRaisesRegex(ValueError, "one run"):
            w.rekey(_master_tpac("t", w.build_anim(a), 2, 3), loop=True, sources=(0.0, 3.0))

    def _shift_payload(self, payload, by):
        a = w.parse_anim(payload)
        for tr in a["rot"] + [a["root_pos"]]:
            tr["t"] = [t + by for t in tr["t"]]
        return w.build_anim(a)

    def _shift(self, raw, by):
        item = tcm.parse(raw).items[0]
        return self._shift_payload(tcm.segment_payload(item, item.segments[0]), by)


class RootHeightTests(unittest.TestCase):
    """set_root_z: the ADOD master rides 0.13 m above its own rest frame from its second key on (2026-11-19)."""

    def setUp(self):
        q = (0.5, 0.5, 0.5, 0.5)
        self.payload = _anim_payload("all", [[q] * 4, [q] * 4], [(0, 0, 0), (0.1, 0.2, 0.135), (0.1, 0.2, 0.140), (0, 0, -1.2)])
        self.raw = _master_tpac("all", self.payload, 2, 4)

    def _root(self, raw):
        item = tcm.parse(raw).items[0]
        return w.parse_anim(tcm.segment_payload(item, item.segments[0]))

    def test_every_key_from_the_first_one_named_moves_by_the_same_amount(self):
        out, info = w.set_root_z(self.raw, ref_time=2.0, z=0.006, from_time=2.0)
        self.assertAlmostEqual(info["dz"], -0.129, places=6)
        self.assertEqual(info["keys"], 3)
        a = self._root(out)
        np.testing.assert_allclose(a["root_pos"]["v"][:, 2], [0.0, 0.006, 0.011, -1.329], atol=1e-6)
        np.testing.assert_allclose(a["root_pos"]["v"][:, :2], [[0, 0], [0.1, 0.2], [0.1, 0.2], [0, 0]], atol=1e-6)

    def test_rotations_times_and_metadata_stay(self):
        out, _ = w.set_root_z(self.raw, ref_time=2.0, z=0.006, from_time=2.0)
        a, was = self._root(out), self._root(self.raw)
        self.assertEqual(a["root_pos"]["t"], was["root_pos"]["t"])
        for new, old in zip(a["rot"], was["rot"]):
            np.testing.assert_array_equal(new["v"], old["v"])
        item, old_item = tcm.parse(out).items[0], tcm.parse(self.raw).items[0]
        _, end = tcm._metadata_span(item)
        self.assertEqual(bytes(item.toc[:end + 8]), bytes(old_item.toc[:end + 8]))
        seg = item.segments[0]
        self.assertEqual(tcm.segment_hash(item, seg), xxhash.xxh64_intdigest(tcm.segment_payload(item, seg)))

    def test_a_second_run_changes_nothing(self):
        once, _ = w.set_root_z(self.raw, ref_time=2.0, z=0.006, from_time=2.0)
        twice, info = w.set_root_z(once, ref_time=2.0, z=0.006, from_time=2.0)
        self.assertEqual(once, twice)
        self.assertAlmostEqual(info["dz"], 0.0, places=6)

    def test_refuses_a_reference_time_with_no_key(self):
        with self.assertRaisesRegex(ValueError, "no key at time"):
            w.set_root_z(self.raw, ref_time=9.0, z=0.0, from_time=2.0)

    def test_refuses_a_reference_key_that_the_write_would_not_move(self):
        # a key before from_time stays where it is, so every further run would lower the others again
        with self.assertRaisesRegex(ValueError, "before the first key that moves"):
            w.set_root_z(self.raw, ref_time=1.0, z=0.05, from_time=2.0)


def _treadmill(frames, stride, feet_stride=None, duty=0.6):
    """Four soles on a ground that runs at `feet_stride` metres a loop (default: the claim), planted for `duty`
    of the loop and carried forward 0.3 m above it for the rest."""
    feet_stride = stride if feet_stride is None else feet_stride
    out = {}
    for k, tag in enumerate(("FL", "FR", "BL", "BR")):
        p = np.zeros((frames, 3))
        for f in range(frames):
            ph = (f / frames + k * 0.25) % 1.0
            if ph < duty:
                p[f] = [0.3, 0.5 * feet_stride * duty - feet_stride * ph, 0.0]
            else:
                u = (ph - duty) / (1 - duty)
                p[f] = [0.3, -0.5 * feet_stride * duty + feet_stride * duty * u, 0.3]
        out[tag] = p
    return out


class StrideGateTests(unittest.TestCase):
    def test_feet_that_ride_the_claimed_ground_pass(self):
        m = c.measure(_treadmill(60, 3.0), 3.0)
        self.assertAlmostEqual(m["stride"], 3.0, places=6)
        self.assertEqual(c.judge(m, 3.0, 0.05, 0.08), [])

    def test_feet_that_cover_half_the_claim_fail_on_stride_and_slip(self):
        # the 2026-11-18 defect: canter claimed 3.6 m, the feet stepped about 1.8 m
        m = c.measure(_treadmill(60, 3.6, feet_stride=1.8), 3.6)
        why = c.judge(m, 3.6, 0.05, 0.08)
        self.assertTrue(any("claims 3.60" in w for w in why), why)
        self.assertTrue(any("slips" in w for w in why), why)

    def test_a_backward_walk_measures_like_a_forward_one(self):
        soles = {t: p * [1, -1, 1] for t, p in _treadmill(60, 2.0).items()}
        m = c.measure(soles, 2.0)
        self.assertAlmostEqual(m["stride"], 2.0, places=6)
        self.assertEqual(c.judge(m, 2.0, 0.05, 0.08), [])

    def test_a_foot_that_never_plants_is_named(self):
        soles = _treadmill(60, 2.0)
        soles["FL"][:, 2] = 0.5
        why = c.judge(c.measure(soles, 2.0), 2.0, 0.05, 0.08)
        self.assertTrue(any(w.startswith("FL is planted") for w in why), why)

    def test_a_clip_with_no_travel_fails(self):
        self.assertTrue(c.judge(c.measure(_treadmill(60, 2.0), 0.0), None, 0.05, 0.08))

    def test_bound_clips_are_the_animation_names_in_the_action_sets(self):
        xml = '<action_set id="a"><action type="act_x" animation="elephant_walk" />\n<action type="act_y"\n animation="mumakil_walk"/></action_set>'
        self.assertEqual(c.bound_clips(xml), {"elephant_walk", "mumakil_walk"})


def _record_bytes(name, duration, keys, usage=None):
    """A single-clip package as far as clip_record reads it: the name at 0x48, then Duration and the key range."""
    raw = bytes(0x48) + struct.pack("<i", len(name)) + name.encode() + bytes(12) + struct.pack("<3f", duration, 0.0, keys)
    if usage:
        raw += bytes(40) + struct.pack("<i", 13) + b"quad_movement" + bytes(4) + struct.pack("<3f", *usage)
    return raw


class PaceTableTests(unittest.TestCase):
    """What the engine's mount movement node does with a clip record (the 2026-11-19 defects)."""
    CONFIG = {"sets": ("as_a",), "stand_action": "act_stand",
              "pace_actions": {"backward": "act_b", "strafe": ("act_sr", "act_sl"), "walk": "act_w", "faster": ("act_t", "act_c")}}

    def _rec(self, duration, keys, limit_min=0.2, limit_max=2.5):
        return {"duration": duration, "keys": keys, "loop": 3.0, "limit_min": limit_min, "limit_max": limit_max}

    def _sound(self):
        """One named set with every pace row, on records the engine plays well."""
        rows = {"as_a": {"act_b": "back", "act_sr": "side", "act_sl": "side", "act_w": "walk", "act_t": "trot",
                         "act_c": "canter", "act_stand": "pose"}}
        records = {"back": self._rec(1.0, 30.0, -2.5, -0.2), "side": self._rec(2.533, 76.0), "walk": self._rec(1.0, 76.0),
                   "trot": self._rec(1.0, 61.0, 2.3, 4.5), "canter": self._rec(1.0, 48.0, 4.8, 7.2),
                   "pose": self._rec(4.0, 1.0, None, None)}
        return rows, records

    def _findings(self, rows, records):
        return c.record_findings(rows, records, self.CONFIG)[0]

    def test_a_pace_action_on_a_clip_the_stride_check_skips_is_a_finding(self):
        # `turns` and `pending` are keyed by clip name: without this a gait action bound to one escaped the gate
        rows, records = self._sound()
        rows["as_a"]["act_c"], rows["as_a"]["act_w"] = "leap", "spin"
        records["leap"], records["spin"] = self._rec(1.0, 44.0, 4.8, 7.2), self._rec(0.8, 48.0, 0.0, 0.0)
        config = dict(self.CONFIG, turns=("spin",), pending={"leap": "kept until phase 2"})
        got = c.record_findings(rows, records, config)[0]
        self.assertTrue([f for f in got if "act_c" in f and "leap" in f and "skips" in f], got)
        self.assertTrue([f for f in got if "act_w" in f and "spin" in f and "skips" in f], got)
        self.assertEqual([f for f in c.record_findings(*self._sound(), config)[0] if "skips" in f], [])

    def test_the_spider_entry_names_only_clips_the_generator_makes_or_june_left(self):
        import spider_gait
        spider = c.CREATURES["spider"]
        for clip in list(spider["gaits"]) + list(spider["turns"]) + list(spider["axis"]):
            self.assertIn(clip, spider_gait.CLIPS, clip)
        self.assertNotIn("pending", spider, "#1550 put act_spider_jump on spider_jump_start, a clip with no travel")
        self.assertEqual(len(spider["feet"]), 8)

    def test_records_are_found_by_the_clips_own_name_and_two_files_with_one_name_are_a_finding(self):
        with tempfile.TemporaryDirectory() as folder:
            def put(file_name, clip, loop):
                with open(os.path.join(folder, file_name), "wb") as fh:
                    fh.write(_record_bytes(clip, 1.0, 30.0, (loop, 0.2, 2.5)))

            put("x_walk_anm.tpac", "x_walk", 2.0)
            put("new_animation_clip_3_anm.tpac", "x_idle", 0.0)              # the Kit's name for a record file
            put("x_trot_anm.tpac", "x_trot", 3.0)
            put("new_animation_clip_7_anm.tpac", "x_trot", 9.0)              # a second file with the trot's name
            put("x_walk_geo.tpac", "not a record", 0.0)
            records, files, twice = c.records_in(folder, {"x_walk", "x_idle", "x_trot", "x_gone"})
        self.assertEqual(sorted(records), ["x_idle", "x_trot", "x_walk"])
        self.assertEqual(os.path.basename(files["x_idle"]), "new_animation_clip_3_anm.tpac")
        self.assertAlmostEqual(records["x_trot"]["loop"], 3.0, places=6)     # the file of its own name is the one read
        self.assertEqual(len(twice), 1, twice)
        self.assertIn("x_trot is the clip of 2 record files", twice[0])
        self.assertIn("new_animation_clip_7_anm.tpac", twice[0])

    def test_a_record_file_that_is_cut_short_is_a_named_finding_not_a_traceback(self):
        with tempfile.TemporaryDirectory() as folder:
            with open(os.path.join(folder, "x_walk_anm.tpac"), "wb") as fh:
                fh.write(_record_bytes("x_walk", 1.0, 30.0, (2.0, 0.2, 2.5)))
            with open(os.path.join(folder, "broken_anm.tpac"), "wb") as fh:
                fh.write(b"TPAC")
            records, _, problems = c.records_in(folder, {"x_walk"})
        self.assertEqual(sorted(records), ["x_walk"])
        self.assertEqual(len(problems), 1, problems)
        self.assertIn("broken_anm.tpac cannot be read as a clip record", problems[0])

    def test_clip_problems_names_a_missing_file_and_an_uncooked_clip(self):
        with tempfile.TemporaryDirectory() as root:
            folder = os.path.join(root, "clips")
            os.makedirs(folder)
            os.makedirs(os.path.join(root, "RuntimeDataCache"))
            guid = uuid.uuid4()
            for name in ("ready_anm.tpac", "ready_geo.tpac", "raw_anm.tpac", "raw_geo.tpac"):
                with open(os.path.join(folder, name), "wb") as fh:
                    fh.write(b"TPAC" + b"\0" * 4 + (guid if name.startswith("ready") else uuid.uuid4()).bytes_le + b"\0" * 8)
                os.utime(os.path.join(folder, name), (1_000_000, 1_000_000))
            entry = os.path.join(root, "RuntimeDataCache", str(guid) + ".rdc")
            with open(entry, "wb") as fh:
                fh.write(b"RDC0")
            os.utime(entry, (2_000_000, 2_000_000))
            got = c.clip_problems(root, "clips", ["ready", "raw", "gone"])
        self.assertEqual(len(got), 2, got)
        self.assertIn("raw: it has no cache entry", got[0])
        self.assertIn("gone: no clip record or no master", got[1])

    def test_clip_record_reads_duration_key_range_and_the_pace_limits(self):
        r = c.clip_record(_record_bytes("mumakil_walk", 1.0, 76.0, (3.0, 0.2, 2.5)))
        self.assertAlmostEqual(r["duration"], 1.0, places=6)
        self.assertAlmostEqual(r["keys"], 76.0, places=6)
        self.assertAlmostEqual(r["loop"], 3.0, places=6)
        self.assertAlmostEqual(r["limit_min"], 0.2, places=6)
        self.assertAlmostEqual(r["limit_max"], 2.5, places=6)

    def test_a_record_with_no_usage_has_no_limits(self):
        r = c.clip_record(_record_bytes("mumakil_idle_1", 18.666, 200.0))
        self.assertAlmostEqual(r["duration"], 18.666, places=3)
        self.assertEqual((r["loop"], r["limit_min"], r["limit_max"]), (None, None, None))

    def test_set_rows_maps_each_set_to_its_actions(self):
        xml = ('<action_set id="as_a" skeleton="s"><action type="act_b" animation="back" />\n'
               '<action animation="side"\n type="act_sr"/></action_set><action_set id="as_b"><action type="act_b" animation="b2"/></action_set>')
        self.assertEqual(c.set_rows(xml), {"as_a": {"act_b": "back", "act_sr": "side"}, "as_b": {"act_b": "b2"}})

    def test_set_rows_keeps_a_self_closing_set_out_of_the_next_sets_rows(self):
        # the live file has 593 sets with no body; the rows after one belong to the next set that opens
        xml = '<action_set id="as_x" base_set="y" />\n<action_set id="as_a"><action type="act_b" animation="back"/></action_set>'
        self.assertEqual(c.set_rows(xml), {"as_a": {"act_b": "back"}})

    def test_a_set_whose_records_the_engine_plays_well_passes_and_counts_its_rows(self):
        self.assertEqual(c.record_findings(*self._sound(), self.CONFIG), ([], 7))

    def test_a_backward_clip_with_a_positive_upper_limit_is_a_finding(self):
        # ADOD's record: 0.2 to 2.5. Once in the backward pace, the beast stays in it up to 2.5 m/s forward.
        rows, records = self._sound()
        records["back"] = self._rec(1.0, 30.0)
        why = self._findings(rows, records)
        self.assertEqual(len(why), 1, why)
        self.assertIn("as_a", why[0])
        self.assertIn("back", why[0])
        self.assertIn("2.50", why[0])

    def test_a_pace_clip_with_no_limits_is_a_finding(self):
        rows, records = self._sound()
        records["back"] = self._rec(1.0, 30.0, None, None)
        why = self._findings(rows, records)
        self.assertEqual(len(why), 1, why)
        self.assertIn("no quad_movement", why[0])

    def test_a_strafe_clip_whose_duration_is_far_under_its_keys_is_a_finding(self):
        # the mumak walk on the strafe rows: 76 keys (2.53 s) with Duration 1.0, so one loop plays in 0.7 to 1.3 s
        rows, records = self._sound()
        records["side"] = self._rec(1.0, 76.0)
        why = self._findings(rows, records)
        self.assertEqual(len(why), 2, why)
        self.assertTrue(all("2.53" in w and "1.00" in w for w in why), why)

    def test_a_strafe_clip_whose_duration_is_far_over_its_keys_is_a_finding(self):
        rows, records = self._sound()
        records["side"] = self._rec(30.0, 76.0)
        why = self._findings(rows, records)
        self.assertEqual(len(why), 2, why)
        self.assertTrue(all("30.00" in w for w in why), why)

    def test_a_strafe_clip_near_its_real_duration_passes(self):
        # the vanilla horse: 19 keys with Duration 0.75, and 30 keys with 0.8
        for duration, keys in ((2.533, 76.0), (0.8, 30.0), (0.75, 19.0)):
            rows, records = self._sound()
            records["side"] = self._rec(duration, keys)
            self.assertEqual(self._findings(rows, records), [], (duration, keys))

    def test_a_faster_pace_whose_minimum_is_not_above_the_walks_is_a_finding(self):
        # the trot records cloned the walk's 0.2 to 2.5: the engine steps down only below the current pace's
        # minimum, so after one charge the beast stayed in the trot down to 0.2 m/s
        rows, records = self._sound()
        records["trot"] = self._rec(1.0, 61.0, 0.2, 2.5)
        why = self._findings(rows, records)
        self.assertEqual(len(why), 1, why)
        self.assertIn("act_t", why[0])
        self.assertIn("trot", why[0])
        self.assertIn("0.20", why[0])

    def test_only_the_walk_is_judged_against_the_faster_paces(self):
        # ADOD's gallop minimum (4.3) is under its canter's (4.8): the fast-pace rebuild owns that
        rows, records = self._sound()
        records["trot"] = self._rec(1.0, 61.0, 4.9, 6.0)
        self.assertEqual(self._findings(rows, records), [])

    def test_a_named_set_a_row_or_a_record_that_is_not_there_is_a_finding(self):
        rows, records = self._sound()
        self.assertEqual(self._findings({}, records), ["no action set as_a with rows"])
        del rows["as_a"]["act_stand"]
        del records["walk"]
        why, checked = c.record_findings(rows, records, self.CONFIG)
        self.assertEqual(len(why), 2, why)
        self.assertTrue(any("no act_stand row" in w for w in why), why)
        self.assertTrue(any("walk" in w and "no clip record" in w for w in why), why)
        self.assertEqual(checked, 5)

    def test_any_row_of_a_named_set_whose_clip_has_no_record_is_a_finding(self):
        # not a pace row: an attack whose loose record is gone would never have its cache entry compared
        rows, records = self._sound()
        rows["as_a"]["act_attack"] = "attack"
        why, checked = c.record_findings(rows, records, self.CONFIG)
        self.assertEqual(why, ["as_a binds act_attack to attack, which has no clip record in the clip folder"])
        self.assertEqual(checked, 7)

    def test_clip_record_names_its_master_and_its_first_key(self):
        master = uuid.UUID("118971c3-b2d6-487f-91f4-a5f5a3b6fc22")
        name = "elephant_stand_for_movement_data"
        raw = (bytes(0x48) + struct.pack("<i", len(name)) + name.encode() + bytes(12) + struct.pack("<3f", 4.0, 10.0, 11.0)
               + bytes(16) + master.bytes_le + bytes(40))
        r = c.clip_record(raw)
        self.assertEqual(r["animation"], master.bytes_le)
        self.assertAlmostEqual(r["first"], 10.0, places=6)
        self.assertAlmostEqual(r["keys"], 1.0, places=6)

    def test_a_standing_pose_that_rides_above_the_ground_is_a_finding(self):
        # ADOD's master: every sole 0.13 m up on the pose's keys, so the mumak (3 times the size) stood 0.4 m high
        soles = {t: np.array([[0.0, 0.0, 0.13 + 0.01 * k]] * 2) for k, t in enumerate(("FL", "FR", "BL", "BR"))}
        why = c.stand_finding(soles)
        self.assertIn("0.130", why)
        down = {t: p - [0, 0, 0.2] for t, p in soles.items()}
        self.assertIn("-0.070", c.stand_finding(down))

    def test_a_standing_pose_on_the_ground_passes(self):
        soles = {t: np.array([[0.0, 0.0, 0.001 * k]] * 2) for k, t in enumerate(("FL", "FR", "BL", "BR"))}
        self.assertIsNone(c.stand_finding(soles))


class KeyRangeGateTests(unittest.TestCase):
    """A record must play keys its master has (KeyTimeTests), and a loop's closing key is no step of its own."""

    @staticmethod
    def _anim(times):
        n = len(times)
        return {"rot": [{"t": list(times), "v": np.arange(4 * n, dtype=float).reshape(n, 4)}],
                "root_pos": {"t": list(times), "v": np.zeros((n, 4))}}

    def test_sources_inside_the_keys_fit(self):
        self.assertIsNone(c.range_finding([0.0, 1.0, 2.0, 3.0], {"first": 0.0, "keys": 3.0}))
        self.assertIsNone(c.range_finding([0.0, 1.0, 2.0, 3.0], {"first": 1.0, "keys": 2.0}))    # the warg's shape
        self.assertIsNone(c.range_finding([0.0, 1.0], {"first": 0.0, "keys": 1.0}))                # a held pose

    def test_a_first_source_before_the_first_key_is_a_finding(self):
        why = c.range_finding([1.0, 2.0, 3.0], {"first": 0.0, "keys": 3.0})                        # the old writer
        self.assertIn("plays keys 0 to 3", why)
        self.assertIn("holds keys 1 to 3", why)

    def test_a_last_source_after_the_last_key_is_a_finding(self):
        self.assertIn("holds keys 0 to 29", c.range_finding([float(i) for i in range(30)], {"first": 0.0, "keys": 30.0}))

    def test_a_master_with_no_key_is_a_finding(self):
        self.assertIn("no key", c.range_finding([], {"first": 0.0, "keys": 3.0}))

    def test_one_loop_is_the_keys_before_the_closing_one(self):
        got = c.played_keys(self._anim([0.0, 1.0, 2.0, 3.0, 4.0]), {"first": 0.0, "keys": 4.0})
        self.assertEqual(list(got["rot"][0]["t"]), [0.0, 1.0, 2.0, 3.0])
        self.assertEqual(len(got["rot"][0]["v"]), 4)
        self.assertEqual(len(got["root_pos"]["v"]), 4)

    def test_a_record_that_starts_inside_the_master_plays_from_its_first_source(self):
        got = c.played_keys(self._anim([0.0, 1.0, 2.0, 3.0, 4.0]), {"first": 1.0, "keys": 3.0})
        self.assertEqual(list(got["rot"][0]["t"]), [1.0, 2.0, 3.0])

    def test_range_findings_name_the_clip_and_a_clip_whose_master_is_not_found_is_a_finding(self):
        # the first version skipped such a clip: on the live elephant two bound clips passed unread, their masters
        # beside the mesh and not in the clips folder
        records = {"a": {"first": 0.0, "keys": 3.0, "animation": b"A"}, "b": {"first": 0.0, "keys": 2.0, "animation": b"B"},
                   "c": {"first": 0.0, "keys": 9.0, "animation": b"C"}}
        held = {b"A": ("a_geo.tpac", self._anim([1.0, 2.0, 3.0])), b"B": ("b_geo.tpac", self._anim([0.0, 1.0, 2.0]))}
        with mock.patch.object(c, "master_by_guid", side_effect=lambda paths, guid: held.get(guid)):
            got, read = c.range_findings(["a", "b", "c", "no_record"], records, ["x"])
        self.assertEqual(read, 2)
        self.assertEqual(len(got), 2, got)
        self.assertIn("a: its record plays keys 0 to 3", got[0])
        self.assertIn("a_geo.tpac", got[0])
        self.assertIn("c: no master", got[1])

    def test_a_track_whose_key_times_are_no_numbers_is_a_finding(self):
        anim = self._anim([0.0, 1.0, 2.0, 3.0])
        anim["rot"].append({"t": [float("nan"), float("nan")], "v": np.zeros((2, 4))})
        self.assertIn("holds keys nan to nan", c.range_finding(c.key_span(anim), {"first": 0.0, "keys": 3.0}))

    def test_the_range_is_held_on_every_track_that_has_keys(self):
        anim = self._anim([0.0, 1.0, 2.0, 3.0])
        anim["rot"].append({"t": [1.0, 2.0], "v": np.zeros((2, 4))})          # a bone the Kit keyed for less
        anim["rot"].append({"t": [], "v": np.zeros((0, 4))})                   # a bone with no key is not held
        self.assertEqual(c.key_span(anim), [1.0, 2.0])
        self.assertIsNone(c.range_finding(c.key_span(anim), {"first": 1.0, "keys": 1.0}))
        self.assertIn("holds keys 1 to 2", c.range_finding(c.key_span(anim), {"first": 0.0, "keys": 3.0}))
        self.assertEqual(c.key_span({"rot": [{"t": [], "v": []}], "root_pos": {"t": [], "v": []}}), [])


class CacheStateTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        root = self.dir.name
        os.makedirs(os.path.join(root, "RuntimeDataCache"))
        self.anm, self.geo = os.path.join(root, "x_anm.tpac"), os.path.join(root, "x_geo.tpac")
        guid = bytes(range(16))
        with open(self.anm, "wb") as fh:
            fh.write(b"TPAC" + struct.pack("<I", 2) + guid)
        with open(self.geo, "wb") as fh:
            fh.write(b"master")
        self.entry = os.path.join(root, "RuntimeDataCache", str(uuid.UUID(bytes_le=guid)) + ".rdc")

    def _cook(self, entry_time, master_time):
        with open(self.entry, "wb") as fh:
            fh.write(b"RDC0")
        os.utime(self.entry, (entry_time, entry_time))
        os.utime(self.geo, (master_time, master_time))

    def test_a_clip_with_no_entry_is_named(self):
        self.assertIn("no cache entry", c.cache_state(self.dir.name, self.anm, self.geo))

    def test_an_entry_cooked_before_the_master_was_written_is_stale(self):
        self._cook(1_000_000, 2_000_000)
        self.assertIn("older than its master", c.cache_state(self.dir.name, self.anm, self.geo))

    def test_an_entry_cooked_after_the_master_is_current(self):
        self._cook(2_000_000, 1_000_000)
        os.utime(self.anm, (1_500_000, 1_500_000))
        self.assertIsNone(c.cache_state(self.dir.name, self.anm, self.geo))

    def test_an_entry_cooked_before_the_record_was_written_is_stale(self):
        # the Kit cooked the backward walk again after a limits-only rewrite of its record (2026-11-19)
        self._cook(2_000_000, 1_000_000)
        os.utime(self.anm, (3_000_000, 3_000_000))
        self.assertIn("older than its clip record", c.cache_state(self.dir.name, self.anm, self.geo))


class StandFindingsTests(unittest.TestCase):
    """stand_findings on a module folder: one bone that is its own four feet, a two-key pose in the skeleton's package."""
    CONFIG = {"sets": ("as_a",), "stand_action": "act_stand", "clips": "clips", "skeleton": ("skel_geo.tpac", "s"),
              "feet": {t: "root" for t in ("FL", "FR", "BL", "BR")}}
    BONES = [{"name": "root", "parent_idx": -1, "rest_frame": [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]}]
    ROWS = {"as_a": {"act_stand": "pose", "act_attack": "pose"}}

    def _module(self, sole_z):
        d = tempfile.TemporaryDirectory()
        self.addCleanup(d.cleanup)
        os.makedirs(os.path.join(d.name, "clips"))
        os.makedirs(os.path.join(d.name, "RuntimeDataCache"))
        ident = (1.0, 0.0, 0.0, 0.0)
        self.skel = os.path.join(d.name, "skel_geo.tpac")
        with open(self.skel, "wb") as fh:
            fh.write(_master_tpac("all", _anim_payload("all", [[ident] * 2], [(0, 0, sole_z)] * 2), 1, 2))
        record = (bytes(0x48) + struct.pack("<i", 4) + b"pose" + bytes(12) + struct.pack("<3f", 4.0, 1.0, 2.0)
                  + bytes(16) + bytes([7] * 16) + bytes(40))
        self.anm = os.path.join(d.name, "clips", "pose_anm.tpac")
        with open(self.anm, "wb") as fh:
            fh.write(record)
        self.entry = os.path.join(d.name, "RuntimeDataCache", str(uuid.UUID(bytes_le=record[8:24])) + ".rdc")
        with open(self.entry, "wb") as fh:
            fh.write(b"RDC0")
        for path, when in ((self.skel, 1_000_000), (self.anm, 1_000_000), (self.entry, 2_000_000)):
            os.utime(path, (when, when))
        return d.name, {"pose": c.clip_record(record)}

    def test_a_grounded_pose_with_a_current_entry_passes_and_is_counted(self):
        root, records = self._module(0.001)
        self.assertEqual(c.stand_findings(self.ROWS, records, self.CONFIG, root, self.BONES), ([], 1, 1))

    def test_a_pose_that_rides_above_the_ground_is_a_finding(self):
        root, records = self._module(0.13)
        why = c.stand_findings(self.ROWS, records, self.CONFIG, root, self.BONES)[0]
        self.assertEqual(len(why), 1, why)
        self.assertIn("+0.130", why[0])

    def test_a_master_written_after_its_clips_were_cooked_is_a_finding(self):
        # the HIGH review finding: --root-z grounds the master, and until a Kit start the game plays the old keys
        root, records = self._module(0.001)
        os.utime(self.skel, (3_000_000, 3_000_000))
        why = c.stand_findings(self.ROWS, records, self.CONFIG, root, self.BONES)[0]
        self.assertEqual(len(why), 1, why)
        self.assertIn("pose", why[0])
        self.assertIn("older than its master", why[0])

    def test_a_clip_whose_master_is_in_another_package_is_not_judged_against_the_skeleton_package(self):
        root, records = self._module(0.001)
        records["other"] = dict(records["pose"], animation=bytes([8] * 16))
        os.utime(self.skel, (3_000_000, 3_000_000))
        rows = {"as_a": {"act_walk": "other"}}
        self.assertEqual(c.stand_findings(rows, records, self.CONFIG, root, self.BONES), ([], 0, 0))


def _kit_master(name, payload, bones, frames):
    """A two-item master as the Kit writes one: the import record `<name>.fbx`, then the animation."""
    g_rec, g_anim = bytes([3] * 16), bytes([7] * 16)

    def item(type_guid, guid, item_name, meta, data, pack):
        blob = lz4.block.compress(data, mode="high_compression", store_size=False) if pack else data
        body = struct.pack("<q", len(meta)) + meta
        toc = (type_guid + guid + struct.pack("<I", 0) + struct.pack("<i", len(item_name)) + item_name.encode() + body
               + struct.pack("<Q", xxhash.xxh64_intdigest(body)) + struct.pack("<i", 1)
               + struct.pack("<QQQ", 0, len(data), len(blob)) + guid + bytes.fromhex("6d817dd0") + bytes(12)
               + struct.pack("<Q", xxhash.xxh64_intdigest(data)) + bytes(5) + struct.pack("<i", 0))
        return tcm._reparse(toc, [blob])

    path = "$BASE/Modules/X/AssetSources/a/%s.fbx" % name
    rec = item(bytes([5] * 16), g_rec, name + ".fbx",
               struct.pack("<ii", 1, len(path)) + path.encode() + bytes(8) + struct.pack("<i", 1)
               + w.SKELETAL_ANIMATION_TYPE + g_anim + struct.pack("<i", 0), b"import settings", False)
    anim = item(w.SKELETAL_ANIMATION_TYPE, g_anim, name,
                struct.pack("<i", 1) + g_rec + b"\x00" + bytes(range(16)) + struct.pack("<iii", bones, frames, 0),
                payload, True)
    return tcm.serialize(bytes([1] * 16), [rec, anim])


class CloneMasterTests(unittest.TestCase):
    def setUp(self):
        ident = (1.0, 0.0, 0.0, 0.0)
        self.raw = _kit_master("old", _anim_payload("old", [[ident] * 3, [ident] * 3], [(0, 0, 0)] * 3), 2, 3)
        self.clip = dict(_clip(5), clip="new_walk")

    def test_the_clone_has_new_names_new_ids_and_its_own_keys(self):
        out, info = w.clone_master(self.raw, self.clip, "new_walk")
        was, now = tcm.parse(self.raw), tcm.parse(out)
        self.assertEqual([it.name for it in now.items], ["new_walk.fbx", "new_walk"])
        self.assertNotEqual(now.package_guid, was.package_guid)
        for a, b in zip(was.items, now.items):
            self.assertNotEqual(a.item_guid, b.item_guid)
            self.assertEqual(b.segments[0].guid, b.item_guid)
        anim = now.items[1]
        keys = w.parse_anim(tcm.segment_payload(anim, anim.segments[0]))
        self.assertEqual((keys["name"], info["frames"]), ("new_walk", 5))
        self.assertEqual(keys["rot"][0]["t"], [0.0, 1.0, 2.0, 3.0, 4.0])

    def test_the_two_items_name_each_other_and_the_skeleton_is_kept(self):
        out, _ = w.clone_master(self.raw, self.clip, "new_walk")
        rec, anim = tcm.parse(out).items
        s, e = tcm._metadata_span(anim)
        meta = bytes(anim.toc[s + 8:e])
        self.assertEqual(meta[4:20], rec.item_guid)
        self.assertEqual(meta[21:37], bytes(range(16)))
        self.assertEqual(struct.unpack_from("<iii", meta, 37), (2, 5, 0))
        s, e = tcm._metadata_span(rec)
        meta = bytes(rec.toc[s + 8:e])
        self.assertIn(b"/a/new_walk.fbx", meta)
        self.assertEqual(meta[-20:-4], anim.item_guid)
        self.assertEqual(rec.blobs, tcm.parse(self.raw).items[0].blobs)

    def test_a_rebuild_keeps_every_id_and_another_name_gets_other_ids(self):
        a, _ = w.clone_master(self.raw, self.clip, "new_walk")
        b, _ = w.clone_master(self.raw, dict(_clip(5, lift=0.5), clip="new_walk"), "new_walk")
        other, _ = w.clone_master(self.raw, self.clip, "other")
        self.assertNotEqual(a, b)
        self.assertEqual(a[8:24], b[8:24])
        self.assertEqual([it.item_guid for it in tcm.parse(a).items], [it.item_guid for it in tcm.parse(b).items])
        self.assertNotEqual(tcm.parse(a).items[1].item_guid, tcm.parse(other).items[1].item_guid)

    def test_refuses_a_package_that_is_not_a_record_and_an_animation(self):
        ident = (1.0, 0.0, 0.0, 0.0)
        lone = _master_tpac("t", _anim_payload("t", [[ident] * 3, [ident] * 3], [(0, 0, 0)] * 3), 2, 3)
        with self.assertRaises(ValueError):
            w.clone_master(lone, self.clip, "new_walk")


def _take_sample(frames):
    """Two hips and one foot pad: the hips drift 1 cm a frame along X; the pad stands 0.6 m ahead of its rest
    place and rises 0.1 m a frame from frame 3."""
    rest = {"DEF_Thigh.002.L": {"head": [0.3, 0, 2.0]}, "DEF_Thigh.002.R": {"head": [-0.3, 0, 2.0]},
            "DEF_FootPads.Fr.L": {"head": [0.3, -2.0, 0.0]}}
    rot = [1, 0, 0, 0, 1, 0, 0, 0, 1]
    fr = {}
    for f in range(frames + 1):
        dx = 5.0 + 0.01 * f
        fr[str(f)] = {"DEF_Thigh.002.L": [0.3 + dx, 1.0, 2.0, 0, 0, 0] + rot,
                      "DEF_Thigh.002.R": [-0.3 + dx, 1.0, 2.0, 0, 0, 0] + rot,
                      "DEF_FootPads.Fr.L": [5.3, -1.6, 0.1 * max(0, f - 3), 0, 0, 0] + rot}
    return {"fps": 30, "f0": 0, "f1": frames, "rest": rest, "frames": fr}


class TakeTests(unittest.TestCase):
    HIPS = ("DEF_Thigh.002.L", "DEF_Thigh.002.R")
    PAD = "DEF_FootPads.Fr.L"

    def test_the_animal_is_centred_where_it_stands_at_the_reference_frame(self):
        t = g.Take(_take_sample(10), 0, 10)
        np.testing.assert_allclose(t.mid(self.HIPS, 0.0), [0, 0, 2.0], atol=1e-9)
        np.testing.assert_allclose(t.mid(self.HIPS, 1.0), [0.1, 0, 2.0], atol=1e-9)

    def test_phase_does_not_wrap(self):
        t = g.Take(_take_sample(10), 0, 10)
        self.assertAlmostEqual(t.pos(self.PAD, 1.0)[2], 0.7)
        self.assertAlmostEqual(t.pos(self.PAD, 1.3)[2], 0.7)

    def test_a_foot_pad_is_measured_from_where_it_stands_at_the_reference_frame(self):
        t = g.Take(_take_sample(10), 0, 10)
        np.testing.assert_allclose(t.pos(self.PAD, 0.0) - t.rest_pos(self.PAD), [0, 0, 0], atol=1e-9)
        np.testing.assert_allclose(t.pos(self.PAD, 0.5) - t.rest_pos(self.PAD), [0, 0, 0.2], atol=1e-9)
        np.testing.assert_allclose(t.rest_pos(self.HIPS[0]), [0.3, 0, 2.0])

    def test_last_can_be_the_reference_frame(self):
        t = g.Take(_take_sample(10), 0, 10, ref="last")
        np.testing.assert_allclose(t.mid(self.HIPS, 1.0), [0, 0, 2.0], atol=1e-9)
        np.testing.assert_allclose(t.pos(self.PAD, 1.0) - t.rest_pos(self.PAD), [0, 0, 0], atol=1e-9)

    def test_refuses_frames_outside_the_sample(self):
        with self.assertRaises(ValueError):
            g.Take(_take_sample(10), 4, 20)


if __name__ == "__main__":
    unittest.main()
