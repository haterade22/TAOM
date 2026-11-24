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
from unittest import mock

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


def gait_clip(name="warg_canter", travel=4.0, limits=(4.6, 8.0), usage="quad_movement", duration=0.55, count=1,
              tail=b"", declared=None, listed=0):
    """A gait record as the warg's are: the flags, then one clip usage (its name, an empty list, travel and the two
    pace limits). `usage` None builds the in-place twin, which has no usage. `count` writes that many usages,
    `tail` bytes after the last, and `declared` a usage count that differs from the usages written: shapes the tool
    must not take for a gait. `listed` is the length the usage's list states (0 in every record read)."""
    said = count if declared is None else declared
    m = bytearray(tsb.meta(flags=("make_walk_sound",), usages=0 if usage is None else said))
    struct.pack_into("<3f", m, 4, duration, 1.0, 17.0)
    if usage is not None:
        m += (tsb.sstr(usage) + struct.pack("<i", listed) + struct.pack("<3f", travel, *limits)) * count + tail
    return tsb.package(name, m=bytes(m))


class GaitFieldTests(unittest.TestCase):
    """#1563: a quadruped gait record also states its travel, its pace limits and its step points."""

    def test_travel_limits_and_step_points_are_written_and_read_back(self):
        new = rp.repoint(gait_clip(), uuid.uuid4(), 820.0, 850.0, duration=0.46, travel=3.22, limits=(4.6, 7.0),
                         step_points=(0.1, 0.3, 0.6, 0.8))
        t = rp.timing(new)
        self.assertAlmostEqual(t["duration"], 0.46, places=5)
        self.assertAlmostEqual(t["travel"], 3.22, places=5)
        self.assertEqual([round(x, 5) for x in t["limits"]], [4.6, 7.0])
        self.assertEqual([round(x, 5) for x in t["step_points"]], [0.1, 0.3, 0.6, 0.8])
        self.assertTrue(scb.checksum_ok(new))

    def test_only_the_named_fields_change(self):
        old = gait_clip()
        new = rp.repoint(old, uuid.UUID(bytes_le=b"" * 16), 1.0, 17.0, travel=3.22, limits=(4.6, 7.0))
        a = scb.parse(old)
        self.assertEqual(len(new), len(old))
        diff = {i for i in range(len(old)) if old[i] != new[i]}
        tail = set(range(a.ck_at - 12, a.ck_at + 8))               # travel, two limits, the checksum
        self.assertTrue(diff and diff <= tail, "bytes outside travel, limits and checksum changed: %r" % sorted(diff - tail)[:8])

    def test_fields_not_given_are_kept(self):
        old = gait_clip(travel=4.0, limits=(4.6, 8.0))
        t = rp.timing(rp.repoint(old, uuid.uuid4(), 820.0, 850.0))
        self.assertAlmostEqual(t["travel"], 4.0, places=5)
        self.assertEqual([round(x, 5) for x in t["limits"]], [4.6, 8.0])
        self.assertEqual([round(x, 5) for x in t["step_points"]], [0.2, -1.0, -1.0, -1.0])

    def test_a_record_with_no_usage_reads_no_travel_and_refuses_one(self):
        twin = gait_clip(usage=None)
        self.assertIsNone(rp.timing(twin)["travel"])
        self.assertIsNone(rp.timing(twin)["limits"])
        with self.assertRaisesRegex(rp.RepointError, "no quad_movement usage"):
            rp.repoint(twin, uuid.uuid4(), 820.0, 850.0, travel=3.22)
        with self.assertRaisesRegex(rp.RepointError, "no quad_movement usage"):
            rp.repoint(twin, uuid.uuid4(), 820.0, 850.0, limits=(1.0, 2.0))

    def test_another_usage_is_not_taken_for_a_gait(self):
        other = gait_clip(usage="quad_something")
        self.assertIsNone(rp.timing(other)["travel"])
        with self.assertRaisesRegex(rp.RepointError, "no quad_movement usage"):
            rp.repoint(other, uuid.uuid4(), 820.0, 850.0, travel=3.22)

    def test_a_record_with_two_usages_or_bytes_after_its_usage_is_not_taken_for_a_gait(self):
        # declared=2 over one usage's bytes is the case only the count guard refuses: the end-of-record guard
        # alone refuses the other two
        # and listed=1 is the case only the empty-list guard refuses
        for odd in (gait_clip(count=2), gait_clip(tail=b"\x00" * 4), gait_clip(declared=2), gait_clip(listed=1)):
            self.assertIsNone(rp.timing(odd)["travel"])
            with self.assertRaisesRegex(rp.RepointError, "no quad_movement usage"):
                rp.repoint(odd, uuid.uuid4(), 820.0, 850.0, travel=3.22)

    def test_equal_limits_are_kept_as_a_strafe_record_has_them(self):
        t = rp.timing(rp.repoint(gait_clip(), uuid.uuid4(), 820.0, 850.0, limits=(0.0, 0.0)))
        self.assertEqual(t["limits"], [0.0, 0.0])

    def test_bad_values_are_refused(self):
        for kwargs in ({"travel": float("nan")}, {"travel": -1.0}, {"limits": (7.0, 4.6)}, {"limits": (1.0,)},
                       {"limits": (float("inf"), 2.0)}, {"step_points": (0.1, 0.2, 0.3)},
                       {"step_points": (0.1, 0.2, 0.3, 1.0)}, {"step_points": (0.1, 0.2, 0.3, -0.5)},
                       {"duration": 0.0}, {"duration": float("nan")}):
            with self.subTest(kwargs=kwargs):
                with self.assertRaises(rp.RepointError):
                    rp.repoint(gait_clip(), uuid.uuid4(), 820.0, 850.0, **kwargs)

    def test_a_plan_row_carries_the_gait_fields_and_check_sees_a_reverted_one(self):
        g = uuid.uuid4()
        row = {"clip": "warg_canter", "master": "Warg_Slow_run", "source1": 820, "source2": 850, "duration": 0.46,
               "travel": 3.22, "limits": [4.6, 7.0], "step_points": [0.1, 0.3, 0.6, 0.8]}
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "warg_canter_anm.tpac")
            with open(path, "wb") as fh:
                fh.write(gait_clip())
            with open(os.path.join(d, "Warg_Slow_run_geo.tpac"), "wb") as fh:
                fh.write(tcf.master_package("warg_slow_run", g))
            rows = rp.plan([row], d)
            self.assertTrue(rows[0].changed)
            rp.apply(rows, stamp="t")
            self.assertFalse(rp.plan([row], d)[0].changed)             # idempotent
            with open(os.path.join(d, "Warg_Trot_geo.tpac"), "wb") as fh:
                fh.write(tcf.master_package("warg_trot", uuid.uuid4()))
            for field, value in (("travel", 8.5), ("limits", [4.6, 8.0]), ("duration", 0.55),
                                 ("step_points", [0.05, 0.3, 0.55, 0.8]), ("master", "Warg_Trot"),
                                 ("source1", 1), ("source2", 17)):
                with self.subTest(field=field):                        # each value alone, as a reinstall would undo it
                    self.assertTrue(rp.plan([dict(row, **{field: value})], d)[0].changed)


class PlanRowTests(unittest.TestCase):
    """A plan is typed by hand and its --check is a gate: a row the tool does not understand in full is refused, in
    the dry run too, and never passes for done."""

    ROW = {"clip": "warg_canter", "master": "Warg_Slow_run", "source1": 820, "source2": 850, "travel": 3.22}

    def folder(self, d):
        with open(os.path.join(d, "warg_canter_anm.tpac"), "wb") as fh:
            fh.write(gait_clip())
        with open(os.path.join(d, "Warg_Slow_run_geo.tpac"), "wb") as fh:
            fh.write(tcf.master_package("warg_slow_run", uuid.uuid4()))

    def test_a_misspelled_field_is_refused_by_name(self):
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)
            row = dict(self.ROW, travle=3.22)
            del row["travel"]
            with self.assertRaisesRegex(rp.RepointError, "warg_canter.*travle"):
                rp.plan([row], d)

    def test_a_row_without_a_required_field_is_refused_by_name(self):
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)
            for missing in ("master", "source1", "source2"):
                with self.subTest(missing=missing):
                    row = {k: v for k, v in self.ROW.items() if k != missing}
                    with self.assertRaisesRegex(rp.RepointError, missing):
                        rp.plan([row], d)
            with self.assertRaisesRegex(rp.RepointError, "clip"):
                rp.plan([{k: v for k, v in self.ROW.items() if k != "clip"}], d)

    def test_a_clip_named_by_two_rows_is_refused_and_nothing_is_planned(self):
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)
            first = {k: v for k, v in self.ROW.items() if k != "travel"}
            with self.assertRaisesRegex(rp.RepointError, "two rows.*warg_canter"):
                rp.plan([dict(first, travel=3.22), dict(first, limits=[4.6, 7.0])], d)

    def main(self, *argv):
        import contextlib
        import io
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = rp.main(list(argv))
        return code, out.getvalue()

    def plan_file(self, d, rows):
        import json
        path = os.path.join(d, "plan.json")
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(rows, fh)
        return path

    def test_a_refused_plan_exits_1_with_its_reason_and_no_traceback(self):
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)
            for flags in ((), ("--check",)):
                with self.subTest(flags=flags):
                    code, text = self.main("--plan", self.plan_file(d, [dict(self.ROW, travle=1)]), "--clips-dir", d, *flags)
                    self.assertEqual(code, 1)
                    self.assertIn("refused", text)
                    self.assertIn("travle", text)

    def test_a_machine_without_the_clips_folder_is_not_run_not_failed(self):
        with tempfile.TemporaryDirectory() as d:
            code, text = self.main("--plan", self.plan_file(d, [self.ROW]), "--module", os.path.join(d, "nope"),
                                   "--under", "creature/warg/animations", "--check")
            self.assertEqual(code, 2)
            self.assertIn("NOT RUN", text)

    def test_a_machine_without_xxhash_is_not_run_not_failed(self):
        from unittest import mock
        gone = ModuleNotFoundError("No module named 'xxhash'", name="xxhash")
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)
            with mock.patch.object(scb, "_xxh64", side_effect=gone):
                code, text = self.main("--plan", self.plan_file(d, [self.ROW]), "--clips-dir", d, "--check")
            self.assertEqual(code, 2)
            self.assertIn("NOT RUN", text)

    def test_an_import_fault_of_the_tool_itself_is_not_taken_for_a_missing_xxhash(self):
        from unittest import mock
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)
            for fault in (ImportError("cannot import name 'master_item' from 'creature_fit'"),
                          ModuleNotFoundError("No module named 'creature_fit'", name="creature_fit")):
                with self.subTest(fault=type(fault).__name__):
                    with mock.patch.object(rp, "master_guid", side_effect=fault):
                        with self.assertRaises(ImportError):
                            self.main("--plan", self.plan_file(d, [self.ROW]), "--clips-dir", d, "--check")

    def test_a_plan_that_cannot_be_read_as_a_list_of_rows_is_refused_with_no_traceback(self):
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)
            for label, body in (("an object", '{"clip": "warg_canter"}'), ("a list of names", '["warg_canter"]'),
                                ("not JSON", "not json"), ("no row", "[]")):
                with self.subTest(plan=label):
                    path = os.path.join(d, "plan.json")
                    with open(path, "w", encoding="utf-8") as fh:
                        fh.write(body)
                    for flags in ((), ("--check",)):
                        code, text = self.main("--plan", path, "--clips-dir", d, *flags)
                        self.assertEqual(code, 1, label)
                        self.assertIn("refused", text)
            code, text = self.main("--plan", os.path.join(d, "no_such_plan.json"), "--clips-dir", d, "--check")
            self.assertEqual(code, 1)
            self.assertIn("refused", text)


class CookedCheckTests(unittest.TestCase):
    """--check also says whether the Kit cooked the record as it is now: the cache entry carries the record's
    checksum. The game plays the cooked keys, so a repointed clip is not live until then."""

    ROW = {"clip": "warg_canter", "master": "Warg_Slow_run", "source1": 820, "source2": 850, "duration": 0.46,
           "travel": 3.22, "limits": [4.6, 7.0]}

    def module(self, d, entry_for=None):
        """A module with the clip, its master and (entry_for: package bytes) a cache entry stamped for those bytes."""
        clips = os.path.join(d, "Assets", "creature", "warg", "animations")
        os.makedirs(clips)
        os.makedirs(os.path.join(d, "RuntimeDataCache"))
        with open(os.path.join(clips, "warg_canter_anm.tpac"), "wb") as fh:
            fh.write(gait_clip())
        with open(os.path.join(clips, "Warg_Slow_run_geo.tpac"), "wb") as fh:
            fh.write(tcf.master_package("warg_slow_run", uuid.uuid4()))
        plan_path = os.path.join(d, "plan.json")
        with open(plan_path, "w", encoding="utf-8") as fh:
            import json
            json.dump([self.ROW], fh)
        return clips, plan_path

    def stamp(self, d, clips):
        buf = read(os.path.join(clips, "warg_canter_anm.tpac"))
        with open(scb.rdc_path(d, scb.parse(buf).package_guid), "wb") as fh:
            fh.write(tsb.rdc(tsb.stored_checksum(buf)))

    def check(self, d, plan_path):
        import contextlib
        import io
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = rp.main(["--plan", plan_path, "--module", d, "--under", "creature/warg/animations", "--check"])
        return code, out.getvalue()

    def test_a_record_not_yet_repointed_is_pending(self):
        with tempfile.TemporaryDirectory() as d:
            clips, plan_path = self.module(d)
            self.stamp(d, clips)
            code, text = self.check(d, plan_path)
            self.assertEqual(code, 1)
            self.assertIn("PENDING", text)

    def test_a_repointed_record_the_kit_has_not_cooked_fails_and_says_so(self):
        with tempfile.TemporaryDirectory() as d:
            clips, plan_path = self.module(d)
            self.stamp(d, clips)                                       # cooked for the OLD bytes
            rp.apply(rp.plan([self.ROW], clips), stamp="t")
            code, text = self.check(d, plan_path)
            self.assertEqual(code, 1)
            self.assertIn("DONE", text)
            self.assertIn("cooked=False", text)
            self.assertIn("Modding Kit", text)

    def test_a_repointed_record_with_no_cache_entry_fails(self):
        with tempfile.TemporaryDirectory() as d:
            clips, plan_path = self.module(d)
            rp.apply(rp.plan([self.ROW], clips), stamp="t")
            code, text = self.check(d, plan_path)
            self.assertEqual(code, 1)
            self.assertIn("cooked=False", text)

    def test_a_master_written_after_the_cook_fails_though_the_record_did_not_change(self):
        """The stamp is the RECORD's checksum. A master whose keys changed under one GUID leaves record and stamp
        alike while the entry holds the old keys; the file dates are the only signal (Codex pre-review, #1563)."""
        with tempfile.TemporaryDirectory() as d:
            clips, plan_path = self.module(d)
            rp.apply(rp.plan([self.ROW], clips), stamp="t")
            self.stamp(d, clips)
            self.assertEqual(self.check(d, plan_path)[0], 0)
            entry = scb.rdc_path(d, scb.parse(read(os.path.join(clips, "warg_canter_anm.tpac"))).package_guid)
            later = os.path.getmtime(entry) + 60
            os.utime(os.path.join(clips, "Warg_Slow_run_geo.tpac"), (later, later))
            code, text = self.check(d, plan_path)
            self.assertEqual(code, 1)
            self.assertIn("cooked=False", text)
            self.assertIn("older than its master", text)

    def test_a_repointed_and_cooked_record_passes(self):
        with tempfile.TemporaryDirectory() as d:
            clips, plan_path = self.module(d)
            rp.apply(rp.plan([self.ROW], clips), stamp="t")
            self.stamp(d, clips)                                       # the Kit cooked the new bytes
            code, text = self.check(d, plan_path)
            self.assertEqual(code, 0, text)
            self.assertIn("cooked=True", text)


class FlagTests(unittest.TestCase):
    """A row may state the record's whole flag list (#1565: the rider's two gallop turn clips lacked
    synch_with_horse, so they ran at their own Duration beside a warg whose loop time follows its speed)."""

    def test_a_flag_is_added_and_every_other_field_survives(self):
        old = gait_clip(name="rider_x", travel=2.5, limits=(1.0, 3.0))
        new = rp.repoint(old, uuid.uuid4(), 1.0, 17.0, flags=["make_walk_sound", "synch_with_horse"])
        a, b = scb.parse(old), scb.parse(new)
        self.assertEqual(b.flags, ["make_walk_sound", "synch_with_horse"])
        self.assertEqual(len(new) - len(old), 4 + len("synch_with_horse"))
        self.assertTrue(scb.checksum_ok(new))
        self.assertEqual((a.name, a.field, a.usages, a.userdata), (b.name, b.field, b.usages, b.userdata))
        meta = rp.timing(new)
        self.assertAlmostEqual(meta["travel"], 2.5, places=5)            # the usage after the flags moved with them
        self.assertEqual([round(v, 3) for v in meta["limits"]], [1.0, 3.0])

    def test_flags_and_gait_fields_in_one_row_land_in_the_right_bytes(self):
        new = rp.repoint(gait_clip(), uuid.uuid4(), 1.0, 17.0, flags=["make_walk_sound", "enforce_lowerbody"],
                         travel=3.0, limits=[2.0, 5.0], duration=0.5)
        meta = rp.timing(new)
        self.assertEqual(scb.parse(new).flags, ["make_walk_sound", "enforce_lowerbody"])
        self.assertAlmostEqual(meta["travel"], 3.0, places=5)
        self.assertEqual(meta["limits"], [2.0, 5.0])
        self.assertAlmostEqual(meta["duration"], 0.5, places=5)
        self.assertTrue(scb.checksum_ok(new))

    def test_a_flag_can_be_taken_off_and_the_same_list_changes_nothing(self):
        old = gait_clip()
        g = uuid.UUID(rp.timing(old)["master_guid"])
        self.assertEqual(scb.parse(rp.repoint(old, g, 1.0, 17.0, flags=[])).flags, [])
        self.assertEqual(rp.repoint(old, g, 1.0, 17.0, flags=["make_walk_sound"]), old)

    def test_a_bad_flag_list_is_refused(self):
        for bad in ("synch_with_horse", ["a", "a"], [""], ["two words"], [7], ["caf\u00e9"]):
            with self.assertRaises(rp.RepointError, msg=repr(bad)):
                rp.repoint(gait_clip(), uuid.uuid4(), 1.0, 17.0, flags=bad)

    def test_a_plan_refuses_a_flag_no_record_of_the_folder_carries(self):
        """A misspelled flag is a name the engine does not know: the record would read DONE and the clip would
        stay unsynchronised. A flag some record of the folder already carries is a spelling the engine reads."""
        with tempfile.TemporaryDirectory() as d:
            for name, flags in (("rider_turn", ("client_prediction",)), ("rider_walk", ("client_prediction", "synch_with_horse"))):
                m = bytearray(tsb.meta(flags=flags, usages=0))
                struct.pack_into("<3f", m, 4, 0.58, 1.0, 20.0)
                with open(os.path.join(d, name + "_anm.tpac"), "wb") as fh:
                    fh.write(tsb.package(name, m=bytes(m), pkg=tsb.pkg_guid(name)))
            with open(os.path.join(d, "rider_master_geo.tpac"), "wb") as fh:
                fh.write(tcf.master_package("rider_master", uuid.uuid4()))
            row = {"clip": "rider_turn", "master": "rider_master", "source1": 1, "source2": 20}
            with self.assertRaisesRegex(rp.RepointError, "synch_with_hores"):
                rp.plan([dict(row, flags=["client_prediction", "synch_with_hores"])], d)
            rows = rp.plan([dict(row, flags=["client_prediction", "synch_with_horse"])], d)
            rp.apply(rows, stamp="t")
            self.assertEqual(scb.parse(read(os.path.join(d, "rider_turn_anm.tpac"))).flags,
                             ["client_prediction", "synch_with_horse"])
            self.assertFalse(rp.plan([dict(row, flags=["client_prediction", "synch_with_horse"])], d)[0].changed)


class RestFrameTests(unittest.TestCase):
    """Frame 0 of a Kit import is the rest pose, so a range that touches it is refused. The dire wolf's masters are
    keyed from 0 and its records play from 0: a master some record of the folder already plays from frame 0 may be
    played from 0 by another row (#1565)."""

    def folder(self, d, front_from=0.0):
        self.master = uuid.uuid4()
        with open(os.path.join(d, "wolf_walk_geo.tpac"), "wb") as fh:
            fh.write(tcf.master_package("wolf_walk", self.master))
        for name, first in (("wolf_walkfast", front_from), ("wolf_trot_stand", 1.0)):
            m = bytearray(tsb.meta(flags=(), usages=0))
            struct.pack_into("<3f", m, 4, 1.0, first, 32.0)
            m[4 + 28:4 + 44] = self.master.bytes_le
            with open(os.path.join(d, name + "_anm.tpac"), "wb") as fh:
                fh.write(tsb.package(name, m=bytes(m), pkg=tsb.pkg_guid(name)))

    def test_a_record_that_plays_from_frame_0_may_keep_it(self):
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)
            rows = rp.plan([{"clip": "wolf_walkfast", "master": "wolf_walk", "source1": 0, "source2": 32, "duration": 0.82}], d)
            self.assertTrue(rows[0].changed)
            self.assertEqual(rp.timing(rows[0].new)["source1"], 0.0)

    def test_a_twin_may_play_from_frame_0_of_a_master_its_front_record_plays_from_0(self):
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)
            rows = rp.plan([{"clip": "wolf_walk_stand", "template": "wolf_trot_stand", "master": "wolf_walk",
                             "source1": 0, "source2": 32, "duration": 2.38}], d)
            self.assertEqual(rp.timing(rows[0].new)["source1"], 0.0)

    def test_frame_0_of_a_master_no_record_plays_from_0_is_still_refused(self):
        with tempfile.TemporaryDirectory() as d:
            self.folder(d, front_from=1.0)
            with self.assertRaisesRegex(rp.RepointError, "frame 0"):
                rp.plan([{"clip": "wolf_trot_stand", "master": "wolf_walk", "source1": 0, "source2": 32}], d)

    def test_frame_0_of_one_master_does_not_open_frame_0_of_another(self):
        """The permission belongs to the master a record plays from 0. The warg's folder holds a placeholder record
        that plays its own master from 0: that must not let a gait row start at a rest frame."""
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)                                            # wolf_walkfast plays wolf_walk from 0
            with open(os.path.join(d, "wolf_trot_geo.tpac"), "wb") as fh:
                fh.write(tcf.master_package("wolf_trot", uuid.uuid4()))
            with self.assertRaisesRegex(rp.RepointError, "frame 0"):
                rp.plan([{"clip": "wolf_trot_stand", "master": "wolf_trot", "source1": 0, "source2": 24}], d)

    def test_the_pure_function_refuses_frame_0_unless_told_the_master_plays_it(self):
        with self.assertRaisesRegex(rp.RepointError, "frame 0"):
            rp.repoint(clip(), uuid.uuid4(), 0.0, 32.0)
        self.assertEqual(rp.timing(rp.repoint(clip(), uuid.uuid4(), 0.0, 32.0, rest_frame_played=True))["source1"], 0.0)
        with self.assertRaisesRegex(rp.RepointError, "frame 0"):          # a frame before the first is never a key
            rp.repoint(clip(), uuid.uuid4(), -1.0, 32.0, rest_frame_played=True)


class TemplateTests(unittest.TestCase):
    """A row may name a "template": a clip that is not in the folder is then made from that record, under its own
    name and GUIDs (#1565: the warg's walk had no in-place twin, so its front and "none" rows shared one Duration)."""

    MASTER = uuid.UUID("11111111-2222-3333-4444-555555555555")
    ROW = {"clip": "warg_walk_stand", "template": "warg_trot_stand", "master": "Warg_Walk", "source1": 1,
           "source2": 29, "duration": 2.06}

    def folder(self, d):
        with open(os.path.join(d, "warg_trot_stand_anm.tpac"), "wb") as fh:
            fh.write(gait_clip(name="warg_trot_stand", usage=None, duration=0.73))
        with open(os.path.join(d, "Warg_Walk_geo.tpac"), "wb") as fh:
            fh.write(tcf.master_package("warg_walk", self.MASTER))
        return os.path.join(d, "warg_walk_stand_anm.tpac")

    def test_a_clip_that_is_not_there_is_made_from_its_template(self):
        with tempfile.TemporaryDirectory() as d:
            made = self.folder(d)
            donor = read(os.path.join(d, "warg_trot_stand_anm.tpac"))
            rows = rp.plan([self.ROW], d)
            self.assertTrue(rows[0].changed)
            self.assertFalse(os.path.exists(made), "the plan writes nothing")
            self.assertEqual(rp.apply(rows, stamp="t"), 1)
            new, old = scb.parse(read(made)), scb.parse(donor)
            self.assertEqual(new.name, "warg_walk_stand")
            self.assertNotEqual(new.package_guid, old.package_guid)
            self.assertNotEqual(new.item_guid, old.item_guid)
            self.assertNotEqual(new.package_guid, new.item_guid)
            self.assertEqual((new.flags, new.usages, new.userdata, new.field), (old.flags, old.usages, old.userdata, old.field))
            meta = rp.timing(read(made))
            self.assertEqual((meta["master_guid"], meta["source1"], meta["source2"]), (str(self.MASTER), 1.0, 29.0))
            self.assertAlmostEqual(meta["duration"], 2.06, places=5)
            self.assertTrue(scb.checksum_ok(read(made)))
            self.assertEqual(read(os.path.join(d, "warg_trot_stand_anm.tpac")), donor, "the template is not written")
            self.assertEqual(sorted(os.listdir(d)), ["Warg_Walk_geo.tpac", "warg_trot_stand_anm.tpac",
                                                     "warg_walk_stand_anm.tpac"], "a new file has nothing to back up")

    def test_a_second_run_finds_the_made_clip_and_changes_nothing(self):
        with tempfile.TemporaryDirectory() as d:
            made = self.folder(d)
            rp.apply(rp.plan([self.ROW], d), stamp="t")
            first = read(made)
            again = rp.plan([self.ROW], d)
            self.assertFalse(again[0].changed)
            self.assertEqual(rp.apply(again, stamp="u"), 0)
            self.assertEqual(read(made), first)

    def test_the_guids_come_from_the_name_so_every_machine_makes_one_package(self):
        donor = gait_clip(name="warg_trot_stand", usage=None)
        self.assertEqual(rp.clone(donor, "warg_walk_stand"), rp.clone(donor, "warg_walk_stand"))
        a, b = scb.parse(rp.clone(donor, "warg_walk_stand")), scb.parse(rp.clone(donor, "warg_walk_idle"))
        self.assertNotEqual(a.package_guid, b.package_guid)
        self.assertNotEqual(a.item_guid, b.item_guid)

    def test_a_name_of_another_length_still_reads_as_a_package(self):
        donor = gait_clip(name="warg_trot_stand", usage=None)
        for name in ("w", "warg_walk_stand_of_a_much_longer_name"):
            new = rp.clone(donor, name)
            self.assertEqual(scb.parse(new).name, name)                 # parse holds the size field to the file
            self.assertEqual(len(new) - len(donor), len(name) - len("warg_trot_stand"))

    def test_a_name_the_engine_or_a_file_cannot_hold_is_refused(self):
        donor = gait_clip(name="warg_trot_stand", usage=None)
        for name in ("", "warg walk", "warg/walk", "..\\warg", "w" * 64, "warg_wälk"):
            with self.assertRaises(rp.RepointError, msg=name):
                rp.clone(donor, name)

    def test_a_clip_that_is_not_there_and_names_no_template_is_refused_as_before(self):
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)
            row = {k: v for k, v in self.ROW.items() if k != "template"}
            with self.assertRaisesRegex(rp.RepointError, "0 packages carry that clip"):
                rp.plan([row], d)

    def test_a_template_that_is_not_there_is_refused(self):
        with tempfile.TemporaryDirectory() as d:
            self.folder(d)
            with self.assertRaisesRegex(rp.RepointError, "template warg_no_such"):
                rp.plan([dict(self.ROW, template="warg_no_such")], d)

    def test_a_file_of_that_name_that_holds_another_clip_is_never_overwritten(self):
        with tempfile.TemporaryDirectory() as d:
            made = self.folder(d)
            with open(made, "wb") as fh:
                fh.write(gait_clip(name="some_other_clip", usage=None))
            before = read(made)
            with self.assertRaisesRegex(rp.RepointError, "does not carry that clip"):
                rp.plan([self.ROW], d)
            self.assertEqual(read(made), before)

    def test_a_template_keyed_to_a_clip_or_generated_from_two_is_refused(self):
        """Its copy would carry the key of the template, or stay a generated child of the template's parents."""
        for field in ({"field": "warg_trot_stand"}, {"src1": "a", "src2": "b", "gen": 0}):
            m = bytearray(tsb.meta(flags=(), usages=0, **field))
            with self.assertRaisesRegex(rp.RepointError, "template", msg=repr(field)):
                rp.clone(tsb.package("warg_trot_stand", m=bytes(m)), "warg_walk_stand")

    def test_a_write_that_fails_leaves_no_file_of_the_new_clip(self):
        with tempfile.TemporaryDirectory() as d:
            made = self.folder(d)
            row = rp.plan([self.ROW], d)[0]
            with self.assertRaisesRegex(rp.RepointError, "TypeError"):
                rp.apply([row._replace(new="not bytes")], stamp="t")      # the file is open when the write fails
            self.assertFalse(os.path.exists(made))
            with mock.patch.object(rp.scb, "checksum_ok", return_value=False), self.assertRaises(rp.RepointError):
                rp.apply([row], stamp="t")                                # the read-back check fails
            self.assertFalse(os.path.exists(made))
            self.assertEqual(rp.apply(rp.plan([self.ROW], d), stamp="t"), 1, "the next run is not blocked")

    def test_a_failure_while_applying_exits_1_with_a_reason_and_no_traceback(self):
        import contextlib
        import io
        import json
        with tempfile.TemporaryDirectory() as d:
            clips = os.path.join(d, "Assets", "creature", "warg", "animations")
            os.makedirs(clips)
            self.folder(clips)
            plan_path = os.path.join(d, "plan.json")
            with open(plan_path, "w", encoding="utf-8") as fh:
                json.dump([self.ROW], fh)
            out = io.StringIO()
            with mock.patch.object(rp, "game_or_kit_running", return_value=False), \
                    mock.patch.object(rp, "apply", side_effect=rp.RepointError("warg_walk_stand: read-back differs")), \
                    contextlib.redirect_stdout(out):
                code = rp.main(["--plan", plan_path, "--module", d, "--under", "creature/warg/animations", "--apply"])
            self.assertEqual(code, 1)
            self.assertIn("refused: warg_walk_stand: read-back differs", out.getvalue())
            self.assertIn("--check", out.getvalue())

    def test_a_clip_that_is_there_ignores_its_template(self):
        """The template only makes a missing record. A record that exists keeps every byte the row does not name."""
        with tempfile.TemporaryDirectory() as d:
            made = self.folder(d)
            rp.apply(rp.plan([self.ROW], d), stamp="t")
            first = read(made)
            os.remove(os.path.join(d, "warg_trot_stand_anm.tpac"))
            again = rp.plan([self.ROW], d)
            self.assertFalse(again[0].changed)
            self.assertEqual(read(made), first)

    def test_check_reads_pending_for_a_clip_not_made_yet_and_writes_nothing(self):
        import contextlib
        import io
        import json
        with tempfile.TemporaryDirectory() as d:
            clips = os.path.join(d, "Assets", "creature", "warg", "animations")
            os.makedirs(clips)
            made = self.folder(clips)
            plan_path = os.path.join(d, "plan.json")
            with open(plan_path, "w", encoding="utf-8") as fh:
                json.dump([self.ROW], fh)
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                code = rp.main(["--plan", plan_path, "--module", d, "--under", "creature/warg/animations", "--check"])
            self.assertEqual(code, 1)
            self.assertRegex(out.getvalue(), r"PENDING\s+warg_walk_stand")
            self.assertIn("warg_trot_stand", out.getvalue())
            self.assertFalse(os.path.exists(made))


class LowFindingTests(unittest.TestCase):
    """The low findings of the #1563 review (#1566): a half-done --apply says how far it got, --under stays inside
    the module's Assets folder, and a NaN range is refused."""

    def test_a_nan_range_is_refused(self):
        for s1, s2 in ((float("nan"), 50.0), (10.0, float("nan")), (float("inf"), 50.0)):
            with self.assertRaisesRegex(rp.RepointError, "source"):
                rp.repoint(clip(), uuid.uuid4(), s1, s2)

    def test_a_failure_on_a_middle_row_says_how_far_apply_got(self):
        with tempfile.TemporaryDirectory() as d:
            rows = []
            for i in range(3):
                name = "anim_x_%d" % i
                path = os.path.join(d, name + "_anm.tpac")
                buf = clip(name)
                with open(path, "wb") as fh:
                    fh.write(buf)
                new = rp.repoint(buf, uuid.uuid4(), 27.0, 59.0)
                rows.append(rp.Row(name, path, buf, new, True, "m", None))
            rows[1] = rows[1]._replace(new="not bytes")
            with self.assertRaises(rp.RepointError) as ctx:
                rp.apply(rows, stamp="t")
            text = str(ctx.exception)
            self.assertIn("anim_x_1", text)
            self.assertIn("1 of 3", text)
            self.assertIn("anim_x_0", text)
            self.assertEqual(read(rows[1].path), rows[1].buf, "the failed row is restored")
            self.assertEqual(read(rows[2].path), rows[2].buf, "the later row is untouched")

    def test_under_resolves_inside_assets_and_refuses_an_escape(self):
        ok = rp.under_folder("M", "creature/warg/animations")
        self.assertEqual(ok, os.path.join("M", "Assets", "creature", "warg", "animations"))
        for bad in ("../x", "creature/../../x", "/abs/path", "C:/abs", "C:\\abs", "a\\..\\b", "", "."):
            with self.assertRaises(rp.RepointError, msg=bad):
                rp.under_folder("M", bad)

    def test_main_refuses_an_escaping_under_with_exit_1(self):
        import contextlib
        import io
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = rp.main(["--plan", "p.json", "--module", "M", "--under", "../outside", "--check"])
        self.assertEqual(code, 1)
        self.assertIn("refused", out.getvalue())


if __name__ == "__main__":
    unittest.main()
