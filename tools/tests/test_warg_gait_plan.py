"""tools/warg_gait_plan.json (#1563, #1565): the plan that gives each of the warg's four forward paces the travel its
paws cover and a cadence that follows the speed, and the canter its own master.

The first class holds the numbers together with no install. The second measures the installed masters, so a typed
travel that the paws do not cover fails here; it skips on a machine without the Armory.
"""
import json
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.join(HERE, "..")
sys.path.insert(0, TOOLS)
from _gamedir import game_dir  # noqa: E402

PLAN = os.path.join(TOOLS, "warg_gait_plan.json")
WARG = os.path.join(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"), "Modules", "LOTRLOME_Armory",
                    "Assets", "creature", "warg")
PAWS = {"FL": "Fingers1_L", "FR": "Fingers1_R", "BL": "Toes1_L", "BR": "Toes1_R"}
# pace: (front record, its in-place twin, the lowest speed at model size from which the paws must fit the ground).
# The walk's lower limit is 0.1 m/s, where no loop time fits: one stride there would take 18 s.
PACES = {"walk": ("warg_walkfast", "warg_walk_stand", 1.2),
         "trot": ("warg_trot", "warg_trot_stand", 2.2),
         "canter": ("warg_canter", "warg_canter_stand", 4.6),
         "gallop": ("warg_forward_gallop_left_foot", "warg_forward_gallop_left_foot_stand", 7.0)}
MISFIT = 0.08          # the most the ground a loop covers may differ from the paws' travel, as a part of the travel


def rows():
    with open(PLAN, encoding="utf-8-sig") as fh:
        return {r["clip"]: r for r in json.load(fh)}


class PlanNumbersTests(unittest.TestCase):
    def test_the_plan_names_these_thirteen_records_each_once(self):
        with open(PLAN, encoding="utf-8-sig") as fh:
            names = [r["clip"] for r in json.load(fh)]
        self.assertEqual(sorted(names), [
            "rider_warg_gallop_turn_left_head", "rider_warg_gallop_turn_right_head",
            "warg_canter", "warg_canter_l", "warg_canter_r", "warg_canter_stand", "warg_canter_strafe",
            "warg_forward_gallop_left_foot", "warg_forward_gallop_left_foot_stand",
            "warg_trot", "warg_trot_stand", "warg_walk_stand", "warg_walkfast"])

    def test_the_riders_gallop_turn_clips_follow_the_wargs_stride(self):
        """Every other moving rider clip carries synch_with_horse, which takes its progress from the mount. These
        two ran at their own Duration (0.58 s, the gallop's old loop time): in step by an equal number until the
        gallop's loop time began to follow its speed (#1563)."""
        r = rows()
        for side, clip in (("l", "rider_warg_gallop_turn_left_head"), ("r", "rider_warg_gallop_turn_right_head")):
            self.assertEqual(r[clip]["flags"], ["client_prediction", "synch_with_horse"], clip)
            self.assertEqual((r[clip]["master"], r[clip]["source1"], r[clip]["source2"]),
                             ("rider_warg_gallop_" + side, 1, 20), clip)
            self.assertFalse({"duration", "travel", "limits", "step_points", "template"} & set(r[clip]), clip)
        self.assertEqual(sorted(c for c in r if "flags" in r[c]),
                         ["rider_warg_gallop_turn_left_head", "rider_warg_gallop_turn_right_head"])

    def test_the_walk_and_the_trot_keep_their_masters_keys_and_limits(self):
        r = rows()
        for clip in ("warg_walkfast", "warg_walk_stand"):
            self.assertEqual((r[clip]["master"], r[clip]["source1"], r[clip]["source2"]), ("Warg_Walk", 1, 29), clip)
        for clip in ("warg_trot", "warg_trot_stand"):
            self.assertEqual((r[clip]["master"], r[clip]["source1"], r[clip]["source2"]), ("Warg_Trot", 1, 17), clip)
        self.assertEqual(r["warg_walkfast"]["limits"], [0.1, 2.4])
        self.assertEqual(r["warg_trot"]["limits"], [2.2, 4.8])

    def test_each_in_place_twin_plays_its_front_records_keys(self):
        """The node gives the "none" clip the weight the front clip does not take, at one phase. The twin holds the
        same poses, so the mix is the gait whatever the weight; only its Duration differs."""
        r = rows()
        for pace, (front, stand, _) in PACES.items():
            self.assertEqual([r[stand][k] for k in ("master", "source1", "source2")],
                             [r[front][k] for k in ("master", "source1", "source2")], pace)
            self.assertGreater(r[stand]["duration"], r[front]["duration"], "%s: the slow end has the longer loop" % pace)
            self.assertFalse({"travel", "limits", "step_points"} & set(r[stand]), "%s: a twin has no usage" % pace)

    def test_only_the_walk_had_no_twin_and_is_made_from_one(self):
        """as_warg bound warg_walkfast on the walk's front and "none" row: one record, so one loop time."""
        r = rows()
        self.assertEqual([c for c in r if "template" in r[c]], ["warg_walk_stand"])
        self.assertEqual(r["warg_walk_stand"]["template"], "warg_forward_gallop_left_foot_stand")

    def test_the_paws_stay_near_the_ground_speed_over_each_pace(self):
        """One loop takes the twin's Duration at the lower limit and the front record's at the upper, on a straight
        line between. The body then covers speed x loop time in a loop, and the paws cover the travel."""
        r = rows()
        for pace, (front, stand, fit_from) in PACES.items():
            travel, (low, high) = r[front]["travel"], r[front]["limits"]
            slow, fast = r[stand]["duration"], r[front]["duration"]
            for i in range(41):
                speed = fit_from + (high - fit_from) * i / 40.0
                loop = slow + (fast - slow) * (speed - low) / (high - low)
                self.assertLessEqual(abs(speed * loop / travel - 1.0), MISFIT,
                                     "%s at %.2f m/s: the body covers %.2f m in a loop, the paws %.2f m" % (
                                         pace, speed, speed * loop, travel))

    def test_each_pace_begins_inside_the_pace_below_it(self):
        """The node steps up above the current pace's upper limit and down below its lower. A lower limit above the
        slower pace's upper limit would leave speeds where no pace is at rest."""
        r = rows()
        order = [r[PACES[p][0]]["limits"] for p in ("walk", "trot", "canter", "gallop")]
        for slower, faster in zip(order, order[1:]):
            self.assertLessEqual(faster[0], slower[1])
            self.assertGreater(faster[0], slower[0])

    def test_the_canter_plays_the_slow_run_and_the_gallop_the_run(self):
        r = rows()
        for clip in ("warg_canter", "warg_canter_stand", "warg_canter_strafe"):
            self.assertEqual((r[clip]["master"], r[clip]["source1"], r[clip]["source2"]), ("Warg_Slow_run", 820, 850), clip)
        for clip in ("warg_forward_gallop_left_foot", "warg_forward_gallop_left_foot_stand"):
            self.assertEqual((r[clip]["master"], r[clip]["source1"], r[clip]["source2"]), ("Warg_Run", 180, 200), clip)
        self.assertEqual((r["warg_canter_l"]["master"], r["warg_canter_l"]["source1"], r["warg_canter_l"]["source2"]),
                         ("Warg_Run_L", 760, 780))
        self.assertEqual((r["warg_canter_r"]["master"], r["warg_canter_r"]["source1"], r["warg_canter_r"]["source2"]),
                         ("Warg_Run_R", 790, 810))

    def test_the_canter_states_four_step_points_and_no_other_record_states_any(self):
        r = rows()
        self.assertEqual(len(r["warg_canter"]["step_points"]), 4)
        self.assertEqual([c for c in r if "step_points" in r[c]], ["warg_canter"])

    def test_the_two_paces_meet_and_the_canter_starts_where_it_did(self):
        r = rows()
        canter, gallop = r["warg_canter"]["limits"], r["warg_forward_gallop_left_foot"]["limits"]
        self.assertEqual(canter[0], 4.6, "the trot's upper limit is 4.8: the canter's lower limit stays under it")
        self.assertEqual(canter[1], gallop[0], "the gallop begins where the canter ends")
        self.assertEqual(gallop, [7.0, 12.0])

    def test_each_pace_covers_its_travel_at_both_ends_of_its_limits(self):
        """The movement node plays one loop in the "none" clip's Duration at the lower limit and the front clip's at
        the upper. The paws match the ground when travel / Duration is that speed."""
        r = rows()
        for front, stand in (("warg_canter", "warg_canter_stand"),
                             ("warg_forward_gallop_left_foot", "warg_forward_gallop_left_foot_stand")):
            travel, (low, high) = r[front]["travel"], r[front]["limits"]
            self.assertAlmostEqual(travel / r[front]["duration"], high, delta=0.03 * high, msg=front)
            self.assertAlmostEqual(travel / r[stand]["duration"], low, delta=0.03 * low, msg=stand)

    def test_the_canters_side_records_take_the_canters_duration(self):
        """The strafe and the two turn records play beside the front record at one phase. The movement node does not
        read a strafe record's Duration in a moving pace (native read, 2026-11-23), so the value only keeps the three
        like the record they belong to."""
        r = rows()
        for clip in ("warg_canter_strafe", "warg_canter_l", "warg_canter_r"):
            self.assertEqual(r[clip]["duration"], r["warg_canter"]["duration"], clip)

    def test_only_the_four_front_records_set_travel_and_limits(self):
        r = rows()
        self.assertEqual(sorted(c for c in r if "travel" in r[c] or "limits" in r[c]),
                         sorted(front for front, _, _ in PACES.values()))


@unittest.skipUnless(os.path.isdir(os.path.join(WARG, "animations")), "the Armory is not installed here")
class InstalledMastersTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        try:
            import numpy  # noqa: F401
            import check_gait_stride as gait
            import tpac_skeleton_dump as tsd
        except ImportError as exc:
            raise unittest.SkipTest("the stride measure needs numpy (%s)" % exc)
        cls.gait = gait
        skeleton = os.path.join(WARG, "mesh", "Warg_Rig_V5_geo.tpac")
        _, segment, _ = tsd.find_skeleton(skeleton, "skeleton_warg")
        cls.bones = tsd.parse_skeleton_definition(tsd.read_segment_data(skeleton, segment))["bones"]

    def soles(self, row):
        master = self.gait.load_anim(os.path.join(WARG, "animations", row["master"] + "_geo.tpac"))
        low, high = self.gait.key_span(master)
        self.assertTrue(low <= row["source1"] and row["source2"] <= high,
                        "%s plays %s..%s of a master keyed %s..%s" % (row["clip"], row["source1"], row["source2"], low, high))
        played = self.gait.played_keys(master, {"first": row["source1"], "keys": row["source2"] - row["source1"]})
        return self.gait.soles_of(played, self.bones, PAWS)

    def test_the_paws_cover_the_travel_each_gait_claims(self):
        r = rows()
        for clip in sorted(front for front, _, _ in PACES.values()):
            with self.subTest(clip=clip):
                stride = self.gait.measure(self.soles(r[clip]), r[clip]["travel"])["stride"]
                self.assertAlmostEqual(stride, r[clip]["travel"], delta=0.05 * r[clip]["travel"])

    def test_the_canters_step_points_are_where_its_paws_land(self):
        row = rows()["warg_canter"]
        soles = self.soles(row)
        frames = len(soles["FL"])
        lands = []
        for path in soles.values():
            down = path[:, 2] <= self.gait.CONTACT
            lands += [i / float(frames) for i in range(frames) if down[i] and not down[i - 1]]
        self.assertEqual(len(lands), 4, "each paw lands once in the loop")
        self.assertEqual(len(row["step_points"]), len(lands))
        for want, got in zip(row["step_points"], sorted(lands)):
            self.assertAlmostEqual(want, got, delta=1.0 / frames)

    def test_every_planned_range_lies_inside_its_masters_keys(self):
        for row in rows().values():
            with self.subTest(clip=row["clip"]):
                master = self.gait.load_anim(os.path.join(WARG, "animations", row["master"] + "_geo.tpac"))
                low, high = self.gait.key_span(master)
                self.assertTrue(low <= row["source1"] and row["source2"] <= high, (row["clip"], low, high))


if __name__ == "__main__":
    unittest.main()
