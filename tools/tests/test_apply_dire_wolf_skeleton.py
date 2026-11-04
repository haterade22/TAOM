#!/usr/bin/env python3
"""Unit tests for tools/apply_dire_wolf_skeleton.py.

Run:  python -m unittest discover -s tools/tests -t . -p test_apply_dire_wolf_skeleton.py

Synthetic packages only; no game install, Kit or Blender needed. Pins:
  - the skeleton joins a mesh-only package with Usage 'horse', and its bodies, joints and definition stay the source's
  - the mesh's segments do not change, and the segment hashes stay valid
  - a package that already holds the finished skeleton is left alone (a second run writes nothing)
  - a package holding the skeleton with another Usage is refused, not overwritten
  - a source without the skeleton is refused
  - the dry run writes nothing; --apply writes a backup; it refuses while the game or the Kit runs
  - the master copy drops the second skeleton, keeps every other item and never overwrites
Skips where lz4, numpy or xxhash is missing, as the sibling capsule tests do.
"""
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
try:
    import test_skeleton_hit_capsules as th  # the synthetic tpac builder
    import test_tpac_skeleton_copy_physics as tc  # the synthetic skeleton
except ImportError as exc:
    raise unittest.SkipTest("apply_dire_wolf_skeleton needs lz4, numpy and xxhash (%s)" % exc)
import apply_dire_wolf_skeleton as ads  # noqa: E402
import skeleton_hit_capsules as shc  # noqa: E402
import tpac_clone_metamesh as tcm  # noqa: E402
import tpac_skeleton_copy_physics as cp  # noqa: E402


def _mesh_only():
    return th._tpac([(shc.METAMESH_ITEM_TYPE, "mesh", [(th.MESH_SEGMENT_TYPE, th.MESHDATA)])])


def _source():
    donor, _ = tc._pair()
    return tc._package(donor)


class BundleTests(unittest.TestCase):
    def test_the_skeleton_joins_the_package_with_usage_horse_and_keeps_its_physics(self):
        source = _source()
        out = ads.bundle(_mesh_only(), source, "s")
        got, want = cp.load_skeleton(out, "s"), cp.load_skeleton(source, "s")
        self.assertEqual(got["userdata"]["usage"], "horse")
        self.assertEqual(got["userdata"]["bodies"], want["userdata"]["bodies"])
        self.assertEqual(got["userdata"]["constraints"], want["userdata"]["constraints"])
        self.assertEqual([b["name"] for b in got["bones"]], [b["name"] for b in want["bones"]])

    def test_the_mesh_is_unchanged_and_hashes_stay_valid(self):
        before = _mesh_only()
        out = ads.bundle(before, _source(), "s")
        self.assertEqual(shc.stale_segment_hashes(out), [])
        old = tcm.parse(before).items[0]
        new = next(i for i in tcm.parse(out).items if i.name == "mesh")
        self.assertEqual([tcm.segment_payload(new, s) for s in new.segments],
                         [tcm.segment_payload(old, s) for s in old.segments])

    def test_a_finished_package_is_left_alone(self):
        once = ads.bundle(_mesh_only(), _source(), "s")
        self.assertEqual(ads.bundle(once, _source(), "s"), once)

    def test_a_package_with_another_usage_is_refused(self):
        with self.assertRaises(cp.Refused):
            ads.bundle(_source(), _source(), "s")

    def test_a_source_without_the_skeleton_is_refused(self):
        with self.assertRaises(cp.Refused):
            ads.bundle(_mesh_only(), _mesh_only(), "s")


class MastersTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.src = os.path.join(self.tmp.name, "src")
        self.dst = os.path.join(self.tmp.name, "dst")
        os.makedirs(self.src)
        self.armature = "adod_wolf_armature_unused_geo.tpac"
        with open(os.path.join(self.src, self.armature), "wb") as fh:
            fh.write(_source())
        self.plain = _mesh_only()
        with open(os.path.join(self.src, "plain_geo.tpac"), "wb") as fh:
            fh.write(self.plain)

    def tearDown(self):
        self.tmp.cleanup()

    def _run(self, *extra):
        out = io.StringIO()
        with redirect_stdout(out):
            code = ads.main(["--masters-from", self.src, "--masters-to", self.dst, "--skeleton", "s",
                             "--masters", self.armature, "plain_geo.tpac", *extra])
        return code, out.getvalue()

    def test_strip_removes_the_skeleton_and_keeps_every_other_item(self):
        out = ads.strip_skeleton(_source(), "s")
        self.assertEqual([i.name for i in tcm.parse(out).items], ["mesh"])
        self.assertEqual(shc.stale_segment_hashes(out), [])

    def test_strip_leaves_a_package_without_the_skeleton_alone(self):
        self.assertEqual(ads.strip_skeleton(self.plain, "s"), self.plain)

    def test_dry_run_writes_nothing(self):
        code, out = self._run()
        self.assertEqual(code, 0, out)
        self.assertFalse(os.path.exists(self.dst))

    def test_apply_copies_without_the_skeleton_and_never_overwrites(self):
        with mock.patch.object(ads, "game_or_kit_running", return_value=False):
            code, out = self._run("--apply")
            self.assertEqual(code, 0, out)
            path = os.path.join(self.dst, self.armature)
            self.assertEqual([i.name for i in tcm.parse(th._read(path)).items], ["mesh"])
            self.assertEqual(th._read(os.path.join(self.dst, "plain_geo.tpac")), self.plain)
            with open(path, "wb") as fh:
                fh.write(b"kept")
            code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        self.assertEqual(th._read(path), b"kept")

    def test_a_missing_master_is_refused_and_nothing_is_written(self):
        os.remove(os.path.join(self.src, "plain_geo.tpac"))
        with mock.patch.object(ads, "game_or_kit_running", return_value=False):
            code, _ = self._run("--apply")
        self.assertEqual(code, 1)
        self.assertFalse(os.path.exists(self.dst))


class CliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.target = os.path.join(self.tmp.name, "grey_geo.tpac")
        self.source = os.path.join(self.tmp.name, "source.tpac")
        with open(self.target, "wb") as fh:
            fh.write(_mesh_only())
        with open(self.source, "wb") as fh:
            fh.write(_source())
        self.before = th._read(self.target)

    def tearDown(self):
        self.tmp.cleanup()

    def _run(self, *extra):
        out = io.StringIO()
        with redirect_stdout(out):
            code = ads.main(["--tpac", self.target, "--source", self.source, "--skeleton", "s", *extra])
        return code, out.getvalue()

    def test_dry_run_writes_nothing(self):
        code, out = self._run()
        self.assertEqual(code, 0, out)
        self.assertIn("dry run", out)
        self.assertEqual(th._read(self.target), self.before)

    def test_apply_writes_a_backup_and_a_second_run_writes_nothing(self):
        with mock.patch.object(ads, "game_or_kit_running", return_value=False):
            code, out = self._run("--apply")
            self.assertEqual(code, 0, out)
            backups = [f for f in os.listdir(self.tmp.name) if ".bak-skeleton-" in f]
            self.assertEqual(len(backups), 1)
            self.assertEqual(th._read(os.path.join(self.tmp.name, backups[0])), self.before)
            after = th._read(self.target)
            code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        self.assertEqual(th._read(self.target), after)
        self.assertEqual(len([f for f in os.listdir(self.tmp.name) if ".bak-skeleton-" in f]), 1)

    def test_apply_refuses_while_the_game_or_kit_runs(self):
        with mock.patch.object(ads, "game_or_kit_running", return_value=True):
            code, _ = self._run("--apply")
        self.assertEqual(code, 2)
        self.assertEqual(th._read(self.target), self.before)


if __name__ == "__main__":
    unittest.main()
