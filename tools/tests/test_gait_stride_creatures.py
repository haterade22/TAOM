#!/usr/bin/env python3
"""tools/check_gait_stride.py as a gate for a creature whose masters were bought, not made here (#1565 item 2: the
warg): a master is found by the GUID its record names, the slip and stance limits are the creature's, and a cache
entry is held to its record's checksum where the files' dates are a copy's. And for every creature (#1591): a clip
that only the standing strafe rows bind is printed with what the node does with it, and not judged by its stride.

Run:  python -m unittest tools.tests.test_gait_stride_creatures

The reports run on a module the test builds: one bone that is its own four feet, one master, three records.
"""
import contextlib
import io
import os
import struct
import sys
import tempfile
import unittest
import uuid
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import check_gait_stride as c  # noqa: E402
import set_clip_balance_name as scb  # noqa: E402
import test_elephant_gait_tools as teg  # noqa: E402
import test_repoint_anim_clip as trp  # noqa: E402
import test_set_clip_balance_name as tsb  # noqa: E402
from _gamedir import game_dir  # noqa: E402

MASTER_GUID = bytes([7] * 16)       # the item GUID teg._master_tpac gives its animation
BACK_GUID = bytes([6] * 16)         # the backward walk's own master: its planted foot moves the other way
IDENT = (1.0, 0.0, 0.0, 0.0)
BONES = [{"name": "root", "parent_idx": -1, "rest_frame": [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]}]
SETS = ('<action_sets><action_set id="as_x">'
        '<action type="act_w" animation="x_walk"/><action type="act_b" animation="x_back"/>'
        '<action type="act_stand" animation="x_pose"/>'
        '</action_set></action_sets>')
CONFIG = {"module": "X", "clips": "clips", "skeleton": ("skel_geo.tpac", "s"),
          "feet": {t: "root" for t in ("FL", "FR", "BL", "BR")},
          "gaits": ["x_walk", "x_back"], "action_sets": "sets.xml", "sets": ("as_x",),
          "pace_actions": {"backward": "act_b", "strafe": (), "walk": "act_w", "faster": ()},
          "stand_action": "act_stand"}


def record(name, travel=None, limits=(0.2, 2.5), guid=MASTER_GUID, last=5.0, duration=1.0, first=1.0):
    """A clip record as far as the gate reads it: keys `first` to `last` of the master with `guid`, and a usage when
    it has travel. Bytes 8 to 24, the package GUID, are zero: every record of the module shares one cache entry."""
    raw = (bytes(0x48) + struct.pack("<i", len(name)) + name.encode() + bytes(12) + struct.pack("<3f", duration, first, last)
           + bytes(16) + guid + bytes(40))
    if travel is not None:
        raw += struct.pack("<i", 13) + b"quad_movement" + bytes(4) + struct.pack("<3f", travel, *limits)
    return raw


def master(stride=2.0, guid=MASTER_GUID, keys=5, down=3, sideways=False, drift=0.0, lift=0.0):
    """One loop of `keys` keys, the last a copy of the first: the foot is on the ground for the first `down` keys,
    moves back `stride` / (keys - 1) a key (along X when `sideways`), and is in the air for the rest. `drift` moves
    the third key that far across its travel: a slip. `lift` raises every key: a rig whose foot joint does not rest
    at height 0."""
    step = stride / (keys - 1.0)
    root = []
    for i in range(keys - 1):
        along, across, up = -step * i, drift if i == 2 else 0.0, lift + (0.0 if i < down else 0.3)
        root.append((along, across, up) if sideways else (across, along, up))
    root.append((0.0, 0.0, lift))
    raw = teg._master_tpac("all", teg._anim_payload("all", [[IDENT] * keys], root), 1, keys)
    assert raw.count(MASTER_GUID) == 1
    return raw.replace(MASTER_GUID, guid)


class Module(unittest.TestCase):
    """A module folder: the walk and the backward walk claim 2.0 m on a master whose foot covers 2.0 m."""

    def setUp(self):
        d = tempfile.TemporaryDirectory()
        self.addCleanup(d.cleanup)
        self.root = d.name
        os.makedirs(os.path.join(self.root, "clips"))
        os.makedirs(os.path.join(self.root, "RuntimeDataCache"))
        self.put("skel_geo.tpac", b"TPAC a skeleton, no animation", top=True)
        self.master = self.put("Bought_Name_geo.tpac", master())
        self.put("Backward_geo.tpac", master(stride=-2.0, guid=BACK_GUID))
        self.walk = self.put("x_walk_anm.tpac", record("x_walk", 2.0))
        self.put("x_back_anm.tpac", record("x_back", 2.0, (-1.25, -0.1), guid=BACK_GUID))
        self.put("x_pose_anm.tpac", record("x_pose"))
        self.entry = os.path.join(self.root, "RuntimeDataCache", str(uuid.UUID(int=0)) + ".rdc")
        with open(self.entry, "wb") as fh:
            fh.write(b"RDC0")
        os.utime(self.entry, (2_000_000, 2_000_000))

    def put(self, name, raw, top=False, when=1_000_000):
        path = os.path.join(self.root, name) if top else os.path.join(self.root, "clips", name)
        with open(path, "wb") as fh:
            fh.write(raw)
        os.utime(path, (when, when))
        return path

    def report(self, **config):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = c.report("x", dict(CONFIG, **config), self.root, BONES, SETS)
        return code, out.getvalue()

    def line(self, text, clip):
        return next(ln for ln in text.splitlines() if (" %s " % clip) in ln and not ln.startswith(" "))


class MasterByGuidTests(Module):
    def test_a_gait_is_measured_on_the_master_its_record_names_whatever_the_file_is_called(self):
        code, text = self.report()
        self.assertEqual(code, 0, text)
        self.assertTrue(self.line(text, "x_walk").startswith("ok "), text)
        self.assertIn("feet cover 2.00 m", self.line(text, "x_walk"))
        self.assertIn("x: all 2 gait clips cover the ground they claim", text)

    def test_a_file_with_the_clips_name_and_another_guid_is_not_its_master(self):
        """The gate took `<clip>_geo.tpac` and its first animation. A record names its master by GUID, and the
        warg's masters carry the pack's names."""
        self.put("x_walk_geo.tpac", master(stride=1.0, guid=bytes([8] * 16)))
        code, text = self.report()
        self.assertEqual(code, 0, text)
        self.assertIn("feet cover 2.00 m", self.line(text, "x_walk"))

    def test_a_bound_gait_whose_master_is_in_no_file_fails(self):
        os.remove(self.master)
        self.put("x_walk_geo.tpac", master(guid=bytes([8] * 16)))
        code, text = self.report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)
        self.assertIn("master", self.line(text, "x_walk"))

    def test_an_animation_that_two_master_files_hold_is_a_finding(self):
        """The first file in the list was measured without a word (three reviewers, 2026-11-24). A copy that fits
        would pass for a master that does not."""
        self.put("Copy_of_it_geo.tpac", master(stride=1.0))
        code, text = self.report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)
        self.assertIn("2 master files hold the animation its record names", text)
        self.assertIn("Bought_Name_geo.tpac", text)
        self.assertIn("Copy_of_it_geo.tpac", text)

    def test_a_package_that_only_mentions_the_guid_is_not_a_second_master(self):
        """A rig package names the animations of its other items; only an animation item with the GUID is a master."""
        self.put("mentions_it_geo.tpac", master(stride=1.0, guid=bytes([8] * 16)) + MASTER_GUID)
        code, text = self.report()
        self.assertEqual(code, 0, text)
        self.assertIn("feet cover 2.00 m", self.line(text, "x_walk"))

    def test_a_master_in_another_folder_of_the_creature_is_found(self):
        os.remove(self.master)
        os.makedirs(os.path.join(self.root, "mesh"))
        with open(os.path.join(self.root, "mesh", "beside_the_rig_geo.tpac"), "wb") as fh:
            fh.write(master())
        os.utime(os.path.join(self.root, "mesh", "beside_the_rig_geo.tpac"), (1_000_000, 1_000_000))
        code, text = self.report()
        self.assertEqual(code, 0, text)

    def test_feet_that_do_not_cover_the_claim_fail(self):
        self.put("x_walk_anm.tpac", record("x_walk", 3.0))
        code, text = self.report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)
        self.assertIn("the clip claims 3.00 m", text)


class BackwardDirectionTests(Module):
    """#1591: the dire wolf's backward rows played the forward walk's keys, so its paws stepped forward while it
    backed up. The stride measure takes the sign of the travel from the data, so it passed any direction."""

    def test_a_backward_walk_whose_feet_move_against_the_walks_passes(self):
        code, text = self.report()
        self.assertEqual(code, 0, text)
        self.assertNotIn("backs up", text)

    def test_a_backward_row_on_the_walks_own_keys_is_a_finding(self):
        self.put("x_back_anm.tpac", record("x_back", 2.0, (-1.25, -0.1)))
        code, text = self.report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_back").startswith("ok "), "the stride itself fits")
        self.assertIn("FAIL pace: as_x binds act_b to x_back, whose planted feet move the way the walk's (x_walk) do",
                      text)

    def test_a_backward_clip_on_two_rows_is_one_finding(self):
        """The wolf's front row and its "none" row bind one backward record."""
        sets = SETS.replace('<action type="act_stand"', '<action type="act_b_none" animation="x_back"/><action type="act_stand"')
        self.put("x_back_anm.tpac", record("x_back", 2.0, (-1.25, -0.1)))
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = c.report("x", CONFIG, self.root, BONES, sets)
        self.assertEqual(code, 1, out.getvalue())
        self.assertEqual(out.getvalue().count("backs up"), 1, out.getvalue())

    def test_a_second_record_of_the_backward_pace_is_held_the_same_way(self):
        """The backward pace's two strafe rows bind a record of their own (`dire_wolf_walkbackfast`,
        `warg_walkbackfast`). The gate knows it by its negative pace limits, whatever action binds it."""
        sets = SETS.replace('<action type="act_stand"', '<action type="act_bs" animation="x_back_side"/><action type="act_stand"')
        self.put("x_back_side_anm.tpac", record("x_back_side", 2.0, (-1.25, -0.1), guid=BACK_GUID))
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = c.report("x", CONFIG, self.root, BONES, sets)
        self.assertEqual(code, 0, out.getvalue())
        self.put("x_back_side_anm.tpac", record("x_back_side", 2.0, (-1.25, -0.1)))      # the walk's own keys
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = c.report("x", CONFIG, self.root, BONES, sets)
        text = out.getvalue()
        self.assertEqual(code, 1, text)
        self.assertIn("FAIL pace: as_x binds act_bs to x_back_side, whose planted feet move the way the walk's "
                      "(x_walk) do", text)
        self.assertEqual(text.count("backs up"), 1, text)

    def test_a_record_that_plays_the_walks_keys_from_last_to_first_is_a_backward_walk(self):
        """A record whose second source is under its first plays its master backward (vanilla
        `camel_walk_backwards`, docs/reference/bannerlord-animation-system-map.md). The dire wolf has no backward
        master, so its backward records play the walk's keys 32 to 0."""
        self.put("x_back_anm.tpac", record("x_back", 2.0, (-1.25, -0.1), first=5.0, last=1.0))
        code, text = self.report()
        self.assertEqual(code, 0, text)
        self.assertIn("feet cover 2.00 m in 4 frames", self.line(text, "x_back"))

    def test_measure_reports_the_way_the_planted_feet_move(self):
        self.assertEqual(c.measure(teg._treadmill(60, 2.0), 2.0)["direction"], -1.0)
        back = {t: p * [1, -1, 1] for t, p in teg._treadmill(60, 2.0).items()}
        self.assertEqual(c.measure(back, 2.0)["direction"], 1.0)


class ReversedRangeTests(unittest.TestCase):
    ANIM = {"rot": [{"t": [1.0, 2.0, 3.0, 4.0, 5.0], "v": [[i, 0, 0, 0] for i in range(1, 6)]}],
            "root_pos": {"t": [1.0, 2.0, 3.0, 4.0, 5.0], "v": [[i, 0, 0, 0] for i in range(1, 6)]}}

    def test_a_reversed_record_plays_from_its_first_source_down_to_the_key_after_its_last(self):
        played = c.played_keys(self.ANIM, {"first": 5.0, "keys": -4.0})
        self.assertEqual(list(played["rot"][0]["t"]), [5.0, 4.0, 3.0, 2.0])
        self.assertEqual([float(v[0]) for v in played["rot"][0]["v"]], [5.0, 4.0, 3.0, 2.0])
        self.assertEqual([float(v[0]) for v in played["root_pos"]["v"]], [5.0, 4.0, 3.0, 2.0])

    def test_a_forward_record_is_read_as_before(self):
        played = c.played_keys(self.ANIM, {"first": 1.0, "keys": 4.0})
        self.assertEqual(list(played["rot"][0]["t"]), [1.0, 2.0, 3.0, 4.0])

    def test_a_reversed_range_fits_keys_that_hold_both_its_ends(self):
        self.assertIsNone(c.range_finding([1.0, 5.0], {"first": 5.0, "keys": -4.0}))
        self.assertIn("plays keys 6 to 1", c.range_finding([1.0, 5.0], {"first": 6.0, "keys": -5.0}))
        self.assertIn("plays keys 5 to 0", c.range_finding([1.0, 5.0], {"first": 5.0, "keys": -5.0}))


class GroundRuleTests(Module):
    """The dire wolf's paw joints do not rest at height 0: in its walk they stay 0.04 to 0.08 m above it, and in its
    gallop they go 0.2 m under it. A creature that says ground "lowest" has each foot's height taken from that
    foot's own lowest point in the loop."""

    def setUp(self):
        super().setUp()
        self.put("Bought_Name_geo.tpac", master(lift=0.06))
        self.put("x_pose_anm.tpac", record("x_pose", guid=BACK_GUID))       # the pose stands on a master at height 0

    def test_a_foot_that_stays_above_the_contact_height_is_never_planted_by_the_fixed_rule(self):
        code, text = self.report()
        self.assertEqual(code, 1, text)
        self.assertIn("is planted for only 0% of the loop", text)

    def test_a_creature_that_says_lowest_measures_the_same_loop(self):
        code, text = self.report(ground="lowest")
        self.assertEqual(code, 0, text)
        self.assertIn("feet cover 2.00 m", self.line(text, "x_walk"))

    def test_a_ground_rule_the_gate_does_not_know_is_a_finding(self):
        code, text = self.report(ground="lowset")
        self.assertEqual(code, 1, text)
        self.assertIn("FAIL table: ground", text)

    def test_a_foot_that_stays_above_the_others_for_the_whole_loop_is_a_finding(self):
        """The rule takes each foot's ground from that foot, so a leg that a retarget lifted would read as planted.
        The feet's lowest points may be 2 x CONTACT apart: the wolf's are 0.04 m apart at most."""
        feet = teg._treadmill(60, 2.0)
        self.assertEqual(c.lifted_foot(feet), [])
        self.assertEqual(c.lifted_foot({t: p + [0.0, 0.0, 0.2] for t, p in feet.items()}), [],
                         "all four at one height, wherever it is: the wolf's walk is above 0, its gallop under it")
        feet["FL"] = feet["FL"] + [0.0, 0.0, 0.2]
        self.assertEqual(c.lifted_foot(feet),
                         ["FL never comes within 0.06 m of the lowest foot's ground (it stays 0.20 m above it)"])
        with mock.patch.object(c, "soles_of", return_value=feet):
            code, text = self.report(ground="lowest")
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)
        self.assertIn("FL never comes within", text)
        with mock.patch.object(c, "soles_of", return_value=feet):
            code, text = self.report()                           # the fixed rule says it its own way
        self.assertNotIn("never comes within", text)


class PendingTests(Module):
    """A clip that claims travel and has no stride to measure: here a side record with the walk's forward keys."""
    SIDE = SETS.replace('<action type="act_stand"', '<action type="act_side" animation="x_side"/><action type="act_stand"')

    def side_report(self, **config):
        self.put("x_side_anm.tpac", record("x_side", 9.0, (0.0, 0.0)))
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = c.report("x", dict(CONFIG, **config), self.root, BONES, self.SIDE)
        return code, out.getvalue()

    def test_a_bound_clip_that_claims_travel_is_measured_though_no_list_names_it(self):
        code, text = self.side_report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_side").startswith("FAIL"), text)

    def test_a_pending_clip_is_printed_with_its_reason_and_not_measured(self):
        code, text = self.side_report(pending={"x_side": "it plays the walk's forward keys"})
        self.assertEqual(code, 0, text)
        self.assertTrue(self.line(text, "x_side").startswith("--"), text)
        self.assertIn("not checked: it plays the walk's forward keys", text)
        self.assertIn("x: all 2 gait clips", text)

    def test_a_pending_clip_that_no_named_set_binds_is_a_finding(self):
        code, text = self.report(pending={"x_gone": "it was a side record once"})
        self.assertEqual(code, 1, text)
        self.assertIn("FAIL pending list: x_gone", text)
        self.assertIn("x: 1 finding(s) in the record files, the lists, the 2 gait clips and the 0 side clips", text)


SIDE_GUID = bytes([9] * 16)


class SideStepTests(Module):
    """The standing strafe rows bind x_side: 1.60 m of travel in a record of 0.1333 s (its four keys at 30 a second),
    on a master whose foot moves 1.00 m sideways in a loop. The node plays one loop in travel / (Duration x the
    sideways speed) seconds, so inside its clamp the body moves travel / Duration = 12.00 m in a loop. A stride
    judged against the travel would fail it, and would be the wrong measure (0x749C20, 0x5F75B0)."""
    SETS = SETS.replace('<action type="act_stand"', '<action type="act_sr" animation="x_side"/>'
                        '<action type="act_sl" animation="x_side"/><action type="act_stand"')
    PACES = dict(CONFIG["pace_actions"], strafe=("act_sr", "act_sl"))

    def setUp(self):
        super().setUp()
        self.side_master = self.put("Side_geo.tpac", master(stride=1.0, guid=SIDE_GUID, sideways=True))
        self.put("x_side_anm.tpac", record("x_side", 1.6, (0.0, 0.0), guid=SIDE_GUID, duration=4 / 30.0))

    def side_report(self, sets=None, **config):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = c.report("x", dict(CONFIG, pace_actions=self.PACES, **config), self.root, BONES, sets or self.SETS)
        return code, out.getvalue()

    def test_a_clip_on_the_standing_strafe_rows_is_printed_and_not_judged_by_stride(self):
        code, text = self.side_report()
        self.assertEqual(code, 0, text)
        line = self.line(text, "x_side")
        self.assertTrue(line.startswith("side "), text)
        self.assertIn("12.00 m in a loop", line)
        self.assertIn("travel 1.60 m / Duration 0.13 s", line)
        self.assertIn("the feet move 1.00 m sideways", line)
        self.assertIn("x: all 2 gait clips cover the ground they claim", text)

    def test_listing_it_as_a_gait_does_not_make_it_one(self):
        code, text = self.side_report(gaits=["x_walk", "x_back", "x_side"])
        self.assertEqual(code, 0, text)
        self.assertEqual([ln[:5] for ln in text.splitlines() if " x_side " in ln], ["side "], text)
        self.assertIn("x: all 2 gait clips cover the ground they claim", text)

    def test_its_cache_entry_is_held_as_a_gaits_is(self):
        self.put("x_side_anm.tpac", record("x_side", 1.6, (0.0, 0.0), guid=SIDE_GUID, duration=4 / 30.0), when=3_000_000)
        code, text = self.side_report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_side").startswith("FAIL"), text)
        self.assertIn("older than its clip record", text)

    def test_a_side_clip_with_no_master_or_two_fails(self):
        self.put("Copy_geo.tpac", master(stride=1.0, guid=SIDE_GUID, sideways=True))
        code, text = self.side_report()
        self.assertEqual(code, 1, text)
        self.assertIn("2 master files hold the animation its record names", text)
        os.remove(os.path.join(self.root, "clips", "Copy_geo.tpac"))
        os.remove(self.side_master)
        code, text = self.side_report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_side").startswith("FAIL"), text)

    def test_a_side_clip_with_no_travel_fails(self):
        """The node divides by the record's travel for the loop time: a strafe row on a record with no usage."""
        self.put("x_side_anm.tpac", record("x_side", guid=SIDE_GUID, duration=4 / 30.0))
        code, text = self.side_report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_side").startswith("FAIL"), text)
        self.assertIn("carries no quad_movement travel", text)

    def test_a_side_clip_whose_foot_never_plants_fails(self):
        """The stride verdict is gone for a side clip; the stance and the slip are not (trunk judged both)."""
        self.put("Side_geo.tpac", master(stride=1.0, guid=SIDE_GUID, sideways=True, down=0))
        code, text = self.side_report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_side").startswith("FAIL"), text)
        self.assertIn("is planted for only 0% of the loop", text)

    def test_a_side_clip_whose_foot_slips_fails(self):
        self.put("Side_geo.tpac", master(stride=1.0, guid=SIDE_GUID, sideways=True, drift=0.8))
        code, text = self.side_report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_side").startswith("FAIL"), text)
        self.assertIn("slips", text)

    def test_a_side_clip_listed_with_its_own_slip_limit_is_held_to_that_and_no_other_clip_is(self):
        """The dire wolf's strafe rows play the pack's turn clips, whose paws go round a point. The limit is the
        clip's, by name: a side step the wolf gets later is held to the gaits' limit."""
        self.put("Side_geo.tpac", master(stride=1.0, guid=SIDE_GUID, sideways=True, drift=0.8))
        code, text = self.side_report(side_slip={"x_side": 2.0})
        self.assertEqual(code, 0, text)
        code, text = self.side_report(side_slip={"x_side": 0.5})
        self.assertEqual(code, 1, text)
        self.assertIn("slips", text)
        self.put("x_walk_anm.tpac", record("x_walk", 2.0))
        self.put("Bought_Name_geo.tpac", master(drift=0.8))
        code, text = self.side_report(side_slip={"x_side": 2.0})
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)

    def test_a_clip_listed_with_a_side_slip_limit_that_is_not_a_side_clip_is_a_finding(self):
        code, text = self.side_report(side_slip={"x_walk": 2.0, "x_gone": 2.0})
        self.assertEqual(code, 1, text)
        for clip in ("x_walk", "x_gone"):
            self.assertIn("FAIL side_slip list: %s is listed and is not a clip on the standing strafe rows" % clip, text)

    def test_the_side_line_gives_the_worst_slip(self):
        self.assertIn("worst slip 0.000 m", self.line(self.side_report()[1], "x_side"))
        self.put("Side_geo.tpac", master(stride=1.0, guid=SIDE_GUID, sideways=True, drift=0.8))
        self.assertNotIn("worst slip 0.000 m", self.line(self.side_report()[1], "x_side"))

    def test_a_side_clip_on_forward_keys_is_held_along_the_way_its_feet_move(self):
        """The elephant's strafe rows play its walk's keys: the feet step forward, and plant and slip are judged
        along that travel."""
        self.put("Side_geo.tpac", master(stride=1.0, guid=SIDE_GUID))
        code, text = self.side_report()
        self.assertEqual(code, 0, text)
        self.assertIn("the feet move 0.00 m sideways and 1.00 m lengthways", self.line(text, "x_side"))
        self.put("Side_geo.tpac", master(stride=1.0, guid=SIDE_GUID, drift=0.8))
        code, text = self.side_report()
        self.assertEqual(code, 1, text)
        self.assertIn("slips", text)

    def test_a_side_clip_with_no_duration_fails_on_its_own_line(self):
        """The line prints travel / Duration, so a record with a Duration of 0 has no number to print."""
        self.put("x_side_anm.tpac", record("x_side", 1.6, (0.0, 0.0), guid=SIDE_GUID, duration=0.0))
        code, text = self.side_report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_side").startswith("FAIL"), text)
        self.assertIn("or no Duration", text)

    def test_a_side_clip_played_backward_is_held_to_the_length_of_its_keys(self):
        """Keys 5 down to 1: four keys at 30 a second, as the same record played forward."""
        self.put("x_side_anm.tpac", record("x_side", 1.6, (0.0, 0.0), guid=SIDE_GUID, duration=4 / 30.0,
                                           first=5.0, last=1.0))
        code, text = self.side_report()
        self.assertEqual(code, 0, text)
        self.assertNotIn("whose Duration is", text)
        self.assertIn("the feet move 1.00 m sideways", self.line(text, "x_side"))

    def test_a_standing_pose_on_a_record_played_backward_is_measured(self):
        self.put("x_pose_anm.tpac", record("x_pose", first=5.0, last=1.0))
        code, text = self.side_report()
        self.assertEqual(code, 0, text)
        self.assertIn("1 standing pose(s)", text)
        self.assertNotIn("its key range holds no key", text)

    def test_a_clip_that_a_front_row_binds_too_is_judged_as_a_gait(self):
        """The elephant's strafe rows played the walk's clip until 2026-11-19: a gait stays a gait."""
        sets = self.SETS.replace('animation="x_side"', 'animation="x_walk"')
        code, text = self.side_report(sets=sets)
        self.assertEqual(code, 1, text)                       # the walk's 1.0 s for four keys: the strafe Duration rule
        self.assertTrue(self.line(text, "x_walk").startswith("ok "), text)
        self.assertNotIn("side ", "".join(ln[:5] for ln in text.splitlines()))
        self.put("x_walk_anm.tpac", record("x_walk", 3.0))
        code, text = self.side_report(sets=sets)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)
        self.assertIn("the clip claims 3.00 m", text)


class CreatureLimitTests(unittest.TestCase):
    def test_the_defaults_are_the_elephants(self):
        self.assertEqual(c.limits({}), {"tolerance": 0.05, "max_slip": 0.08, "min_duty": 0.2})

    def test_a_creature_sets_its_own_and_an_argument_goes_over_it(self):
        creature = {"max_slip": 1.0, "min_duty": 0.15}
        self.assertEqual(c.limits(creature), {"tolerance": 0.05, "max_slip": 1.0, "min_duty": 0.15})
        self.assertEqual(c.limits(creature, tolerance=0.1, max_slip=0.5),
                         {"tolerance": 0.1, "max_slip": 0.5, "min_duty": 0.15})

    def test_a_zero_given_as_an_argument_is_a_limit_not_an_absence(self):
        self.assertEqual(c.limits({"max_slip": 1.0}, max_slip=0.0)["max_slip"], 0.0)
        self.assertEqual(c.limits({}, tolerance=0.0)["tolerance"], 0.0)

    def test_a_short_stance_is_named_at_the_default_and_passes_at_the_creatures(self):
        m = c.measure(teg._treadmill(100, 2.0, duty=0.18), 2.0)
        self.assertTrue(any("planted for only" in w for w in c.judge(m, 2.0, 0.05, 0.08)))
        self.assertEqual(c.judge(m, 2.0, 0.05, 0.08, min_duty=0.15), [])


class ReportLimitTests(Module):
    def test_the_report_judges_with_the_creatures_limits(self):
        """A foot that leaves the belt by 0.3 m in its stance: over the default 0.08 m, under a creature's 1.0 m."""
        self.put("Bought_Name_geo.tpac", master(drift=0.3))
        code, text = self.report()
        self.assertEqual(code, 1, text)
        self.assertIn("slips", text)
        code, text = self.report(max_slip=1.0)
        self.assertEqual(code, 0, text)

    def test_a_short_stance_is_named_at_the_default_and_passes_at_the_creatures_limit(self):
        """The warg's trot: a fore paw is down for 3 of 16 keys. Here 3 of 12, which is 2 planted steps."""
        self.put("Bought_Name_geo.tpac", master(keys=13))
        self.put("x_walk_anm.tpac", record("x_walk", 2.0, last=13.0))
        self.put("Backward_geo.tpac", master(stride=-2.0, guid=BACK_GUID, keys=13))
        self.put("x_back_anm.tpac", record("x_back", 2.0, (-1.25, -0.1), guid=BACK_GUID, last=13.0))
        code, text = self.report()
        self.assertEqual(code, 1, text)
        self.assertIn("is planted for only 17% of the loop", text)
        code, text = self.report(min_duty=0.15)
        self.assertEqual(code, 0, text)

    def test_an_argument_goes_over_the_creatures_limit(self):
        """A slip of 0.3 m: under the creature's 1.0 m, over an argument of 0.2 m. Then the tolerance."""
        self.put("Bought_Name_geo.tpac", master(drift=0.3))
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            self.assertEqual(c.report("x", dict(CONFIG, max_slip=1.0), self.root, BONES, SETS), 0, out.getvalue())
            self.assertEqual(c.report("x", dict(CONFIG, max_slip=1.0), self.root, BONES, SETS, max_slip=0.2), 1)
        self.assertIn("slips 0.30 m", out.getvalue())
        self.put("x_walk_anm.tpac", record("x_walk", 2.01))
        with contextlib.redirect_stdout(out):
            self.assertEqual(c.report("x", dict(CONFIG, max_slip=1.0), self.root, BONES, SETS), 0)
            self.assertEqual(c.report("x", dict(CONFIG, max_slip=1.0), self.root, BONES, SETS, tolerance=0.0001), 1)


class StampStateTests(unittest.TestCase):
    """A cache entry carries the checksum of the record the Kit cooked it from. That holds an entry to its record
    where the files' dates cannot: a copied install gives every file the copy's date."""

    def setUp(self):
        d = tempfile.TemporaryDirectory()
        self.addCleanup(d.cleanup)
        self.root = d.name
        self.buf = trp.gait_clip(name="x_walk")
        self.anm = os.path.join(self.root, "x_walk_anm.tpac")
        with open(self.anm, "wb") as fh:
            fh.write(self.buf)
        os.makedirs(os.path.join(self.root, "RuntimeDataCache"))
        self.entry = scb.rdc_path(self.root, scb.parse(self.buf).package_guid)

    def cook(self, stamp, when=1_000_000):
        with open(self.entry, "wb") as fh:
            fh.write(tsb.rdc(stamp, item=scb.parse(self.buf).item_guid))
        os.utime(self.entry, (when, when))

    def test_an_entry_with_the_records_checksum_is_current_though_the_record_file_is_newer(self):
        self.cook(tsb.stored_checksum(self.buf), when=1_000_000)
        os.utime(self.anm, (3_000_000, 3_000_000))
        self.assertIsNone(c.stamp_state(self.root, self.anm))

    def test_an_entry_cooked_from_another_state_of_the_record_is_named(self):
        self.cook(bytes(8), when=5_000_000)
        self.assertIn("does not carry its record's checksum", c.stamp_state(self.root, self.anm))

    def test_a_record_with_no_entry_is_named(self):
        self.assertIn("no cache entry", c.stamp_state(self.root, self.anm))

    def test_a_record_cut_short_is_named_not_raised(self):
        for cut in (30, 80, len(self.buf) - 20):
            with open(self.anm, "wb") as fh:
                fh.write(self.buf[:cut])
            self.assertIsNotNone(c.stamp_state(self.root, self.anm), cut)

    def test_a_file_that_is_no_record_is_named_not_raised(self):
        with open(self.anm, "wb") as fh:
            fh.write(b"TPAC")
        self.assertIn("cannot be read", c.stamp_state(self.root, self.anm))


class CookedRuleTests(Module):
    def test_a_creature_that_says_stamp_is_held_to_the_checksum_and_not_to_the_dates(self):
        os.utime(self.master, (3_000_000, 3_000_000))            # newer than the entry: the date rule fails it
        self.assertEqual(self.report()[0], 1)
        with mock.patch.object(c, "stamp_state", return_value=None) as stamp:
            code, text = self.report(cooked="stamp")
        self.assertEqual(code, 0, text)
        self.assertEqual({os.path.basename(call.args[1]) for call in stamp.call_args_list},
                         {"x_walk_anm.tpac", "x_back_anm.tpac"})

    def test_a_stamp_fault_fails_a_bound_gait(self):
        with mock.patch.object(c, "stamp_state", return_value="its cache entry does not carry its record's checksum"):
            code, text = self.report(cooked="stamp")
        self.assertEqual(code, 1, text)
        self.assertIn("does not carry its record's checksum", text)

    def test_a_cook_rule_the_gate_does_not_know_is_a_finding(self):
        """Codex P2: any other word chose the date rule without a line of output."""
        code, text = self.report(cooked="stmap")
        self.assertEqual(code, 1, text)
        self.assertIn("FAIL table: cooked", text)

    def test_the_check_of_clips_on_the_rigs_package_uses_the_creatures_rule_too(self):
        os.remove(self.master)
        self.put("skel_geo.tpac", master(), top=True, when=3_000_000)      # the rig's package holds the master
        code, text = self.report()
        self.assertEqual(code, 1, text)
        self.assertIn("older than its master", text)
        with mock.patch.object(c, "stamp_state", return_value=None) as stamp:
            code, text = self.report(cooked="stamp")
        self.assertEqual(code, 0, text)
        self.assertIn("x_pose_anm.tpac", {os.path.basename(call.args[1]) for call in stamp.call_args_list})


class WargEntryTests(unittest.TestCase):
    """The warg's entry, as literals: a typo in the table would otherwise pass on what it no longer reads."""

    def setUp(self):
        self.warg = c.CREATURES["warg"]

    def test_it_reads_as_warg_and_the_rig_the_plan_test_measures(self):
        self.assertEqual(self.warg["sets"], ("as_warg",))
        self.assertEqual(self.warg["skeleton"], ("Assets/creature/warg/mesh/Warg_Rig_V5_geo.tpac", "skeleton_warg"))
        self.assertEqual(self.warg["clips"], "Assets/creature/warg/animations")
        self.assertEqual(self.warg["feet"], {"FL": "Fingers1_L", "FR": "Fingers1_R", "BL": "Toes1_L", "BR": "Toes1_R"})

    def test_its_pace_rows_are_the_wargs_actions(self):
        self.assertEqual(self.warg["pace_actions"], {
            "backward": "act_warg_backward_walk", "strafe": ("act_warg_strafe_right", "act_warg_strafe_left"),
            "walk": "act_warg_forward_walk",
            "faster": ("act_warg_forward_trot", "act_warg_forward_canter", "act_warg_forward_gallop_right_foot",
                       "act_warg_forward_gallop_left_foot")})
        self.assertEqual(self.warg["stand_action"], "act_warg_stand_for_movement_data")

    def test_its_gaits_are_the_four_forward_paces_and_the_backward_walk(self):
        self.assertEqual(sorted(self.warg["gaits"]), [
            "warg_canter", "warg_forward_gallop_left_foot", "warg_trot", "warg_walkbackfast",
            "warg_walkbackfast_straight", "warg_walkfast"])

    def test_its_limits_are_its_own_and_its_entries_are_held_to_the_checksum(self):
        self.assertEqual(c.limits(self.warg), {"tolerance": 0.05, "max_slip": 1.0, "min_duty": 0.15})
        self.assertEqual(self.warg["cooked"], "stamp")

    def test_only_the_moving_paces_strafe_records_are_not_measured(self):
        self.assertEqual(sorted(self.warg["pending"]), [
            "warg_canter_strafe", "warg_forward_gallop_left_foot_strafe", "warg_trot_strafe", "warg_walk_strafe"])
        self.assertFalse(set(self.warg["pending"]) & set(self.warg["gaits"]))

    def test_the_elephant_and_the_spider_keep_the_default_limits_and_the_date_rule(self):
        for name in ("elephant", "spider"):
            self.assertEqual(c.limits(c.CREATURES[name]), {"tolerance": 0.05, "max_slip": 0.08, "min_duty": 0.2}, name)
            self.assertNotIn("cooked", c.CREATURES[name], name)

    def test_no_creature_lists_a_standing_strafe_clip_as_a_gait(self):
        """Literals: the clips each creature's two standing strafe rows play on the install of 2026-11-24."""
        side = {"elephant": {"war_elephant_strafe", "mumakil_strafe"},
                "spider": {"spider_strafe_left", "spider_strafe_right"},
                "warg": {"warg_strafe_left_faster", "warg_strafe_right_faster"},
                "dire_wolf": {"dire_wolf_strafe_left_faster", "dire_wolf_strafe_right_faster"}}
        for name, clips in side.items():
            self.assertFalse(clips & set(c.CREATURES[name]["gaits"]), name)
            self.assertNotIn("axis", c.CREATURES[name], name)
            self.assertNotIn("known", c.CREATURES[name], name)
            self.assertEqual("side_slip" in c.CREATURES[name], name == "dire_wolf", name)


class DireWolfEntryTests(unittest.TestCase):
    """The dire wolf's entry (#1591), as literals."""

    def setUp(self):
        self.wolf = c.CREATURES["dire_wolf"]

    def test_it_reads_as_dire_wolf_and_the_rig_the_plan_test_measures(self):
        self.assertEqual(self.wolf["sets"], ("as_dire_wolf",))
        self.assertEqual(self.wolf["skeleton"],
                         ("Assets/creature/direwolf/mesh/dire_wolf_grey_geo.tpac", "adod_wolf_armature_unused"))
        self.assertEqual(self.wolf["clips"], "Assets/creature/direwolf/animations")
        self.assertEqual(self.wolf["feet"], {"FL": "L Finger0", "FR": "R Finger0", "BL": "L Foot", "BR": "R Foot"})

    def test_its_sets_use_the_wargs_actions(self):
        self.assertEqual(self.wolf["pace_actions"], c.CREATURES["warg"]["pace_actions"])
        self.assertEqual(self.wolf["stand_action"], "act_warg_stand_for_movement_data")

    def test_its_gaits_are_the_four_forward_paces_and_the_backward_walk(self):
        self.assertEqual(sorted(self.wolf["gaits"]), [
            "dire_wolf_canter", "dire_wolf_forward_gallop_left_foot", "dire_wolf_trot", "dire_wolf_walkbackfast",
            "dire_wolf_walkbackfast_straight", "dire_wolf_walkfast"])

    def test_its_ground_limits_and_cook_rule_are_its_own(self):
        self.assertEqual(self.wolf["ground"], "lowest")
        self.assertEqual(c.limits(self.wolf), {"tolerance": 0.05, "max_slip": 0.35, "min_duty": 0.1})
        # its strafe rows play the pack's turn clips: a slip of 1.10 m and 1.09 m on 2026-11-24
        self.assertEqual(self.wolf["side_slip"], {"dire_wolf_strafe_left_faster": 1.2, "dire_wolf_strafe_right_faster": 1.2})
        self.assertEqual(self.wolf["cooked"], "stamp")

    def test_only_the_moving_paces_strafe_records_are_not_measured(self):
        self.assertEqual(sorted(self.wolf["pending"]), [
            "dire_wolf_canter_strafe", "dire_wolf_forward_gallop_left_foot_strafe", "dire_wolf_trot_strafe",
            "dire_wolf_walk_strafe"])
        self.assertFalse(set(self.wolf["pending"]) & set(self.wolf["gaits"]))

    def test_no_other_creature_takes_its_ground_from_the_feet(self):
        for name in ("elephant", "spider", "warg"):
            self.assertNotIn("ground", c.CREATURES[name], name)


GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
ARMORY = os.path.join(GAME, "Modules", "LOTRLOME_Armory")
INPUTS = [os.path.join(ARMORY, *rel.split("/")) for rel in (
    "Assets/creature/warg/animations", "Assets/creature/warg/mesh/Warg_Rig_V5_geo.tpac", "ModuleData/action_sets.xml")]


@unittest.skipUnless(all(os.path.exists(p) for p in INPUTS), "the Armory's warg is not installed here, or not whole")
class InstalledWargTests(unittest.TestCase):
    def test_the_installed_warg_passes_and_prints_its_two_side_steps(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = c.main(["--creature", "warg"])
        text = out.getvalue()
        self.assertEqual(code, 0, text)
        for clip in ("warg_walkfast", "warg_trot", "warg_canter", "warg_forward_gallop_left_foot",
                     "warg_walkbackfast", "warg_walkbackfast_straight"):
            self.assertTrue([ln for ln in text.splitlines() if ln.startswith("ok ") and (" %s " % clip) in ln], clip)
        side = [ln for ln in text.splitlines() if ln.startswith("side ")]
        self.assertEqual(len(side), 2, text)
        for ln in side:
            self.assertIn("2.13 m in a loop", ln)                # 1.60 m of travel in a record of 0.75 s
        self.assertIn("warg: all 6 gait clips cover the ground they claim", text)
        self.assertIn("clip(s): 0 finding(s)", text)


WOLF_INPUTS = [os.path.join(ARMORY, *rel.split("/")) for rel in (
    "Assets/creature/direwolf/animations", "Assets/creature/direwolf/mesh/dire_wolf_grey_geo.tpac",
    "ModuleData/action_sets.xml")]


@unittest.skipUnless(all(os.path.exists(p) for p in WOLF_INPUTS), "the Armory's dire wolf is not installed here, or not whole")
class InstalledDireWolfTests(unittest.TestCase):
    def test_the_installed_dire_wolf_passes_with_a_backward_walk_that_steps_backward(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = c.main(["--creature", "dire_wolf"])
        text = out.getvalue()
        self.assertEqual(code, 0, text)
        for clip in ("dire_wolf_walkfast", "dire_wolf_trot", "dire_wolf_canter", "dire_wolf_forward_gallop_left_foot",
                     "dire_wolf_walkbackfast", "dire_wolf_walkbackfast_straight"):
            self.assertTrue([ln for ln in text.splitlines() if ln.startswith("ok ") and (" %s " % clip) in ln], clip)
        side = [ln for ln in text.splitlines() if ln.startswith("side ")]
        self.assertEqual(len(side), 2, text)
        for ln in side:
            self.assertIn("1.60 m in a loop", ln)                # 1.60 m of travel in a record of 1.00 s
        self.assertNotIn("the legs step forward while the beast backs up", text)
        self.assertIn("dire_wolf: all 6 gait clips cover the ground they claim", text)
        self.assertIn("clip(s): 0 finding(s)", text)


if __name__ == "__main__":
    unittest.main()
