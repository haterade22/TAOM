"""tools/warg_gait_plan.json (#1563): the plan that gives the warg's canter and gallop their own masters, the travel
their paws cover and a cadence that follows the speed.

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


def rows():
    with open(PLAN, encoding="utf-8-sig") as fh:
        return {r["clip"]: r for r in json.load(fh)}


class PlanNumbersTests(unittest.TestCase):
    def test_the_plan_names_these_seven_records_each_once(self):
        with open(PLAN, encoding="utf-8-sig") as fh:
            names = [r["clip"] for r in json.load(fh)]
        self.assertEqual(sorted(names), [
            "warg_canter", "warg_canter_l", "warg_canter_r", "warg_canter_stand", "warg_canter_strafe",
            "warg_forward_gallop_left_foot", "warg_forward_gallop_left_foot_stand"])

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

    def test_only_the_two_front_records_set_travel_and_limits(self):
        r = rows()
        self.assertEqual(sorted(c for c in r if "travel" in r[c] or "limits" in r[c]),
                         ["warg_canter", "warg_forward_gallop_left_foot"])


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
        for clip in ("warg_canter", "warg_forward_gallop_left_foot"):
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
                self.soles(row)


if __name__ == "__main__":
    unittest.main()
