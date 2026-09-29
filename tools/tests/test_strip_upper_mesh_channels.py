"""Tests for tools/blender/strip_upper_mesh_channels.py: which objects lose their channels, and what is refused."""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "blender"))
import strip_upper_mesh_channels as sm  # noqa: E402

NAMES = ["SK_saruman_hair", "SK_saruman_hair.lod1", "SK_saruman_hair.lod5", "SK_saruman_beard",
         "SK_saruman_beard.lod2", "sk_saruman_head.base", "SK_saruman_head.eye", "SK_saruman_hairpin"]


class TargetTests(unittest.TestCase):
    def test_a_mesh_takes_its_lods_and_nothing_else(self):
        self.assertEqual(sm.targets(NAMES, ["SK_saruman_hair"]),
                         ["SK_saruman_hair", "SK_saruman_hair.lod1", "SK_saruman_hair.lod5"])

    def test_several_meshes_combine(self):
        self.assertEqual(len(sm.targets(NAMES, ["SK_saruman_hair", "SK_saruman_beard"])), 5)

    def test_a_face_part_is_refused(self):
        for name in ("sk_saruman_head.base", "SK_saruman_head.eye", "x.mouth"):
            with self.assertRaises(SystemExit):
                sm.targets(NAMES, [name])

    def test_an_unknown_mesh_is_refused(self):
        with self.assertRaises(SystemExit):
            sm.targets(NAMES, ["SK_saruman_cloak"])

    def test_arguments_need_an_fbx_and_a_mesh(self):
        with self.assertRaises(SystemExit):
            sm.parse_args(["blender", "--", "--fbx", "a.fbx"])
        args = sm.parse_args(["blender", "--", "--fbx", "a.fbx", "--mesh", "m", "--apply"])
        self.assertEqual((args["fbx"], args["mesh"], args["apply"]), ("a.fbx", ["m"], True))


if __name__ == "__main__":
    unittest.main()
