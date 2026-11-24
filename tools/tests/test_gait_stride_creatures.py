#!/usr/bin/env python3
"""tools/check_gait_stride.py as a gate for a creature whose masters were bought, not made here (#1565 item 2: the
warg): a master is found by the GUID its record names, the slip and stance limits are the creature's, a cache entry
is held to its record's checksum where the files' dates are a copy's, and a record that is known to fail is measured
on every run and may not start to pass unseen.

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


def record(name, travel=None, limits=(0.2, 2.5), guid=MASTER_GUID, last=5.0):
    """A clip record as far as the gate reads it: keys 1 to `last` of the master with `guid`, and a usage when it
    has travel. Bytes 8 to 24, the package GUID, are zero: every record of the module shares one cache entry."""
    raw = (bytes(0x48) + struct.pack("<i", len(name)) + name.encode() + bytes(12) + struct.pack("<3f", 1.0, 1.0, last)
           + bytes(16) + guid + bytes(40))
    if travel is not None:
        raw += struct.pack("<i", 13) + b"quad_movement" + bytes(4) + struct.pack("<3f", travel, *limits)
    return raw


def master(stride=2.0, guid=MASTER_GUID, keys=5, down=3, sideways=False, drift=0.0):
    """One loop of `keys` keys, the last a copy of the first: the foot is on the ground for the first `down` keys,
    moves back `stride` / (keys - 1) a key (along X when `sideways`), and is in the air for the rest. `drift` moves
    the third key that far across its travel: a slip."""
    step = stride / (keys - 1.0)
    root = []
    for i in range(keys - 1):
        along, across, up = -step * i, drift if i == 2 else 0.0, 0.0 if i < down else 0.3
        root.append((along, across, up) if sideways else (across, along, up))
    root.append((0.0, 0.0, 0.0))
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
        self.walk = self.put("x_walk_anm.tpac", record("x_walk", 2.0))
        self.put("x_back_anm.tpac", record("x_back", 2.0, (-1.25, -0.1)))
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

    def test_a_gait_listed_with_an_axis_is_measured_along_it(self):
        self.put("Bought_Name_geo.tpac", master(sideways=True))
        self.assertEqual(self.report()[0], 1)
        code, text = self.report(axis={"x_walk": 0, "x_back": 0})
        self.assertEqual(code, 0, text)
        self.assertIn("feet cover 2.00 m", self.line(text, "x_walk"))

    def test_feet_that_do_not_cover_the_claim_fail(self):
        self.put("x_walk_anm.tpac", record("x_walk", 3.0))
        code, text = self.report()
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)
        self.assertIn("the clip claims 3.00 m", text)


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
        self.assertIn("x: 1 finding(s) in the record files, the lists and the 2 gait clips", text)


class KnownFailureTests(Module):
    """x_walk claims 3.0 m on feet that cover 2.0 m, and the list says exactly that: (claims, covers, why)."""
    WHY = {"x_walk": (3.0, 2.0, "the pack's typed travel (#1591)")}

    def setUp(self):
        super().setUp()
        self.put("x_walk_anm.tpac", record("x_walk", 3.0))

    def test_without_the_list_the_clip_fails(self):
        self.assertEqual(self.report()[0], 1)

    def test_a_listed_failure_is_measured_printed_with_its_reason_and_not_counted(self):
        code, text = self.report(known=self.WHY)
        self.assertEqual(code, 0, text)
        self.assertTrue(self.line(text, "x_walk").startswith("KNOWN"), text)
        self.assertIn("claims 3.00 m, feet cover 2.00 m", self.line(text, "x_walk"))
        self.assertIn("the pack's typed travel (#1591)", text)
        self.assertIn("1 of them held at the numbers the known list gives and not judged by stride", text)

    def test_a_listed_record_that_lost_its_travel_fails(self):
        self.put("x_walk_anm.tpac", record("x_walk", 0.0))
        code, text = self.report(known=self.WHY)
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)
        self.assertIn("carries no quad_movement travel", text)

    def test_a_listed_record_that_passes_fails_the_gate_until_it_leaves_the_list(self):
        self.put("x_walk_anm.tpac", record("x_walk", 2.0))
        code, text = self.report(known={"x_walk": (2.0, 2.0, "why (#1)")})
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)
        self.assertIn("remove it from the creature's known list", text)

    def test_the_list_excuses_the_stride_alone_a_second_finding_still_fails(self):
        """Codex P1 and the Data flow lens, 2026-11-24: the list excused every finding on its clip, so a listed
        clip could gain a slip, or a paw that never plants, and pass."""
        self.put("Bought_Name_geo.tpac", master(drift=0.3))
        code, text = self.report(known=self.WHY)
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)
        self.assertIn("slips", text)
        self.assertIn("the known list covers the stride alone", text)

    def test_a_record_whose_claim_changed_is_not_the_failure_the_list_names(self):
        self.put("x_walk_anm.tpac", record("x_walk", 4.0))
        code, text = self.report(known=self.WHY)
        self.assertEqual(code, 1, text)
        self.assertIn("the known list holds 3.00 m claimed and 2.00 m covered", text)

    def test_a_master_whose_stride_changed_is_not_the_failure_the_list_names(self):
        """The other half: the feet got worse and the claim stayed. x_back plays the same master and fails too."""
        self.put("Bought_Name_geo.tpac", master(stride=1.0))
        code, text = self.report(known=self.WHY)
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)
        self.assertIn("the known list holds 3.00 m claimed and 2.00 m covered", text)

    def test_the_list_does_not_cover_an_entry_the_kit_has_not_cooked(self):
        self.put("x_walk_anm.tpac", record("x_walk", 3.0), when=3_000_000)
        code, text = self.report(known=self.WHY)
        self.assertEqual(code, 1, text)
        self.assertIn("older than its clip record", text)

    def test_the_list_does_not_cover_an_entry_under_the_stamp_rule_either(self):
        fault = "its cache entry does not carry its record's checksum"
        with mock.patch.object(c, "stamp_state", side_effect=lambda root, anm: fault if "x_walk" in anm else None):
            code, text = self.report(known=self.WHY, cooked="stamp")
        self.assertEqual(code, 1, text)
        self.assertTrue(self.line(text, "x_walk").startswith("FAIL"), text)
        self.assertIn(fault, text)

    def test_a_listed_clip_that_the_run_does_not_measure_is_a_finding(self):
        code, text = self.report(known=dict(self.WHY, x_pose=(1.0, 0.5, "a pose has no travel (#1)")))
        self.assertEqual(code, 1, text)
        self.assertIn("FAIL known list: x_pose", text)

    def test_a_listed_gait_that_no_named_set_binds_is_a_finding(self):
        """Codex P2: a name in `gaits` is not a measurement. An absent, unbound gait is only a note."""
        code, text = self.report(gaits=["x_walk", "x_back", "x_gone"],
                                 known=dict(self.WHY, x_gone=(1.0, 0.5, "it was bound once (#1)")))
        self.assertEqual(code, 1, text)
        self.assertIn("FAIL known list: x_gone", text)
        self.assertIn("no named set binds it", text)

    def test_a_clip_in_the_known_list_and_the_pending_list_is_a_finding(self):
        code, text = self.report(known=self.WHY, pending={"x_walk": "why"})
        self.assertEqual(code, 1, text)
        self.assertIn("FAIL known list: x_walk is also in the pending list", text)


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
        self.put("x_back_anm.tpac", record("x_back", 2.0, (-1.25, -0.1), last=13.0))
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

    def test_its_gaits_are_the_four_forward_paces_the_backward_walk_and_the_side_steps(self):
        self.assertEqual(sorted(self.warg["gaits"]), [
            "warg_canter", "warg_forward_gallop_left_foot", "warg_strafe_left_faster", "warg_strafe_right_faster",
            "warg_trot", "warg_walkbackfast", "warg_walkbackfast_straight", "warg_walkfast"])
        self.assertEqual(self.warg["axis"], {"warg_strafe_left_faster": 0, "warg_strafe_right_faster": 0})

    def test_its_limits_are_its_own_and_its_entries_are_held_to_the_checksum(self):
        self.assertEqual(c.limits(self.warg), {"tolerance": 0.05, "max_slip": 1.0, "min_duty": 0.15})
        self.assertEqual(self.warg["cooked"], "stamp")

    def test_the_known_list_is_the_two_side_steps_each_with_its_issue(self):
        self.assertEqual({clip: entry[:2] for clip, entry in self.warg["known"].items()},
                         {"warg_strafe_left_faster": (1.6, 0.73), "warg_strafe_right_faster": (1.6, 1.0)})
        for entry in self.warg["known"].values():
            self.assertIn("#1591", entry[2])

    def test_only_the_moving_paces_strafe_records_are_not_measured(self):
        self.assertEqual(sorted(self.warg["pending"]), [
            "warg_canter_strafe", "warg_forward_gallop_left_foot_strafe", "warg_trot_strafe", "warg_walk_strafe"])
        self.assertFalse(set(self.warg["pending"]) & (set(self.warg["gaits"]) | set(self.warg["known"])))

    def test_the_elephant_and_the_spider_keep_the_default_limits_and_the_date_rule(self):
        for name in ("elephant", "spider"):
            self.assertEqual(c.limits(c.CREATURES[name]), {"tolerance": 0.05, "max_slip": 0.08, "min_duty": 0.2}, name)
            self.assertNotIn("cooked", c.CREATURES[name], name)
            self.assertNotIn("known", c.CREATURES[name], name)


GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
ARMORY = os.path.join(GAME, "Modules", "LOTRLOME_Armory")
INPUTS = [os.path.join(ARMORY, *rel.split("/")) for rel in (
    "Assets/creature/warg/animations", "Assets/creature/warg/mesh/Warg_Rig_V5_geo.tpac", "ModuleData/action_sets.xml")]


@unittest.skipUnless(all(os.path.exists(p) for p in INPUTS), "the Armory's warg is not installed here, or not whole")
class InstalledWargTests(unittest.TestCase):
    def test_the_installed_warg_passes_with_its_two_known_side_steps(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = c.main(["--creature", "warg"])
        text = out.getvalue()
        self.assertEqual(code, 0, text)
        for clip in ("warg_walkfast", "warg_trot", "warg_canter", "warg_forward_gallop_left_foot",
                     "warg_walkbackfast", "warg_walkbackfast_straight"):
            self.assertTrue([ln for ln in text.splitlines() if ln.startswith("ok ") and (" %s " % clip) in ln], clip)
        self.assertEqual(len([ln for ln in text.splitlines() if ln.startswith("KNOWN")]), 2, text)
        self.assertIn("0 finding(s)", text)


if __name__ == "__main__":
    unittest.main()
