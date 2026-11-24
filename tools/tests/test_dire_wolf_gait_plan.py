"""tools/dire_wolf_gait_plan.json (#1565): the dire wolf's four forward paces with the travel its paws cover and a
cadence that follows the speed. Its records were clones of the warg's with the pack's own travel and Duration,
which claimed 26% to 57% less ground than the paws cover.

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

PLAN = os.path.join(TOOLS, "dire_wolf_gait_plan.json")
WOLF = os.path.join(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"), "Modules", "LOTRLOME_Armory",
                    "Assets", "creature", "direwolf")
PAWS = {"FL": "L Finger0", "FR": "R Finger0", "BL": "L Foot", "BR": "R Foot"}
# pace: (front record, its in-place twin, the lowest speed at model size from which the paws must fit the ground).
# The walk's lower limit is 0.1 m/s, where no loop time fits.
PACES = {"walk": ("dire_wolf_walkfast", "dire_wolf_walk_stand", 1.2),
         "trot": ("dire_wolf_trot", "dire_wolf_trot_stand", 2.2),
         "canter": ("dire_wolf_canter", "dire_wolf_canter_stand", 4.6),
         "gallop": ("dire_wolf_forward_gallop_left_foot", "dire_wolf_forward_gallop_left_foot_stand", 8.0)}
MISFIT = 0.08          # the most the ground a loop covers may differ from the paws' travel, as a part of the travel


def rows():
    with open(PLAN, encoding="utf-8-sig") as fh:
        return {r["clip"]: r for r in json.load(fh)}


class PlanNumbersTests(unittest.TestCase):
    def test_the_plan_names_the_four_paces_their_twins_and_the_canters_strafe_each_once(self):
        with open(PLAN, encoding="utf-8-sig") as fh:
            names = [r["clip"] for r in json.load(fh)]
        self.assertEqual(sorted(names), sorted([c for front, stand, _ in PACES.values() for c in (front, stand)]
                                               + ["dire_wolf_canter_strafe"]))

    def test_each_record_plays_a_whole_loop_of_its_master(self):
        """The wolf's masters are keyed from 0 and its records play from 0. The pack's canter played keys 1 to 16
        of the gallop's 0 to 16: a loop that skips its first interval, so the paws jumped up to 0.39 m at the seam
        (keys 0 and 16 are one pose). The canter plays the whole loop now, and its strafe record with it."""
        r = rows()
        want = {"walk": ("adod_wolf_walk", 0, 32), "trot": ("adod_wolf_trot", 0, 24),
                "canter": ("adod_wolf_gallop", 0, 16), "gallop": ("adod_wolf_gallop", 0, 16)}
        for pace, (front, stand, _) in PACES.items():
            for clip in (front, stand):
                self.assertEqual((r[clip]["master"], r[clip]["source1"], r[clip]["source2"]), want[pace], clip)
        strafe = r["dire_wolf_canter_strafe"]
        self.assertEqual((strafe["master"], strafe["source1"], strafe["source2"]), want["canter"])
        self.assertEqual(strafe["duration"], r["dire_wolf_canter"]["duration"])
        self.assertFalse({"travel", "limits", "step_points", "template", "flags"} & set(strafe))

    def test_each_in_place_twin_has_the_longer_loop_and_no_usage(self):
        r = rows()
        for pace, (front, stand, _) in PACES.items():
            self.assertGreater(r[stand]["duration"], r[front]["duration"], pace)
            self.assertFalse({"travel", "limits", "step_points"} & set(r[stand]), "%s: a twin has no usage" % pace)

    def test_only_the_walk_had_no_twin_and_is_made_from_one(self):
        r = rows()
        self.assertEqual([c for c in r if "template" in r[c]], ["dire_wolf_walk_stand"])
        self.assertEqual(r["dire_wolf_walk_stand"]["template"], "dire_wolf_forward_gallop_left_foot_stand")

    def test_the_limits_are_the_wolfs_own_and_each_pace_begins_inside_the_pace_below_it(self):
        r = rows()
        order = [r[PACES[p][0]]["limits"] for p in ("walk", "trot", "canter", "gallop")]
        self.assertEqual(order, [[0.1, 2.4], [2.2, 4.8], [4.6, 8.0], [8.0, 14.1]])
        for slower, faster in zip(order, order[1:]):
            self.assertLessEqual(faster[0], slower[1])
            self.assertGreater(faster[0], slower[0])

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

    def test_the_canter_and_the_gallop_play_one_master_at_one_rate_where_they_meet(self):
        """Both play the gallop's keys, so at 8.0 m/s a key must take the same time in each, or the legs jump in
        tempo at the change of pace."""
        r = rows()
        self.assertEqual(r["dire_wolf_canter"]["duration"], r["dire_wolf_forward_gallop_left_foot_stand"]["duration"])
        self.assertEqual(r["dire_wolf_canter"]["travel"], r["dire_wolf_forward_gallop_left_foot"]["travel"])


@unittest.skipUnless(os.path.isdir(os.path.join(WOLF, "animations")), "the Armory is not installed here")
class InstalledMastersTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        try:
            import numpy
            import check_gait_stride as gait
            import tpac_skeleton_dump as tsd
        except ImportError as exc:
            raise unittest.SkipTest("the stride measure needs numpy (%s)" % exc)
        cls.gait, cls.np = gait, numpy
        skeleton = os.path.join(WOLF, "mesh", "dire_wolf_grey_geo.tpac")
        _, segment, _ = tsd.find_skeleton(skeleton, "adod_wolf_armature_unused")
        cls.bones = tsd.parse_skeleton_definition(tsd.read_segment_data(skeleton, segment))["bones"]

    def soles(self, row, closed=False):
        """Each paw's path (`closed`: with the loop's last key, which repeats its first, kept on the end) with its height taken from that paw's own lowest point. This rig's paw joints do not
        rest at z = 0 (the walk's stay 0.04 to 0.08 m above it, the gallop's go 0.2 m under), so the stride
        measure's fixed ground height would find no planted paw."""
        master = self.gait.load_anim(os.path.join(WOLF, "animations", row["master"] + "_geo.tpac"))
        low, high = self.gait.key_span(master)
        self.assertTrue(low <= row["source1"] and row["source2"] <= high,
                        "%s plays %s..%s of a master keyed %s..%s" % (row["clip"], row["source1"], row["source2"], low, high))
        played = self.gait.played_keys(master, {"first": row["source1"],
                                                "keys": row["source2"] - row["source1"] + (1 if closed else 0)})
        out = {}
        for paw, path in self.gait.soles_of(played, self.bones, PAWS).items():
            path = self.np.array(path, dtype=float)
            path[:, 2] -= path[:, 2].min()
            out[paw] = path
        return out

    def test_the_paws_cover_the_travel_each_gait_claims(self):
        r = rows()
        for front, _, _ in PACES.values():
            with self.subTest(clip=front):
                stride = self.gait.measure(self.soles(r[front]), r[front]["travel"])["stride"]
                self.assertAlmostEqual(stride, r[front]["travel"], delta=0.05 * r[front]["travel"])

    def test_each_gaits_loop_closes_and_each_paw_is_planted_in_it(self):
        """The stride is the median speed of the planted keys times the keys of the loop: a loop that does not end
        where it begins, or a paw that is never planted, makes that number mean nothing."""
        r = rows()
        for front, _, _ in PACES.values():
            with self.subTest(clip=front):
                soles = self.soles(r[front], closed=True)
                for paw, path in soles.items():
                    gap = float(self.np.abs(path[0] - path[-1]).max())
                    self.assertLess(gap, 0.02, "%s: the loop's first and last key differ by %.3f m" % (paw, gap))
                    self.assertGreaterEqual(int((path[:-1, 2] <= self.gait.CONTACT).sum()), 2, paw)

    def test_every_planned_range_lies_inside_its_masters_keys(self):
        for row in rows().values():
            with self.subTest(clip=row["clip"]):
                self.soles(row)


if __name__ == "__main__":
    unittest.main()
