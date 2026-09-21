#!/usr/bin/env python3
"""Unit tests for the pure parts of tools/blender/add_collision_body.py.

Run:  python -m unittest tools.tests.test_add_collision_body

The script imports bpy at module level, so a stub module is installed before the import and
`main()` is behind a `__main__` guard. Blender-side behaviour (import, duplicate, export) is
proven by the round-trip report the script writes when it runs; these cover the argument
contract and the comparison that decides whether `--apply` is allowed.
"""
import importlib.util
import os
import sys
import types
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "blender", "add_collision_body.py")

if "bpy" not in sys.modules:
    sys.modules["bpy"] = types.ModuleType("bpy")
_spec = importlib.util.spec_from_file_location("add_collision_body", SCRIPT)
acb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(acb)


def mesh(verts, polys, dims=(1.0, 1.0, 1.0), loc=(0.0, 0.0, 0.0), uv=1, mats=("m",)):
    return {"type": "MESH", "verts": verts, "polys": polys, "dims": list(dims),
            "loc": list(loc), "uv_layers": uv, "materials": list(mats)}


class ParseArgsTests(unittest.TestCase):
    def test_all_three_required(self):
        base = ["blender", "-b", "--"]
        for missing, argv in (
            ("fbx", ["--mesh", "M", "--material", "wood_weapon"]),
            ("mesh", ["--fbx", "f.fbx", "--material", "wood_weapon"]),
            ("material", ["--fbx", "f.fbx", "--mesh", "M"]),
        ):
            with self.assertRaises(SystemExit) as cm:
                acb.parse_args(base + argv)
            self.assertIn("--%s is required" % missing, str(cm.exception))

    def test_material_is_no_longer_guessed(self):
        """The three #633 bows were authored with the Loke default metal_weapon while the
        artist's elven bow body carries wood_weapon; the tool now asks."""
        args = acb.parse_args(["x", "--", "--fbx", "f.fbx", "--mesh", "M", "--material", "wood_weapon"])
        self.assertEqual(args["material"], "wood_weapon")
        self.assertFalse(args["apply"])
        self.assertIsNone(args["from_lod"])

    def test_unknown_argument_and_dangling_flag_are_refused(self):
        with self.assertRaises(SystemExit):
            acb.parse_args(["x", "--", "--fbx", "f", "--mesh", "M", "--material", "w", "--bogus"])
        with self.assertRaises(SystemExit):
            acb.parse_args(["x", "--", "--fbx", "f", "--mesh", "M", "--material"])

    def test_fbx_from_argv_survives_bad_arguments(self):
        """Under the detached launcher the report file is the only channel, so its path must
        be derivable even when parse_args will refuse the call."""
        self.assertTrue(acb.fbx_from_argv(["x", "--", "--fbx", "some.fbx"]).endswith("some.fbx"))
        self.assertTrue(acb.fbx_from_argv(["x", "--", "--mesh", "M"]).endswith("add_collision_body"))
        self.assertTrue(acb.fbx_from_argv(["x", "--", "--fbx"]).endswith("add_collision_body"))


class CompareTests(unittest.TestCase):
    def setUp(self):
        self.before = {"M": mesh(100, 50), "M.lod5": mesh(30, 32)}
        self.built = {"verts": 30, "polys": 32, "dims": [1.0, 1.0, 1.0], "loc": [0.0, 0.0, 0.0]}

    def test_clean_when_only_the_body_was_added(self):
        after = dict(self.before, bo_M=mesh(30, 32, mats=("metal_weapon",)))
        self.assertEqual(acb.compare(self.before, after, "bo_M", self.built), [])

    def test_a_lost_object_is_a_diff(self):
        after = {"M": mesh(100, 50), "bo_M": mesh(30, 32)}
        diffs = acb.compare(self.before, after, "bo_M", self.built)
        self.assertTrue(any("lost" in d and "M.lod5" in d for d in diffs), diffs)

    def test_a_changed_existing_object_is_a_diff(self):
        after = dict(self.before, bo_M=mesh(30, 32))
        after["M"] = mesh(100, 49)
        diffs = acb.compare(self.before, after, "bo_M", self.built)
        self.assertTrue(any(d.startswith("M: polys") for d in diffs), diffs)

    def test_a_missing_or_drifted_body_is_a_diff(self):
        diffs = acb.compare(self.before, dict(self.before), "bo_M", self.built)
        self.assertTrue(any("not in the exported file" in d for d in diffs), diffs)
        after = dict(self.before, bo_M=mesh(29, 32))
        diffs = acb.compare(self.before, after, "bo_M", self.built)
        self.assertTrue(any("bo_M: verts built 30, re-imported 29" in d for d in diffs), diffs)

    def test_an_unexpected_extra_object_is_a_diff(self):
        after = dict(self.before, bo_M=mesh(30, 32), stray=mesh(1, 1))
        diffs = acb.compare(self.before, after, "bo_M", self.built)
        self.assertTrue(any("unexpected new objects: stray" in d for d in diffs), diffs)


if __name__ == "__main__":
    unittest.main()
