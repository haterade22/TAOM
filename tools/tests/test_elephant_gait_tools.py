#!/usr/bin/env python3
"""Tests for tools/elephant_gait_retarget.py and tools/elephant_write_masters.py (#1474).

Run:  python -m unittest tools.tests.test_elephant_gait_tools

The retarget's claims are geometric, so they are tested as geometry: a leg's re-timing keeps the stance linear
and has no step in foot speed, the 2-bone solve keeps both bone lengths and reports a target it cannot reach.
The master writer is tested on a tpac built here by hand from the layout it relies on, so the test does not
lean on the tool's own parser for its expectations.
"""
import math
import os
import struct
import sys
import tempfile
import unittest
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


def _clip(frames, bones=2, lift=0.25):
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
            "mats": [[m.reshape(-1).tolist() for m in row] for row in rows]}


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
        self.assertEqual(a["rot"][0]["t"], [1.0, 2.0, 3.0])
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
        self.assertEqual(c.loop_displacement(b"no usage here"), None)
        self.assertTrue(c.judge(c.measure(_treadmill(60, 2.0), 0.0), None, 0.05, 0.08))

    def test_loop_displacement_is_the_float_after_the_usage_name_and_four_zero_bytes(self):
        blob = b"xx" + struct.pack("<i", 13) + b"quad_movement" + bytes(4) + struct.pack("<3f", 3.6, 4.8, 7.2)
        self.assertAlmostEqual(c.loop_displacement(blob), 3.6, places=6)

    def test_bound_clips_are_the_animation_names_in_the_action_sets(self):
        xml = '<action_set id="a"><action type="act_x" animation="elephant_walk" />\n<action type="act_y"\n animation="mumakil_walk"/></action_set>'
        self.assertEqual(c.bound_clips(xml), {"elephant_walk", "mumakil_walk"})


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
        self.assertIsNone(c.cache_state(self.dir.name, self.anm, self.geo))


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
        self.assertEqual(keys["rot"][0]["t"], [1.0, 2.0, 3.0, 4.0, 5.0])

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
