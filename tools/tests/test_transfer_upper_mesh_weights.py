"""Tests for tools/blender/transfer_upper_mesh_weights.py: targets, references, blending, pruning, height profile."""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "blender"))
import transfer_upper_mesh_weights as tw  # noqa: E402

NAMES = ["SK_Dwarf_Beard_A_12", "SK_Dwarf_Beard_A_12.lod1", "SK_Dwarf_Beard_A_12.lod5", "SK_Dwarf_Beard_A_11",
         "SK_Dwarf_Beard_A_11.lod1", "SK_Dwarf_Beard_A_07", "SM_Dwarf_Basemesh_A1_head",
         "SM_Dwarf_Basemesh_A1_head.eye"]
BASE = ["blender", "--", "--fbx", "a.fbx", "--mesh", "m", "--ref", "r"]


class TargetTests(unittest.TestCase):
    def test_a_mesh_takes_its_lods_and_nothing_else(self):
        self.assertEqual(tw.targets(NAMES, ["SK_Dwarf_Beard_A_12"]),
                         ["SK_Dwarf_Beard_A_12", "SK_Dwarf_Beard_A_12.lod1", "SK_Dwarf_Beard_A_12.lod5"])

    def test_a_face_part_is_refused(self):
        for name in ("SM_Dwarf_Basemesh_A1_head", "SM_Dwarf_Basemesh_A1_head.eye", "x.mouth"):
            with self.assertRaises(SystemExit):
                tw.targets(NAMES + [name], [name])

    def test_an_unknown_mesh_is_refused(self):
        with self.assertRaises(SystemExit):
            tw.targets(NAMES, ["SK_Dwarf_Beard_A_13"])


class ReferenceTests(unittest.TestCase):
    def test_named_references_are_kept_in_order(self):
        self.assertEqual(tw.references(NAMES, ["SK_Dwarf_Beard_A_11", "SK_Dwarf_Beard_A_07"], []),
                         ["SK_Dwarf_Beard_A_11", "SK_Dwarf_Beard_A_07"])

    def test_a_head_may_be_a_reference(self):
        self.assertEqual(tw.references(NAMES, ["SM_Dwarf_Basemesh_A1_head"], []), ["SM_Dwarf_Basemesh_A1_head"])

    def test_a_target_cannot_be_its_own_reference(self):
        work = tw.targets(NAMES, ["SK_Dwarf_Beard_A_12"])
        for ref in ("SK_Dwarf_Beard_A_12", "SK_Dwarf_Beard_A_12.lod1"):
            with self.assertRaises(SystemExit):
                tw.references(NAMES, [ref], work)

    def test_an_unknown_reference_is_refused(self):
        with self.assertRaises(SystemExit):
            tw.references(NAMES, ["SK_Dwarf_Beard_A_99"], [])


class ArgumentTests(unittest.TestCase):
    def test_required_arguments(self):
        with self.assertRaises(SystemExit):
            tw.parse_args(["blender", "--", "--fbx", "a.fbx", "--mesh", "m"])
        args = tw.parse_args(BASE + ["--ref", "s", "--apply"])
        self.assertEqual((args["fbx"], args["ref_fbx"], args["mesh"], args["ref"], args["apply"],
                          args["max_influences"]), ("a.fbx", None, ["m"], ["r", "s"], True, 4))

    def test_a_separate_reference_fbx(self):
        self.assertEqual(tw.parse_args(BASE + ["--ref-fbx", "h.fbx"])["ref_fbx"], "h.fbx")

    def test_max_influences_is_bounded(self):
        self.assertEqual(tw.parse_args(BASE + ["--max-influences", "2"])["max_influences"], 2)
        for bad in ("0", "9"):
            with self.assertRaises(SystemExit):
                tw.parse_args(BASE + ["--max-influences", bad])


class BarycentricTests(unittest.TestCase):
    A, B, C = (0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0)

    def test_corners_and_centre(self):
        self.assertEqual([round(x, 6) for x in tw.barycentric(self.A, self.A, self.B, self.C)], [1, 0, 0])
        self.assertEqual([round(x, 6) for x in tw.barycentric(self.C, self.A, self.B, self.C)], [0, 0, 1])
        centre = (1 / 3, 1 / 3, 0.0)
        self.assertTrue(all(abs(x - 1 / 3) < 1e-9 for x in tw.barycentric(centre, self.A, self.B, self.C)))

    def test_a_point_off_the_triangle_is_clamped_and_sums_to_one(self):
        u = tw.barycentric((2.0, -1.0, 0.0), self.A, self.B, self.C)
        self.assertTrue(all(x >= 0 for x in u))
        self.assertAlmostEqual(sum(u), 1.0)

    def test_a_degenerate_triangle_falls_back_to_the_nearest_corner(self):
        u = tw.barycentric((0.9, 0.0, 0.0), self.A, self.B, (2.0, 0.0, 0.0))
        self.assertEqual(u, (0.0, 1.0, 0.0))


class MixTests(unittest.TestCase):
    def test_blends_corners_by_their_share(self):
        got = tw.mix_weights([{"head": 1.0}, {"head": 0.5, "neck": 0.5}, {"neck": 1.0}], (0.5, 0.5, 0.0), 4)
        self.assertAlmostEqual(got["head"], 0.75)
        self.assertAlmostEqual(got["neck"], 0.25)

    def test_keeps_the_strongest_influences_and_renormalises(self):
        corner = {"head": 0.4, "neck": 0.3, "spine2": 0.2, "l_clavicle": 0.1}
        got = tw.mix_weights([corner, corner, corner], (1 / 3, 1 / 3, 1 / 3), 2)
        self.assertEqual(sorted(got), ["head", "neck"])
        self.assertAlmostEqual(sum(got.values()), 1.0)
        self.assertAlmostEqual(got["head"], 0.4 / 0.7)

    def test_an_unweighted_neighbourhood_is_refused(self):
        with self.assertRaises(ValueError):
            tw.mix_weights([{}, {}, {}], (0.2, 0.3, 0.5), 4)


class ProfileTests(unittest.TestCase):
    def test_shares_per_height_band(self):
        samples = [(1.40, {"head": 1.0}), (1.36, {"head": 0.5, "neck": 0.5}), (1.10, {"spine2": 1.0})]
        got = tw.profile(samples, [1.35, 1.20])
        self.assertEqual([b["band"] for b in got], ["z>=1.35", "1.20<=z<1.35", "z<1.20"])
        self.assertEqual([b["verts"] for b in got], [2, 0, 1])
        self.assertEqual(got[0]["shares"], {"head": 0.75, "neck": 0.25})
        self.assertEqual(got[1]["shares"], {})
        self.assertEqual(got[2]["shares"], {"spine2": 1.0})


if __name__ == "__main__":
    unittest.main()
