#!/usr/bin/env python3
"""Tests for tools/spider_gait.py (#1541).

Run:  python -m unittest tools.tests.test_spider_gait

The generator's claims are geometric, so they are tested as geometry on the real rest skeleton
(fixtures/spider_rig.json, dumped from the installed spider_skeleton): a planted tip moves back at the clip's
speed and nowhere else, the two sides mirror half a loop apart, the leg solve puts the tip on its target, and
the keys that tools/elephant_write_masters.py would write give the same tips when the gate's own reader
(tools/check_gait_stride.py) plays them.

The gate measures a tip's SOLE, the point under the tip joint at rest, up to 2.8 cm from the joint. It turns with
the tip bone, so a sole moves a few millimetres where the joint is still: NEAR is the tolerance for that. A tip
is also within the gate's 3 cm of the ground for the first and last tenth of its swing, where it has begun to
leave the ground's speed: that is the 4 cm of slip the stride test allows (the gate's own limit is 8 cm).
"""
import io
import json
import math
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from unittest import mock

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_gait_stride as c  # noqa: E402
import elephant_write_masters as w  # noqa: E402
import spider_gait as s  # noqa: E402

NEAR = 1e-2
FIXTURE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fixtures", "spider_rig.json")


def rig():
    with open(FIXTURE) as fh:
        return s.Rig(json.load(fh)["bones"])


def tips_from_keys(clip, bones):
    """The leg tips as the engine would pose them: the clip's keys through the gate's forward kinematics."""
    rot, root = w.clip_keys(clip)
    n = clip["frames"]
    anim = {"rot": [{"t": list(range(1, n + 1)), "v": rot[b]} for b in range(len(bones))],
            "root_pos": {"v": np.column_stack([root, np.zeros(n)])}}
    return c.soles_of(anim, bones, {tag: leg[4] for tag, leg in s.LEGS.items()})


class StancePathTests(unittest.TestCase):
    def test_stance_is_linear_from_front_to_back(self):
        for duty in (0.32, 0.58):
            qs = [s.stance_q(p, duty)[0] for p in np.linspace(0.0, duty, 9, endpoint=False)]
            self.assertAlmostEqual(qs[0], 0.5)
            steps = np.diff(qs)
            self.assertTrue(np.allclose(steps, steps[0]))
            self.assertLess(steps[0], 0.0)
            self.assertEqual(s.stance_q(duty * 0.5, duty)[1], 0.0)

    def test_swing_joins_the_stance_with_no_step_in_speed(self):
        duty, h = 0.4, 1e-5
        rate = -1.0 / duty
        for at in (duty, 1.0):
            before = (s.stance_q((at - h) % 1.0, duty)[0] - s.stance_q((at - 2 * h) % 1.0, duty)[0]) / h
            after = (s.stance_q((at + 2 * h) % 1.0, duty)[0] - s.stance_q((at + h) % 1.0, duty)[0]) / h
            self.assertAlmostEqual(before, rate, places=2)
            self.assertAlmostEqual(after, rate, places=2)

    def test_the_tip_is_off_the_ground_only_in_the_swing(self):
        self.assertEqual(s.stance_q(0.2, 0.5)[1], 0.0)
        self.assertAlmostEqual(s.stance_q(0.75, 0.5)[1], 1.0)


class LegSolveTests(unittest.TestCase):
    def test_the_tip_lands_on_a_target_in_reach_and_bone_lengths_stay(self):
        r = rig()
        rng = np.random.default_rng(7)
        for tag, leg in s.LEGS.items():
            p = [r.rest[r.idx[b]][:3, 3] for b in leg]
            for _ in range(20):
                target = p[4] + rng.uniform(-0.5, 0.5, 3) * np.array([1.0, 1.0, 0.6])
                aim, bend, short = s.solve_leg(p[1], p[2], p[4], target)
                self.assertEqual(short, 0.0)
                knee = p[1] + aim @ (p[2] - p[1])
                tip = knee + aim @ bend @ (p[4] - p[2])
                self.assertLess(np.linalg.norm(tip - target), 1e-9, tag)
                self.assertAlmostEqual(np.linalg.norm(knee - p[1]), np.linalg.norm(p[2] - p[1]))
                self.assertAlmostEqual(np.linalg.norm(tip - knee), np.linalg.norm(p[4] - p[2]))

    def test_a_target_out_of_reach_is_pulled_in_and_reported(self):
        r = rig()
        p = [r.rest[r.idx[b]][:3, 3] for b in s.LEGS["R1"]]
        far = p[1] + (p[4] - p[1]) * 3.0
        aim, bend, short = s.solve_leg(p[1], p[2], p[4], far)
        self.assertGreater(short, 1.0)
        tip = p[1] + aim @ (p[2] - p[1]) + aim @ bend @ (p[4] - p[2])
        self.assertLess(np.linalg.norm(np.cross(tip - p[1], far - p[1])), 1e-9)   # still on the line to the target


class ClipTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rig = rig()
        with open(FIXTURE) as fh:
            cls.bones = json.load(fh)["bones"]
        cls.clips = {name: s.build_clip(cls.rig, name, spec) for name, spec in s.CLIPS.items()}

    def test_every_clip_keeps_its_tips_on_their_targets(self):
        for name, (clip, row) in self.clips.items():
            self.assertLess(row["tip_error_m"], 1e-5, name)
            self.assertEqual(row["short_m"], 0.0, "%s: a tip target is out of the leg's reach" % name)

    def test_the_gate_reads_each_gait_as_its_claimed_stride_with_no_slip(self):
        for name, (clip, row) in self.clips.items():
            if not row["loop_displacement"] or s.CLIPS[name]["kind"] != "line":
                continue
            axis = 0 if s.CLIPS[name]["travel"][0] else 1
            m = c.measure(tips_from_keys(clip, self.bones), row["loop_displacement"], axis=axis)
            self.assertEqual(c.judge(m, row["loop_displacement"], 0.02, 0.04), [], name)

    def test_left_and_right_legs_mirror_half_a_loop_apart(self):
        clip, row = self.clips["spider_walk"]
        tips = tips_from_keys(clip, self.bones)
        half = clip["frames"] // 2
        for n in "1234":
            left, right = tips["L" + n], np.roll(tips["R" + n], -half, axis=0)
            self.assertTrue(np.allclose(left * np.array([-1.0, 1.0, 1.0]), right, atol=1e-4), n)

    def test_an_in_place_twin_and_the_stand_do_not_travel(self):
        for name in ("spider_walk_inplace", "spider_gallop_inplace", "spider_stand", "spider_idle"):
            clip, row = self.clips[name]
            self.assertEqual(row["loop_displacement"], 0.0, name)
            tips = tips_from_keys(clip, self.bones)
            for tag, p in tips.items():
                self.assertLess(np.ptp(p[:, :2], axis=0).max(), NEAR, "%s %s" % (name, tag))

    def test_a_twin_steps_in_time_with_its_gait(self):
        gait, twin = self.clips["spider_trot"], self.clips["spider_trot_inplace"]
        self.assertEqual(gait[0]["frames"], twin[0]["frames"])
        self.assertEqual(gait[1]["duration"], twin[1]["duration"])
        a, b = tips_from_keys(gait[0], self.bones), tips_from_keys(twin[0], self.bones)
        for tag in a:
            self.assertTrue(np.allclose(a[tag][:, 2], b[tag][:, 2], atol=NEAR), tag)

    def test_a_turn_sweeps_each_planted_tip_about_the_pivot_by_the_clip_angle(self):
        for name, sign in (("spider_turn_left", 1.0), ("spider_turn_right", -1.0)):
            clip, row = self.clips[name]
            spec = s.CLIPS[name]
            tips = tips_from_keys(clip, self.bones)
            per_frame = -sign * math.radians(spec["degrees"]) / clip["frames"]
            for tag, p in tips.items():
                d = p[:, :2] - s.PIVOT[:2]
                ang = np.unwrap(np.arctan2(d[:, 1], d[:, 0]))
                planted = (p[:-1, 2] <= 0.03) & (p[1:, 2] <= 0.03) & (np.abs(np.diff(p[:, 2])) < 1e-3)
                self.assertGreater(planted.sum(), 5, tag)
                self.assertTrue(np.allclose(np.diff(ang)[planted], per_frame, atol=1e-4), "%s %s" % (name, tag))

    def test_the_strafes_mirror_each_other(self):
        left = tips_from_keys(self.clips["spider_strafe_left"][0], self.bones)
        right = tips_from_keys(self.clips["spider_strafe_right"][0], self.bones)
        half = self.clips["spider_strafe_left"][0]["frames"] // 2     # L1 leads one way, R1 the other
        for n in "1234":
            mirrored = np.roll(right["R" + n], -half, axis=0)
            self.assertTrue(np.allclose(left["L" + n] * np.array([-1.0, 1.0, 1.0]), mirrored, atol=1e-4), n)

    def test_frame_zero_is_the_rest_pose_and_the_loop_has_no_seam(self):
        clip, _ = self.clips["spider_canter"]
        mats = np.array(clip["mats"])
        rest = np.array([s.RZ @ m for m in self.rig.rest])
        self.assertTrue(np.allclose(mats[0].reshape(-1, 4, 4), rest, atol=1e-9))
        tips = tips_from_keys(clip, self.bones)
        for tag, p in tips.items():
            steps = np.linalg.norm(np.diff(np.vstack([p, p[:1]]), axis=0), axis=1)
            self.assertLess(steps[-1], steps[:-1].max() * 1.5 + 1e-9, tag)


class BuildTests(unittest.TestCase):
    def test_the_knee_stays_above_the_line_from_the_joint_to_the_tip(self):
        r = rig()
        for name in ("spider_walk", "spider_gallop", "spider_strafe_left", "spider_turn_left"):
            spec = s.CLIPS[name]
            n = int(round(spec["seconds"] * s.KEY_RATE))
            for f in range(n):
                world = s.frame_pose(r, spec, f / n)[0]
                for tag, leg in s.LEGS.items():
                    joint, knee, tip = (world[r.idx[b]][:3, 3] for b in (leg[1], leg[2], leg[4]))
                    along = (knee - joint) @ (tip - joint) / ((tip - joint) @ (tip - joint))
                    self.assertGreater(knee[2], (joint + along * (tip - joint))[2], "%s %s frame %d" % (name, tag, f))

    def test_the_turn_pivot_is_the_middle_of_the_rest_tips(self):
        r = rig()
        tips = np.array([r.rest[r.idx[leg[4]]][:3, 3] for leg in s.LEGS.values()])
        self.assertTrue(np.allclose(tips.mean(axis=0)[:2], s.PIVOT[:2], atol=0.01))

    def test_a_build_fails_when_a_tip_ends_off_its_target(self):
        # a straight leg has no bend plane: the solver leaves it where it was and reports no shortfall
        joint, knee, tip, target = (np.array(v, dtype=float) for v in ((0, 0, 0), (0, 0, 1), (0, 0, 2), (0, 0.6, 0.8)))
        aim, bend, short = s.solve_leg(joint, knee, tip, target)
        landed = joint + aim @ (knee - joint) + aim @ bend @ (tip - knee)
        self.assertEqual(short, 0.0)
        self.assertGreater(np.linalg.norm(landed - target), s.TIP_TOLERANCE)     # the miss the build must catch
        with tempfile.TemporaryDirectory() as out, mock.patch.object(
                s, "build_clip", return_value=({"clip": "x"}, {"clip": "x", "frames": 1, "duration": 1.0, "loop_displacement": 0.0,
                                                               "limits": None, "reach": 0.5, "short_m": 0.0, "tip_error_m": 0.4})), \
                redirect_stdout(io.StringIO()) as said:
            code = s.main(["--rig", FIXTURE, "--out", out])
        self.assertEqual(code, 1)
        self.assertIn("MISSES ITS TARGET", said.getvalue())

    def test_a_build_of_every_clip_passes_and_writes_one_report_row_each(self):
        with tempfile.TemporaryDirectory() as out, redirect_stdout(io.StringIO()):
            code = s.main(["--rig", FIXTURE, "--out", out])
            with open(os.path.join(out, "report.json")) as fh:
                rows = json.load(fh)["gaits"]
        self.assertEqual(code, 0)
        self.assertEqual([r["clip"] for r in rows], list(s.CLIPS))

    def test_there_is_no_way_to_build_a_part_of_the_set(self):
        # a partial run overwrote report.json, and the record writer then planned and verified fewer clips
        with tempfile.TemporaryDirectory() as out, redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            s.main(["--rig", FIXTURE, "--out", out, "--only", "spider_walk"])


class RecordTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        r = rig()
        cls.rows = {name: s.build_clip(r, name, spec)[1] for name, spec in s.CLIPS.items()}

    def test_a_gait_reaches_full_stride_at_its_upper_limit(self):
        for name in ("spider_walk", "spider_trot", "spider_canter"):
            row = self.rows[name]
            self.assertAlmostEqual(row["limits"][1], row["loop_displacement"] / row["duration"], places=2)

    def test_each_faster_pace_starts_below_the_pace_under_it_and_above_the_walk(self):
        order = ["spider_walk", "spider_trot", "spider_canter", "spider_gallop"]
        for slow, fast in zip(order, order[1:]):
            self.assertLess(self.rows[fast]["limits"][0], self.rows[slow]["limits"][1])
            self.assertGreater(self.rows[fast]["limits"][0], self.rows["spider_walk"]["limits"][0])
            self.assertGreater(self.rows[fast]["limits"][0], self.rows[slow]["limits"][0])

    def test_the_backward_walk_has_negative_limits(self):
        lo, hi = self.rows["spider_walk_backwards"]["limits"]
        self.assertLess(lo, hi)
        self.assertLess(hi, 0.0)

    def test_duration_is_the_keys_real_length(self):
        for name, row in self.rows.items():
            if s.CLIPS[name]["kind"] in ("line", "turn"):
                self.assertAlmostEqual(row["duration"], row["frames"] / s.KEY_RATE, places=3, msg=name)

    def test_every_row_names_a_template_and_four_step_points(self):
        for name, row in self.rows.items():
            self.assertTrue(row["template"], name)
            self.assertEqual(len(row["step_points"]), 4, name)
            if s.CLIPS[name]["kind"] in ("line", "turn"):
                self.assertTrue(all(0.0 <= p < 1.0 for p in row["step_points"]), name)
            else:     # a clip with no steps: unset, so no sound point can pass the end of its blend-out
                self.assertEqual(row["step_points"], [-1.0] * 4, name)

    def test_a_twin_is_its_gaits_record_with_no_usage(self):
        for name in ("spider_walk", "spider_trot", "spider_canter", "spider_gallop"):
            gait, twin = self.rows[name], self.rows[name + "_inplace"]
            self.assertFalse(twin["usage"])
            self.assertTrue(gait["usage"])
            for key in ("template", "duration", "frames", "step_points"):
                self.assertEqual(twin[key], gait[key], "%s %s" % (name, key))
            self.assertIsNone(twin["limits"], "a record with no usage has no limits to state")
            self.assertIsNotNone(gait["limits"])

    def test_the_gallop_is_the_top_pace_and_its_typed_limit_is_above_its_full_stride_speed(self):
        row = self.rows["spider_gallop"]
        self.assertEqual(row["limits"][1], 13.0)
        self.assertGreaterEqual(row["limits"][1], row["loop_displacement"] / row["duration"])

    def test_the_idle_clears_its_templates_sound(self):
        self.assertEqual(self.rows["spider_idle"]["sound_code"], "")
        self.assertIsNone(self.rows["spider_walk"]["sound_code"])


if __name__ == "__main__":
    unittest.main()
