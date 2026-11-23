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


if __name__ == "__main__":
    unittest.main()
