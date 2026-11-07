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
  - the pack-size master copy is gone, and strip_skeleton (for --replace) keeps every other item
  - verify_segment refuses another changed segment and a wrong payload
  - --scale resizes rest translations and capsules, keeps axes, masses and joint limits, and runs once
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
    import numpy as np
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


class ScaleTests(unittest.TestCase):
    """--scale resizes the bundled skeleton: lengths grow, angles, masses and names do not (the wolf at warg size)."""

    def setUp(self):
        self.source = _source()
        self.want = cp.load_skeleton(self.source, "s")
        self.got = cp.load_skeleton(ads.bundle(_mesh_only(), self.source, "s", scale=2.0), "s")

    def test_rest_translations_scale_and_rest_axes_stay(self):
        for g, w in zip(self.got["bones"], self.want["bones"]):
            np.testing.assert_allclose(g["rest"][3, :3], 2.0 * w["rest"][3, :3], atol=1e-6, err_msg=g["name"])
            np.testing.assert_allclose(g["rest"][:3, :3], w["rest"][:3, :3], atol=1e-7, err_msg=g["name"])
            self.assertEqual((g["name"], g["parent"]), (w["name"], w["parent"]))

    def test_capsules_scale_and_masses_stay(self):
        for g, w in zip(self.got["userdata"]["bodies"], self.want["userdata"]["bodies"]):
            for key in ("rp1", "rp2", "cp1", "cp2"):
                np.testing.assert_allclose(g[key][:3], [2.0 * x for x in w[key][:3]], atol=1e-6)
                self.assertEqual(g[key][3], w[key][3])
            for key in ("rr", "cr", "cmax"):
                self.assertAlmostEqual(g[key], w[key] if w[key] < 0 else 2.0 * w[key], places=5)
            self.assertEqual(g["mass"], w["mass"])

    def test_joint_frames_and_limits_stay(self):
        for g, w in zip(self.got["userdata"]["constraints"], self.want["userdata"]["constraints"]):
            self.assertEqual((g["rot"], g["limits"], g["type"]), (w["rot"], w["limits"], w["type"]))
            np.testing.assert_allclose(g["pos"][:3], [2.0 * x for x in w["pos"][:3]], atol=1e-6)

    def test_the_usage_is_horse_and_hashes_stay_valid(self):
        out = ads.bundle(_mesh_only(), self.source, "s", scale=2.0)
        self.assertEqual(cp.load_skeleton(out, "s")["userdata"]["usage"], "horse")
        self.assertEqual(shc.stale_segment_hashes(out), [])

    def test_a_package_already_at_the_size_is_left_alone(self):
        once = ads.bundle(_mesh_only(), self.source, "s", scale=2.0)
        self.assertEqual(ads.bundle(once, self.source, "s", scale=2.0), once)

    def test_a_package_at_another_size_is_refused(self):
        once = ads.bundle(_mesh_only(), self.source, "s")
        with self.assertRaises(cp.Refused):
            ads.bundle(once, self.source, "s", scale=2.0)

    def test_replace_swaps_a_skeleton_at_another_size(self):
        # A mesh re-import can keep the old skeleton item (2026-11-06): --replace drops it and bundles afresh.
        once = ads.bundle(_mesh_only(), self.source, "s")
        out = ads.bundle(once, self.source, "s", scale=2.0, replace=True)
        got = cp.load_skeleton(out, "s")
        for g, w in zip(got["bones"], self.got["bones"]):
            np.testing.assert_allclose(g["rest"], w["rest"], atol=1e-7, err_msg=g["name"])
        self.assertEqual(got["userdata"], self.got["userdata"])
        self.assertEqual(sorted(it.name for it in tcm.parse(out).items), ["mesh", "s"])

    def test_replace_leaves_a_skeleton_already_at_the_size_alone(self):
        once = ads.bundle(_mesh_only(), self.source, "s", scale=2.0)
        self.assertEqual(ads.bundle(once, self.source, "s", scale=2.0, replace=True), once)


class BackTests(unittest.TestCase):
    """--back moves the root bones toward the tail (model -y), so the seat lands under the warg's rider clip."""

    def setUp(self):
        self.source = _source()
        self.want = cp.load_skeleton(self.source, "s")
        self.out = ads.bundle(_mesh_only(), self.source, "s", scale=2.0, back=0.5)
        self.got = cp.load_skeleton(self.out, "s")

    def test_only_the_root_bones_move(self):
        roots = 0
        for g, w in zip(self.got["bones"], self.want["bones"]):
            want = 2.0 * w["rest"][3, :3]
            if w["parent"] < 0:
                want = want + np.array([0.0, -0.5, 0.0])
                roots += 1
            np.testing.assert_allclose(g["rest"][3, :3], want, atol=1e-6, err_msg=g["name"])
        self.assertGreater(roots, 0)

    def test_the_bodies_stay_bone_local(self):
        plain = cp.load_skeleton(ads.bundle(_mesh_only(), self.source, "s", scale=2.0), "s")
        self.assertEqual(self.got["userdata"], plain["userdata"])

    def test_a_package_already_moved_is_left_alone(self):
        self.assertEqual(ads.bundle(self.out, self.source, "s", scale=2.0, back=0.5), self.out)

    def test_a_package_not_moved_is_refused_and_replace_moves_it(self):
        plain = ads.bundle(_mesh_only(), self.source, "s", scale=2.0)
        with self.assertRaises(cp.Refused):
            ads.bundle(plain, self.source, "s", scale=2.0, back=0.5)
        moved = cp.load_skeleton(ads.bundle(plain, self.source, "s", scale=2.0, back=0.5, replace=True), "s")
        for g, w in zip(moved["bones"], self.got["bones"]):
            np.testing.assert_allclose(g["rest"], w["rest"], atol=1e-7, err_msg=g["name"])


class StripTests(unittest.TestCase):
    def test_strip_removes_the_skeleton_and_keeps_every_other_item(self):
        out = ads.strip_skeleton(_source(), "s")
        self.assertEqual([i.name for i in tcm.parse(out).items], ["mesh"])
        self.assertEqual(shc.stale_segment_hashes(out), [])

    def test_strip_leaves_a_package_without_the_skeleton_alone(self):
        plain = _mesh_only()
        self.assertEqual(ads.strip_skeleton(plain, "s"), plain)

    def test_the_pack_master_copy_is_gone(self):
        # The pack's masters are pack size; the rig is RIG_SCALE times it, so its masters come from the Kit (2026-11-06).
        with redirect_stdout(io.StringIO()), self.assertRaises(SystemExit):
            ads.main(["--masters-from", "x", "--masters-to", "y"])


class VerifySegmentTests(unittest.TestCase):
    """The last check before a live write: it must refuse, not only pass."""

    def setUp(self):
        self.joined = ads.bundle(_mesh_only(), _source(), "s")
        item = shc._skeleton_item(tcm.parse(self.joined), "s")
        self.d = ads.scale_definition(tcm.segment_payload(item, shc._segment(item, shc.SKELETON_DEFINITION_TAG)), 2.0)
        self.resized = cp.replace_segment(self.joined, "s", shc.SKELETON_DEFINITION_TAG, self.d)

    def test_the_replaced_segment_passes(self):
        cp.verify_segment(self.joined, self.resized, "s", shc.SKELETON_DEFINITION_TAG, self.d)

    def test_another_segment_changed_too_is_refused(self):
        u = cp.load_skeleton(self.resized, "s")["userdata"]
        both = cp.replace_userdata(self.resized, "s", cp.build_userdata(dict(u, usage="other")))
        with self.assertRaises(cp.Refused):
            cp.verify_segment(self.joined, both, "s", shc.SKELETON_DEFINITION_TAG, self.d)

    def test_a_payload_other_than_the_one_written_is_refused(self):
        with self.assertRaises(cp.Refused):
            cp.verify_segment(self.joined, self.resized, "s", shc.SKELETON_DEFINITION_TAG, self.d + b"\0")


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
